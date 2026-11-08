# C5f: delivery and report window cost (8 November 2026, offline)

Plan: [C5 section 4, row C5f](../plans/c5-agency-research.md#c5f--delivery-report-window-activation-and-cleanup) and section 6. Checks: [docs/checks/c5f-delivery-reports.md](../checks/c5f-delivery-reports.md).

## Where the cost is

- **Window closed:** `OnGUI` tests `researchWindow.Open` and skips the draw. No ledger read, no name lookup, no delegate, no string. This is the steady state.
- **Window open:** the view is rebuilt on opening, on a new selection and at most once a second. Other frames lay out the cached strings in one `GUILayout.Window`.
- **Billing boundary:** once per game day, only when an order is Surveyed. The C5d idle path is unchanged: the notice tracker runs after the `HasWork` check. Then there is one native `Contact.SendMessage` per moved order, at most 8 a day.
- **`Available`:** five boolean reads and one reference check, once per campaign screen build (C5e gate). Now that `Available` can be true, the C5e "gate, visible" row applies: p95 0.008 ms with a 2,000-registration scan ([C5e perf](c5e-agency-dialog.md)).
- **Console:** only on request.

## Method

The test is `ResearchDeliveryTests.WindowViewBuildCostAtTheLedgerBoundsAndZeroWorkClosed`, run with `--filter WindowViewBuildCost --logger "console;verbosity=detailed"`. Debug build, net10.0, Windows 11, one foreground dotnet process. Each row has 2,000 or 20,000 timed calls after 200 untimed warm-up calls. Timing is `Stopwatch`; allocation is `GC.GetAllocatedBytesForCurrentThread`.

The ledger is at its bounds: 8 open Surveyed orders, 16 delivered Detailed city reports with 62 cells each, and 16 closed orders, 40 rows in all. The detail pane shows a 62-cell report.

| Row | Calls | p50 ms | p95 ms | max ms | bytes/call |
| --- | ---: | ---: | ---: | ---: | ---: |
| Window closed, `ResearchWindowModel.Update` | 20,000 | 0.0000 | 0.0001 | 0.0001 | 0 |
| Window open, view rebuild (40 rows, 62-cell report) | 2,000 | 0.0250 | 0.0569 | 13.67 | 53,544 |
| Window open, cached frame | 20,000 | 0.0000 | 0.0001 | 0.0668 | 5 |
| `pcresearch report`, 62 cells | 2,000 | 0.0092 | 0.0161 | 0.5101 | 20,736 |
| Notice tracker, 8 Surveyed orders (`Before` + `After`) | 20,000 | 0.0001 | 0.0002 | 0.0007 | 0 |

The live closed path is cheaper than the first row, because `OnGUI` does not call `Update` while the window is closed.

The rebuild max of 13.67 ms is one sample in 2,000. The loop allocates about 107 MB back to back, so this is a garbage collection inside the timed loop. In game the rebuild runs at most once a second. It is under the 30 ms flag line and does not move p95.

## Cheap wins taken

- The closed path tests one boolean before evaluating the ledger property or making the business-name delegate. The delegate is cached once. Before: the first draft built a method-group delegate in `OnGUI` on every call, 64 B per call in the closed row. After: 0 B.
- The view is cached and rebuilt at most once a second while open, instead of every `OnGUI` event. That is several events a frame, and the C5f open row would otherwise repeat for each one.

## Not measured offline

- Native IMGUI layout and text drawing of the open window (`GUILayout.Window`, 40 buttons, one wrapped label).
- Native `Contact.SendMessage`, which enqueues a message and updates the phone badge.
- The registration and contact walks in `ResearchMessages.Send`: O(registrations + contacts) per moved order, at most 8 a day.

The batched rig measures these in check 10. The gate is mod-work p95 under 5 ms, with every spike of 30 ms or more flagged and whole-frame timing reported separately. The four parked hotspots stay parked; this slice does not touch them.
