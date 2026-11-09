"""Fail-closed check that the F4b probe's expected scope list matches Persistent City's source.

Scans Core/, Compatibility/, Persistence/ and Runtime/ for Harmony patch calls, HarmonyMethod uses,
MonoBehaviour messages, native event subscriptions and transpiler helpers, and compares them with
ScopeInventory.cs in both directions. Component methods and their call sites (including F2e's
player-hold calls) must still exist where the list says. Any construct the scanner does not
recognise is a failure, never a silent pass. Exit 0 only when everything matches.
"""
import argparse
from collections import Counter
import hashlib
import json
from pathlib import Path
import re
import sys

MODULE = Path(__file__).resolve().parent
SOURCE_DIRS = ('Core', 'Compatibility', 'Persistence', 'Runtime')
INVENTORY = MODULE / 'ScopeInventory.cs'
PATCH_KINDS = ('prefix', 'postfix', 'transpiler', 'finalizer')  # Harmony.Patch positional order after the original
UNITY_BASES = ('MonoBehaviour', 'ScriptableObject', 'StateMachineBehaviour', 'UIBehaviour')
UNITY_MESSAGES = {
    'Awake', 'Start', 'Update', 'LateUpdate', 'FixedUpdate', 'OnGUI', 'OnEnable', 'OnDisable', 'OnDestroy', 'OnValidate', 'Reset',
    'OnTriggerEnter', 'OnTriggerExit', 'OnTriggerStay', 'OnTriggerEnter2D', 'OnTriggerExit2D', 'OnTriggerStay2D',
    'OnCollisionEnter', 'OnCollisionExit', 'OnCollisionStay', 'OnCollisionEnter2D', 'OnCollisionExit2D', 'OnCollisionStay2D',
    'OnControllerColliderHit', 'OnApplicationQuit', 'OnApplicationFocus', 'OnApplicationPause', 'OnRenderObject', 'OnPreRender',
    'OnPostRender', 'OnPreCull', 'OnWillRenderObject', 'OnRenderImage', 'OnBecameVisible', 'OnBecameInvisible', 'OnAnimatorMove',
    'OnAnimatorIK', 'OnAudioFilterRead', 'OnDrawGizmos', 'OnDrawGizmosSelected', 'OnMouseDown', 'OnMouseUp', 'OnMouseEnter',
    'OnMouseExit', 'OnMouseOver', 'OnMouseDrag', 'OnMouseUpAsButton', 'OnParticleCollision', 'OnParticleTrigger',
    'OnTransformChildrenChanged', 'OnTransformParentChanged', 'OnRectTransformDimensionsChange', 'OnJointBreak', 'OnCanvasGroupChanged',
}
EVENT_SOURCES = r'(?:GlobalEvents|SceneManager|Application|Camera|Canvas|RenderPipelineManager|Physics|Physics2D|Font)'
FORBIDDEN = [(r'\bPatchAll\s*\(', 'Harmony PatchAll'), (r'\bCreateClassProcessor\b', 'Harmony class processor'),
             (r'\bCreateProcessor\b', 'Harmony patch processor'), (r'\bPatchProcessor\b', 'Harmony patch processor'),
             (r'\bHarmonyPatch\b', 'Harmony attribute patch'), (r'\bilmanipulator\s*:', 'Harmony IL manipulator'),
             (r'\bReversePatch\b', 'Harmony reverse patch'), (r'\bCreateReversePatcher\b', 'Harmony reverse patch'),
             (r'\bRuntimeInitializeOnLoadMethod\b', 'Unity load-time entry point'), (r'\bStartCoroutine\s*\(', 'Unity coroutine'),
             (r'\bInvokeRepeating\s*\(', 'Unity repeating invoke'), (r'\bPlayerLoop\.SetPlayerLoop\b', 'player-loop entry point')]
NOT_DEFINITION_PREFIX = {'new', 'return', 'await', 'throw', 'in', 'is', 'as', 'else', 'case', 'nameof', 'typeof', 'when', 'yield', 'out', 'ref'}


def strip(text):
    """Blank comments and string/char literal contents, keeping length, newlines, quotes and interpolation holes."""
    out = list(text)
    n = len(text)

    def blank(a, b):
        for k in range(a, b):
            if out[k] != '\n':
                out[k] = ' '

    def code(i, stop_on_brace):
        depth = 0
        while i < n:
            c = text[i]
            if c == '/' and i + 1 < n and text[i + 1] == '/':
                j = text.find('\n', i)
                j = n if j < 0 else j
                blank(i, j)
                i = j
            elif c == '/' and i + 1 < n and text[i + 1] == '*':
                j = text.find('*/', i + 2)
                j = n if j < 0 else j + 2
                blank(i, j)
                i = j
            elif c == "'":
                j = i + 1
                while j < n and text[j] != "'":
                    j += 2 if text[j] == '\\' else 1
                blank(i + 1, j)
                i = j + 1
            elif c in '$@' and i + 1 < n and (text[i + 1] == '"' or (text[i + 1] in '$@' and i + 2 < n and text[i + 2] == '"')):
                prefix = text[i:i + 3] if text[i + 1] in '$@' else text[i:i + 2]
                i = string(i + len(prefix), '$' in prefix, '@' in prefix)
            elif c == '"':
                i = string(i + 1, False, False)
            elif stop_on_brace and c == '{':
                depth += 1
                i += 1
            elif stop_on_brace and c == '}':
                if depth == 0:
                    return i + 1
                depth -= 1
                i += 1
            else:
                i += 1
        return i

    def string(i, interpolated, verbatim):
        start = i
        while i < n:
            c = text[i]
            if verbatim and c == '"' and i + 1 < n and text[i + 1] == '"':
                i += 2
            elif not verbatim and c == '\\':
                i += 2
            elif c == '"':
                blank(start, i)
                return i + 1
            elif interpolated and c == '{' and i + 1 < n and text[i + 1] == '{':
                i += 2
            elif interpolated and c == '{':
                blank(start, i)
                i = code(i + 1, True)
                start = i
            else:
                i += 1
        blank(start, n)
        return n

    code(0, False)
    return ''.join(out)


def match_close(s, i, opener='(', closer=')'):
    """Index just after the bracket matching s[i] (an opener)."""
    depth = 0
    for k in range(i, len(s)):
        if s[k] == opener:
            depth += 1
        elif s[k] == closer:
            depth -= 1
            if depth == 0:
                return k + 1
    return -1


def split_args(s, generics=False):
    """Top-level comma split of an argument list body; generics=True also nests <...> (parameter lists)."""
    parts, depth, start, quoted = [], 0, 0, False
    openers, closers = ('([{<', ')]}>') if generics else ('([{', ')]}')
    for k, c in enumerate(s):
        if c == '"' and (k == 0 or s[k - 1] != '\\'):
            quoted = not quoted
        if quoted:
            continue
        if c in openers:
            depth += 1
        elif c in closers:
            depth -= 1
        if c == ',' and depth == 0:
            parts.append(s[start:k])
            start = k + 1
    parts.append(s[start:])
    return [p.strip() for p in parts if p.strip()]


def short(name):
    return re.split(r'[.+]', name)[-1]


class SourceFile:
    def __init__(self, root, path):
        self.rel = path.relative_to(root).as_posix()
        self.raw = path.read_text(encoding='utf-8-sig')
        self.code = strip(self.raw)
        self.classes = []  # (name, body_start, body_end, bases)
        for m in re.finditer(r'\b(class|struct|interface|record)\s+(\w+)', self.code):
            brace = self.code.find('{', m.end())
            semi = self.code.find(';', m.end())
            if brace < 0 or (0 <= semi < brace):
                continue
            end = match_close(self.code, brace, '{', '}')
            self.classes.append((m.group(2), brace, end, self.code[m.end():brace]))
        self.methods = []  # (name, name_pos, body_start, body_end, params)
        for m in re.finditer(r'(\w+)\s*\(', self.code):
            name = m.group(1)
            p = m.start() - 1
            while p >= 0 and self.code[p].isspace():
                p -= 1
            if p < 0 or self.code[p] == '.' or not (self.code[p].isalnum() or self.code[p] in '_>]?'):
                continue
            prev = re.search(r'(\w+)\W*$', self.code[max(0, p - 80):p + 1])
            if prev and prev.group(1) in NOT_DEFINITION_PREFIX:
                continue
            if name in NOT_DEFINITION_PREFIX or name in ('if', 'for', 'foreach', 'while', 'switch', 'catch', 'using', 'lock', 'fixed'):
                continue
            close = match_close(self.code, m.end() - 1)
            if close < 0:
                continue
            rest = self.code[close:]
            follow = re.match(r'\s*(?:where\b[^{;=]*)?(\{|=>)', rest)
            if not follow:
                continue
            body = close + follow.start(1)
            if follow.group(1) == '{':
                end = match_close(self.code, body, '{', '}')
            else:
                end = self.code.find(';', body)
                depth, k = 0, body
                while k < len(self.code):
                    if self.code[k] in '({[':
                        depth += 1
                    elif self.code[k] in ')}]':
                        depth -= 1
                    elif self.code[k] == ';' and depth <= 0:
                        end = k + 1
                        break
                    k += 1
            self.methods.append((name, m.start(1), body, end, self.code[m.end():close - 1]))

    def enclosing_class(self, pos):
        best = None
        for c in self.classes:
            if c[1] < pos < c[2] and (best is None or c[1] > best[1]):
                best = c
        return best

    def enclosing_method(self, pos):
        best = None
        for mth in self.methods:
            if mth[2] <= pos < mth[3] and (best is None or mth[2] > best[2]):
                best = mth
        return best


def load(root):
    files = []
    for folder in SOURCE_DIRS:
        for path in sorted((root / folder).glob('*.cs')):
            files.append(SourceFile(root, path))
    return files


def owner_of(f, problems):
    owners = set()
    for m in re.finditer(r'new\s+Harmony\s*\(', f.code):
        close = match_close(f.code, m.end() - 1)
        arg = f.raw[m.end():close - 1].strip()
        if arg.startswith('"'):
            owners.add(arg.strip('"'))
            continue
        const = re.search(r'const\s+string\s+' + re.escape(arg) + r'\s*=\s*"([^"]+)"', f.raw)
        if not const:
            problems.append(f'{f.rel}: Harmony owner {arg!r} is not a string constant in this file')
            continue
        owners.add(const.group(1))
    if len(owners) > 1:
        problems.append(f'{f.rel}: more than one Harmony owner ({", ".join(sorted(owners))}); the scanner cannot attribute patches')
    return next(iter(owners)) if len(owners) == 1 else None


def nameof(arg, raw_arg):
    m = re.fullmatch(r'nameof\s*\(\s*([\w.]+)\s*\)', arg)
    if m:
        return short(m.group(1))
    m = re.fullmatch(r'"(\w+)"', raw_arg.strip())
    return m.group(1) if m else None


METHOD_CALL = re.compile(r'(AccessTools\s*\.\s*(?:Method|DeclaredMethod)|\.\s*GetMethod)\s*\(')


def top_level_cut(expr, token):
    """expr up to the first depth-0 occurrence of token."""
    depth = 0
    for k, c in enumerate(expr):
        if c in '([{':
            depth += 1
        elif c in ')]}':
            depth -= 1
        elif depth == 0 and expr.startswith(token, k):
            return expr[:k]
    return expr


def statement_end(code, start):
    depth = 0
    for k in range(start, len(code)):
        c = code[k]
        if c in '([{':
            depth += 1
        elif c in ')]}':
            depth -= 1
        elif c == ';' and depth <= 0:
            return k
    return len(code)


def assignments(f, name, pos):
    """(position, raw right-hand side) pairs assigned to `name`; the last one before pos in its method when there is one."""
    found = []
    for m in re.finditer(r'(?<![\w.])' + re.escape(name) + r'\s*=(?![=>])', f.code):
        rhs = f.raw[m.end():statement_end(f.code, m.end())].strip()
        if not re.fullmatch(r'(?:\w+\s*=\s*)*(?:null!?|default)', rhs):  # resets, including chained ones (a = b = null)
            found.append((m.start(), rhs))
    host = f.enclosing_method(pos) if pos is not None else None
    if host:
        local = [a for a in found if host[2] <= a[0] < pos]
        if local:
            return [local[-1]]
    return found


def type_of_variable(f, name, pos, depth):
    if not re.fullmatch(r'\w+', name) or depth > 4:
        return None
    types = set()
    for _, rhs in assignments(f, name, pos):
        t = re.search(r'GetType\s*\(\s*"([\w.]+)"', rhs) or re.search(r'typeof\s*\(\s*([\w.]+)\s*\)', rhs)
        types.add(short(t.group(1)) if t else None)
    return types.pop() if len(types) == 1 and None not in types else None


def target_of(f, expr, pos, depth=0):
    """Native 'Type.Method' named by a method-reference expression, following local and field assignments; None if unknown."""
    expr = top_level_cut(expr.strip(), '??').strip().rstrip('!').strip()
    if depth > 4 or not expr:
        return None
    if re.fullmatch(r'\w+', expr):
        targets = {target_of(f, text, at, depth + 1) for at, text in assignments(f, expr, pos)}
        return targets.pop() if len(targets) == 1 and None not in targets else None
    m = METHOD_CALL.search(expr)
    if not m:
        return None
    close = match_close(expr, m.end() - 1)
    args = split_args(expr[m.end():close - 1])
    if not args:
        return None
    if 'GetMethod' in m.group(1):
        t = re.search(r'typeof\s*\(\s*([\w.]+)\s*\)\s*$', expr[:m.start()])
        typ, name_arg = (short(t.group(1)) if t else None), args[0]
    else:
        t = re.fullmatch(r'typeof\s*\(\s*([\w.]+)\s*\)', args[0])
        typ = short(t.group(1)) if t else type_of_variable(f, args[0], pos, depth + 1)
        name_arg = args[1] if len(args) > 1 else ''
    name = nameof(name_arg, name_arg)
    return f'{typ}.{name}' if typ and name else None


def substitute(text, names, values):
    """Replace a helper's parameter names in its template expression with the call's argument texts."""
    for name, value in zip(names, values):
        text = re.sub(r'(?<![\w."])' + re.escape(name) + r'(?![\w"])', lambda _, v=value: v, text)
    return text


def contract_table(files):
    """FoodContractHookTable rows: id -> 'Type.Method', the targets of the dynamic HookN postfixes."""
    table = {}
    for f in files:
        for m in re.finditer(r'new\s+FoodContractHook\s*\(\s*(\d+)\s*,\s*"\w+"\s*,\s*"(\w+)"\s*,\s*"(\w+)"', f.raw):
            table[int(m.group(1))] = m.group(2) + '.' + m.group(3)
    return table


# Method references inside an inventoried transpiler. AccessTools.Method/DeclaredMethod and typeof(T).GetMethod are parsed;
# the other forms (delegates, CodeInstruction.Call, function pointers, other AccessTools method lookups) fail closed.
TRANSPILER_REFERENCES = re.compile(r'AccessTools\s*\.\s*(\w+)\s*\(|\.\s*GetMethod\s*\(|\bSymbolExtensions\b|\bCodeInstruction\s*\.\s*Call\w*'
                                   r'|\bEmitDelegate\b|\bTranspilers\s*\.\s*\w+|\bCreateDelegate\b|\bOpCodes\s*\.\s*Ldv?i?r?t?ftn\b|\bnew\s+CodeInstruction\s*\(')
NON_METHOD_ACCESS = {'Field', 'DeclaredField', 'FieldRefAccess', 'StaticFieldRefAccess', 'TypeByName', 'Inner'}


def scan(root):
    """Returns (found dict of sets, problems list)."""
    files = load(root)
    problems = []
    found = dict(patches=[], messages=[], events=[], helpers=[])  # multisets: a repeated registration counts twice
    table = contract_table(files)
    behaviours = set()
    for f in files:
        for name, _, _, bases in f.classes:
            if re.search(r'\b(?:' + '|'.join(UNITY_BASES) + r')\b', bases):
                behaviours.add(name)
    changed = True
    while changed:  # Classes deriving from a PC behaviour are behaviours too.
        changed = False
        for f in files:
            for name, _, _, bases in f.classes:
                if name not in behaviours and any(re.search(r'\b' + b + r'\b', bases) for b in behaviours):
                    behaviours.add(name)
                    changed = True
    pc_classes = {c[0] for f in files for c in f.classes}
    transpilers = []
    for f in files:
        for pattern, what in FORBIDDEN:
            for m in re.finditer(pattern, f.code):
                problems.append(f'{f.rel}:{f.code.count(chr(10), 0, m.start()) + 1}: unrecognised entry point ({what})')
        harmony_file = 'HarmonyLib' in f.code
        if not harmony_file:
            if re.search(r'\bHarmonyMethod\b|\bnew\s+Harmony\s*\(', f.code):
                problems.append(f'{f.rel}: Harmony use without HarmonyLib in the file')
        else:
            owner = owner_of(f, problems)
            covered = []  # spans of recognised Patch calls (HarmonyMethod uses must fall inside one)
            helpers = {}  # helper Patch definitions in this file: name_pos -> (params, kind, type, param_index)
            for name, pos, body, end, params in f.methods:
                if name != 'Patch':
                    continue
                names = [p.split()[-1] for p in split_args(params, generics=True)]
                for m in re.finditer(r'new\s+HarmonyMethod\s*\(', f.code[body:end]):
                    start = body + m.end() - 1
                    inner = split_args(f.code[start + 1:match_close(f.code, start) - 1])
                    kind = re.search(r'(\w+)\s*:\s*$', f.code[body:body + m.start()])
                    t = re.fullmatch(r'typeof\s*\(\s*([\w.]+)\s*\)', inner[0]) if inner else None
                    if len(inner) == 2 and t and inner[1] in names and kind and kind.group(1) in PATCH_KINDS:
                        # The template's target: the Patch call's first argument, or the local it was assigned from.
                        calls = list(re.finditer(r'\bPatch\s*\(', f.code[body:body + m.start()]))
                        template = ''
                        if calls:
                            open_at = body + calls[-1].end() - 1
                            template = split_args(f.raw[open_at + 1:match_close(f.code, open_at) - 1])[0]
                            if re.fullmatch(r'\w+', template) and template not in names:
                                local = assignments(f, template, body + calls[-1].start())
                                template = local[-1][1] if len(local) == 1 else ''
                        helpers[pos] = (names, kind.group(1), short(t.group(1)), names.index(inner[1]), template)
            for m in re.finditer(r'\bPatch\s*\(', f.code):
                if any(m.start() == pos for name, pos, *_ in f.methods if name == 'Patch'):
                    continue  # a definition
                if re.search(r'\bHarmony\.\s*$', f.code[:m.start()]):
                    continue
                line = f.code.count('\n', 0, m.start()) + 1
                close = match_close(f.code, m.end() - 1)
                args_text, raw_args = f.code[m.end():close - 1], f.raw[m.end():close - 1]
                qualified = f.code[:m.start()].rstrip().endswith('.')
                covered.append((m.start(), close))
                host = f.enclosing_method(m.start())
                if 'HarmonyMethod' in args_text:
                    if host and host[0] == 'Patch' and host[1] in helpers:
                        continue  # the helper's own template call
                    parts = split_args(args_text)
                    raw_first = split_args(raw_args)[0] if parts else ''
                    target = target_of(f, raw_first, m.start())
                    offsets, k = [], 0
                    for p in parts:  # raw counterparts by position
                        k = args_text.find(p, k)
                        offsets.append((k, k + len(p)))
                        k += len(p)
                    for index, part in enumerate(parts[1:], start=1):
                        named = re.match(r'(\w+)\s*:\s*', part)
                        kind = named.group(1) if named else (PATCH_KINDS[index - 1] if index - 1 < len(PATCH_KINDS) else None)
                        body = part[named.end():] if named else part
                        hm = re.match(r'new\s+HarmonyMethod\s*\(', body)
                        if kind not in PATCH_KINDS or not hm:
                            problems.append(f'{f.rel}:{line}: unrecognised Patch argument {part[:60]!r}')
                            continue
                        inner_start = hm.end() - 1
                        inner = split_args(body[inner_start + 1:match_close(body, inner_start) - 1])
                        t = re.fullmatch(r'typeof\s*\(\s*([\w.]+)\s*\)', inner[0]) if inner else None
                        a0, a1 = offsets[index]
                        raw_part = raw_args[a0:a1]
                        raw_inner = re.search(r'HarmonyMethod\s*\((.*)\)', raw_part, re.S)
                        raw_second = split_args(raw_inner.group(1))[1] if raw_inner and len(inner) == 2 else ''
                        method = nameof(inner[1], raw_second) if len(inner) == 2 else None
                        if t and method:
                            if target is None:
                                problems.append(f'{f.rel}:{line}: cannot resolve the native target of this Patch call ({raw_first.strip()[:60]!r})')
                            found['patches'].append((owner, kind.capitalize(), short(t.group(1)), method, target, f.rel))
                            if kind == 'transpiler':
                                transpilers.append((f, short(t.group(1)), method))
                        elif len(inner) == 1 and re.fullmatch(r'\w+', inner[0]) and host:
                            # Dynamic patch method: typeof(T).GetMethod("Prefix" + id) inside the host method.
                            dyn = re.search(r'typeof\s*\(\s*(\w+)\s*\)\s*\.\s*GetMethod\s*\(\s*"(\w+)"\s*\+', f.raw[host[2]:host[3]])
                            if not dyn:
                                problems.append(f'{f.rel}:{line}: HarmonyMethod({inner[0]}) without a recognised typeof(T).GetMethod("Name" + id) source')
                                continue
                            cls = next((c for c in f.classes if c[0] == dyn.group(1)), None)
                            if cls is None:
                                problems.append(f'{f.rel}:{line}: dynamic patch class {dyn.group(1)} not declared in this file')
                                continue
                            ids = [mm.group(1) for mm in re.finditer(r'\bstatic\s+void\s+(' + dyn.group(2) + r'\d+)\s*\(', f.code[cls[1]:cls[2]])]
                            if not ids:
                                problems.append(f'{f.rel}:{line}: no {dyn.group(2)}N methods in {dyn.group(1)}')
                            for method_name in ids:
                                hook_target = table.get(int(method_name[len(dyn.group(2)):]))
                                if hook_target is None:
                                    problems.append(f'{f.rel}:{line}: {method_name} has no FoodContractHook table row')
                                found['patches'].append((owner, kind.capitalize(), dyn.group(1), method_name, hook_target, f.rel))
                        else:
                            problems.append(f'{f.rel}:{line}: unrecognised HarmonyMethod arguments {", ".join(inner)[:80]!r}')
                    if owner is None:
                        problems.append(f'{f.rel}:{line}: Patch call with no Harmony owner in the file')
                elif not qualified:
                    helper = next((helpers[pos] for name, pos, *_ in f.methods if name == 'Patch' and pos in helpers), None)
                    parts = split_args(args_text)
                    raw_parts = split_args(raw_args)
                    if helper is None or len(parts) != len(helper[0]):
                        problems.append(f'{f.rel}:{line}: unrecognised Patch call {args_text.strip()[:80]!r}')
                        continue
                    method = nameof(parts[helper[3]], raw_parts[helper[3]])
                    if not method:
                        problems.append(f'{f.rel}:{line}: helper Patch call without a nameof/literal patch method')
                        continue
                    hook_target = target_of(f, substitute(helper[4], helper[0], raw_parts), None) if helper[4] else None
                    if hook_target is None:
                        problems.append(f'{f.rel}:{line}: cannot resolve the native target of this helper Patch call')
                    found['patches'].append((owner, helper[1].capitalize(), helper[2], method, hook_target, f.rel))
                else:
                    problems.append(f'{f.rel}:{line}: unrecognised Patch call without HarmonyMethod {args_text.strip()[:80]!r}')
            for m in re.finditer(r'\bHarmonyMethod\b', f.code):
                if not any(a <= m.start() < b for a, b in covered):
                    if not any(name == 'Patch' and body <= m.start() < end for name, _, body, end, _ in f.methods):
                        problems.append(f'{f.rel}:{f.code.count(chr(10), 0, m.start()) + 1}: HarmonyMethod outside a recognised Patch call')
        # MonoBehaviour messages, at class level only.
        for name, start, end, _ in f.classes:
            if name not in behaviours:
                continue
            nested = [c for c in f.classes if start < c[1] and c[2] <= end]
            for mname, pos, *_ in f.methods:
                if start < pos < end and mname in UNITY_MESSAGES and not any(c[1] < pos < c[2] for c in nested):
                    found['messages'].append((name, mname, f.rel))
        # Native event subscriptions.
        for m in re.finditer(r'\b' + EVENT_SOURCES + r'\s*\.\s*(\w+)\s*\+=\s*([^;]+);', f.code):
            source = re.match(r'\w+', m.group(0)).group(0)
            handler = m.group(2).strip()
            line = f.code.count('\n', 0, m.start()) + 1
            hm = re.fullmatch(r'(?:this\s*\.\s*)?(\w+)', handler)
            cls = f.enclosing_class(m.start())
            if not hm or cls is None:
                problems.append(f'{f.rel}:{line}: unrecognised native event subscription {m.group(0)[:80]!r} (lambda or expression handler)')
                continue
            found['events'].append((source + '.' + m.group(1), cls[0], hm.group(1), f.rel))
        for m in re.finditer(r'\b' + EVENT_SOURCES + r'\s*\.\s*\w+\s*(?:\+=|=)\s*(?:\(|delegate\b|new\b)', f.code):
            problems.append(f'{f.rel}:{f.code.count(chr(10), 0, m.start()) + 1}: unrecognised native event subscription (lambda or delegate)')
    for f, cls, name in transpilers:
        defs = [mth for mth in f.methods if mth[0] == name and (c := f.enclosing_class(mth[1])) and c[0] == cls]
        if len(defs) != 1:
            problems.append(f'{f.rel}: transpiler {cls}.{name} definition not found exactly once')
            continue
        start, end = defs[0][2], defs[0][3]
        for m in TRANSPILER_REFERENCES.finditer(f.code, start, end):
            line = f.code.count('\n', 0, m.start()) + 1
            access = m.group(1)
            if access in NON_METHOD_ACCESS:
                continue
            if not (access in ('Method', 'DeclaredMethod') or (access is None and 'GetMethod' in m.group(0))):
                problems.append(f'{f.rel}:{line}: unrecognised method reference in transpiler {cls}.{name} ({m.group(0).strip()})')
                continue
            paren = m.end() - 1
            begin = max(f.code.rfind(c, start, m.start()) for c in ';{=,') + 1
            resolved = target_of(f, f.raw[begin:match_close(f.code, paren)], None)
            if resolved is None:
                problems.append(f'{f.rel}:{line}: unresolved method reference in transpiler {cls}.{name}')
                continue
            typ, method_name = resolved.split('.')
            if typ in pc_classes:
                found['helpers'].append((name, typ, method_name, f.rel))
    return files, found, problems


def literal_value(expr, constants):
    value = ''
    for term in expr.split('+'):
        term = term.strip()
        if term.startswith('"') and term.endswith('"'):
            value += term[1:-1]
        elif term in constants:
            value += constants[term]
        else:
            raise ValueError('unrecognised inventory term ' + term)
    return value


def parse_inventory(path=INVENTORY):
    text = path.read_text(encoding='utf-8-sig')
    constants = {m.group(1): m.group(2) for m in re.finditer(r'\b(\w+)\s*=\s*"([^"]*)"', text)}
    for m in re.finditer(r'\b(\w+)\s*=\s*(\w+)\s*\+\s*"([^"]*)"', text):
        if m.group(2) in constants:
            constants[m.group(1)] = constants[m.group(2)] + m.group(3)
    version = re.search(r'Version\s*=\s*"([^"]+)"', text).group(1)
    rows = dict(P=[], M=[], E=[], H=[], C=[], S=[])
    for line in text.splitlines():
        m = re.match(r'\s*([PMEHCS])\((.*)\),?\s*$', line)
        if not m:
            continue
        args = []
        for a in split_args(m.group(2)):
            k = re.fullmatch(r'ScopeKind\.(\w+)', a)
            args.append(k.group(1) if k else literal_value(a, constants))
        rows[m.group(1)].append(args)
    return version, rows


def canonical(path=INVENTORY):
    """Same text as ScopeInventory.CanonicalText()."""
    version, rows = parse_inventory(path)
    lines = [version]
    lines += ['|'.join([kind, owner, typ, method, target, file]) for owner, kind, typ, method, target, file in rows['P']]
    lines += ['|'.join(['Message', '', typ, method, '', file]) for typ, method, file in rows['M']]
    lines += ['|'.join(['Event', evt, typ, method, '', file]) for evt, typ, method, file in rows['E']]
    lines += ['|'.join(['TranspilerHelper', '', typ, method, transpiler, file]) for transpiler, typ, method, file in rows['H']]
    lines += ['|'.join(['Component', '', typ, method, params, file]) for typ, method, params, file in rows['C']]
    lines += ['|'.join(['CallSite', file, caller, call]) for file, caller, call in rows['S']]
    return '\n'.join(lines) + '\n'


def inventory_hash(root=None, path=INVENTORY):
    return hashlib.sha256(canonical(path).encode('utf-8')).hexdigest()


def expected_sets(path=INVENTORY):
    _, rows = parse_inventory(path)
    return dict(
        patches=[(owner, kind, short(typ), method, target, file) for owner, kind, typ, method, target, file in rows['P']],
        messages=[(short(typ), method, file) for typ, method, file in rows['M']],
        events=[(evt, short(typ), method, file) for evt, typ, method, file in rows['E']],
        helpers=[(transpiler, short(typ), method, file) for transpiler, typ, method, file in rows['H']],
    ), rows


def check(root, inventory=INVENTORY):
    root = Path(root)
    files, found, problems = scan(root)
    expected, rows = expected_sets(inventory)
    for key in ('patches', 'messages', 'events', 'helpers'):
        # Multisets: the same callback registered again, or on another native target, is a difference.
        source, listed = Counter(found[key]), Counter(expected[key])
        for row in sorted((source - listed).elements(), key=str):
            problems.append(f'{key}: in source but not in ScopeInventory.cs: {" ".join(str(x) for x in row)}')
        for row in sorted((listed - source).elements(), key=str):
            problems.append(f'{key}: in ScopeInventory.cs but not in source: {" ".join(str(x) for x in row)}')
    by_rel = {f.rel: f for f in files}
    for typ, method, params, file in rows['C']:
        f = by_rel.get(file)
        defs = [] if f is None else [m for m in f.methods if m[0] == method and (c := f.enclosing_class(m[1])) and c[0] == short(typ)]
        if len(defs) != 1:
            problems.append(f'components: {short(typ)}.{method} not defined exactly once in {file}')
            continue
        count = len(split_args(defs[0][4], generics=True))
        wanted = 0 if params == '' else len(params.split(','))
        if count != wanted:
            problems.append(f'components: {short(typ)}.{method} in {file} takes {count} parameters, expected {wanted} ({params})')
    for file, caller, call in rows['S']:
        f = by_rel.get(file)
        bodies = [] if f is None else [re.sub(r'\s+', '', f.code[m[2]:m[3]]) for m in f.methods if m[0] == caller]
        if not any(re.sub(r'\s+', '', call) in body for body in bodies):
            problems.append(f'call sites: {call} no longer called from {caller} in {file}')
    return problems


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root', type=Path, default=MODULE.parents[1])
    parser.add_argument('--json', action='store_true', help='print the result as JSON')
    args = parser.parse_args()
    problems = check(args.root)
    expected, _ = expected_sets()
    result = dict(ok=not problems, expected_scope_hash=inventory_hash(), problems=problems,
                  counts={k: len(v) for k, v in expected.items()})
    if args.json:
        print(json.dumps(result, indent=2))
    else:
        print('check_scopes: ' + ('PASS' if not problems else 'FAIL'))
        print('expected_scope_hash ' + result['expected_scope_hash'])
        print('counts ' + ', '.join(f'{k}={v}' for k, v in result['counts'].items()))
        for p in problems:
            print('  ' + p)
    sys.exit(0 if not problems else 1)


if __name__ == '__main__':
    main()
