"""Plot retained PC observations without smoothing or changing the model."""
from pathlib import Path
import json, argparse, csv
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
from matplotlib.ticker import PercentFormatter, MaxNLocator, StrMethodFormatter
p=argparse.ArgumentParser();p.add_argument('run',type=Path);args=p.parse_args();root=args.run
r=json.loads((root/'native-result.json').read_text());points=r['points'];target=r['selection'];shop=next(s for s in target['shops'] if s['Id']==r['targetId'])
single=len(shop['Offers'])==1
base_x=shop['Offers'][0]['PriceCents']/100 if single else 100
x=[v['targetPrices'][0]['PriceCents']/100 if single else v['factor']*100 for v in points];y=[v['meals'] for v in points];rev=[v['revenueCents']/100 for v in points];base=next(v for v in points if v['factor']==1)
plt.rcParams.update({'font.family':'DejaVu Sans','font.size':11,'axes.spines.top':False,'axes.spines.right':False,'axes.titleweight':'bold'})
fig,ax=plt.subplots(1,3,figsize=(16,6),gridspec_kw={'wspace':.32});fig.patch.set_facecolor('#fafaf7')
for a in ax:
 a.set_facecolor('#fafaf7');a.grid(axis='y',alpha=.2);a.axvline(base_x,color='#888888',lw=1,ls=':');a.set_xlabel(shop['Offers'][0]['ProductId'].replace('ba:itemname_','').title()+' price ($)' if single else 'Price vs observed menu');a.xaxis.set_major_formatter(StrMethodFormatter('${x:.0f}') if single else PercentFormatter())
ax[0].plot(x,y,'o-',color='#19754b',markersize=4,lw=2);ax[0].set_title('Mod: meals served');ax[0].set_ylabel('Hypothetical meals / day');ax[0].set_ylim(bottom=0);ax[0].yaxis.set_major_locator(MaxNLocator(integer=True))
ax[0].plot([base_x],[base['meals']],'o',color='#252525',markersize=7)
if single:
 ax[0].axvline(18,color='#9c4747',lw=1,ls='--',alpha=.6)
 ax[0].annotate('Shared $18 limit',xy=(18,7),xytext=(22,10),color='#9c4747',fontsize=10,arrowprops={'arrowstyle':'->','color':'#9c4747','lw':1})
ax[1].plot(x,rev,'o-',color='#b96d1e',markersize=4,lw=2);ax[1].set_title('Mod: gross revenue');ax[1].set_ylabel('Hypothetical revenue / day ($)');ax[1].set_ylim(bottom=0)
curves=r['native']['curves']; colors=['#3c608b','#af4f63','#75649a','#7a7a7a']
shares=r['native']['classShares']
# Class shares are native neighbourhood fractions or percents; normalize their own total.
weighted=[]
for i,curve in enumerate(curves):
 weights=[]
 for c in curve['classes']:
  key=next((k for k in shares if k in c['socialClass'].lower()),None)
  weights.append(float(shares[key]) if key else 0)
 denom=sum(weights)
 if not denom:continue
 accepted=[100*sum(w*int(c['points'][j]['accepts']) for w,c in zip(weights,curve['classes']))/denom for j in range(len(points))]
 ax[2].step([p['price'] for p in curve['classes'][0]['points']] if single else x,accepted,where='post',color=colors[i%len(colors)],lw=2,label=curve['product'].replace('ba:itemname_','').title())
 weighted.append({'product':curve['product'],'acceptancePercent':accepted})
ax[2].set_title('Game: price acceptance');ax[2].set_ylabel('Neighbourhood class-weighted acceptance');ax[2].yaxis.set_major_formatter(PercentFormatter());ax[2].set_ylim(-3,105);ax[2].legend(loc='lower left',frameon=False)
fig.suptitle(f"{target['name']} · small-store price test on the PC rig",x=.06,y=.97,ha='left',fontsize=19,fontweight='bold')
menu=', '.join(f"{o['ProductId'].replace('ba:itemname_','').title()} ${o['PriceCents']/100:.2f}" for o in shop['Offers'])
fig.text(.06,.88,f"{menu}   |   service {shop['CustomerServicePercent']}%   |   {r['residents']} residents, one recorded day",fontsize=11)
fig.subplots_adjust(top=.78,bottom=.24,left=.065,right=.98)
fig.text(.06,.135,'Observed: routines, native walking quotes, opening hours, prices, customer service and district demand.',fontsize=10)
fig.text(.06,.09,'Controlled: 15 meals/hour per shop, ample stock, fixed $18 meal allowance; eight shops. Price changes are shadow-only.',fontsize=10)
fig.text(.06,.045,'The game reference measures willingness to accept a price, not sales volume. Revenue excludes costs; this is not a profit forecast.',fontsize=10,color='#555555')
for ext in ['png','svg','pdf']:fig.savefig(root/f'demand-curve.{ext}',dpi=160,facecolor=fig.get_facecolor())
with(root/'curve.csv').open('w',newline='')as f:
 w=csv.writer(f);w.writerow(['price_factor','target_meals_per_day','hypothetical_gross_revenue','other_shop_meals','unmet','unresolved'])
 for v in points:w.writerow([v['factor'],v['meals'],v['revenueCents']/100,v['allShops']['fulfilled']-v['meals'],v['allShops']['unmet'],v['allShops']['unresolved']])
summary={'target':target['name'],'baseline':base,'peakGrossRevenue':max(points,key=lambda v:v['revenueCents']),'demandNonIncreasing':all(y[i+1]<=y[i] for i in range(len(y)-1)),'nativeWeightedReference':weighted}
(root/'plot-summary.json').write_text(json.dumps(summary,indent=2))
print(json.dumps({k:v for k,v in summary.items() if k!='nativeWeightedReference'},indent=2))
