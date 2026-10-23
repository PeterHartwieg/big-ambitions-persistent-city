# Stationary citizens step one: review, 20–21 October 2026

Six read-only review rounds of the first step of [the stationary-citizens proposal](../stationary-citizens.md): queues at opening, waiting indoors, and held ordinary walkers that walk on. Reviewers were GPT-6 Astra (`gpt-6-astra`), GPT-6.1 Sol (`gpt-6.1-sol`), Grok 4.7 (`grok-4.7`) and Claude Opus 5.5. Each worked on its own snapshot outside the repo, could build and test there, and did not edit the source. Findings were checked against the code and the 1.0 decompile before being acted on. No reviewer ran the game.

All four approve the final source of round six, with nits only. Three nits were taken after that approval (a give-up record when an arriving walker can't reach its spot, clearing the agent path when a queuer starts standing, and two doc examples); they were checked by the offline tests and the native compile, not re-reviewed.

## What the rounds changed

- **Round one.** The two-minute crossing cap was about two real seconds at normal speed and released walkers into traffic. Peter then asked for no cap on waiting for a safe crossing, and it was removed. Other findings: rain re-allocated occupied queue spots, places sharing one door built two lines, held walkers beyond the game's 100 m range were re-held at once and resumed walkers headed for the game's stationary zones, queuers popped onto spots, and the walk-in timeout ran on unscaled time.
- **Round two.** The arrival handoff never ran, because the journey lease ends before the simulator processes the arrival. Staff queued against the place's opening while their admission is the shift start. A failed walk to a spot froze the body in the doorway. The native door picker can return a closed building anywhere in the city. Late joiners took front spots.
- **Round three.** Partial navmesh paths and the native path-retry counter could freeze or pop resumed walkers. With no open door nearby, a walker stayed held while watched. A second arrival in one frame lost its body.
- **Round four.** Moving queuers up could leave two holders of one spot when a move failed (all four reviewers). A queuer that never reached its spot was left standing short of it. The walk-away fallback walked deeper into the view.
- **Round five.** A resumed walker could pick a door beyond the 100 m range and be frozen there. Walk-away retries tested the same six points for ever. A queuer who gave up was reselected two seconds later.
- **Round six.** Approved by all four.

## Limits that remain

- Nothing here has been checked in game. `CHECKS.md` lists what needs the game.
- A held walker beyond the game's 100 m range still stands until it is off screen, because the native movement code skips arrivals and crossings out there.
- With no reachable door, a walker can pace between walk-away points on some streets while watched.
- A queuer who was not already shown appears at the entrance when its window starts.
- Runtime queue behaviour (spot sampling, body handover, walking in) has no offline test; the Core planner and slot rules do.

Final DLL (scratch build, Mac Managed and PC 1.0 overlay, identical): `bebe1f5e9f622c0ed6188b1faed4400aa1eb4cda239d74d6045b2a2f25a0455d`. It also contains the frame-cost edits from the separate lag-spike session, which this panel did not review.
