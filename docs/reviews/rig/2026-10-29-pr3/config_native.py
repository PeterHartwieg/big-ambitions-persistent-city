from pathlib import Path
import sys,json,time,shutil,hashlib
import rig
from schema_native_helpers import unload,load
s=rig.STAGE;config=rig.PROFILE/'ModsLocal/PersistentCity/persistent-city.json';original=config.read_bytes();(s/'campaign-config-original.private.json').write_bytes(original)
rows=[]
try:
 for label,value,expected in [('zero',0,0),('hundred',100,100),('negative',-5,0),('over',150,100),('fraction',12.5,25),('string','invalid',25),('numeric-string','75',75),('missing',None,25)]:
  tag='config-'+label;unload(tag)
  d=json.loads(original);d.pop('OptionalStopChoicePercentage',None)
  if value is not None:d['OptionalStopChoicePercentage']=value
  config.write_text(json.dumps(d,indent=2));load(tag,'FT Food Routines PR3 Stops20261029')
  r=rig.probe(tag+'-used','optional-status',True);row=dict(input=value,missing=value is None,expected=expected,configured=r['configuredPercentage'],attached=r['attachedPercentage'],passed=r['configuredPercentage']==r['attachedPercentage']==expected)
  rows.append(row);(s/'config-summary.json').write_text(json.dumps(rows,indent=2));print(tag,row,flush=True);assert row['passed']
  if value in [0,100]:
   import subprocess
   # Same natural two-day replay as default; no weather/price/guard changes.
   subprocess.run([sys.executable,str(s/'financial_run.py'),tag],check=True)
finally:
 unload('config-restore');config.write_bytes(original)
print('Native settings cases completed; original campaign config restored',flush=True)
