"""Write a perf-gate-report/v1 as JSON plus a generated Markdown summary (Decision 8)."""
import json
from pathlib import Path


def _ms(value):
    return '-' if value is None else f'{value:.3f}'


def _verdict_cell(rows, rid):
    row = rows.get(rid)
    return '-' if not row else row['verdict']


def render_markdown(report):
    h = report['header']
    lines = [f"# Perf gate report ({report['contract']}, source={report['source']})", '',
             f"**Overall: {report['overall']}**" + (f" — {report['sixty_fps']}" if report['sixty_fps'] else ''), '',
             f"Contract SHA-256 `{report['contract_sha256']}`. Movement state `{h['movement_state']}`.", '',
             '## Header', '', '| Field | Value |', '| --- | --- |']
    for key, value in h.items():
        lines.append(f'| {key} | `{json.dumps(value, sort_keys=True)}` |')
    lines += ['', '## Row verdicts (primary arm)', '', '| Row | Verdict | Reason |', '| --- | --- | --- |']
    for rid, row in report['rows'].items():
        lines.append(f"| {rid} | {row['verdict']} | {row.get('reason', '')} |")
    if report['secondary_arm_rows']:
        lines += ['', '## Stress and report-only arms (M1, M2, S1, S2 judged; recorded, not in the overall verdict)', '',
                  '| Row | Verdict | Reason |', '| --- | --- | --- |']
        for rid, row in report['secondary_arm_rows'].items():
            lines.append(f"| {rid} | {row['verdict']} | {row.get('reason', '')} |")
    lines += ['', '## Windows', '',
              '| Window | Condition | Script | Build | Arm | Rep | Valid | Clean frames | Mod p95 / p99 / mean ms | Whole-frame p95 / max ms | Presented p95 / p99 ms | M1 | M3 | M4 | P1 | P2 | P3 | P4 | S1 | S2 | S3 | S4 | Dense | GC |',
              '| --- | --- | --- | --- | --- | --- | --- | ---: | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | ---: |']
    for w in report['windows']:
        p = w['presented'] or {}
        rows = w['rows']
        lines.append('| ' + ' | '.join(str(x) for x in [
            w['id'], w['condition'], w['script'], w['build'], w['arm'], w['repetition'], 'yes' if w['valid'] else 'no', w['clean_frames'],
            f"{_ms(w['mod_work']['p95_ms'])} / {_ms(w['mod_work']['p99_ms'])} / {_ms(w['mod_work']['mean_ms'])}",
            f"{_ms(w['whole_frame']['p95_ms'])} / {_ms(w['whole_frame']['max_ms'])}",
            f"{_ms(p.get('p95_ms'))} / {_ms(p.get('p99_ms'))}",
            *[_verdict_cell(rows, rid) for rid in ('M1', 'M3', 'M4', 'P1', 'P2', 'P3', 'P4', 'S1', 'S2', 'S3', 'S4')],
            w['density']['dense'], w['gc_collections']]) + ' |')
    invalid = [w for w in report['windows'] if not w['valid']]
    lines += ['', '## Invalid windows (kept, excluded from the verdict)', '']
    lines += [f"- `{w['id']}`: " + '; '.join(w['invalid_reasons']) for w in invalid] or ['None.']
    lines += ['', '## P4 pairs', '', '| Candidate | Reference | Verdict | Delta p95 / p99 ms | Reason |', '| --- | --- | --- | --- | --- |']
    for pair in report['pairs']:
        delta = pair['value'] or {}
        lines.append(f"| {pair['candidate']} | {pair['reference'] or '-'} | {pair['verdict']} | "
                     f"{_ms(delta.get('p95_ms'))} / {_ms(delta.get('p99_ms'))} | {pair['reason']} |")
    lines += ['', '## Pooled raw frames (never a percentile of percentiles)', '',
              '| Condition | Script | Build | Arm | Windows | Mod p95 / p99 ms | Presented p95 / p99 ms |', '| --- | --- | --- | --- | ---: | --- | --- |']
    for g in report['pooled']:
        p = g['presented'] or {}
        lines.append(f"| {g['condition']} | {g['script']} | {g['build']} | {g['arm']} | {len(g['windows'])} | "
                     f"{_ms(g['mod_work']['p95_ms'])} / {_ms(g['mod_work']['p99_ms'])} | {_ms(p.get('p95_ms'))} / {_ms(p.get('p99_ms'))} |")
    lines += ['', '## M2 spikes (mod-work >= 30 ms)', '',
              '| Window | Valid | Frame | Mod-work ms | Whole-frame ms | Component | GC | Advance ms | Probe call GC / optional-stop ms | Slowest unit (source) |',
              '| --- | --- | ---: | ---: | ---: | --- | ---: | --- | --- | --- |']
    for s in report['spikes']:
        call = s['advance_call'] or {}
        unit = s['advancement_unit']
        unit_text = 'n/a (outside advancement)' if unit is None else (
            f"{unit['unit_kind']} {unit['unit_ms']} ms, optional-stop {unit['optional_stop_ms']} ms, GC {unit['gc_count']} ({unit['source']})")
        lines.append(f"| {s['window']} | {'yes' if s['window_valid'] else 'no'} | {s['frame']} | {_ms(s['mod_work_ms'])} | {_ms(s['whole_frame_ms'])} | "
                     f"{s['component']} | {s['gc']} | {_ms(s['advance_ms'])} | {call.get('gc_delta', '-')} / {call.get('optional_stop_ms', '-')} | {unit_text} |")
    if not report['spikes']:
        lines.append('| none | | | | | | | | | |')
    lines += ['', '## Stalls (>= 50 ms)', '', '| Window | Kind | Frame or index | Interval ms | Classes |', '| --- | --- | --- | ---: | --- |']
    for s in report['stalls']:
        lines.append(f"| {s['window']} | {s['kind']} | {s.get('frame', s.get('index'))} | {_ms(s['interval_ms'])} | {', '.join(s['classes'])} |")
    if not report['stalls']:
        lines.append('| none | | | | |')
    lines += ['', '## Limits', ''] + [f'- {limit}' for limit in report['limits']] + ['']
    return '\n'.join(lines)


def write(report, json_path, md_path):
    json_path, md_path = Path(json_path), Path(md_path)
    json_path.parent.mkdir(parents=True, exist_ok=True)
    md_path.parent.mkdir(parents=True, exist_ok=True)
    json_path.write_text(json.dumps(report, indent=1) + '\n', encoding='utf-8')
    md_path.write_text(render_markdown(report), encoding='utf-8')
