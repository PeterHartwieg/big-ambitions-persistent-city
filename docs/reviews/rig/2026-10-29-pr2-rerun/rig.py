import pathlib,json,urllib.request,time,os,uuid,shutil
STAGE=pathlib.Path(__file__).parent
PROFILE=pathlib.Path.home()/'AppData/LocalLow/Hovgaard Games/Big Ambitions Agent Tests'
SHOTS=STAGE/'screenshots';SHOTS.mkdir(parents=True,exist_ok=True)
http=urllib.request.build_opener(urllib.request.ProxyHandler({}))
def record(label,data):
 (STAGE/(label+'.json')).write_text(json.dumps(data,indent=2));return data
def req(path,data=None):
 request=urllib.request.Request('http://127.0.0.1:18330'+path,data=None if data is None else json.dumps(data).encode(),headers={'Content-Type':'application/json'})
 with http.open(request) as f:return json.load(f)
def health():
 h=req('/health');run=json.loads(pathlib.Path(r'C:\BigAmbitions-AgentTests\run.json').read_text(encoding='utf-8-sig'))
 assert h['control_lock']['instance']=='agent1' and h['control_lock']['owner']=='persistent-city-pr2-rig-rerun' and h['control_process_pid']==run['pid'],h
 return h
def call(label,cmd,args={}):
 h=health();r=req('/call',dict(cmd=cmd,args=args,expect_session=h['session'],lock_token=(STAGE/'lease-token.private').read_text()))
 record(label+'-reply',r)
 if not r.get('ok') and 'timed out' in r.get('error','').lower():
  while True:
   s=req('/call',{'cmd':'op','args':{'id':r['op']}})
   record(label+'-op',s);v=s.get('result',{})
   if v.get('status')=='failed':raise RuntimeError(v)
   if v.get('status')=='done':return record(label,v['result'])
   time.sleep(.5)
 if not r.get('ok'):raise RuntimeError(r)
 return record(label,r['result'])
def waitcity(label,expected=None):
 while True:
  h=health()
  if (h.get('load') or {}).get('status')=='failed':raise RuntimeError(h)
  if h.get('scene')=='city' and (h.get('load') or {}).get('status')=='done' and (expected is None or (h.get('load') or {}).get('name')==expected):return record(label,h)
  time.sleep(2)
def probe(label,op,guard=False,extra={}):
 health();guard=guard or op in ['status'];folder=PROFILE/'ModsLocal'/('PR2RigGuard' if guard else 'FoodDemandAuditProbe')
 ident=label+'-'+uuid.uuid4().hex[:6];target=folder/('response-'+ident+'.json')
 (folder/'request.json').write_text(json.dumps(dict(id=ident,op=op,**extra)))
 while not target.exists():time.sleep(.2)
 r=json.loads(target.read_text(encoding='utf-8-sig'));record(label,r)
 for src in folder.glob('*'+ident+'*'):
  if src.is_file():shutil.copy2(src,STAGE/src.name)
 if 'error' in r:raise RuntimeError(r)
 return r

def shot(label):
 r=call(label+'-capture','shoot',dict(name=label,x=274,y=8,z=316,game_view=False))
 source=r.get('game_path') or r.get('path');assert source,'No native image returned';shutil.copy2(source,SHOTS/(label+'.png'));record(label+'-image-kind',{'kind':'game-view' if r.get('game_path') else 'diagnostic-scene-camera','gameViewError':r.get('game_error'),'path':str(SHOTS/(label+'.png'))});return r
if __name__=='__main__':print(json.dumps(health(),indent=2))
