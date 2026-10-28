"""Plot observed native outcomes; daily points show variation, not confidence intervals."""
from pathlib import Path
import json,sys
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
from matplotlib.ticker import StrMethodFormatter

root=Path(sys.argv[1]);data=json.loads((root/'comparison-summary.json').read_text())
names={'one-small':'One small shop','two-overlapping':'Two nearby small shops','two-separated':'Two separated small shops',
 'larger':'Larger premises','higher-price':'Single shop: price +50%','lower-service':'Single shop: staff skill 25',
 'longer-hours':'Single shop: 06:00–22:00','lunch-hours':'Single shop: 10:00–14:00'}
rows=[(name,data['cases'][name]) for name in names if name in data['cases']]
assert rows
plt.rcParams.update({'font.family':'DejaVu Sans','font.size':11,'axes.spines.top':False,'axes.spines.right':False})
fig,axes=plt.subplots(1,2,figsize=(14,8),gridspec_kw={'wspace':.15},sharey=True)
fig.patch.set_facecolor('#fafaf7')
for axis in axes:
 axis.set_facecolor('#fafaf7');axis.grid(axis='x',alpha=.18);axis.set_axisbelow(True)
labels=[]
for index,(name,case) in enumerate(rows):
 days=case['days'][:2];capacity=sum(f['customerCapacity'] for f in case['fixtures'])
 labels.append(names[name]+f' ({capacity}/h equipped)')
 for axis,key,color in [(axes[0],'purchases','#2c7184'),(axes[1],'nativeOperatingProfit','#25805c')]:
  values=[d[key] for d in days];mean=sum(values)/len(values)
  axis.barh(index,mean,color=color if mean>=0 else '#b55c4a',height=.58,alpha=.85)
  axis.scatter(values,[index]*len(values),s=30,c='#303f44',zorder=3)
  label=f'{mean:.1f}' if key=='purchases' else (f'−${abs(mean):,.0f}' if mean<0 else f'${mean:,.0f}')
  anchor=max(values) if mean>=0 else min(values)
  axis.annotate(label,(anchor,index),
    xytext=(7 if mean>=0 else -7,0),textcoords='offset points',ha='left' if mean>=0 else 'right',va='center',fontsize=10)
axes[0].set_yticks(range(len(rows)),labels);axes[0].invert_yaxis()
axes[0].set(title='Paid purchases per day',xlabel='Native purchases');axes[0].margins(x=.2)
axes[1].set(title='Native operating profit per day',xlabel='Sales − goods − wages − rent − other operating costs')
axes[1].axvline(0,color='#6a7375',lw=.8);axes[1].xaxis.set_major_formatter(StrMethodFormatter('${x:,.0f}'));axes[1].margins(x=.28)
fig.suptitle('Observed neighborhood shop outcomes',x=.04,y=.97,ha='left',fontsize=19,fontweight='bold')
fig.text(.04,.91,'957 persistent residents · same imported starting city · Mac native game · finite resident accounts and stock',fontsize=11)
fig.subplots_adjust(left=.28,right=.96,top=.82,bottom=.19)
fig.text(.04,.105,'Bars: average of the first two days in each case. Dots: the individual days. These are observed test outcomes, not forecasts.',fontsize=10)
fig.text(.04,.065,'Equipment and opening stock are valued test assets. Operating results exclude upfront investment, loans and personal taxes.',fontsize=10,color='#555555')
for ext in ['png','svg','pdf']:fig.savefig(root/('neighborhood-comparison.'+ext),dpi=160,facecolor=fig.get_facecolor())
