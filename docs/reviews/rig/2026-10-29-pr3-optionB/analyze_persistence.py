from pathlib import Path
import json,hashlib,math
s=Path(__file__).parent
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def one(label,suffix):
 paths=list(s.glob(label+'-*-'+suffix+'.json'));assert len(paths)==1,(label,paths);return paths[0]
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
out={}
for label in ['after-qa-1','after-qa-2','after-qa-3','after-finance-save','optional-dwell-control','event-hand-on-control','event-pass-on-control','event-conversation-control']:
 if not list(s.glob(label+'-reloaded-*-population.json')):continue
 before=read(one(label+'-pre-save','population'));after=read(one(label+'-reloaded','population'))
 a={c['Identity']['Id']:c for c in before['Citizens']};b={c['Identity']['Id']:c for c in after['Citizens']}
 choices=[k for k in a if a[k].get('OptionalStopChoice')!=b[k].get('OptionalStopChoice')];stops=[k for k in a if a[k].get('OptionalStop')!=b[k].get('OptionalStop')]
 new_visits=[];rerolled=[]
 for ident in choices:
  old=a[ident]['RecentVisits'][-1] if a[ident]['RecentVisits'] else None;new=b[ident]['RecentVisits'][-1] if b[ident]['RecentVisits'] else None
  if new!=old and new and before['Minute']<new['AtMinute']<=after['Minute']:
   new_visits.append(dict(id=ident,choiceBefore=a[ident].get('OptionalStopChoice'),choiceAfter=b[ident].get('OptionalStopChoice'),arrival=new,scheduledEventBefore=a[ident]['NextEventMinute']))
  else:rerolled.append(ident)
 fa=read(s/(label+'-pre-save-finance.json'));fb=read(s/(label+'-reloaded-finance.json'))
 row=dict(schemaBefore=before['Schema'],schemaAfter=after['Schema'],citizens=len(a),identityAssignmentChanges=[k for k in a if any(a[k][n]!=b[k][n] for n in ['Identity','HomeId','DayPlaceId','LeisureId','ShopId','Preferences'])],choiceChanges=choices,stopChanges=stops,receiptsEqual=fa['receipts']==fb['receipts'],walletsEqual=fa['wallets']==fb['wallets'],savedPayloadByteIdentical=sha(one(label+'-saved','saved-payload'))==sha(one(label+'-reloaded','saved-payload')))
 row.update(minuteBefore=before['Minute'],minuteAfter=after['Minute'],newVisitChoiceChanges=new_visits,rerolledExistingVisitChoices=rerolled)
 row['passed']=not row['identityAssignmentChanges'] and not rerolled and not stops and row['receiptsEqual'] and row['walletsEqual'] and row['savedPayloadByteIdentical'];out[label]=row
(s/'persistence-summary.json').write_text(json.dumps(out,indent=2));print(json.dumps(out,indent=2))
