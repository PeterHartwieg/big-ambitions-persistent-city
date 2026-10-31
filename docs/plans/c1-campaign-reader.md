# C1: native campaign read contract (31 October 2026)

C1 is an on-demand, read-only Runtime observer plus detached Core DTOs. It has no awareness/choice consumer, scheduler, save fields, UI patch, Harmony hook, agency order or payment. It starts on the B-E planning base, independently of A1/A2/A3 and B0/F0. `NativeFoodMarket.Supported` and persistence are unchanged.

Evidence below comes from read-only inspection with the locally installed ILSpy CLI, against the Steam `BigAmbitions_Data/Managed` assemblies. No game was launched. No assembly or decompiled source is included in Git. `BigAmbitions.dll` SHA-256: `9427561b81aaab2ada09c22a3c98dd9a4017a65225b389513885bf336852e911`. These are assembly findings, not observations of current fixture settings or live campaign outcomes.

## Verified storage and honest unknowns

| Lead / question | Assembly finding | Reader contract |
| --- | --- | --- |
| `marketingCampaigns[]` | Confirmed storage on `BuildingRegistration`, specifically `List<Entities.MarketingCampaign>`, not an array. | Copy only a caller-selected existing registration's list; never create registrations. Maximum 8 entries/business, otherwise refuse the business rather than truncate. |
| `marketingTypeName` | `Entities.MarketingCampaign.marketingTypeName` is `Entities.MarketingTypeName`, Int32 enum, six channel/size values plus `None`. | Verify enum identity, underlying type and the complete name/value contract. Copy raw numeric code when that contract matches; unknown values and `None` have no supported type name or group. |
| `enabled` / active | `MarketingCampaign.enabled` is a Boolean field. The agency UI creates/enables/removes campaigns. `MarketingHelper.RunDaily` can disable campaigns after an unsuccessful daily charge. | Copy enabled exactly, including false. Disabled entries are Unknown for usable campaign evidence. Independent active/lifecycle status is Unknown; enabled is not proof of reach, paid fees or current effectiveness. |
| Timing / targets | The campaign class exposes only `agencyAddress`, `marketingTypeName`, `enabled`. No start, end, duration, target resident, demographic or district fields. | `Active`, `StartMinute`, `EndMinute`, `DurationMinutes`, `NativeTarget` are explicitly nullable Unknown. Observation minute/revision describe this read, never a reconstructed campaign start. |
| Owning business | Campaign list is embedded in its owning registration; no separate campaign owner ID. | Caller supplies the existing generation-safe business ID. Diagnostic facade uses existing `FoodBusinessCatalogue.BusinessIdentity` from raw registration fields; no `Address`/`BuildingCached` getter or provider lookup. |
| Agency identity | `agencyAddress` is an `Address` whose data fields are string `streetName` and Int32 `streetNumber` (`BigAmbitions.Items.dll`). | Copy a bounded escaped street/positive number; missing/malformed fields, controls and invalid Unicode remain Unknown. This is an observed agency address, not proof of the agency's current existence, offered options, business generation or payment capability. No agency resolution scan. |
| `promotion.trafficIndex/marketing/total` | Confirmed Int32 fields on saved `Promotion`. `BusinessHelper.UpdatePromotion` writes them using building traffic, marketing efficiency and neighborhood marketing strength, with a capped total. | Copy only saved 0-100 values; changed types/out-of-range/missing fields are Unknown. Never call `UpdatePromotion` or `GetMarketingEfficiency`. The saved values can be stale/default; zero is an observed zero, not proof of no campaigns. Never multiply them into sales or infer reach/people from them. |
| `MarketingExpenses` | Existing account observer reads native income-statement expenses. Daily marketing code sums enabled types' settings before attempting its native debit. | Expenses prove expense evidence only. The reader never calls that sum/debit, customer/demand/provider helpers, setters or mutators. |

Malformed/null entries survive as Unknown. Duplicate numeric campaign types, including duplicates from different agency addresses, are all Refused; no first-wins rule. A list above the hard cap is Refused without enumerating its contents. A missing or changed list is Unknown, not an observed empty list. An actual empty native list with known promotion is an observed empty list. Unknown promotion makes the business status Unknown without erasing separately observed raw fields. Exceptions from incompatible reflection shapes become Unknown at the observation boundary; argument errors in the caller's scope/budget are explicit refusals to start.

## Reviewed native-type to resident-group table

`Core/CampaignSnapshot.cs` contains the fixed six-row table and Unknown fallback. Verified *type identity* does not establish *demographic targeting*.

| Native Int32 code | Verified name | Resident group | Evidence / decision |
| --- | --- | --- | --- |
| 0 | SmallInternet | Unknown | Native internet channel/small size, no resident target field. |
| 1 | MediumInternet | Unknown | Same channel, medium size; no target evidence. |
| 2 | LargeInternet | Unknown | Same channel, large size; no target evidence. |
| 3 | SmallBillboard | Unknown | Native billboard channel/small size; no route/resident target evidence. |
| 4 | MediumBillboard | Unknown | Same channel, medium size; no target evidence. |
| 5 | LargeBillboard | Unknown | Same channel, large size; no target evidence. |
| 6 / other / changed enum | None / unsupported / unverified | Unknown | Never a supported campaign or exposure. |

The assembly's `MarketingTypeSettings` distinguishes daily prices and square-meter reach, not commuters/health cohorts. Its static settings list contains six types, but the reader does not call its `Get` helper. Existing base Core facts include `Persona.Commuter`, `Persona.Sporty`, `DayPlaceId` and work/study routines; none establishes internet use, billboard exposure or a native health-targeted campaign. An assigned workplace is not native employment. No A3 traits are imported here. Consequently **no non-Unknown demographic mapping is verified** in C1. The table refuses to invent one. Any later explicit PC targeting policy must be reviewed with its trait/routine membership definition and fixture evidence before C2b uses it; all six current mappings remain Unknown until then. C1 discovery does not silently turn channel names into approved demographic rules.

## Bounded reads and publication

`NativeCampaignReader.Begin` accepts 1-1,024 already selected sources; it copies the bounded source vector and rejects duplicate IDs. It does not discover shops or scan the city catalogue. `Refresh.Step` reads 1-64 businesses, at most 8 campaigns each. Only a complete detached `CampaignSnapshot` is published. A changed logical minute or caller revision discards partial/completed publication and latches refusal. Starting another refresh is explicit. Source entries and the published lists are immutable; no native objects escape in the snapshot.

The generic cursor is main-thread only. Its caller must hold the logical observation boundary and native campaign writers stable across continuations, and change its revision on any invalidation. The cursor cannot discover an unreported same-minute native/UI mutation. **C1 does not provide that scheduler or lifecycle revision source.** The bridge facade avoids this issue: `PopulationRuntime.ReadCampaignSnapshot(registrationIndices)` reads explicit existing save indices and completes every unit synchronously in one main-thread call, so native/UI writers cannot interleave. Indices are diagnostic handles for the current save only, never persisted IDs. It returns a typed serializable dump; string/JSON formatting happens only when the caller asks. No periodic work is added.

C2b's future cadence is the next logical hour boundary, with a coherent publication and no retroactive exposure; it must supply boundary hold/invalidation, skip semantics and its own persistent evidence contract. Nothing schedules that cadence here. The maximum is the plan's 1,024 campaign business images, independently of the 20,000-place alternative catalogue. A new reader has a hard maximum of 32 cached reflection shapes; reaching it leaves later shapes Unknown rather than growing without limit. The host owns its reader/cache, with no process-wide native-object retention.

## C3 agency notes only

`Buildings.MarketingAgencySettings.marketingTypesAvailable` supplies a per-agency list of campaign enum options. The assembly does **not** prove that every current agency offers exactly two entries or which assets the disposable fixture has. `Dialogs.MarketingAgencyDialog` moves from its start entry to the `MarketingCampaignSettings` input template. That component builds a business dropdown and one toggle per available type, a selected-type list, a calculated percentage label and daily-price label. Confirmation in the native dialog adds/enables/removes campaigns and calls promotion/event/message code. C1 never constructs those dialogs or invokes any of their methods.

For C3, a possible attachment seam for Peter's third research option is the existing agency dialog/input-template selection before campaign confirmation, keeping both existing campaign options and adding research with tier selection. This is a discovery lead, not a proven extensibility/UI contract or an implementation. The daily native marketing routine uses `GameManager.ChangeMoneySafe` with transaction category/name, business address and previous day. That proves an existing native expense path exists, **not** that a research quote can use it atomically with durable request-specific payment evidence. C3 still needs to establish the safe fee path/range, agency identity, native/UI binding, insufficient funds and save/reload reconciliation. Research remains unavailable without that proof. Tier contents/per-tier fees remain open for C4/C5 after discovery. No payment adapter, UI patch, research stats or report is added by C1.

## Offline and native evidence

Unit doubles cover changed/missing fields, incompatible enum contracts, disabled/malformed/duplicate campaigns, absent/invalid promotion, immutable detached snapshots, scopes, cursor continuation/refusal and the conservative map. Source assertions exclude native helper/mutation/invocation paths; the standalone bridge harness also has throwing native-helper/money spies and byte-identical serialized fixture data before/after repeated reads.

`tools/campaign-reader-regression` measures new-reader cold and warmed reads at 1/8/1,024 businesses (six distinct native campaign types each), one-business continuation units, allocated bytes and retained detached images. These are foreground .NET offline observations, not Unity/Mono or combined mod-work measurements. Detailed results and the numbered 6 Nov checks are in the dated C1 entry of `CHECKS.md`; the same list is in the prepared PR description. Native lifecycle, agency options, group evidence and combined performance remain unverified until the authorized disposable fixture session.
