# Population and transport expansion — 19 October 2026

This extends the separate, RLO-dependent background prototype. It does not replace visible pedestrians or traffic and does not implement an economy. Validation below separates offline invariants, actual native-game observations and policy choices. The four reviewers approve the final source. Native checks and isolated-rig restoration are complete within the limits recorded below.

## Implemented behavior

- Destination selection considers all eligible venues with capacity, category attraction, native building traffic and a smooth distance penalty. Leisure choice varies deterministically by person and day. Committed journeys, opening waits and visits keep their selected venue.
- Workers can change their simulated workplace, only when safely home. Rare departures replace one resident with a newly allocated `pc:<sequence>` identity. Source names and attributes are remixed from the immutable RLO catalogue; age, sex and appearance remain together and persona selection respects RLO's age bands. RLO's 400 social profiles are untouched.
- Native subway entrances and bridge geometry support background transit. Directed native road waypoints support car journeys with complete walking access, curb barriers, private-gate exclusions and saved car locations. Returning home weighs the inconvenience of leaving a car elsewhere. No buses, physical cars, congestion or native parking occupancy are modeled.
- Schema 2 retains the storage key `persistentcity:state_v1`. Strict schema 1 migration preserves existing identities, assignments and interrupted journeys. Future, corrupt and incompatible state remains refused and untouched. New catalogue entries are added only during fresh initialization or the explicit v1 migration; existing schema 2 snapshots remain frozen.

Policy rates, timing, capacity and remaining limits are documented in the README. They are simulation choices, not claims about native population behavior.

## Review and adjudication

The requested panel is Astra, GPT 6.1 Sol, Grok 4.7 and Opus 5.5. Reviews are read-only and are assessed against code and evidence.

Confirmed findings fixed before the final native run include age/persona compatibility, committed leisure destinations after migration and closing waits, invariant job-date hashing, traffic counted twice, inactive transit stations, home-car allocation being skipped by mode pruning, car retrieval incentives, absent road data blocking other modes, and stale road-route caches hiding a newly reopened shorter path. Road searches are fresh; native graph copying is staged before event advancement. A subsequent small fix stops further events in a frame when the graph becomes invalid.

The suggestion that distance weighting necessarily produced mostly far-away trips assumed uniformly distributed venues. Actual-map measurement was used instead of adopting that assumption. The capture used 400 RLO identities and the real catalogue, with repeated daily leisure choices. See the final measurement below. Assignment still does not precompute every candidate's route, so unreachability is a documented limitation.

The initial source capture overlapped the beginning of implementation and already contained some schema 2 model fields. It is explicitly not a schema 1 fixture. Migration evidence uses the genuine earlier native-game payload and strict legacy fixtures.

All four reviewers approve the final source, including the access/routing follow-up: GPT 6 Astra, GPT 6.1 Sol, Grok 4.7 (CLI resolved `grok-4.7-build`) and Claude Opus 5.5. Grok’s initial pass exhausted its turn limit without a verdict; its next pass found the gate fallback scope bug, and its final pass verified the fix and approved. No approval is claimed for rendering or sustained 60× throughput. The external reviewers did not independently repeat native playthroughs.

## Offline evidence

155 tests passed after the final native-access adjustment, covering stable selection, compatible resident remixing, turnover, capacity, lifecycle timing, interrupted car journeys, route continuity, mode pruning, native-road graph semantics and strict persistence/migration. The native Mac package compiles with warnings treated as errors and excludes game and RLO assemblies from its deliverable.

An earlier synthetic one-year run retained 400 residents through 118 departures and recorded 1,029 job changes among active residents. It used an artificial graph and preceded the final donor/hash fixes; it is supplementary stress evidence, not a native performance benchmark or final-build claim.

## Native Mac evidence

The test uses the owned `agentmac` reservation, isolated app/profile, headless build 3682 and released RLO 0.40.4. Windows and the normal game are untouched. Only new FT checkpoints are saved. The diagnostic helper reads validated state on the game thread and is excluded from the deliverable.

- Genuine schema 1 state loaded with 400 citizens and migrated in memory. The native payload remained schema 1 until explicit save. Original assignments were retained while newly verified venues became available.
- Car, subway and walking legs occurred in actual native routes. The first saved expansion checkpoint retained 97 allocated cars. Reload advanced the native clock by approximately 0.333332 game minutes; an independent core replay produced exact canonical full-state equality, including three arrivals and four continuing journeys, with zero new route requests.
- A fresh catalogue on build `47e16ff7…` contained 1,008 places: 335 homes, 384 work roles, 247 shops, 41 leisure venues and one study venue. Three parks and one outdoor party pier passed access checks. At that point neither beach passed; the later dry-NavMesh/shoreline check below resolved Big Beach access. The pier means outdoor walking, without casino admission or boat boarding.
- The 11-day run at the bridge's 60× stress speed accumulated a substantial backlog. This is a known prototype performance limit, not a real-time or frame-rate pass. Maximum sampled lag was 4,640.39 game minutes. Catch-up finished 102.064 seconds after the first paused sample. The final lower-speed measurement is reported below. Individual non-preemptible native route events can exceed the soft 2 ms advancement budget.

The 11-day run finished with 400 residents, three departures and replacements, 33 job changes among current residents, 13,141 completed journeys and 2,252 missed activities. Recent histories included 109 outdoor-pier admissions and 206 park admissions. Thirty-five students remained unassigned under the provisional 16-seat school policy. Source identities remained equal to the original immutable roster.

The day-12 checkpoint reloaded under final candidate `675f5b1d…` with all three generated identities and all 118 car identities/locations retained. Independent replay again matched the full canonical state exactly after the native clock advanced by 0.333333 minutes; no new route queries were needed.

The fresh actual-map weighting sample gave work median 417.76 m (68.89% within 600 m; 8.57% beyond 2 km), shopping median 389.70 m (70.75%; 9.00%), and 12,000 person/day leisure choices median 408.15 m (69.58%; 7.94%). These are selected destination distances, not proof that each destination was reachable or admitted.

On the final dry-walking build, a separate fresh city admitted five outdoor destinations: three parks, the outdoor party pier and Hamptons Big Beach. Big Beach’s actual production point matched a native collider projection with dry NPC area mask `-9` (swimming bit `8` removed). Four complete public paths to that point measured 48.30–77.05 m and passed gate checks. Small Beach remains omitted: the bounded 178-candidate diagnostic found no NPC sample; player-only queries produced 84 partial paths and no complete route. This is evidence of the tested access limits, not proof that no other possible point exists.

The final fresh-city distance sample (including Big Beach) gave work median 376.46 m (69.48% within 600 m; 9.09% beyond 2 km), shopping median 382.40 m (71.00%; 7.00%) and 12,000 leisure choices median 392.48 m (70.98%; 8.44%). Different fresh games have different native businesses and RLO rosters, so these two samples are not a controlled before/after experiment.

The final build’s full day at bridge speed 6 completed with 400 residents, 1,221 completed journeys, 241 misses and 103 allocated cars. Walking, subway and car routes all appeared. Outdoor admissions were 16 across the parks, nine at the pier and two at Big Beach. Five-second samples found at most 2.200036 game minutes of lag, with none remaining at the first paused observation. The complete observation lasted 248.349 seconds; this includes polling overhead. Logged advancement calls peaked at 54.660 ms, so the soft 2 ms budget is not a hard frame-time guarantee. This is a bounded headless throughput check, not an FPS or Windows performance claim.

The final 6× checkpoint also reloaded midjourney with 103 allocated cars and two continuing trips containing 394 car, six subway and 31 walking legs. Independent replay matched the full canonical state exactly after the native clock advanced by 0.333333 minutes, with zero new route requests. This tests continuity of saved geometry; it does not demonstrate deterministic recalculation of native paths after scene changes.

Disabling the final build allowed native load, clock advancement and saving a new FT checkpoint while preserving the schema 2 payload byte-for-byte. DLL removal compatibility was tested in the historical walking milestone; this expansion repeats the disabled-mod check, not the removal playthrough.

## Deliverable and isolation

Final DLL SHA-256: `675f5b1d0a986d60bac062265bbb65c89df6a25426398a49aff770f4aa7d3d33`, assembly version `0.2.0.0`. The private prototype package is disabled by default and contains only its DLL, configuration and README. No game DLL, RLO binary, diagnostic helper, raw save or credentials are packaged. The source and package inventory is in `2026-10-19-expansion-manifest.json` beside this report.

The original Mac installation manifest was restored exactly and its original package hashes verified. Added RLO, Persistent City and probe folders were moved back into ignored local evidence storage. The rig is stopped and its reservation released. Windows and the normal Mac game were not used.

All preexisting named FT checkpoints retained their hashes. One of the 11 preexisting files changed: the game's automatically generated `Recover Midnight.hsg` for the earlier isolated FT test character, during the first overnight trial. That recovery file was not overwritten again to hide the change. New named FT validation checkpoints remain in the isolated profile. No normal-game save was modified.

Detailed local evidence remains ignored under `build/expansion-validation/`: final native summary, timed samples, strict state snapshots, three canonical replay results, disabled-payload comparison, reviewer verdicts and cleanup receipt. These contain test-game data and are not committed or shipped.

## Remaining milestone boundaries

Visible actors, ambient-spawner replacement, a citizen inspector, live venue refresh, reachability-aware assignment, physical transport and social/employee identity arbitration remain separate work. Headless Mac evidence does not establish Windows/rendering compatibility or whether the visible final experience is fun. Native subway time and outdoor attraction/opening policies are explicit approximations. Economy integration remains deferred.
