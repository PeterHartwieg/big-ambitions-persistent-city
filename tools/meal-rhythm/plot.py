#!/usr/bin/env python3
"""Native transaction summary; no synthetic customer counts."""
from pathlib import Path
import json, sys
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt

root = Path(sys.argv[1]); summary = json.loads((root / 'summary.json').read_text())
assert summary['allCasesComplete']
labels = ['all-day', 'breakfast-only', 'evening-only']
names = ['06:00–22:00', '06:00–10:00', '17:00–22:00']
colors = ['#e5ac40', '#3787b3', '#8c6fba']
fig, axes = plt.subplots(1, 2, figsize=(12, 4.6), gridspec_kw={'width_ratios': [1, 1.5]})
fig.patch.set_facecolor('#f6f7f9')
bottom = [0., 0., 0.]
for kind, color in enumerate(colors, start=1):
    values = [sum(d['byMeal'][kind] for d in summary['cases'][label]) / 2 for label in labels]
    axes[0].bar(names, values, bottom=bottom, color=color, label=['Breakfast', 'Lunch', 'Dinner'][kind - 1], width=.6)
    for i, v in enumerate(values):
        if v: axes[0].text(i, bottom[i] + v / 2, f'{v:g}', ha='center', va='center', color='white', fontweight='bold')
    bottom = [a + b for a, b in zip(bottom, values)]
axes[0].set_title('Purchases by meal and opening hours', loc='left', fontsize=12)
axes[0].set_ylabel('Average purchases per game day')
axes[0].legend(frameon=False, loc='upper right')
for label, name, color in zip(labels, names, ['#243a52', '#e5ac40', '#8c6fba']):
    hourly = [sum(d['hours'][h] for d in summary['cases'][label]) / 2 for h in range(24)]
    axes[1].step(range(6, 24), hourly[6:], where='post', label=name, color=color, linewidth=2)
axes[1].set_title('Actual purchases across the day', loc='left', fontsize=12)
axes[1].set_ylabel('Average purchases per game hour'); axes[1].set_xticks(range(6, 24, 2))
axes[1].set_xlabel('Game clock'); axes[1].legend(title='Opening hours', frameon=False)
for ax in axes:
    ax.set_axisbelow(True); ax.grid(axis='y', alpha=.2); ax.spines[['top', 'right']].set_visible(False)
fig.suptitle('Three meal routines · one small croissant shop', x=.06, ha='left', fontsize=16, fontweight='bold')
fig.text(.06, .025, '957 residents · 15 customers/hour capacity · $10.25 · two native Mac game days per case · no restocking', fontsize=10, color='#4c5967')
fig.tight_layout(rect=(.02, .07, 1, .9))
fig.savefig(root / 'meal-demand.png', dpi=160, facecolor=fig.get_facecolor())
fig.savefig(root / 'meal-demand.svg', facecolor=fig.get_facecolor())
print(root / 'meal-demand.png')
