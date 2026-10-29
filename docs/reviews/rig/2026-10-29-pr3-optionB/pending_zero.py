import json,subprocess,sys
import rig
from rate_arms import configure,run,config,original
from schema_native_helpers import unload,load
try:
 opening=configure('pending-zero',0,'FT Food Routines PR3 OB Rate50')
 ending=run('pending-zero',1,'FT Food Routines PR3 OB PendingZero')
 assert opening['audit']['pending']==ending['audit']['pending']
 unload('pending-zero-reload');load('pending-zero-reload','FT Food Routines PR3 OB PendingZero')
 reloaded=rig.probe('pending-zero-reloaded-status','optional-status',True)
 rig.record('pending-zero-summary',dict(opening=opening['audit'],ending=ending['audit'],reloaded=reloaded['audit'],pendingPreserved=opening['audit']['pending']==ending['audit']['pending']==reloaded['audit']['pending']))
finally:
 unload('pending-zero-restore');config.write_bytes(original)
