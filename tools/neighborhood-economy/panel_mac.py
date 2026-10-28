#!/usr/bin/env python3
"""Check the final management display in the owned rendered Mac player."""
from mac_rig import *

run=MacRig(os.environ['PC_FOOD_CAMPAIGN'],physical=True,mode='windowed')
try:
 run.stage('preparing-panel-check');run.begin()
 run.stage('load-rendered-checkpoint');started=time.monotonic()
 run.record('panel-state',run.load('panel','FT Food Routines Mac Neighborhood Rendered 20261023'))
 run.record('load-timing',{'seconds':time.monotonic()-started})
 run.record('initial-discovery',run.probe('runtime-state'))
 run.stage('paused-reload')
 before=run.record('save-reload-before',run.probe('status'))
 checkpoint='FT Food Routines Mac Neighborhood Panel 20261023'
 started=time.monotonic();run.call('save-panel-checkpoint','save',{'name':checkpoint})
 run.record('save-timing',{'seconds':time.monotonic()-started})
 started=time.monotonic();after=run.record('save-reload-after',run.load('panel-reload',checkpoint))
 run.record('reload-timing',{'seconds':time.monotonic()-started})
 run.record('reload-discovery',run.probe('runtime-state'))
 for key in ['openingSavings','income','essentials','householdFood','heldPurchases','arrears']:
  assert before['economy'][key]==after['economy'][key],('reload changed',key)
 assert before['economy']['balances']+before['economy']['preparedFood']==after['economy']['balances']+after['economy']['preparedFood']
 run.stage('panel-visual-check')
 report=run.record('panel-economy',run.probe('economy-report'))
 assert 'ba:street' not in report['managementText'],'Internal address key remains visible'
 run.focus('panel');run.record('panel-screenshot',run.probe('panel-screenshot'));time.sleep(3)
 shutil.copy2(run.helper/'neighborhood-management.png',run.root/'neighborhood-management.png')
 run.record('panel-complete',{'addressUsesNativeFormatting':True,'reloadAccountsReconciled':True,'screenshotRequiresVisualReview':True})
except BaseException as error:
 run.record('error',{'type':type(error).__name__,'error':str(error)[:8000]});raise
finally:run.close()
