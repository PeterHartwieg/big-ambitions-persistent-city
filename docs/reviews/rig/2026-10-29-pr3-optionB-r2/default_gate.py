"""Evaluate current measurements against the owner's long-run criterion."""
from pathlib import Path
import json,math
s=Path(__file__).parent
rates=json.loads((s/'rate-summary.json').read_text())
light=json.loads((s/'light25-summary.json').read_text())
def interval(k,n):
 z=1.959963984540054;p=k/n;d=1+z*z/n
 a=(p+z*z/(2*n))/d;b=z*math.sqrt(p*(1-p)/n+z*z/(4*n*n))/d
 return [a-b,a+b]
def row(n,k,drawn,opening,closing,drops):
 return dict(eligible=n,scheduledRecipients=k,share=k/n,wilson95=interval(k,n),drawn=drawn,drawShare=drawn/n,drawWilson95=interval(drawn,n),openingPending=opening,closingPending=closing,dropped=drops,deltaPendingOverN=(closing-opening)/n,dropsOverN=drops/n,auditExact=drawn==k+closing-opening+drops)
out={}
for label,key in [('cold10','rate25'),('warm28','rate25-continued')]:
 r=rates[key];out[label]=row(r['eligible'],r['scheduledRecipients'],r['drawn'],r['openingPending'],r['closingPending'],r['tallyDelta']['Dropped'])
a,b=out['cold10'],out['warm28']
out['combined38']=row(a['eligible']+b['eligible'],a['scheduledRecipients']+b['scheduledRecipients'],a['drawn']+b['drawn'],a['openingPending'],b['closingPending'],a['dropped']+b['dropped'])
initial,final=light['opening'],light['days'][-1]
t={k:final['tally'][k]-initial['tally'][k] for k in final['tally']}
out['cold28Untraced']=row(t['Eligible'],t['Realised'],t['Drawn'],initial['pending'],final['pending'],t['Dropped'])
out['criterion']='About 25% long-run; owner accepts about 24-25% over a month or warm window. Fixed-seed draw interval is descriptive, not a mandatory gate. Cold lag is reported, with no catch-up logic.'
out['acceptedLongRunWindows']=[k for k in ['warm28','cold28Untraced'] if .24<=out[k]['share']<=.26]
out['passed']=all(out[k]['auditExact'] for k in ['cold10','warm28','combined38','cold28Untraced']) and bool(out['acceptedLongRunWindows']) and all(out[k]['dropped']==0 for k in ['cold10','warm28','cold28Untraced'])
(s/'default-rate-gate.json').write_text(json.dumps(out,indent=2))
print(json.dumps(out,indent=2))
