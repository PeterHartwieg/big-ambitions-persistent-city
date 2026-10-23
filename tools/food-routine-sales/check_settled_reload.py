"""Reload after the game's daily settlement has cleared native pending orders."""
from protocol import *
previous=json.loads((ROOT/'final.json').read_text())
assert previous['purchases']>0 and not previous['orders']
name='FT Food Routines Settled 20261022'
checked('save-settled','save',{'name':name});checked('load-settled','load',{'name':name})
end=time.monotonic()+300
while True:
 h=health()
 if h.get('scene')=='city' and (h.get('load') or {}).get('status')=='done':break
 if time.monotonic()>end:raise RuntimeError('Settled reload timeout')
 time.sleep(2)
checked('pause-settled','time',{'pause':True,'speed':1})
while True:
 current=probe_call('status',30)
 if 'error' not in current and abs(current['minute']-current['nativeMinute'])<.01:break
 if time.monotonic()>end:raise RuntimeError('Population not ready after settled reload')
 time.sleep(2)
record('after-settled-reload',current)
checks={key:previous[key]==current[key] for key in ['purchases','spendingCents','orders','receipts','history']}
checks['stock']=previous['supply']['ProducerUnits']==current['supply']['ProducerUnits'] and previous['supply']['BagUnits']==current['supply']['BagUnits']
replay=record('duplicate-after-settlement',probe_call('replay-paid'))
checks['no-replay-after-settlement']=replay.get('paid') and 'already confirms' in replay.get('reason','') and replay['before']['orders']==replay['after']['orders'] and replay['before']['supply']==replay['after']['supply']
record('settled-reload-checks',checks)
record('population-dump-settled',probe_call('dump'))
print(json.dumps(checks,indent=2))
assert all(checks.values()),checks
