"""Derive bounded food/place separation from the retained campaign inputs."""
from pathlib import Path
import json,hashlib
s=Path(__file__).parent
def read(n):return json.loads((s/n).read_text(encoding='utf-8-sig'))
def sha(n):return hashlib.sha256((s/n).read_bytes()).hexdigest()
snapshot='after-final-af1449-population.json'
owned={x['PlaceId'] for x in read(snapshot)['Food']['Shops']}
rates=read('rate-summary.json')
overlaps={label:{place:count for place,count in arm['venue'].items() if place in owned} for label,arm in rates.items()}
assert not any(overlaps.values()),overlaps
fin=read('after-financial-verification.json')
assert fin['gateA'] and fin['gateB'] and fin['confirmedSales']==110
result={
 'ownedFoodPlaces':sorted(owned),'optionalVenueOverlapByArm':overlaps,
 'confirmedFoodSalesWithOptionBActive':fin['confirmedSales'],
 'gateA':fin['gateA'],'gateB':fin['gateB'],
 'optionalStopNativeOrdersObserved':0,
 'scope':'No optional scheduled venue overlaps the owned food shop in the audited rate arms. All 110 food sales pass exact verification with option B active. No optional-stop sale/payment case was exercised because the scheduler excludes the owned food shop.',
 'sourceGuard':'Core/OptionalStopScheduling.cs:177-178 (tested head)',
 'inputHashes':{n:sha(n) for n in [snapshot,'rate-summary.json','after-financial-verification.json']},
}
(s/'optional-food-separation.json').write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8')
print('Food/place separation verified from existing evidence.')
