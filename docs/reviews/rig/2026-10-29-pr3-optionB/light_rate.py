from rate_arms import configure,skip,config,original
from schema_native_helpers import unload
import rig,json
try:
 opening=configure('light25',None)
 rig.probe('light25-needs-start','skip-needs-start')
 rows=[]
 try:
  for day in range(1,29):
   r=skip('light25-d'+str(day),24);rows.append(r['audit']);print('light25',day,r['audit'],flush=True)
 finally:rig.probe('light25-needs-end','skip-needs-end')
 rig.record('light25-summary',dict(opening=opening['audit'],days=rows,traceEnabled=False,measurement='Untraced Core Eligible/Realised counters; detailed observer previously reconciled counters against independent choices and unique scheduled recipients. Native skip output, not a physical dwell census.'))
finally:
 unload('light25-restore');config.write_bytes(original)
