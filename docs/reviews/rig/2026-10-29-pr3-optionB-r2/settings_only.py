from pathlib import Path
import json
import rig
from schema_native_helpers import unload,load
s=rig.STAGE;config=rig.PROFILE/'ModsLocal/PersistentCity/persistent-city.json';original=config.read_bytes();rows=[]
try:
 for label,value,expected in [('zero',0,0),('hundred',100,100),('negative',-5,0),('over',150,100),('fraction',12.5,25),('string','invalid',25),('numeric-string','75',75),('missing',None,25)]:
  tag='setting-'+label;unload(tag);d=json.loads(original);d.pop('OptionalStopChoicePercentage',None)
  if value is not None:d['OptionalStopChoicePercentage']=value
  config.write_text(json.dumps(d,indent=2));load(tag,'FT Food Routines PR3 OB20261029')
  r=rig.probe(tag+'-used','optional-status',True);row=dict(input=value,missing=value is None,expected=expected,configured=r['configuredPercentage'],attached=r['attachedPercentage'],passed=r['configuredPercentage']==r['attachedPercentage']==expected)
  rows.append(row);(s/'config-summary.json').write_text(json.dumps(rows,indent=2));print(tag,row,flush=True);assert row['passed']
finally:
 unload('settings-restore');config.write_bytes(original)
