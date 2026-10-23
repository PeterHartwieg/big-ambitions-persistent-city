"""Deploy only inside owned, backed-up rig; always stop and restore on exit."""
from pc import *
import time, uuid, zipfile, io
baseline=json.loads((ROOT/'baseline.json').read_text());assert baseline['copyVerified']
assert json.loads(a.locks.lock_action('agent1','status'))['owner']==os.environ['BA_LOCK_OWNER']
package=ROOT/'source/build/PersistentCity';probe=ROOT/'Probe/FoodRoutineSalesProbe.dll'
receipt=json.loads((ROOT/'Probe/build-manifest.json').read_text())
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
assert sha(probe)==receipt['output']['sha256']
assert sha(package/'PersistentCity.dll')==next(x['sha256'] for x in receipt['references'] if x['path'].endswith('/PersistentCity.dll'))
def health():return status().get('health') or {}
def probe_call(op,timeout=180):
 ident=op+'-'+uuid.uuid4().hex[:8]
 text=json.dumps({'id':ident,'op':op})
 remote('[IO.File]::WriteAllText('+a.literal(HELPER+r'\request.tmp')+','+a.literal(text)+');Move-Item -Force -LiteralPath '+a.literal(HELPER+r'\request.tmp')+' -Destination '+a.literal(HELPER+r'\request.json'),True,kind='diagnostic')
 path=HELPER+'\\response-'+ident+'.json';until=time.monotonic()+timeout
 while time.monotonic()<until:
  data=remote('if(Test-Path -LiteralPath '+a.literal(path)+'){[IO.File]::ReadAllText('+a.literal(path)+')}')
  if data:return json.loads(data)
  time.sleep(2)
 raise RuntimeError('Probe timed out: '+op)
def checked(name,cmd,args):
 d=call(name,cmd,args)
 if not d.get('ok'):raise RuntimeError(str(d))
 return d
