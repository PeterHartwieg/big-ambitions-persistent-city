# pc-docs-restructure: performance

No win taken. This slice changes only documentation and adds the offline link checker `tools/docs-links/check.py`. No mod code runs differently, so there is no mod-work or whole-frame effect.

Size of the shared docs in bytes (`wc -c`):

| File | Before (`e9995e5`) | After |
| --- | --- | --- |
| AGENTS.md | 3,930 | 4,527 |
| README.md | 43,579 | 4,948 |
| docs/roadmap.md | 26,011 | 10,031 |
| CHECKS.md | 16,180 | 5,826 |

Required reading drops from AGENTS, README, roadmap, design, steps B–E and three full plans to AGENTS.md, the plan row and the sections the task lists.
