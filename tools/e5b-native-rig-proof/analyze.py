"""Summarize exported native samples; never infer behavioral proof from timing alone."""
import argparse
import json
import math
from pathlib import Path

parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('capture',type=Path,help='JSON object: status, frames[], events[] collected from all exported pages')
parser.add_argument('--output',type=Path,required=True)
args=parser.parse_args()
capture=json.loads(args.capture.read_text(encoding='utf-8-sig'))
status=capture['status']
frames=capture['frames']
events=capture['events']
def distribution(values):
    values=sorted(values)
    if any(not math.isfinite(x) or x<0 for x in values):
        raise ValueError('Invalid native timing sample')
    if not values:
        return dict(count=0,p50_ms=None,p95_ms=None,max_ms=None,spikes_ge30=0)
    return dict(count=len(values),p50_ms=values[math.ceil(len(values)*.5)-1],
        p95_ms=values[math.ceil(len(values)*.95)-1],max_ms=values[-1],spikes_ge30=sum(x>=30 for x in values))
if len({row['frame'] for row in frames})!=len(frames):
    raise ValueError('Duplicate exported frames')
if len({row['n'] for row in events})!=len(events):
    raise ValueError('Duplicate exported event sequence')
if any(events[i]['n']>=events[i+1]['n'] for i in range(len(events)-1)):
    raise ValueError('Unordered exported events')
clean=[row for row in frames if not row['diagnostic']]
components={}
seen=set()
frequency=status['stopwatch_frequency']
for row in events:
    if not row['kind'].endswith(':after'):
        continue
    key=(row['call'],row['kind']) # Same global callback is reported once per selected shop.
    if key in seen:
        continue
    seen.add(key)
    components.setdefault(row['kind'][:-6],[]).append(row['durationTicks']*1000/frequency)
invalid=[]
for field in ['event_dropped','frame_dropped','timed_exceptions','cross_frame_scopes','off_main_callbacks']:
    if status[field]: invalid.append(field)
if status['error']: invalid.append('probe_error')
if status['measuring']: invalid.append('measurement_not_stopped')
if not status.get('timing_coverage_complete',False): invalid.append('partial_mod_work_scope_coverage')
if not clean: invalid.append('no_clean_completed_frames')
if any(row.get('skippedUnityFrames',0) for row in frames): invalid.append('unobserved_unity_frames')
if any(row.get('queueCount',-1)<0 for row in events): invalid.append('unavailable_queue_count')
mod=distribution([row['modWorkMs'] for row in clean])
report=dict(format=1,native_behavior_certified=False,
    qualification='Metrics only. Raw mod-work includes nested probe cost; observerMs is a partial overhead measurement. No sums of nested inclusive components. Autosave, no saves, native fixture provenance, dotnet-idle and complete scope coverage require independent session receipts.',
    invalid_capture_reasons=invalid,frames_total=len(frames),diagnostic_frames_excluded=len(frames)-len(clean),
    mod_work=mod,whole_frame=distribution([row['wholeFrameMs'] for row in clean]),
    measured_trace_overhead=distribution([row['observerMs'] for row in clean]),
    components={name:distribution(values) for name,values in sorted(components.items())},
    timed_pc_scope_counters=[row for row in status.get('bound_scopes',[]) if row['timed_pc_scope']],
    mod_work_numerically_below_5ms=bool(not invalid and mod['p95_ms'] is not None and mod['p95_ms']<5),
    spikes=[row for row in clean if row['modWorkMs']>=30 or row['wholeFrameMs']>=30],
    component_spikes=[dict(call=row['call'],kind=row['kind'],target=row['target'],duration_ms=row['durationTicks']*1000/frequency)
        for row in events if row['kind'].endswith(':after') and row['durationTicks']*1000/frequency>=30])
args.output.parent.mkdir(parents=True,exist_ok=True)
args.output.write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
print(json.dumps(report,indent=2))
