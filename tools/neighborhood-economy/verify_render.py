"""Summarize whole-game native rendering measurements without substituting advance timings."""
from pathlib import Path
import json,sys
root=Path(sys.argv[1]);labels=['candidate-a','native-control','candidate-b'];reports={}
fields=['width','height','quality','vSync','targetFps','calendarFactor','gpu','cpu','graphicsApi','timeScale','focused','runInBackground']
for label in labels:
 report=json.loads((root/(label+'-frames.json')).read_text())
 assert report['frameMilliseconds']['samples']>0 and report['realSeconds']>=175
 assert report['environment']['focused']
 samples=json.loads((root/(label+'-samples.json')).read_text())
 assert samples and all(s['environment']['focused'] for s in samples),('focus changed',label)
 if label!='native-control':
  assert report['citizens']>=900
  assert report['peakObservedPedestrians']<=48 and report['peakObservedCars']<=24
 allocations=report['allocatedBytesPerFrame'] if report.get('allocationCounterObservedPositive') else None
 reports[label]={'realSeconds':report['realSeconds'],'gameMinutes':report['nativeGameMinutes'],
  'wholeFrameMs':report['frameMilliseconds'],'populationUpdateMs':report['totalPopulationUpdateMilliseconds'],
  'framesOver100ms':report['framesOver100ms'],'framesOver250ms':report['framesOver250ms'],
  'peakObservedPedestrians':report['peakObservedPedestrians'],'peakObservedCars':report['peakObservedCars'],
  'populationLagMinutes':report['populationLagMinutes'],'wholeGameAllocationBytesPerFrame':allocations,
  'managedHeapChangeBytes':report['managedBytesAfter']-report['managedBytesBefore'],'collections':report['collections'],
  'unityCpuFrameMs':report['unityCpuFrameMilliseconds'] if report['unityCpuFrameMilliseconds']['samples'] else None,
  'unityGpuFrameMs':report['unityGpuFrameMilliseconds'] if report['unityGpuFrameMilliseconds']['samples'] else None,
  'environment':report['environment']}
control=reports['native-control'];relative={}
for label in ['candidate-a','candidate-b']:
 candidate=reports[label]
 assert all(candidate['environment'][key]==control['environment'][key] for key in fields),('unmatched environment',label)
 for group in ['position','eulerAngles']:
  assert all(abs(candidate['environment']['camera'][group][axis]-control['environment']['camera'][group][axis])<.001 for axis in ['x','y','z']),('unmatched camera',label,group)
 relative[label]={'p95FrameRatio':candidate['wholeFrameMs']['p95']/control['wholeFrameMs']['p95'],
  'meanFrameDifferenceMs':candidate['wholeFrameMs']['mean']-control['wholeFrameMs']['mean'],
  'framesOver100msPerMinute':candidate['framesOver100ms']*60/candidate['realSeconds']}
output={'cases':reports,'relativeToNativePopulationControl':relative,
 'boundary':'Observed full-modpack Mac player, same native checkpoint and calendar. These short samples do not establish Windows performance, maximum-body saturation, long soaks or enjoyable gameplay. Missing or all-zero allocation counters are unavailable, not zero allocation.'}
provenance=root/'render-provenance.json'
if provenance.exists():output['provenance']=json.loads(provenance.read_text())
(root/'render-summary.json').write_text(json.dumps(output,indent=2)+'\n')
print(json.dumps(output,indent=2))
