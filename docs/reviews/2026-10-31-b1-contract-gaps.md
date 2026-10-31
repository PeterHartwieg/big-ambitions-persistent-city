# B1 stopped at the schema-18 contract boundary — 31 October 2026

Task `pc-b1-memory-loyalty`, branch `codex/pc-b1-memory-loyalty`, base
`192037b` (B0 / PR #9). **B1 behavior is not implemented.** This is an offline
prerequisite report, using the task's explicit stop condition in scope item 1:

> if something B1 needs is genuinely missing, add it only as B0's contract allows or stop and explain in the PR

Read the B–E plan from `origin/codex/persistent-city:docs/plans/steps-b-e.md`
(planning commit `6d43276`) without merging it. That plan is absent from this
checkout. No schema, DTO, runtime, settings, economic, route or native authority
contract changes accompany this report.

## Missing replay-critical state

| B1 requirement | Published contract at `192037b` | Consequence |
| --- | --- | --- |
| Capture memory inputs and effective coefficients/revision before drawing weights; held contexts keep those inputs after config reload | `Core/FoodToday.cs`'s `FoodDecisionState` has policy version, logical context, shortlist, quote progress and result, but no coefficient/revision image. `FoodDecisionShop` has only factual business/route inputs. `ContinueFoodDecision` in `Core/FoodWeightedCommerce.cs` selects with the currently attached `ChoiceSettings`. | There is no place to persist B1's bounded per-shop memory contribution or the held policy/config image. Adding a public property makes it a required schema-18 field; old 18 payloads lack it. An in-memory cache would disappear on reload. |
| New capability activation on migration starts on the next logical day, survives reload, and does not postpone ordinary same-day config edits | `PopulationCapabilities` in `Core/ShopMemory.cs` contains exactly `Code`, `Revision`, `Active`. `FoodCommerceState.ChoiceStartsDay` is already A3's activation boundary. | B has no independent saved scheduled activation day. Resetting A3's boundary would change an inherited contract; recomputing tomorrow on each reload could postpone B indefinitely. |

The replay counterexample uses an **existing A3 coefficient** because B's
coefficients do not yet exist. It demonstrates that inherited held contexts do
not already provide the capture mechanism B1 needs; it is not a claim that memory
behavior was run or failed. B1 must supply that mechanism before its behavior can
meet the task, including evaluating checkpoints under edited config.

The [B0 wire contract](../population-schema18.md) explicitly states exact
consumer-extension lengths **8/18/28/38/48**, ten characters per memory, seven
per watermark and no arbitrary extension slots. The **64-character allocation is
a size budget**, not an accepted extension grammar. `MemoryWire.CheckWire` rejects
other lengths; `PopulationWire18` requires exact meal tuple arity/coverage;
`SaveCodec.CheckShape` rejects unknown object fields and missing required fields.
The early authority probe also requires exactly the three capability fields.
Changing readers/writers under 18 would silently change the published shape.

Reusing `Reason`, context hashes, affinity/cause codes, watermark stages or
business IDs to hide config/activation state would change their documented
meaning. In particular, putting tuning into the logical draw key would violate
the tuning-independent draw contract. No such encoding is proposed here.

## Executable evidence

After the required other-dotnet wait, run in the foreground:

```powershell
& "$env:LOCALAPPDATA\Microsoft\dotnet\dotnet.exe" run --project tools/b1-contract-probe/B1ContractProbe.csproj --configuration Release
```

The probe creates only a synthetic Core city and uses the production codec and
save session. It saves an **evaluating** context before any route request, then
resumes that exact checkpoint twice with unchanged config and once with edited
config. It stops at committed travel, before native checkout. It reports the
first counterexample among seeds 0–127 and requires:

- Seed **2**, capture taste weight **0**, unchanged resume **burger**, edited
  taste weight **3** resume **croissant**; unchanged-config repeats match.
- Exactly **2 route calls per resume**, **0 checkout calls**.
- Adding `MemoryInputs` to the held decision is **Invalid: Unknown field**.
- Adding `StartsDay` to capabilities is **Invalid: Unknown field**.
- Both refusals leave the source dictionary's string reference intact.

The probe exits successfully only when this documented limitation reproduces;
that exit code means **evidence reproduced**, not B1 acceptance. Its assertions
must be replaced or retired when the prerequisite is resolved. It does not add
unit tests asserting that the limitation is desired behavior.

## Contract work needed before continuing

The schema owner needs to publish a contract that can retain a bounded held
decision's effective policy/config inputs and memory contribution, and B's
independent migration activation boundary. Specify defaults for previously
published saves, strict validation/refusal, immutable older readers, capability
bootstrap support, counting and size allocation. Then repeat the changed-config
evaluating/selected replay proof and the combined save-size budget before B1
capture/choice activation. This report allocates no replacement schema number
and does not amend schema 18.

The four-entry memory, business-reference and watermark allocation itself is
present. This report does not request a larger diary, more route slots, B2 UI,
regular settings, campaigns, rivals, products or weather. Peter's Q7 decision
remains the B2 projection rule; no inspector words are added.

## Performance and evidence boundary

No new hot-path work exists. B1 lookup/update cost, saturated reference/5,000
consumer cost, rush throughput/lag, retained memory/allocations and save growth
are **not measured**, because a functioning B1 implementation is absent. No
before/after performance win is claimed and no row is added to the roadmap's
Perf wins log. The four parked hotspots remain untouched.

The full requested offline gate is recorded in CHECKS and its raw command
manifest. Passing the inherited baseline does not certify memory behavior or the
native **mod-work p95 <5 ms / single spikes <30 ms** gate. Whole-frame timing is
separate and unmeasured. The pending B1 6 Nov checklist appears in CHECKS and the
PR-description draft; it cannot be marked passed against this behavior-free head.
