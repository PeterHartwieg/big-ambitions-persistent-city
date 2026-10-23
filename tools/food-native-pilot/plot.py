"""Plot the measured multi-day shadow comparison; never label it native sales."""
import argparse, json, math
from pathlib import Path
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
from matplotlib.ticker import PercentFormatter, MaxNLocator

p = argparse.ArgumentParser(__doc__)
p.add_argument('run', type=Path)
root = p.parse_args().run
r = json.loads((root / 'offline-comparison.json').read_text())
points = r['points']
days = math.floor(r['EndsAt'] / 1440) - math.ceil(r['StartedAt'] / 1440)
shop = next(s for s in r['selection']['shops'] if s['Id'] == r['targetId'])
single = len(shop['Offers']) == 1
x = [q['targetPrices'][0]['PriceCents'] / 100 if single else q['factor'] * 100 for q in points]
meals = [q['meals'] for q in points]
base = next(q for q in points if q['factor'] == 1)
plt.rcParams.update({'font.family':'DejaVu Sans', 'font.size':11,
                     'axes.spines.top':False, 'axes.spines.right':False})
fig, axes = plt.subplots(1, 3, figsize=(16, 6))
fig.patch.set_facecolor('#fafaf7')
for ax in axes:
    ax.set_facecolor('#fafaf7')
    ax.grid(axis='y', alpha=.2)
    ax.set_xlabel('Price ($)' if single else 'Menu price (% of observed menu)')
axes[0].plot(x, meals, 'o-', color='#19754b', markersize=4)
axes[0].set_title('Mod: hypothetical purchases', fontweight='bold')
axes[0].set_ylabel(f'Meals over {days} recorded days')
axes[0].set_ylim(bottom=0)
axes[0].yaxis.set_major_locator(MaxNLocator(integer=True))
axes[1].plot(x, [q['revenueCents'] / 100 for q in points], 'o-', color='#b96d1e', markersize=4)
axes[1].set_title('Mod: hypothetical revenue', fontweight='bold')
axes[1].set_ylabel(f'Gross revenue over {days} days ($)')
axes[1].set_ylim(bottom=0)
shares = r['native']['classShares']
weighted = []
for curve in r['native']['curves']:
    weights = [next((float(v) for k, v in shares.items() if k in c['socialClass'].lower()), 0) for c in curve['classes']]
    total = sum(weights)
    if not total:
        continue
    accepted = [100 * sum(w * int(c['points'][j]['accepts']) for w, c in zip(weights, curve['classes'])) / total for j in range(len(points))]
    axes[2].step(x, accepted, where='post', label=curve['product'].replace('ba:itemname_', '').title())
    weighted.append({'product':curve['product'], 'acceptancePercent':accepted})
axes[2].set_title('Game: native price acceptance', fontweight='bold')
axes[2].set_ylabel('Shop-district class-weighted acceptance')
axes[2].yaxis.set_major_formatter(PercentFormatter())
axes[2].set_ylim(-3, 105)
axes[2].legend(frameon=False)
fig.suptitle(r['selection']['name'] + ' · three-day PC capture, offline replay', x=.06, y=.97, ha='left', fontsize=19, fontweight='bold')
fig.text(.06, .88, f"{r['residents']} residents · {days} full recorded days · eight selected shops · varied personal allowances and outside choices")
fig.subplots_adjust(left=.07, right=.98, top=.78, bottom=.25, wspace=.32)
fig.text(.06, .14, 'Observed: routines, home-district mixes, native walking routes, hours, prices, service and district demand.', fontsize=10)
fig.text(.06, .095, 'Controlled shadow supply: 15 meals/hour and ample stock. These curves are separate from the native purchase receipts.', fontsize=10)
fig.text(.06, .05, 'Native acceptance is willingness to accept a price, not sales volume. Gross revenue excludes costs.', fontsize=10, color='#555555')
for ext in ['png', 'pdf', 'svg']:
    fig.savefig(root / ('demand-curve.' + ext), dpi=150, facecolor=fig.get_facecolor())
summary = {'days':days, 'target':r['selection']['name'], 'baseline':base,
           'demandNonIncreasing':all(b <= a for a, b in zip(meals, meals[1:])),
           'nativeWeightedReference':weighted}
(root / 'plot-summary.json').write_text(json.dumps(summary, indent=2))
print(json.dumps({k:v for k,v in summary.items() if k != 'nativeWeightedReference'}, indent=2))
