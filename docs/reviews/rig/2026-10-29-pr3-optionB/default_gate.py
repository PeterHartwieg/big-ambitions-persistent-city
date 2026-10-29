from pathlib import Path
import math,json
s=Path(__file__).parent;z=1.959963984540054;out={}
for label,(k,n) in {'initial10':(2272,10037),'continued28':(7911,32063),'combined38':(10183,42100),'combinedDraws':(10339,42100)}.items():
 p=k/n;d=1+z*z/n;a=(p+z*z/(2*n))/d;b=z*math.sqrt(p*(1-p)/n+z*z/(4*n*n))/d
 out[label]=dict(numerator=k,eligible=n,share=p,wilson95=[a-b,a+b],target25WithinInterval=a-b<=.25<=a+b)
(s/'default-rate-gate.json').write_text(json.dumps(out,indent=2));print(json.dumps(out,indent=2))
