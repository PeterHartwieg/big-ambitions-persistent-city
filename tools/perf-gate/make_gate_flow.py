"""Generate the F4 perf-gate rig flow (perf-gate-flow/v1) from fixtures.json, scripts/ and contract-v1.json.

python tools/perf-gate/make_gate_flow.py --out <flow.json> [--md-out <flow.md>] [--movement-state <label>]

Writes a flow; runs nothing. The batch fills the run header and each window's export labels. Every step names
its op, the checks it serves, its process and build, and the existing and pending rig capabilities it needs.
Plan: docs/plans/f4-perf-gate.md, Decisions 1, 2, 3 and 8, section 7 checks G1-G15.
"""
import argparse
import hashlib
import json
import math
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
FORMAT = 'perf-gate-flow/v1'
DEFAULT_FIXTURES = HERE / 'fixtures.json'
DEFAULT_SCRIPTS = HERE / 'scripts'
DEFAULT_CONTRACT = HERE / 'contract-v1.json'

EXISTING, PENDING = 'existing', 'pending'
CAPABILITIES = {
    'rig flow run': (EXISTING, 'process launch/stop, fixture copies, machine and rig identity, cleanup restore'),
    'rig flow run autosave guard': (EXISTING, 'record, disable and restore the autosave setting'),
    'health autosave_disabled': (EXISTING, 'read back that autosave is off'),
    'RIG-MODSET': (EXISTING, 'per-flow hashed DLL set transaction and restore'),
    'produce_fixture.py': (EXISTING, 'fresh head city'),
    'FT load': (EXISTING, ''), 'FT save': (EXISTING, ''), 'FT time': (EXISTING, ''), 'FT teleport': (EXISTING, ''),
    'FT state': (EXISTING, ''), 'FT console': (EXISTING, ''),
    'RIG-VIEW camera': (EXISTING, 'camera look_at along waypoints'),
    'RIG-VIEW screen_pos': (EXISTING, 'screen_pos list for body counts'),
    'RIG-WEEKS time_machine': (EXISTING, 'time-machine skip'),
    'Player.log collection': (EXISTING, 'Player.log lines and evidence in rig flow run'),
    'RIG-PERF-UNION': (PENDING, 'stage, arm, measure, stop and page-export the F4b probe through RIG-MODSET'),
    'RIG-PRESENT': (PENDING, 'ETW presentation trace bound to the game process, QPC timestamps, CSV export'),
    'RIG-FOREGROUND': (PENDING, 'focused foreground window at declared resolution, preset, vsync and cap, read back'),
    'RIG-PRIORITY': (PENDING, 'player at High priority and the dotnet contamination sampler as flow steps'),
    'RIG-SHOPFIX': (PENDING, 'complete staffed and stocked food shop built natively in a fresh head city'),
    'RIG-UI-SCRIPT': (PENDING, 'scripted pointer and keyboard input in a locked session'),
    'RIG-PCCONFIG': (PENDING, 'per-flow PC config rows (GameClockFactor, MaximumPedestrians, MaximumVehicles)'),
    'RIG-DRIVE': (PENDING, 'drive the owned car along a route (F2 plan)'),
    'RIG-PCFIX': (PENDING, 'pc-stress-10k@1 fixture (F5 plan)'),
    'RIG-SHOPEDIT': (PENDING, 'native price edit (F6a)'),
}

# The capabilities each op needs. A step's capability list is derived from this table only.
OPS = {
    'rig.identity': ['rig flow run'], 'source.identity': ['rig flow run'], 'machine.read': ['rig flow run'],
    'game.build': ['rig flow run'], 'artifacts.hash': ['RIG-MODSET'], 'header.require': ['rig flow run'],
    'autosave.record_disable': ['rig flow run autosave guard'], 'autosave.verify_off': ['health autosave_disabled'],
    'autosave.restore': ['rig flow run autosave guard'], 'autosave.verify_restored': ['rig flow run autosave guard'],
    'settings.apply_readback': ['RIG-FOREGROUND'], 'settings.readback': ['RIG-FOREGROUND'],
    'settings.restore': ['RIG-FOREGROUND'],
    'priority.high': ['RIG-PRIORITY'], 'sampler.start': ['RIG-PRIORITY'], 'sampler.read': ['RIG-PRIORITY'],
    'sampler.stop': ['RIG-PRIORITY'],
    'process.launch': ['rig flow run'], 'process.stop': ['rig flow run'],
    'modset.apply': ['RIG-MODSET'], 'modset.restore': ['RIG-MODSET'],
    'pcconfig.record': ['RIG-PCCONFIG'], 'pcconfig.apply': ['RIG-PCCONFIG'], 'pcconfig.restore': ['RIG-PCCONFIG'],
    'fixture.produce': ['produce_fixture.py'], 'fixture.shops': ['RIG-SHOPFIX'], 'fixture.stress': ['RIG-PCFIX'],
    'fixture.car': ['RIG-DRIVE'], 'fixture.anchors': ['FT state', 'RIG-VIEW screen_pos'],
    'fixture.copy': ['rig flow run'], 'fixture.hash': ['rig flow run'], 'fixture.remove_copies': ['rig flow run'],
    'fixture.verify_inputs': ['rig flow run'],
    'ft.load': ['FT load'], 'ft.save': ['FT save'], 'ft.time': ['FT time'], 'ft.teleport': ['FT teleport'],
    'ft.state': ['FT state'], 'ft.console': ['FT console'],
    'pc.readiness': ['FT state', 'FT console'], 'native.readiness': ['FT state', 'RIG-PERF-UNION'],
    'save_epoch.read': ['FT state'],
    'wait.real': [], 'wait.game': ['FT state'], 'wait.until_game_minute': ['FT state'],
    'probe.arm': ['RIG-PERF-UNION'], 'probe.status': ['RIG-PERF-UNION'], 'probe.measure_start': ['RIG-PERF-UNION'],
    'probe.measure_stop': ['RIG-PERF-UNION'], 'probe.page': ['RIG-PERF-UNION'], 'probe.heap': ['RIG-PERF-UNION'],
    'probe.disarm': ['RIG-PERF-UNION'],
    'trace.start': ['RIG-PRESENT'], 'trace.stop': ['RIG-PRESENT'], 'trace.export': ['RIG-PRESENT'],
    'script.run': [],
    'view.camera': ['RIG-VIEW camera'], 'view.screen_pos': ['RIG-VIEW screen_pos'],
    'log.physical_diagnostics': ['Player.log collection'], 'log.collect': ['Player.log collection'],
    'drive.enter': ['RIG-DRIVE'], 'drive.route': ['RIG-DRIVE'], 'drive.exit': ['RIG-DRIVE'],
    'ui.key': ['RIG-UI-SCRIPT'], 'ui.click': ['RIG-UI-SCRIPT'], 'ui.open_screen': ['RIG-UI-SCRIPT'],
    'shop.edit_price': ['RIG-SHOPEDIT'],
    'time_machine.skip': ['RIG-WEEKS time_machine'],
    'window.receipts': ['rig flow run'], 'rig.restore_files': ['rig flow run'], 'rig.lease': ['rig flow run'],
    'native.load_done': ['FT state'],
    'host.check_scopes': [], 'host.assemble_export': [], 'host.analyze': [], 'host.attribute': [],
}

PREP_AT = 'day 3 00:30'
LOAD_OPS = {'process.launch', 'ft.load', 'fixture.stress'}
START_OPS = {'trace.start', 'probe.measure_start'}
SAVE_OPS = {'ft.save'}
WINDOW_GROUP = 'gate-matrix'
F2D_COMMIT = 'ece4878'  # F2d #169: cars follow the traffic kernel
I1_FIELDS = ['logical_population', 'business_load', 'calendar_pace', 'observed_body_load']
# Header fields observed only while measuring; required when each export is assembled, not at preflight.
OBSERVED_FIELDS = ['observed_body_load']


class FlowError(Exception):
    pass


def minute_of(hhmm):
    h, m = hhmm.split(':')
    return int(h) * 60 + int(m)


def hhmm(minute):
    minute %= 24 * 60
    return f'{minute // 60:02d}:{minute % 60:02d}'


def capabilities_of(op):
    if op not in OPS:
        raise FlowError(f'unknown op {op!r}')
    names = OPS[op]
    return {EXISTING: sorted(n for n in names if CAPABILITIES[n][0] == EXISTING),
            PENDING: sorted(n for n in names if CAPABILITIES[n][0] == PENDING)}


def load_inputs(fixtures_path=DEFAULT_FIXTURES, scripts_dir=DEFAULT_SCRIPTS, contract_path=DEFAULT_CONTRACT):
    fixtures = json.loads(Path(fixtures_path).read_text(encoding='utf-8'))
    contract = json.loads(Path(contract_path).read_text(encoding='utf-8'))
    scripts = {}
    for path in sorted(Path(scripts_dir).glob('*.json')):
        script = json.loads(path.read_text(encoding='utf-8'))
        if script.get('format') != 'perf-gate-script/v1' or script.get('name') != path.stem:
            raise FlowError(f'{path.name}: not a perf-gate-script/v1 named {path.stem}')
        for part in ('setup', 'loop', 'teardown', 'samplers'):
            for step in script.get(part, []):
                capabilities_of(step['op'])
        scripts[path.stem] = script
    digest = hashlib.sha256()
    for path in [Path(fixtures_path), Path(contract_path)] + sorted(Path(scripts_dir).glob('*.json')):
        digest.update(path.name.encode() + b'\0' + path.read_bytes() + b'\0')
    return fixtures, scripts, contract, digest.hexdigest()


class Flow:
    def __init__(self, fixtures, scripts, contract):
        self.fixtures, self.scripts, self.contract = fixtures, scripts, contract
        self.steps, self.windows = [], []
        self.process = None
        self.process_count = 0
        self.process_info = {}
        self.armed = False
        self.window_in_process = 0
        w = contract['windows']
        pace = fixtures['declared_pace']
        self.min_clean = w['min_clean_real_seconds']
        self.warmup = w['warmup_real_seconds']
        planned = self.min_clean * (1 + fixtures['window_margin_fraction'])
        # Game span chosen so even a build at fast_pace_fraction of the declared real seconds per game minute covers
        # the planned real time; rounding guards float noise.
        self.fast_pace = pace['real_seconds_per_game_minute'] * fixtures['fast_pace_fraction']
        self.span = math.ceil(round(planned / self.fast_pace, 6))
        self.planned = round(self.span * pace['real_seconds_per_game_minute'], 3)
        self.abort_after = round(self.planned * fixtures['abort_after_planned_fraction'], 3)
        self.lead = fixtures['checkpoint_lead_game_minutes']

    # -- steps -------------------------------------------------------------------------------------------------
    def add(self, op, checks, args=None, build=None, window=None, note=None, **extra):
        step = dict(id=f'{len(self.steps) + 1:04d}', op=op, checks=list(checks), process=self.process, build=build,
                    window=window, args=args or {}, capabilities=capabilities_of(op))
        if note:
            step['note'] = note
        step.update(extra)
        self.steps.append(step)
        return step

    def checkpoint_name(self, fixture, minute):
        return f'{fixture}@d3-{hhmm(minute).replace(":", "")}'

    def checkpoints(self, fixture):
        spec = self.fixtures['fixtures'][fixture]
        minutes = {minute_of(self.fixtures['windows'][w]['start']) - self.lead for w in spec['windows']}
        minutes.update(minute_of(m) for m in spec['extra_checkpoints'])
        return sorted(minutes)

    # -- process lifecycle -------------------------------------------------------------------------------------
    def launch(self, checks, build, fixture, settings='primary', pc_config=None):
        self.process_count += 1
        self.process = self.process_count
        self.window_in_process = 0
        self.armed = False
        # Candidates always apply the complete declared config, so a profile left at other values cannot leak in.
        rows = dict(self.fixtures['pc_config_defaults'], **(pc_config or {})) if build == 'candidate' else 'n/a'
        self.process_info[self.process] = dict(build=build, fixture=fixture, settings=settings,
                                               pc_config=rows, checks=list(checks))
        mods = ('declared mod set with PersistentCity and the F4b probe' if build == 'candidate'
                else 'declared mod set without PersistentCity, with the F4b probe and pinned 0Harmony.dll')
        self.add('modset.apply', checks, dict(set=mods), build)
        if build == 'candidate':
            self.add('pcconfig.apply', checks, dict(rows=rows), build)
        self.add('process.launch', checks, dict(flags=['-agent-tests', '-rig-test'], profile='isolated Agent Tests'), build)
        self.add('autosave.verify_off', checks, dict(after='launch'), build)
        self.add('priority.high', checks, {}, build)
        self.add('settings.apply_readback', checks, dict(self.fixtures['declared_settings'][settings], focused=True), build)
        if build == 'candidate':
            self.add('ft.state', checks, dict(record='pc_config keys in effect', expect=rows), build)

    def end_process(self, checks):
        build = self.process_info[self.process]['build']
        if self.armed:
            self.add('probe.disarm', checks, {}, build)
            self.armed = False
        self.add('process.stop', checks, {}, build)
        self.process = None

    def load(self, checks, fixture, minute, factor):
        build = self.process_info[self.process]['build']
        if self.armed:
            self.add('probe.disarm', checks, dict(before='load'), build)
            self.armed = False
        source = self.checkpoint_name(fixture, minute)
        copy = dict(source=source, disposable=True, never_save=build == 'reference')
        self.add('fixture.copy', checks, copy, build)
        self.add('ft.load', checks, dict(save=f'copy of {source}'), build)
        self.add('autosave.verify_off', checks, dict(after='load'), build)
        self.add('native.load_done', checks, dict(expect='native load finished (the probe refuses to arm during a load)'),
                 build)
        mode = 'candidate' if build == 'candidate' else 'reference'
        arm = dict(mode=mode, expected_scope_hash='${probe_manifest.expected_scope_hash}')
        if mode == 'reference':
            arm['factor'] = factor
        self.add('probe.arm', checks, arm, build)
        self.armed = True
        expect = ('coverage_complete true; no unexpected or unbindable rows' if mode == 'candidate'
                  else 'pace_shim active at factor; PC scopes n/a')
        self.add('probe.status', checks, dict(expect=expect), build)

    def ready(self, checks):
        build = self.process_info[self.process]['build']
        if build == 'candidate':
            self.add('pc.readiness', checks, dict(expect='simulation ready; accounting text no longer says held',
                                                  record='L3 held seconds after load'), build)
        else:
            self.add('native.readiness', checks, dict(expect='native load finished; pace shim reports active'), build)

    # -- measurement -------------------------------------------------------------------------------------------
    @staticmethod
    def probe_label(wid):
        """F4b accepts letters, digits, '-', '_' and '.', at most 64 characters."""
        label = wid.replace('@', '_')
        if len(label) > 64:
            raise FlowError(f'probe label too long: {label}')
        return label

    def _open(self, checks, wid, build, trace=True, probe=True):
        """Receipts immediately before a window or segment starts, then its start steps."""
        self.add('autosave.verify_off', checks, dict(before='window'), build, wid)
        if build == 'candidate':
            # SaveEpochDiagnostics is a Persistent City projection; the reference uses the probe's save counter.
            self.add('save_epoch.read', checks, dict(record='SaveEpochDiagnostics.sequence at start'), build, wid)
        self.add('sampler.read', checks, dict(expect='no task-owned or foreign dotnet build or test active'), build, wid)
        self.add('settings.readback', checks, dict(expect='High priority, focused, declared resolution, preset, cap, vsync'),
                 build, wid)
        if trace:
            self.add('trace.start', checks, dict(window=self.probe_label(wid)), build, wid)
        if probe:
            self.add('probe.measure_start', checks, dict(window=self.probe_label(wid)), build, wid)

    def _close(self, checks, wid, build, trace=True, probe=True):
        if probe:
            self.add('probe.measure_stop', checks, {}, build, wid)
            self.add('probe.status', checks, dict(expect=dict(saves_in_window=0, scene_events_in_window=0),
                                                  note='native onSaveGame counter, reset at measure-start'), build, wid)
        if trace:
            self.add('trace.stop', checks, {}, build, wid)
        if build == 'candidate':
            self.add('save_epoch.read', checks, dict(expect='sequence unchanged since window start'), build, wid)
        self.add('sampler.read', checks, dict(expect='no dotnet activity during the window'), build, wid)
        if probe:
            self.add('probe.page', checks, dict(sections=['frames', 'ticks', 'holds', 'calls', 'diagnostics', 'scopes']),
                     build, wid)
        if trace:
            self.add('trace.export', checks, dict(format='csv'), build, wid)

    def _script_steps(self, script, part, build):
        return [dict(s) for s in self.scripts[script].get(part, []) if build in s.get('builds', [build])]

    def _run(self, checks, wid, build, script, until, drain_s=30):
        loop = self._script_steps(script, 'loop', build)
        samplers = [dict(s) for s in self.scripts[script].get('samplers', [])]
        for s in samplers + loop:
            s['capabilities'] = capabilities_of(s['op'])
        samplers.append(dict(op='probe.page', args=dict(sections=['frames', 'ticks', 'diagnostics'], drain=True),
                             every_real_seconds=drain_s, capabilities=capabilities_of('probe.page'),
                             feeds='clean_real_seconds for the stop condition'))
        step = self.add('script.run', checks, dict(script=script, repeat_until_stop=True), build, wid,
                        loop=loop, samplers=samplers, until=until)
        # script.run itself has no op capability; it needs what its loop and samplers need.
        for part in loop + samplers:
            for kind in (EXISTING, PENDING):
                step['capabilities'][kind] = sorted(set(step['capabilities'][kind]) | set(part['capabilities'][kind]))
        return step

    def window(self, checks, condition, script, arm, repetition, fixture, start_minute, factor, group=WINDOW_GROUP,
               min_clean=None, game_span=None, native_speed=1, prime=False, kind='window', gate_start=True,
               matched=False):
        """One measured window. With prime=True this is the discarded first window of the process."""
        build = self.process_info[self.process]['build']
        first = self.window_in_process == 0
        if first and not prime:
            raise FlowError('the first window of a process must be a discarded prime window')
        self.window_in_process += 1
        wid = (f'p{self.process:02d}-prime' if prime else
               f'{condition}.{script}.{arm}.{build}.r{repetition}')
        if any(w['id'] == wid for w in self.windows):
            raise FlowError(f'duplicate window id {wid}')
        # The window's speed is set first, so warm-up and the start-minute gate run at the measured pace.
        self.add('ft.time', checks, dict(speed=native_speed), build, wid, phase='setup')
        for s in self._script_steps(script, 'setup', build):
            self.add(s['op'], checks, s.get('args'), build, wid, phase='setup')
        self.ready(checks)
        self.add('wait.real', checks, dict(seconds=self.warmup, discard=True, note='warm-up after readiness'), build, wid,
                 phase='warmup')
        if gate_start and not prime:
            self.add('wait.until_game_minute', checks,
                     dict(minute=start_minute, at=hhmm(start_minute), abort_if_past_by_minutes=1), build, wid,
                     phase='warmup')
        self._open(checks, wid, build)
        if prime:
            until = dict(all=[dict(real_seconds_at_least=self.fixtures['prime_window_real_seconds'])])
            clean_floor = None
        else:
            clean_floor = min_clean or self.min_clean
            if matched:
                # Both builds of a matched pair stop at the same game minute (P4 needs spans within 1 minute).
                until = dict(all=[dict(game_minutes_since_start_at_least=game_span)],
                             short_if_clean_real_seconds_below=clean_floor)
            else:
                until = dict(all=[dict(clean_real_seconds_at_least=clean_floor)])
                if game_span:
                    until['all'].append(dict(game_minutes_since_start_at_least=game_span))
            planned = max(clean_floor, game_span * self.fixtures['declared_pace']['real_seconds_per_game_minute']
                          / native_speed if game_span else 0)
            until['abort_after_real_seconds'] = round(max(self.abort_after, planned * 2), 3)
            until['on_abort'] = 'window aborted (short): reported, does not count'
        until['clean_frames'] = 'not diagnostic, partial, cross-frame or late-boundary; warm-up frames precede measure-start'
        self._run(checks, wid, build, script, until)
        for s in self._script_steps(script, 'teardown', build):
            self.add(s['op'], checks, s.get('args'), build, wid, phase='teardown')
        self._close(checks, wid, build)
        receipts = list(self.contract['receipts']['required'])
        na = self.contract['receipts']['reference_may_be_na'] if build == 'reference' else []
        self.add('window.receipts', checks, dict(required=receipts, may_be_na=na), build, wid)
        labels = dict(id=wid, probe_label=self.probe_label(wid), source='native', condition=condition, script=script,
                      build=build,
                      arm=arm if not prime else 'primary', repetition=repetition, native_speed=native_speed,
                      checkpoint_minute=start_minute, first_in_process=first,
                      game_span_minutes='${fill}', clean_real_seconds='${fill}', aborted='${fill}',
                      receipts='${fill}', density='${fill}', bodies='${fill}')
        if build == 'reference':
            labels['reference_pace_ratio'] = '${fill}'
        entry = dict(id=wid, kind='prime' if prime else kind, checks=list(checks), process=self.process, build=build,
                     fixture=fixture, script=script, export_group=group, prime=prime, first_in_process=first,
                     min_clean_real_seconds=clean_floor, game_span_minutes=game_span, factor=factor, matched=matched,
                     labels=labels)
        self.windows.append(entry)
        return entry

    def segment(self, checks, sid, group, until, probe=True, trace=True, script=None, native_speed=None, op_steps=(),
                latency=False):
        """A measurement segment that is not a gate window (G2 overhead, G3 alignment, G9 pace, G10 latency)."""
        build = self.process_info[self.process]['build']
        if native_speed is not None:
            self.add('ft.time', checks, dict(speed=native_speed), build, sid, phase='setup')
        self._open(checks, sid, build, trace=trace, probe=probe)
        if script:
            self._run(checks, sid, build, script, until)
        else:
            for op, args in op_steps:
                self.add(op, checks, args, build, sid)
            self.add('wait.real' if 'real_seconds_at_least' in until else 'wait.game', checks,
                     dict(until=until), build, sid)
        self._close(checks, sid, build, trace=trace, probe=probe)
        self.windows.append(dict(id=sid, kind='segment', checks=list(checks), process=self.process, build=build,
                                 export_group=group, prime=False, first_in_process=False, until=until,
                                 latency=latency))


def preflight(f, movement_state):
    c = ['G1']
    f.add('rig.identity', c, dict(record=['rig_commit', 'rig_overlay']))
    f.add('source.identity', c, dict(record=['source_commit', 'movement_state'], movement_state=movement_state,
                                     rule=f'movement=pre-F2d unless the candidate commit contains F2d ({F2D_COMMIT}, #169); '
                                          'then movement=F2d, or movement=F2e when F2e (#172) is also present'))
    f.add('artifacts.hash', c, dict(record=['pc_dll_sha256', 'probe_dll_sha256', 'expected_scope_list_hash', 'mod_set'],
                                    probe_manifest='build/perf-union-probe/build-manifest.json'))
    f.add('host.check_scopes', c, dict(command='python tools/perf-union-probe/check_scopes.py',
                                       expect='exit 0; expected list hash equals the manifest'))
    f.add('game.build', c, dict(record='game_build'))
    f.add('machine.read', c, dict(record=f.contract['header']['machine_fields']))
    f.add('autosave.record_disable', c, dict(record='original setting, once'))
    f.add('autosave.verify_off', c, dict(after='disable'))
    f.add('settings.apply_readback', c, dict(f.fixtures['declared_settings']['primary'], target='isolated profile preferences',
                                             note='every launch sets and reads these back again with focus'))
    f.add('pcconfig.record', c, dict(record='original PC config of the isolated profile, once'))
    f.add('sampler.start', c, dict(sampler='dotnet contamination sampler, whole batch'))


def produce_fixtures(f):
    """Producers that own their lifecycle run with no player running and no flow lease held."""
    for name, spec in f.fixtures['fixtures'].items():
        produce = spec.get('produce')
        if produce:
            f.add('fixture.produce', ['fixtures'], dict(produce, fixture=name), None, requires=dict(
                player_running=False, flow_lease_held=False))
    f.add('rig.lease', ['fixtures'], dict(action='acquire', note='after every producer has released its own lease'))


def prepare_fixtures(f):
    for name, spec in f.fixtures['fixtures'].items():
        if not spec['prepare']:
            continue
        c = ['fixtures']
        f.launch(c, 'candidate', name)
        for s in spec['prepare']:
            f.add(s['op'], c, s.get('args'), 'candidate', for_checks=s.get('for_checks'))
            if s['op'] in LOAD_OPS:
                f.add('autosave.verify_off', c, dict(after=s['op']), 'candidate')
        survey = spec.get('survey')
        if survey:
            # Survey on a disposable copy, then reload the preparation save so checkpoints go forward in time.
            prep = f'{name}@prep'
            f.add('ft.save', c, dict(save=prep, at=PREP_AT, outside_windows=True), 'candidate')
            f.add('fixture.copy', c, dict(source=prep, disposable=True, never_save=True), 'candidate')
            f.add('ft.load', c, dict(save=f'copy of {prep}'), 'candidate')
            f.add('autosave.verify_off', c, dict(after='load'), 'candidate')
            f.add('ft.time', c, dict(advance_to=survey['at'], speed=3), 'candidate')
            f.add('fixture.anchors', c, dict(anchors=survey['anchors'], survey_game_minutes=survey['game_minutes'],
                                             rule=survey['rule']), 'candidate')
            f.add('ft.load', c, dict(save=prep), 'candidate')
            f.add('autosave.verify_off', c, dict(after='load'), 'candidate')
        for minute in f.checkpoints(name):
            at = f'day 3 {hhmm(minute)}'
            f.add('ft.time', c, dict(advance_to=at, speed=3), 'candidate')
            f.add('ft.save', c, dict(save=f.checkpoint_name(name, minute), at=at, outside_windows=True), 'candidate')
            f.add('fixture.hash', c, dict(save=f.checkpoint_name(name, minute), record='fixtures[].provenance_hash'),
                  'candidate')
        f.end_process(c)


def warm(f, checks, speed=1):
    """Warm-up after a reload outside window(): speed first, then readiness and 60 s discarded."""
    build = f.process_info[f.process]['build']
    f.add('ft.time', checks, dict(speed=speed), build)
    f.ready(checks)
    f.add('wait.real', checks, dict(seconds=f.warmup, discard=True), build)


def g2_g3(f):
    factor = f.fixtures['declared_pace']['game_clock_factor']
    start = minute_of(f.fixtures['windows']['W-morning']['start'])
    c = ['G2']
    f.launch(c, 'candidate', 'FT-F4-Declared')
    f.load(c, 'FT-F4-Declared', start - f.lead, factor)
    f.window(c, 'G2', 'S-stationary', 'primary', 0, 'FT-F4-Declared', start, factor, group='g2-g3', prime=True)
    f.load(c, 'FT-F4-Declared', start - f.lead, factor)
    warm(f, c)
    for i in (1, 2):
        f.segment(c, f'G2.armed.{i}', 'g2-g3', dict(real_seconds_at_least=120))
        f.add('probe.disarm', c, {}, 'candidate')
        f.armed = False
        f.segment(c, f'G2.disarmed.{i}', 'g2-g3', dict(real_seconds_at_least=120), probe=False)
        f.add('probe.arm', c, dict(mode='candidate', expected_scope_hash='${probe_manifest.expected_scope_hash}'),
              'candidate')
        f.armed = True
    c = ['G3']
    f.load(c, 'FT-F4-Declared', start - f.lead, factor)
    warm(f, c)
    f.segment(c, 'G3.alignment', 'g2-g3', dict(clean_real_seconds_at_least=f.min_clean), script='S-path')
    f.end_process(c)


def window_checks(window, script):
    checks = {'S-stationary': ['G4'], 'S-path': ['G4', 'G5'] if window == 'W-evening' else ['G4'],
              'S-sweep': ['G5'], 'S-drive': ['G6'], 'S-manage': ['G7']}[script]
    if window in ('W-lunch', 'W-evening', 'W-midnight') and script in ('S-stationary', 'S-path'):
        checks = checks + ['G8']
    return checks


def abab(f, condition, window_name, scripts, arm, repetitions, fixture, factor, settings='primary', pc_config=None,
         builds=('reference', 'candidate'), checks=None):
    """ABAB per condition: repetition r runs every build in order, one process per build and repetition.
    `checks` overrides the per-script check list for secondary arms."""
    start = minute_of(f.fixtures['windows'][window_name]['start'])

    def checks_of(script):
        return list(checks) if checks else window_checks(window_name, script)
    for rep in range(1, repetitions + 1):
        for build in builds:
            process_checks = sorted({c for s in scripts for c in checks_of(s)})
            f.launch(process_checks, build, fixture, settings, pc_config if build == 'candidate' else None)
            f.load(process_checks, fixture, start - f.lead, factor)
            f.window(process_checks, condition, scripts[0], arm, rep, fixture, start, factor, prime=True)
            for script in scripts:
                if build not in f.scripts[script]['builds']:
                    continue
                f.load(checks_of(script), fixture, start - f.lead, factor)
                f.window(checks_of(script), condition, script, arm, rep, fixture, start, factor, game_span=f.span,
                         matched='reference' in builds)
            f.end_process(process_checks)


def matrix(f):
    factor = f.fixtures['declared_pace']['game_clock_factor']
    for name, w in f.fixtures['windows'].items():
        abab(f, name, name, w['scripts'], 'primary', w['repetitions'], 'FT-F4-Declared', factor)
    # Decision 1 report-only arm: the evening window at the High preset.
    abab(f, 'W-evening@High', 'W-evening', ['S-path'], 'report-only', 2, 'FT-F4-Declared', factor,
         settings='report_only_high', checks=['G4'])
    # Decision 3 presentation stress arm at caps 256/64, once, candidate only (G5).
    abab(f, 'W-evening@caps-256-64', 'W-evening', ['S-path'], 'report-only', 1, 'FT-F4-Declared', factor,
         pc_config=dict(MaximumPedestrians=256, MaximumVehicles=64), builds=('candidate',), checks=['G5'])


def g9(f):
    c = ['G9']
    fixture = 'FT-F4-FastestTraveller'
    base = f.fixtures['fixtures'][fixture]['base']
    cfg = f.fixtures['fixtures'][fixture]['pc_config']
    minute = minute_of('06:00')
    f.launch(c, 'candidate', fixture, pc_config=cfg)
    f.load(c, base, minute, cfg['GameClockFactor'])
    f.window(c, 'G9', 'S-stationary', 'primary', 0, fixture, minute, cfg['GameClockFactor'], group='g9', prime=True)
    f.load(c, base, minute, cfg['GameClockFactor'])
    warm(f, c)
    for speed, minutes in ((1, 60), (2, 60), (3, 60), (6, 240)):
        f.segment(c, f'G9a.speed{speed}', 'g9', dict(game_minutes_since_start_at_least=minutes), native_speed=speed)
    f.segment(c, 'G9a.time-machine-120', 'g9', dict(game_minutes_since_start_at_least=120), trace=False,
              op_steps=[('time_machine.skip', dict(game_minutes=120))])
    f.load(c, base, minute, cfg['GameClockFactor'])
    warm(f, c)
    f.segment(c, 'G9b.day-speed3', 'g9', dict(game_minutes_since_start_at_least=1440), native_speed=3)
    f.end_process(c)
    f.launch(c, 'reference', fixture)
    f.load(c, base, minute, cfg['GameClockFactor'])
    f.window(c, 'G9', 'S-stationary', 'primary', 0, fixture, minute, cfg['GameClockFactor'], group='g9', prime=True)
    f.load(c, base, minute, cfg['GameClockFactor'])
    warm(f, c)
    f.segment(c, 'G9c.reference-day-speed3', 'g9', dict(game_minutes_since_start_at_least=1440), native_speed=3)
    f.end_process(c)


def g13(f):
    c = ['G13']
    factor = f.fixtures['declared_pace']['game_clock_factor']
    minute = minute_of('10:00')
    f.launch(c, 'candidate', 'FT-F4-Declared')
    f.load(c, 'FT-F4-Declared', minute, factor)
    f.window(c, 'G13', 'S-session', 'primary', 0, 'FT-F4-Declared', minute, factor, group='g13', prime=True)
    f.load(c, 'FT-F4-Declared', minute, factor)
    f.window(c, 'G13-session', 'S-session', 'primary', 1, 'FT-F4-Declared', minute, factor, group='g13',
             min_clean=f.scripts['S-session']['min_real_seconds'], gate_start=False)
    f.end_process(c)


def g14(f):
    c = ['G14']
    factor = f.fixtures['declared_pace']['game_clock_factor']
    minute = minute_of('06:00')
    f.launch(c, 'candidate', 'FT-F4-Declared')
    f.load(c, 'FT-F4-Declared', minute, factor)
    f.window(c, 'G14', 'S-stationary', 'primary', 0, 'FT-F4-Declared', minute, factor, group='g14', prime=True)
    f.load(c, 'FT-F4-Declared', minute, factor)
    for day in (1, 2, 3):
        f.window(c, 'G14-soak', 'S-stationary', 'primary', day, 'FT-F4-Declared', minute, factor, group='g14',
                 game_span=1440, native_speed=3, gate_start=False)
        f.add('probe.heap', c, dict(record=f'R1.heap_mb_per_day[{day - 1}]', outside_windows=True), 'candidate')
    f.end_process(c)


def g15(f):
    factor = f.fixtures['declared_pace']['game_clock_factor']
    for name in f.fixtures['fixtures']['FT-F4-Stress10k']['windows']:
        abab(f, f'{name}@Stress10k', name, ['S-path'], 'stress', 1, 'FT-F4-Stress10k', factor, builds=('candidate',),
             checks=['G15'])


def g10(f):
    c = ['G10']
    factor = f.fixtures['declared_pace']['game_clock_factor']
    minute = minute_of('06:00')
    name = 'FT-F4-Declared'
    for label in ('cold start', 'fresh-process load'):
        f.launch(c, 'candidate', name)
        f.add('ft.state', c, dict(record=f'L1 start timestamp ({label})'), 'candidate')
        f.load(c, name, minute, factor)
        f.ready(c)
        f.add('ft.state', c, dict(record='L1 population active; L2 CompleteInitialization (F7a diagnostics); L3'),
              'candidate')
        if label == 'fresh-process load':
            f.load(c, name, minute, factor)
            f.ready(c)
            f.add('ft.state', c, dict(record='in-session reload: L2 activation and L3 held seconds'), 'candidate')
            f.add('ft.time', c, dict(speed=1), 'candidate')
            f.segment(c, 'G10.skip-120.candidate', 'g10', dict(game_minutes_since_start_at_least=120), trace=False,
                      op_steps=[('time_machine.skip', dict(game_minutes=120, record='L4 real_s'))], latency=True)
        f.end_process(c)
    f.launch(c, 'reference', name)
    f.load(c, name, minute, factor)
    f.ready(c)
    f.add('ft.time', c, dict(speed=1), 'reference')
    f.segment(c, 'G10.skip-120.reference', 'g10', dict(game_minutes_since_start_at_least=120), trace=False,
              op_steps=[('time_machine.skip', dict(game_minutes=120, record='L4 reference_real_s'))], latency=True)
    f.end_process(c)


def g11(f):
    c = ['G11']
    f.add('log.collect', c, dict(collect=['Player.log', 'RLO frame-diagnostic lines', 'fix-128 summary lines']))
    groups = sorted({w['export_group'] for w in f.windows})
    for g in groups:
        f.add('host.assemble_export', c, dict(group=g, format='perf-gate-export/v1', require_header=OBSERVED_FIELDS,
                                              inputs=['run_header', 'window labels', 'probe pages', 'trace CSV',
                                                      'measurements (S5, L1-L4, R1)']))
        f.add('host.attribute', c, dict(group=g, command='F4c attribute.py over presented and whole-frame intervals >= 50 ms',
                                        inputs=['probe pages', 'trace CSV', 'Player.log']))
        f.add('host.analyze', c, dict(group=g, command=f'python tools/perf-gate/analyze.py export-{g}.json '
                                                       f'--json-out report-{g}.json --md-out report-{g}.md'))


def cleanup(f):
    """Cleanup runs at the end and also when the batch stops early (header.require, an abort or an error)."""
    c = ['cleanup']
    if f.process is not None:
        f.end_process(c)
    for op, args in (('process.stop', dict(note='no-op when no player is running')), ('sampler.stop', {}),
                     ('modset.restore', dict(to='original mod DLL set')),
                     ('pcconfig.restore', dict(rows='original PC config recorded in G1')),
                     ('settings.restore', dict(to='original resolution, preset, vsync, cap')),
                     ('autosave.restore', dict(to='original setting recorded in G1')), ('autosave.verify_restored', {}),
                     ('fixture.remove_copies', {}), ('fixture.verify_inputs', {}),
                     ('rig.restore_files', dict(restore='preexisting rig files and preferences')),
                     ('rig.lease', dict(action='release'))):
        f.add(op, c, args, on_stop=True)


def run_header(f, movement_state):
    def by_op(op, contains=None):
        for s in f.steps:
            if s['op'] == op and (contains is None or contains in json.dumps(s['args'])):
                return s['id']
        raise FlowError(f'no step fills {op} {contains or ""}')
    filled = {
        'source_commit': by_op('source.identity'), 'pc_dll_sha256': by_op('artifacts.hash'),
        'probe_dll_sha256': by_op('artifacts.hash'), 'expected_scope_list_hash': by_op('artifacts.hash'),
        'game_build': by_op('game.build'), 'mod_set': by_op('artifacts.hash'), 'rig_commit': by_op('rig.identity'),
        'rig_overlay': by_op('rig.identity'), 'machine': by_op('machine.read'),
        'settings_read_back': by_op('settings.apply_readback'), 'pc_config': by_op('ft.state', 'pc_config'),
        'fixtures': by_op('fixture.hash'), 'contract_version': None, 'movement_state': by_op('source.identity'),
        'rig_capabilities': by_op('rig.identity'),
        'logical_population': by_op('ft.console', 'pc population manifest'),
        'business_load': by_op('fixture.shops'),
        'calendar_pace': None,
        'observed_body_load': by_op('script.run', 'S-path'),
    }
    header = {}
    for field in f.contract['header']['required'] + I1_FIELDS:
        header[field] = dict(value='${fill}', filled_by=filled[field],
                             required_at='export' if field in OBSERVED_FIELDS else 'preflight')
    header['contract_version'].update(value=f.contract['contract'])
    header['calendar_pace'].update(value=f.fixtures['declared_pace'], measured_by=by_op('probe.page'),
                                   note='declared pace at preflight; measured pace per window from probe clock reads')
    header['movement_state']['value'] = movement_state or '${fill}'
    header['machine']['fields'] = f.contract['header']['machine_fields']
    header['settings_read_back']['fields'] = f.contract['header']['settings_fields']
    header['settings_read_back']['declared'] = f.fixtures['declared_settings']['primary']
    header['pc_config']['declared'] = f.fixtures['pc_config_defaults']
    header['fixtures']['names'] = sorted(f.fixtures['fixtures'])
    header['calendar_pace']['declared'] = f.fixtures['declared_pace']
    header['logical_population']['declared'] = f.fixtures['fixtures']['FT-F4-Declared']['expected_population']
    header['business_load']['declared'] = f.fixtures['fixtures']['FT-F4-Declared']['business_load']
    header['observed_body_load']['note'] = 'per window: bodies and density from the script samplers'
    header['rig_capabilities']['used'] = sorted({n for s in f.steps for k in (EXISTING, PENDING)
                                                for n in s['capabilities'][k]})
    return header


CHECK_ORDER = ['G1', 'fixtures', 'G2', 'G3', 'G4', 'G5', 'G6', 'G7', 'G8', 'G9', 'G13', 'G14', 'G15', 'G10', 'G11',
               'cleanup']


def check_summary(f):
    out = {}
    fixture_steps = {}
    for s in f.steps:
        if 'fixtures' in s['checks'] and s['process'] is not None:
            fixture_steps.setdefault(f.process_info[s['process']]['fixture'], []).append(s)

    def fixture_pending(fixture, check):
        return {n for s in fixture_steps.get(fixture, []) if s.get('for_checks') is None or check in s['for_checks']
                for n in s['capabilities'][PENDING]}
    for check in CHECK_ORDER:
        steps = [s for s in f.steps if check in s['checks']]
        pending = {n for s in steps for n in s['capabilities'][PENDING]}
        fixtures_used = {f.process_info[s['process']]['fixture'] for s in steps if s['process'] is not None}
        via_fixture = set()
        for fx in fixtures_used:
            via_fixture |= fixture_pending(fx, check)
            via_fixture |= fixture_pending(f.fixtures['fixtures'][fx].get('base'), check)
        out[check] = dict(steps=len(steps), windows=sum(1 for w in f.windows if check in w['checks']),
                          existing=sorted({n for s in steps for n in s['capabilities'][EXISTING]}),
                          pending=sorted(pending),
                          pending_via_fixture=sorted(via_fixture - pending) if check != 'fixtures' else [])
    return out


def estimate_real_seconds(f):
    cost = {'process.launch': 90, 'ft.load': 60, 'pc.readiness': 30, 'native.readiness': 10, 'fixture.produce': 180,
            'ft.save': 20, 'process.stop': 15}
    total = sum(cost.get(s['op'], 1) for s in f.steps)
    for s in f.steps:
        if s['op'] == 'wait.real' and 'seconds' in s['args']:
            total += s['args']['seconds']
    for w in f.windows:
        if w['kind'] == 'prime':
            total += f.fixtures['prime_window_real_seconds']
        elif w['kind'] == 'window':
            span = w['game_span_minutes']
            total += max(w['min_clean_real_seconds'], span * f.fixtures['declared_pace']['real_seconds_per_game_minute']
                         / w['labels']['native_speed'] if span else 0)
    return round(total)


def build_flow(fixtures, scripts, contract, inputs_sha256, movement_state=None):
    f = Flow(fixtures, scripts, contract)
    preflight(f, movement_state)
    produce_fixtures(f)
    prepare_fixtures(f)
    f.add('header.require', ['G1'], dict(fields=[x for x in contract['header']['required'] + I1_FIELDS
                                                 if x not in OBSERVED_FIELDS],
                                         on_missing='stop the batch before timing; run the on_stop steps'))
    g2_g3(f)
    matrix(f)
    g9(f)
    g13(f)
    g14(f)
    g15(f)
    g10(f)
    g11(f)
    cleanup(f)
    pending_index = {}
    for s in f.steps:
        for n in s['capabilities'][PENDING]:
            pending_index.setdefault(n, []).append(s['id'])
    return {
        'format': FORMAT,
        'plan': 'docs/plans/f4-perf-gate.md, Decisions 1, 2, 3 and 8; section 7',
        'contract_version': contract['contract'],
        'inputs_sha256': inputs_sha256,
        'runs_nothing': 'This file is a flow description. The batched rig executes it; the generator does not.',
        'rules': dict(min_clean_real_seconds=f.min_clean, warmup_real_seconds=f.warmup,
                      window_game_span_minutes=f.span, window_planned_real_seconds=f.planned,
                      window_abort_after_real_seconds=f.abort_after, checkpoint_lead_game_minutes=f.lead,
                      order='ABAB per condition: repetition r runs reference then candidate',
                      first_window_per_process='discarded prime window',
                      matched_stop='matched windows stop at the game span only; a window below the clean floor is short',
                      on_stop='if the batch stops early, run every step with on_stop true, in order',
                      autosave='recorded and disabled in G1; verified off after every launch and load and before every window; restored at cleanup',
                      saves='only in fixture preparation, outside windows; the reference arm never saves',
                      excluded=dict(G12='save-specific segment, requested separately (F7 plan section 7); never in this flow')),
        'capabilities': {n: dict(status=s, what=w) for n, (s, w) in CAPABILITIES.items()},
        'run_header': run_header(f, movement_state),
        'checks': check_summary(f),
        'pending_capabilities': {n: dict(steps=len(ids), first=ids[0]) for n, ids in sorted(pending_index.items())},
        'processes': [dict(process=p, **i) for p, i in sorted(f.process_info.items())],
        'windows': f.windows,
        'estimated_real_seconds': estimate_real_seconds(f),
        'steps': f.steps,
    }


def markdown(flow):
    lines = [f"# Perf gate flow ({flow['format']})", '',
             f"Contract `{flow['contract_version']}`. Inputs SHA-256 `{flow['inputs_sha256'][:16]}`. "
             f"{len(flow['steps'])} steps, {len(flow['processes'])} processes, "
             f"{sum(1 for w in flow['windows'] if w['kind'] == 'window')} gate windows, "
             f"{sum(1 for w in flow['windows'] if w['kind'] == 'prime')} discarded prime windows, "
             f"{sum(1 for w in flow['windows'] if w['kind'] == 'segment')} segments. "
             f"Estimated {flow['estimated_real_seconds'] / 3600:.1f} real hours.", '',
             '| Check | Steps | Windows | Pending capabilities | Pending via fixture |', '| --- | --- | --- | --- | --- |']
    for check, s in flow['checks'].items():
        lines.append(f"| {check} | {s['steps']} | {s['windows']} | {', '.join(s['pending']) or '-'} | "
                     f"{', '.join(s['pending_via_fixture']) or '-'} |")
    lines += ['', '| Header field | Filled by step |', '| --- | --- |']
    for field, spec in flow['run_header'].items():
        lines.append(f"| {field} | {spec['filled_by'] or 'constant ' + str(spec['value'])} |")
    return '\n'.join(lines) + '\n'


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument('--out', type=Path, required=True)
    parser.add_argument('--md-out', type=Path)
    parser.add_argument('--movement-state', help='header movement label, for example movement=pre-F2d; '
                                                 'default: filled by the batch from the candidate commit')
    parser.add_argument('--fixtures', type=Path, default=DEFAULT_FIXTURES)
    parser.add_argument('--scripts', type=Path, default=DEFAULT_SCRIPTS)
    parser.add_argument('--contract', type=Path, default=DEFAULT_CONTRACT)
    args = parser.parse_args(argv)
    try:
        flow = build_flow(*load_inputs(args.fixtures, args.scripts, args.contract), movement_state=args.movement_state)
    except FlowError as e:
        print(f'REFUSED: {e}', file=sys.stderr)
        return 2
    args.out.parent.mkdir(parents=True, exist_ok=True)
    args.out.write_text(json.dumps(flow, indent=1, sort_keys=True) + '\n', encoding='utf-8')
    if args.md_out:
        args.md_out.write_text(markdown(flow), encoding='utf-8')
    return 0


if __name__ == '__main__':
    sys.exit(main())
