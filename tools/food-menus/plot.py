#!/usr/bin/env python3
"""Plot verified native menu outcomes; input is the reconciled campaign summary."""
from pathlib import Path
import json,sys
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
root=Path(sys.argv[1]);summary=json.loads((root/'comparison-summary.json').read_text())
keys=['pastry','cafe-menu','different-menus','higher-burger-price']
assert all(k in summary['cases'] for k in keys)
labels=['Pastry only*\n1 café','Café menu\n1 café','Different menus\n2 shops','Burger +50% price*\n2 shops']
fig,axes=plt.subplots(1,2,figsize=(12,5),sharey=True)
fig.patch.set_facecolor('#f5f4f0')
colors=['#e4ad52','#549d91','#666b9e']
for ax,kind,title in zip(axes,['meal','product'],['Sales by meal','Sales by product']):
 ax.set_facecolor('#f5f4f0');bottom=[0.0]*4
 groups=['Breakfast','Lunch','Dinner'] if kind=='meal' else ['Croissant','Hot dog','Burger']
 for index,group in enumerate(groups):
  values=[]
  for key in keys:
   days=summary['cases'][key]['days']
   if kind=='meal':value=sum(h['PurchaseMeals'][index+1] for d in days for h in d['history'])/len(days)
   else:
    product='ba:itemname_'+['croissant','hotdog','burger'][index]
    value=sum(p['Purchases'] for d in days for h in d['history'] for p in h['ProductSales'] if p['ProductId']==product)/len(days)
   values.append(value)
  ax.bar(range(4),values,bottom=bottom,color=colors[index],label=group,width=.62)
  bottom=[a+b for a,b in zip(bottom,values)]
 for x,total in enumerate(bottom):ax.text(x,total+2,f'{total:g}',ha='center',fontsize=11,weight='bold')
 ax.set_xticks(range(4),labels,fontsize=9);ax.set_title(title,loc='left',fontsize=13,pad=12)
 ax.spines[['top','right','left']].set_visible(False);ax.grid(axis='y',alpha=.18);ax.set_axisbelow(True)
 ax.legend(frameon=False,ncol=3,fontsize=9,loc='upper left');ax.set_ylim(0,max(bottom)*1.24)
axes[0].set_ylabel('Native purchases per game day (two-day average)')
fig.suptitle('Small shops: meal suitability, menu variety and price',x=.065,ha='left',fontsize=17,weight='bold')
fig.text(.065,.025,'957 residents · 15/hour per shop · 06–22 · same starting city · endowed stock, no restocking\n* Café cashier absent on the second day. These averages do not isolate menu/price effects.\nTwo-shop cases also change location and business type.',fontsize=9,color='#525a60')
fig.tight_layout(rect=[.01,.14,1,.94])
output=Path(sys.argv[2]) if len(sys.argv)>2 else root/'menu-comparison.png'
output.parent.mkdir(parents=True,exist_ok=True);fig.savefig(output,dpi=180,facecolor=fig.get_facecolor());print(output)
