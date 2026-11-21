# Saved state contents and bounds, 21 November 2026

Recorded on 21 November 2026 from code comments that the comment cleanup removed (Peter, 20 November 2026). The other decisions in force are in the [register](2026-11-10-city-direction.md).

## Decision

- `NativeEmployment` has one value, `None` ([PersonIdentity](../../Core/PersonIdentity.cs)). Any other value is refused until a reviewed contract defines it. No population code calls native hiring or staff APIs.
- Visitor sequence gaps ([PersonWire27](../../Persistence/PersonWire27.cs)): a sequence number below `NextVisitorSequence` without a visitor is one the fill skipped for a commuter, so its frozen derived identity must be a student or retiree (owner decision, 8 November 2026). A missing number with a working identity is a lost person and refuses the payload. A lost student or retiree visitor cannot be told from a skipped number, so the check is weaker than count equality for those.
- Travel visibility ([InteriorViewState](../../Runtime/InteriorViewState.cs)) is runtime presentation only and is absent from `CityState` and every save DTO.
- Research orders ([ResearchLedger](../../Core/ResearchLedger.cs)): nothing retries, refunds or resets a terminal order. Every order has its own ID, so an unknown payment blocks only that order. The research dialog, the survey and the daily bill all run on the game thread; the ledger is single-threaded.
