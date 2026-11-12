"""Offline tests for the F4b union probe tooling: check_scopes.py, the expected list and build_probe.py."""
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys
import tempfile
import unittest

MODULE = Path(__file__).resolve().parent
REPO = MODULE.parents[1]
sys.path.insert(0, str(MODULE))
import check_scopes  # noqa: E402

# The F4 plan's section 2 inventory ("Native hooks PC installs", docs/plans/f4-perf-gate.md), as patch class and method.
SECTION_2 = {
    ('NativeCityClock', 'TickPrefix'), ('NativeCityClock', 'AccountingPrefix'), ('NativeCityClock', 'RidePrefix'), ('NativeCityClock', 'RidePostfix'),
    ('NativeTravelPace', 'ObserveTickPostfix'),
    ('NativeFoodOwnership', 'AfterNativeTick'), ('NativeFoodOwnership', 'HourlyCustomers'), ('NativeFoodOwnership', 'CustomerSale'),
    ('NativeFoodOwnership', 'CustomerDraw'), ('NativeFoodOwnership', 'GenerateCustomers'), ('NativeFoodOwnership', 'ResetRegistration'),
    ('NativeFoodMarket', 'ResidentPrice'),
    ('AmbientPedestrians', 'SetActivePrefix'), ('AmbientPedestrians', 'EndCyclePrefix'), ('AmbientPedestrians', 'ChangeStatePrefix'),
    ('StationaryCrowds', 'SpawnPrefix'),
    ('ResidentInspector', 'MouseRunPrefix'), ('ResidentInspector', 'CameraInputPrefix'), ('ResidentInspector', 'CameraInputPostfix'),
    ('ResidentInspector', 'UiRaycastPrefix'),
    ('ResearchBilling', 'RunDailyPostfix'), ('ResearchAgencyDialog', 'CampaignSettingsPostfix'),
}
# Section 2 entry points other than patches, and Decision 5's component list (plus F2e's two hold call sites).
SECTION_2_MESSAGES = {('PopulationRuntime', 'Update'), ('PopulationRuntime', 'OnGUI'),
                      ('PhysicalPedestrianCrosswalk', 'OnTriggerEnter'), ('PhysicalPedestrianCrosswalk', 'OnTriggerExit')}
DECISION_5_COMPONENTS = {'AdvancePopulation', 'Prepare', 'RefreshOptionalStops', 'RefreshTrafficClosures', 'Update', 'UpdateInspector',
                         'RefreshDiscovery', 'RefreshRoutineHours'}
F2E_COMPONENTS = {'ApplyHoldReplay', 'ObservePlayerContact'}


class SourceCopy:
    """A temporary copy of the scanned source folders, edited per test."""

    def __enter__(self):
        self.root = Path(tempfile.mkdtemp(prefix='f4b-scopes-'))
        for folder in check_scopes.SOURCE_DIRS:
            (self.root / folder).mkdir()
            for path in (REPO / folder).glob('*.cs'):
                shutil.copy2(path, self.root / folder / path.name)
        return self

    def __exit__(self, *exc):
        shutil.rmtree(self.root, ignore_errors=True)

    def edit(self, rel, old, new, count=1):
        path = self.root / rel
        text = path.read_text(encoding='utf-8-sig')
        if old not in text:
            raise AssertionError(f'{old!r} not in {rel}')
        path.write_text(text.replace(old, new, count), encoding='utf-8')

    def add(self, rel, text):
        (self.root / rel).write_text(text, encoding='utf-8')

    def problems(self):
        return check_scopes.check(self.root)


# GR-9: the transpiler-reference cases were removed with the borrowed-car transpilers; Persistent City installs no
# transpiler, so TranspilerHelpers is empty and no transpiler body is pinned.
class CheckScopesTests(unittest.TestCase):
    def test_passes_on_base(self):
        self.assertEqual(check_scopes.check(REPO), [])

    def test_command_line_exit_codes(self):
        ok = subprocess.run([sys.executable, str(MODULE / 'check_scopes.py'), '--json'], capture_output=True, text=True)
        self.assertEqual(ok.returncode, 0, ok.stdout + ok.stderr)
        self.assertTrue(json.loads(ok.stdout)['ok'])
        with SourceCopy() as src:
            src.edit('Runtime/StationaryCrowds.cs', 'harmony.Patch(spawn, prefix: new HarmonyMethod(typeof(StationaryCrowds), nameof(SpawnPrefix)));',
                     'harmony.Patch(spawn, prefix: new HarmonyMethod(typeof(StationaryCrowds), nameof(SpawnPrefix)));\n'
                     '                harmony.Patch(spawn, postfix: new HarmonyMethod(typeof(StationaryCrowds), nameof(SpawnPrefix)));')
            bad = subprocess.run([sys.executable, str(MODULE / 'check_scopes.py'), '--root', str(src.root)], capture_output=True, text=True)
        self.assertEqual(bad.returncode, 1, bad.stdout)
        self.assertIn('FAIL', bad.stdout)

    def test_fails_on_synthetic_extra_patch_call(self):
        with SourceCopy() as src:
            src.edit('Runtime/StationaryCrowds.cs', 'harmony.Patch(spawn, prefix: new HarmonyMethod(typeof(StationaryCrowds), nameof(SpawnPrefix)));',
                     'harmony.Patch(spawn, prefix: new HarmonyMethod(typeof(StationaryCrowds), nameof(SpawnPrefix)));\n'
                     '                harmony.Patch(spawn, postfix: new HarmonyMethod(typeof(StationaryCrowds), nameof(ExtraPostfix)));')
            problems = src.problems()
        self.assertTrue(any('ExtraPostfix' in p and 'not in ScopeInventory' in p for p in problems), problems)

    def test_fails_when_a_callback_is_reused_on_another_target(self):
        # Sol-r1-3: same owner, kind and callback, different native target.
        with SourceCopy() as src:
            src.edit('Runtime/StationaryCrowds.cs', 'harmony.Patch(spawn, prefix: new HarmonyMethod(typeof(StationaryCrowds), nameof(SpawnPrefix)));',
                     'harmony.Patch(spawn, prefix: new HarmonyMethod(typeof(StationaryCrowds), nameof(SpawnPrefix)));\n'
                     '                harmony.Patch(AccessTools.Method(typeof(PedestrianSpawner), "SetActivePedestrians"), '
                     'prefix: new HarmonyMethod(typeof(StationaryCrowds), nameof(SpawnPrefix)));')
            problems = src.problems()
        self.assertTrue(any('SpawnPrefix PedestrianSpawner.SetActivePedestrians' in p and 'not in ScopeInventory' in p for p in problems), problems)

    def test_fails_on_a_repeated_registration(self):
        with SourceCopy() as src:
            src.edit('Runtime/StationaryCrowds.cs', 'harmony.Patch(spawn, prefix: new HarmonyMethod(typeof(StationaryCrowds), nameof(SpawnPrefix)));',
                     'harmony.Patch(spawn, prefix: new HarmonyMethod(typeof(StationaryCrowds), nameof(SpawnPrefix)));\n'
                     '                harmony.Patch(spawn, prefix: new HarmonyMethod(typeof(StationaryCrowds), nameof(SpawnPrefix)));')
            problems = src.problems()
        self.assertTrue(any('SpawnPrefix StationaryAiSpawner.Spawn' in p and 'not in ScopeInventory' in p for p in problems), problems)

    def test_fails_when_an_existing_target_changes(self):
        cases = [
            ('Runtime/StationaryCrowds.cs', 'AccessTools.Method(typeof(StationaryAiSpawner), "Spawn"',
             'AccessTools.Method(typeof(StationaryAiSpawner), "SpawnAll"', 'StationaryAiSpawner.SpawnAll'),  # field-initialised variable
            ('Runtime/NativeFoodOwnership.cs', 'Patch(typeof(BuildingRegistration), "Reset", nameof(ResetRegistration));',
             'Patch(typeof(BuildingRegistration), "Clear", nameof(ResetRegistration));', 'BuildingRegistration.Clear'),  # helper call
            ('Runtime/ResearchBilling.cs', 'GetMethod("RunDaily"', 'GetMethod("RunWeekly"', 'MarketingHelper.RunWeekly'),  # out parameter
        ]
        for rel, old, new, expected in cases:
            with self.subTest(expected=expected), SourceCopy() as src:
                src.edit(rel, old, new)
                problems = src.problems()
                self.assertTrue(any(expected in p and 'not in ScopeInventory' in p for p in problems), problems)

    def test_fails_on_an_unresolvable_target(self):
        with SourceCopy() as src:
            src.edit('Runtime/StationaryCrowds.cs', 'harmony.Patch(spawn, prefix:', 'harmony.Patch(Lookup(), prefix:')
            problems = src.problems()
        self.assertTrue(any('cannot resolve the native target' in p for p in problems), problems)

    def test_fails_when_the_dynamic_target_is_not_the_table_binding(self):
        # Sol-r2-2: the food-contract postfixes take their targets from the table only through Bind(hook, ...).
        cases = [
            ('harmony.Patch(method, postfix:', 'harmony.Patch(AccessTools.Method(typeof(GameManager), "RunMainGameTick"), postfix:'),
            ('var method = Bind(hook, out var failure);', 'var method = Bind(hook, out var failure); var other = method;\n                method = other;'),
            ('return null;\n            }\n            public void UnpatchAll()',
             'harmony.Patch(method, postfix: new HarmonyMethod(postfix) { priority = Priority.Last });\n                return null;\n            }\n            public void UnpatchAll()'),
        ]
        for old, new in cases:
            with self.subTest(new=new[:50]), SourceCopy() as src:
                src.edit('Runtime/LoadedCatalogueLifecycleMembers.cs', old, new)
                problems = src.problems()
                self.assertTrue(any('does not register exactly the table binding' in p for p in problems), problems)

    def test_fails_on_a_conditional_dynamic_target(self):
        # Sol-r3-1: the initializer must be exactly one Bind(hook, ...) call, with nothing after it.
        with SourceCopy() as src:
            src.edit('Runtime/LoadedCatalogueLifecycleMembers.cs', 'var method = Bind(hook, out var failure);',
                     'var method = Bind(hook, out var failure) == null ? null : AccessTools.Method(typeof(GameManager), "RunMainGameTick");')
            problems = src.problems()
        self.assertTrue(any('does not register exactly the table binding' in p for p in problems), problems)

    def test_fails_on_a_second_registration_inside_a_helper(self):
        # Sol-r2-2: a helper whose body registers twice would be recorded once per call site.
        with SourceCopy() as src:
            src.edit('Runtime/NativeFoodOwnership.cs',
                     'private static void Patch(Type type, string method, string prefix) => patches.Patch(AccessTools.Method(type, method),',
                     'private static void Patch(Type type, string method, string prefix) { patches.Patch(AccessTools.Method(type, method), '
                     'prefix: new HarmonyMethod(typeof(NativeFoodOwnership), prefix) { priority = Priority.First }); patches.Patch(AccessTools.Method(type, method),')
            src.edit('Runtime/NativeFoodOwnership.cs', 'prefix: new HarmonyMethod(typeof(NativeFoodOwnership), prefix) { priority = Priority.First });\n',
                     'prefix: new HarmonyMethod(typeof(NativeFoodOwnership), prefix) { priority = Priority.First }); }\n')
            problems = src.problems()
        self.assertTrue(any('unsupported helper Patch shape' in p for p in problems), problems)

    def test_fails_on_extra_helper_patch_call(self):
        with SourceCopy() as src:
            src.edit('Runtime/NativeFoodOwnership.cs', 'Patch(typeof(BuildingRegistration), "Reset", nameof(ResetRegistration));',
                     'Patch(typeof(BuildingRegistration), "Reset", nameof(ResetRegistration));\n'
                     '                Patch(typeof(BuildingRegistration), "TemporarilyClose", nameof(ExtraClosePrefix));')
            problems = src.problems()
        self.assertTrue(any('ExtraClosePrefix' in p for p in problems), problems)

    def test_fails_on_new_food_contract_postfix(self):
        with SourceCopy() as src:
            src.edit('Runtime/LoadedCatalogueLifecycleMembers.cs', 'private static void Hook22() => FoodContractHintRouter.Unattributed(22);',
                     'private static void Hook22() => FoodContractHintRouter.Unattributed(22);\n'
                     '            private static void Hook23() => FoodContractHintRouter.Unattributed(22);')
            problems = src.problems()
        self.assertTrue(any('Hook23' in p for p in problems), problems)

    def test_fails_closed_on_unrecognised_patch_forms(self):
        cases = [
            ('Runtime/StationaryCrowds.cs', 'harmony.Patch(spawn, prefix: new HarmonyMethod(typeof(StationaryCrowds), nameof(SpawnPrefix)));',
             'var m = new HarmonyMethod(typeof(StationaryCrowds), nameof(SpawnPrefix)); harmony.Patch(spawn, prefix: m);', 'HarmonyMethod outside a recognised Patch call'),
            ('Runtime/StationaryCrowds.cs', 'active = true;', 'active = true; harmony.PatchAll();', 'PatchAll'),
            ('Runtime/StationaryCrowds.cs', 'active = true;', 'active = true; harmony.Patch(spawn, SpawnMethod);', 'without HarmonyMethod'),
            ('Runtime/StationaryCrowds.cs', 'active = true;', 'active = true; StartCoroutine(null);', 'coroutine'),
        ]
        for rel, old, new, expected in cases:
            with self.subTest(expected=expected), SourceCopy() as src:
                src.edit(rel, old, new)
                problems = src.problems()
                self.assertTrue(any(expected in p for p in problems), problems)

    def test_fails_when_an_expected_patch_is_removed(self):
        with SourceCopy() as src:
            src.edit('Runtime/AmbientPedestrians.cs', 'harmony.Patch(changeState!, prefix: new HarmonyMethod(typeof(AmbientPedestrians), nameof(ChangeStatePrefix)));', '')
            problems = src.problems()
        self.assertTrue(any('ChangeStatePrefix' in p and 'not in source' in p for p in problems), problems)

    def test_fails_on_new_monobehaviour_message_or_class(self):
        with SourceCopy() as src:
            src.edit('Runtime/PhysicalPedestrians.cs', 'private void OnTriggerExit(Collider other) { Body?.Exit(other); }',
                     'private void OnTriggerExit(Collider other) { Body?.Exit(other); }\n        private void FixedUpdate() { }')
            src.add('Runtime/ExtraBehaviour.cs', 'namespace PersistentCity.Runtime { internal sealed class Extra : UnityEngine.MonoBehaviour { private void LateUpdate() { } } }\n')
            problems = src.problems()
        self.assertTrue(any('FixedUpdate' in p for p in problems), problems)
        self.assertTrue(any('LateUpdate' in p and 'Extra' in p for p in problems), problems)

    def test_fails_on_new_native_event_subscription(self):
        with SourceCopy() as src:
            src.edit('Runtime/SaveEpochRuntime.cs', 'GlobalEvents.onSaveGame += Save;',
                     'GlobalEvents.onSaveGame += Save;\n            GlobalEvents.onNewDay += Save;\n            GlobalEvents.onExitBuilding += () => { };')
            problems = src.problems()
        self.assertTrue(any('onNewDay' in p for p in problems), problems)
        self.assertTrue(any('lambda' in p for p in problems), problems)

    def test_fails_when_f2e_hold_call_site_moves(self):
        with SourceCopy() as src:
            src.edit('Runtime/PhysicalPopulation.cs', 'if (player) ObservePlayerContact(binding, playerPose);', 'if (player) { }')
            src.edit('Runtime/PersistentCityMod.cs', 'ApplyHoldReplay(); // F2e', '// F2e')
            problems = src.problems()
        self.assertTrue(any('ObservePlayerContact' in p and 'call sites' in p for p in problems), problems)
        self.assertTrue(any('ApplyHoldReplay' in p and 'call sites' in p for p in problems), problems)

    def test_fails_when_a_component_is_renamed(self):
        with SourceCopy() as src:
            src.edit('Runtime/PersistentCityMod.cs', 'private void UpdateInspector()', 'private void UpdateInspectorV2()')
            problems = src.problems()
        self.assertTrue(any('UpdateInspector' in p and 'components' in p for p in problems), problems)

    def test_comments_and_strings_do_not_count(self):
        with SourceCopy() as src:
            src.edit('Runtime/StationaryCrowds.cs', 'active = true;',
                     'active = true; // harmony.Patch(spawn, prefix: new HarmonyMethod(typeof(StationaryCrowds), nameof(Ghost)));\n'
                     '                var note = $"harmony.Patch({spawn}) {{HarmonyMethod}}";')
            self.assertEqual(src.problems(), [])


class InventoryTests(unittest.TestCase):
    def setUp(self):
        self.version, self.rows = check_scopes.parse_inventory()

    def test_expected_list_equals_plan_section_2_inventory(self):
        planned = {(check_scopes.short(t), m) for owner, kind, t, m, target, f in self.rows['P'] if owner != 'persistentcity.food-contract'}
        self.assertEqual(planned, SECTION_2)
        messages = {(check_scopes.short(t), m) for t, m, f in self.rows['M']}
        self.assertEqual(messages - {('PopulationRuntime', 'OnDestroy')}, SECTION_2_MESSAGES)
        components = {m for t, m, p, f in self.rows['C']}
        self.assertEqual(components, DECISION_5_COMPONENTS | F2E_COMPONENTS)

    def test_section_2_names_are_in_the_plan(self):
        plan = (REPO / 'docs/plans/f4-perf-gate.md').read_text(encoding='utf-8')
        section = plan[plan.index('**Native hooks PC installs**'):plan.index('- **The union timing probe.**')]
        for cls in {c for c, _ in SECTION_2}:
            self.assertIn(cls, section)
        for cls, method in SECTION_2:
            if method in ('AfterNativeTick', 'ResidentPrice', 'ObserveTickPostfix', 'SpawnPrefix', 'RunDailyPostfix', 'CampaignSettingsPostfix'):
                self.assertIn(method, section)

    def test_food_contract_rows_equal_the_hook_table(self):
        table = (REPO / 'Runtime/FoodContractHooks.cs').read_text(encoding='utf-8')
        hooks = {f'Hook{m.group(1)}': f'{m.group(2)}.{m.group(3)}'
                 for m in re.finditer(r'new FoodContractHook\((\d+), "\w+", "(\w+)", "(\w+)"', table)}
        rows = {m: target for owner, kind, t, m, target, f in self.rows['P'] if owner == 'persistentcity.food-contract'}
        self.assertEqual(len(hooks), 23)
        self.assertEqual(rows, hooks)

    def test_hash_is_stable_and_versioned(self):
        self.assertEqual(self.version, 'f4b-scope-inventory/1')
        self.assertEqual(check_scopes.inventory_hash(), check_scopes.inventory_hash())
        self.assertEqual(len(check_scopes.canonical().splitlines()),
                         1 + sum(len(self.rows[k]) for k in 'PMEHCS'))


class ProbeSourceTests(unittest.TestCase):
    def test_logic_file_is_unity_free(self):
        for name in ('ProbeLogic.cs', 'ScopeInventory.cs'):
            text = (MODULE / name).read_text(encoding='utf-8')
            for forbidden in ('UnityEngine', 'HarmonyLib', 'BAModAPI', 'PersistentCity.'):
                self.assertNotIn('using ' + forbidden, text, name)

    def test_probe_never_references_persistent_city_at_compile_time(self):
        text = (MODULE / 'Probe.cs').read_text(encoding='utf-8')
        self.assertNotRegex(text, r'using\s+PersistentCity')
        build = (MODULE / 'build_probe.py').read_text(encoding='utf-8')
        self.assertNotIn('PersistentCity.dll', build.split('REFERENCES')[1].split(']')[0])

    def test_probe_owner_is_outside_the_inventory(self):
        text = (MODULE / 'Probe.cs').read_text(encoding='utf-8')
        owner = re.search(r'PatchId = "([^"]+)"', text).group(1)
        self.assertNotIn(owner, {r[0] for r in self.rows()['P']})
        self.assertFalse(owner.startswith('persistentcity.') or owner.startswith('hart.persistentcity.'))

    def rows(self):
        return check_scopes.parse_inventory()[1]

    def test_never_packaged(self):
        build = (REPO / 'tools/build.py').read_text(encoding='utf-8')
        self.assertNotIn('perf-union-probe', build)
        self.assertNotIn('PerfUnionProbe', build)

    def test_readme_states_the_limits(self):
        readme = (MODULE / 'README.md').read_text(encoding='utf-8').lower().replace('`', '').replace('*', '')
        for phrase in ('never packaged', 'includes nested probe cost', 'observerms measures only part',
                       'native work that pc forces inside a scope stays in that scope', 'native work outside pc scopes is not mod-work',
                       'untimed-transpiled', 'component-unbound', 'inlined', 'not cpu or gpu time', 'changes native state',
                       'unknown', 'native validation is pending'):
            self.assertIn(phrase, readme)


def game_managed():
    explicit = os.environ.get('PERF_UNION_GAME_MANAGED')
    default = Path(r'C:\Program Files (x86)\Steam\steamapps\common\Big Ambitions\Big Ambitions_Data\Managed')
    path = Path(explicit) if explicit else default
    return path if (path / 'BigAmbitions.dll').is_file() else None


class BuildManifestTests(unittest.TestCase):
    @unittest.skipUnless(game_managed() and (REPO / 'build/PersistentCity/Dependencies/0Harmony.dll').is_file(),
                         'needs the local game Managed folder and the pinned 0Harmony.dll from tools/build.py')
    def test_hash_manifest_is_stable_across_two_builds(self):
        import build_probe
        harmony = REPO / 'build/PersistentCity/Dependencies/0Harmony.dll'
        first, first_text = build_probe.build(game_managed(), harmony, REPO / 'build/perf-union-probe-test/a')
        second, second_text = build_probe.build(game_managed(), harmony, REPO / 'build/perf-union-probe-test/b')
        self.assertEqual(first_text, second_text)
        self.assertEqual(first['output']['sha256'], second['output']['sha256'])
        self.assertEqual(first['expected_scope_hash'], check_scopes.inventory_hash())
        self.assertFalse(first['packaged'])
        self.assertNotIn(str(REPO), first_text)  # names and hashes only, no machine paths
        a = (REPO / 'build/perf-union-probe-test/a/PerfUnionProbe.dll').read_bytes()
        b = (REPO / 'build/perf-union-probe-test/b/PerfUnionProbe.dll').read_bytes()
        self.assertEqual(a, b)

    def test_output_outside_build_is_refused(self):
        import build_probe
        with self.assertRaises(SystemExit):
            build_probe.build(Path('.'), Path('missing.dll'), REPO / 'tools/not-build')


if __name__ == '__main__':
    unittest.main()
