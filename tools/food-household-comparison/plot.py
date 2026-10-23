from pathlib import Path
import json,sys
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
from matplotlib.ticker import MaxNLocator,StrMethodFormatter
r=Path(sys.argv[1]);d=json.loads((r/'report.json').read_text())
plt.rcParams.update({'font.family':'DejaVu Sans','font.size':11,'axes.spines.top':False,'axes.spines.right':False})
fig,ax=plt.subplots(1,2,figsize=(14,6),gridspec_kw={'wspace':.26});fig.patch.set_facecolor('#fafaf7')
for a in ax:a.set_facecolor('#fafaf7');a.grid(axis='y',alpha=.2)
for s,color,style in zip(d['scenarios'],['#969696','#b77726','#19754b'],['--','-', '-']):
 ax[0].plot([p['priceCents']/100 for p in s['points']],[p['meals'] for p in s['points']],label=s['name'],color=color,ls=style,lw=2,marker='o',markersize=3)
ax[0].set(xlabel='Salad price',ylabel='Hypothetical meals / day',title="Bergman's: same residents, routes and shops",ylim=(0,None));ax[0].xaxis.set_major_formatter(StrMethodFormatter('${x:.0f}'));ax[0].yaxis.set_major_locator(MaxNLocator(integer=True));ax[0].legend(frameon=False,fontsize=9,loc='upper right')
for s,color in zip(d['districtSensitivity'],['#3f6fa0','#b06f51']):
 ax[1].hist([v/100 for v in s['allowances']],bins=list(range(8,57,2)),histtype='step',density=True,color=color,lw=2,label=s['name'])
ax[1].set(xlabel='Temporary personal meal allowance',ylabel='Share of residents per dollar',title='Controlled home-district examples');ax[1].xaxis.set_major_formatter(StrMethodFormatter('${x:.0f}'));ax[1].legend(frameon=False,fontsize=10)
fig.suptitle('Different residents, different buying decisions',x=.07,y=.97,ha='left',fontsize=19,fontweight='bold')
fig.text(.07,.87,'Individual spending limits · personality and daily prepared-food choices · no guaranteed shop demand',fontsize=11)
fig.subplots_adjust(left=.07,right=.98,top=.76,bottom=.24)
fig.text(.07,.135,'Left: offline replay of the earlier PC capture. Home-district data was absent, so individual budgets use the neutral prior.',fontsize=10)
fig.text(.07,.09,'Right: controlled 80/15/5 vs 15/45/40 working/middle/upper mixes; overlapping personal budgets, not observed income.',fontsize=10)
fig.text(.07,.045,'Same 15/hour throughput and ample stock. Declining a purchase does not claim a meal was eaten. No native sales changed.',fontsize=10,color='#555555')
for ext in ['png','svg','pdf']:fig.savefig(r/('household-comparison.'+ext),dpi=160,facecolor=fig.get_facecolor())
