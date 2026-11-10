"""Synthetic perf-gate-export/v1 builders for tests and the analyzer timing run. No game data.

python fixtures/synthetic.py --windows 50 --out <path>  writes a 50-window export (18,000 frames each).
"""
import argparse
import json
import random
from pathlib import Path

RECEIPTS = ['autosave', 'no_dotnet_activity', 'priority_high', 'focused', 'settings_read_back', 'probe_coverage',
            'probe_flags', 'presentation_trace', 'pc_readiness', 'accounting_diagnostics']
REFERENCE_NA = ['pc_readiness', 'accounting_diagnostics', 'probe_coverage']


def header(**overrides):
    h = dict(source_commit='b8feb06c', pc_dll_sha256='a' * 64, probe_dll_sha256='b' * 64, expected_scope_list_hash='c' * 64,
             game_build='synthetic-build', mod_set=[dict(name='PersistentCity', sha256='a' * 64), dict(name='RealLifeOverhaul', sha256='d' * 64)],
             rig_commit='synthetic-rig', rig_overlay='none', machine=dict(cpu='cpu', gpu='gpu', ram='32 GB', os_build='26200', gpu_driver='1.0',
             refresh_hz=144, power_plan='High performance'), settings_read_back=dict(resolution='1920x1080', preset='Low', vsync=False, frame_cap=90),
             pc_config=dict(GameClockFactor=0.3333), fixtures=[dict(name='FT-F4-Declared', provenance_hash='e' * 64)],
             contract_version='perf-gate/v1', movement_state='pre-F2d', rig_capabilities=[dict(name='RIG-PERF-UNION', version='synthetic')])
    h.update(overrides)
    return h


def frames(mod_values, whole=16.0):
    return [dict(frame=i, mod_work_ms=v, whole_frame_ms=whole, gc=0, components={'AdvancePopulation': v}) for i, v in enumerate(mod_values)]


def window(id, build='candidate', mod=None, presented=None, **overrides):
    reference = build == 'reference'
    w = dict(id=id, condition='W-evening', script='S-path', build=build, arm='primary', repetition=1, native_speed=1,
             checkpoint_minute=1065, game_span_minutes=90.0, clean_real_seconds=300.0,
             receipts={r: ('n/a' if reference and r in REFERENCE_NA else 'pass') for r in RECEIPTS},
             frames=frames(mod if mod is not None else [0.5] * 100, 16.0),
             presented_ms=presented if presented is not None else [10.0] * 100)
    if reference:
        w['reference_pace_ratio'] = 1.0
        for f in w['frames']:
            f['mod_work_ms'] = 0.0
            f['components'] = {}
    else:
        w['accounting'] = dict(clock_misses=0, lag_samples=[[0.0, 0.0], [150.0, 0.2], [300.0, 0.0]], holds=[])
    w.update(overrides)
    return w


def export(windows, source='native', **header_overrides):
    return {'format': 'perf-gate-export/v1', 'source': source, 'header': header(**header_overrides), 'windows': windows}


def large_export(count=50, frames_per_window=18000, seed=7):
    rng = random.Random(seed)
    windows = []
    for i in range(count):
        build = 'candidate' if i % 2 == 0 else 'reference'
        mod = [rng.lognormvariate(-1.0, 0.6) for _ in range(frames_per_window)]
        if build == 'candidate':
            mod[rng.randrange(frames_per_window)] = 31.0 + rng.random()
        presented = [rng.gauss(11.1, 1.2) for _ in range(frames_per_window)]
        w = window(f'w{i:02d}', build=build, mod=mod, presented=[max(0.0, p) for p in presented], repetition=i // 2)
        windows.append(w)
    return export(windows)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument('--windows', type=int, default=50)
    parser.add_argument('--frames', type=int, default=18000)
    parser.add_argument('--out', type=Path, required=True)
    args = parser.parse_args()
    args.out.write_text(json.dumps(large_export(args.windows, args.frames)), encoding='utf-8')
