import rig,json
from rate_arms import configure,skip,config,original
from schema_native_helpers import unload
try:
 for rate in [25,100]:
  label='metrics'+str(rate);configure(label,rate)
  rig.probe(label+'-start','optional-metrics-start',True)
  rig.probe(label+'-needs-start','skip-needs-start')
  try:skip(label,48)
  finally:
   rig.probe(label+'-needs-end','skip-needs-end')
   r=rig.probe(label+'-stop','optional-metrics-stop',True)
  print(label,r['scheduleCalls'],r['spotCalls'],r['occupancyCalls'],flush=True)
finally:
 unload('metrics-restore');config.write_bytes(original)
