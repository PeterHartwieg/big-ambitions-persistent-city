"""Synthetic probe frames and a matching PresentMon-class trace for F4c tests and the join timing run. No game data.

python fixtures/present/synthetic_trace.py --hours 7 --out-dir <dir>  writes frames.json (probe rows) and trace.csv.
"""
import argparse
import json
import random
from pathlib import Path

QPC_FREQUENCY = 10_000_000
# PresentMon 2.x default columns (README-ConsoleApplication.md, v2.3.0) with --qpc_time: rows start at CPUStartQPC.
V2_HEADER = ['Application', 'ProcessID', 'SwapChainAddress', 'PresentRuntime', 'SyncInterval', 'PresentFlags', 'AllowsTearing',
             'PresentMode', 'CPUStartQPC', 'FrameTime', 'CPUBusy', 'CPUWait', 'GPULatency', 'GPUTime', 'GPUBusy', 'GPUWait',
             'DisplayLatency', 'DisplayedTime', 'AnimationError', 'ClickToPhotonLatency', 'AllInputToPhotonLatency']
V2_GPU_COLUMNS = ('GPULatency', 'GPUBusy', 'GPUWait')  # --no_track_gpu
V1_HEADER = ['Application', 'ProcessID', 'SwapChainAddress', 'Runtime', 'SyncInterval', 'PresentFlags', 'AllowsTearing', 'PresentMode',
             'Dropped', 'TimeInSeconds', 'msInPresentAPI', 'msBetweenPresents', 'msBetweenDisplayChange', 'msUntilRenderComplete', 'msUntilDisplayed']


def session(frames=600, seed=3, stalls=None, start_ms=5_000.0, mod=None, gpu_busy=None, cpu_wait=None, cpu_busy=None):
    """Probe frame rows and the true present time of each frame (ms on the probe's QPC clock).

    stalls: {frame index: whole-frame ms}; mod: {frame index: union ms}; gpu_busy/cpu_wait/cpu_busy: {frame index: ms}."""
    rng = random.Random(seed)
    stalls, mod = stalls or {}, mod or {}
    rows, presents, t = [], [], start_ms
    for i in range(frames):
        whole = stalls.get(i, 16.667 + rng.uniform(-0.6, 0.6))
        rows.append(dict(frame=1000 + i, qpc=round(t * QPC_FREQUENCY / 1000.0), whole_frame_ms=whole, mod_work_ms=mod.get(i, 0.3),
                         observer_ms=0.01, gc=0, components={'AdvancePopulation': mod.get(i, 0.3)} if i not in mod else {},
                         saves=0, scene=False, loading=False, diagnostic=False, warmup=False, logical_minute=1065 + i / 1000.0))
        presents.append(dict(frame=i, start_ms=t + 0.1 + rng.uniform(-0.05, 0.05), t_ms=t + whole - 0.25 + rng.uniform(-0.1, 0.1),
                             cpu_busy=(cpu_busy or {}).get(i, 10.0),
                             cpu_wait=(cpu_wait or {}).get(i, 0.4), gpu_busy=(gpu_busy or {}).get(i, 6.5)))
        t += whole
    return rows, presents


def trace_rows(presents, version=2, offset_ms=0.0, seconds_origin_ms=None, missing=(), dropped=(), gpu=True, process_id=4242, extra_process=False):
    """CSV rows (header first). offset_ms shifts the trace clock; seconds_origin_ms writes seconds from that origin (v1 TimeInSeconds).

    Version 2 rows start at the frame's CPU start; FrameTime runs to the next frame's start; each shown frame is
    displayed 5 ms after its present and DisplayedTime runs to the next shown frame's display."""
    missing, dropped = set(missing), set(dropped)
    header = V2_HEADER if version == 2 else V1_HEADER
    if not gpu and version == 2:
        header = [h for h in header if h not in V2_GPU_COLUMNS]
    out = [header]
    shown_after = {}  # frame -> display time of the next shown frame
    next_display = None
    for p in reversed(presents):
        shown_after[p['frame']] = next_display
        if p['frame'] not in dropped:
            next_display = p['t_ms'] + 5.0
    last_present, last_display = None, None
    for k, p in enumerate(presents):
        if p['frame'] in missing:
            continue
        t = p['t_ms'] + offset_ms
        between = 'NA' if last_present is None else f'{t - last_present:.4f}'
        last_present = t
        shown = p['frame'] not in dropped
        display_at = t + 5.0
        change = 'NA' if not shown or last_display is None else f'{display_at - last_display:.4f}'
        if shown:
            last_display = display_at
        if version == 2:
            start = p['start_ms'] + offset_ms
            frame_time = (presents[k + 1]['start_ms'] + offset_ms if k + 1 < len(presents) else t + 0.35) - start
            after = shown_after[p['frame']]
            displayed_for = 'NA' if not shown else f"{(after - p['t_ms']) - 5.0 if after is not None else frame_time:.4f}"
            row = dict(Application='Big Ambitions.exe', ProcessID=process_id, SwapChainAddress='0x1', PresentRuntime='DXGI', SyncInterval=0,
                       PresentFlags=512, AllowsTearing=1, PresentMode='Hardware: Independent Flip', CPUStartQPC=round(start * QPC_FREQUENCY / 1000.0),
                       FrameTime=f'{frame_time:.4f}', CPUBusy=f"{p['cpu_busy']:.4f}", CPUWait=f"{p['cpu_wait']:.4f}", GPULatency='1.0',
                       GPUTime=f"{p['gpu_busy']:.4f}", GPUBusy=f"{p['gpu_busy']:.4f}", GPUWait='1.0',
                       DisplayLatency=f'{display_at - start:.4f}' if shown else 'NA', DisplayedTime=displayed_for, AnimationError='0.0',
                       ClickToPhotonLatency='NA', AllInputToPhotonLatency='NA')
        else:
            origin = seconds_origin_ms if seconds_origin_ms is not None else 0.0
            row = dict(Application='Big Ambitions.exe', ProcessID=process_id, SwapChainAddress='0x1', Runtime='DXGI', SyncInterval=0,
                       PresentFlags=512, AllowsTearing=1, PresentMode='Hardware: Independent Flip', Dropped=0 if shown else 1,
                       TimeInSeconds=f'{(t - origin) / 1000.0:.7f}', msInPresentAPI='0.2', msBetweenPresents=between,
                       msBetweenDisplayChange=change if change != 'NA' else '0.0', msUntilRenderComplete='3.0', msUntilDisplayed='5.0')
        out.append([str(row[h]) for h in header])
        if extra_process:
            other = list(out[-1])
            other[1] = str(process_id + 1)
            out.append(other)
    return out


def write_csv(rows, path):
    Path(path).write_text('\n'.join(','.join(r) for r in rows) + '\n', encoding='utf-8')


def long_session(hours=7.0, seed=11):
    """A long session with a stall about every 8 seconds and 0.1 % of presents missing."""
    rng = random.Random(seed)
    frames = int(hours * 3600 * 60)
    stalls = {i: rng.uniform(60.0, 400.0) for i in range(240, frames, 480)}
    rows, presents = session(frames, seed, stalls)
    missing = {i for i in range(frames) if rng.random() < 0.001}
    dropped = {i for i in range(frames) if rng.random() < 0.002} - missing
    return rows, presents, missing, dropped


def main():
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument('--hours', type=float, default=7.0)
    parser.add_argument('--offset-ms', type=float, default=37.0)
    parser.add_argument('--out-dir', type=Path, required=True)
    args = parser.parse_args()
    args.out_dir.mkdir(parents=True, exist_ok=True)
    rows, presents, missing, dropped = long_session(args.hours)
    (args.out_dir / 'frames.json').write_text(json.dumps(rows), encoding='utf-8')
    write_csv(trace_rows(presents, 2, args.offset_ms, missing=missing, dropped=dropped), args.out_dir / 'trace.csv')
    print(f'{len(rows)} frames, {len(presents) - len(missing)} presents, {len(missing)} missing, {len(dropped)} dropped')


if __name__ == '__main__':
    main()
