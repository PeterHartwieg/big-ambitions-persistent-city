using System;
using System.Collections.Generic;
using System.Linq;

namespace PersistentCity.Core
{
    // F8f (docs/plans/f8-capability-boundaries.md D5): each materially different way money and stock move is a mechanism
    // with its own native proof. Rules: M1 a business is served under exactly one mechanism or reports one unsupported
    // reason; M2 a proof state uses the status words and links its evidence, and "verified by rig" needs a rig result
    // link; M3 product metadata (E5c/E5e) never grants a mechanism. Labels only: Supported and checkout are unchanged.

    public enum TransactionMechanismId : byte { ShopFullServiceBag, VehicleStreetFoodApi, ShopDynamicCatalogue, ResearchAgencyBill }

    /// <summary>The status words (AGENTS.md).</summary>
    public enum MechanismProof : byte { Planned, Implemented, Partial, Stopped, VerifiedByRig }

    public sealed class TransactionMechanism
    {
        private TransactionMechanism(TransactionMechanismId id, string name, string evidence, MechanismProof proof, string evidenceLink, string? rigResultLink)
        { Id = id; Name = name; NativeEvidence = evidence; Proof = proof; EvidenceLink = evidenceLink; RigResultLink = rigResultLink; }
        public TransactionMechanismId Id { get; }
        public string Name { get; }
        /// <summary>The native evidence the mechanism needs (D5 table).</summary>
        public string NativeEvidence { get; }
        public MechanismProof Proof { get; }
        /// <summary>The offline evidence kept in the tree (repository-relative test file); the older checks files are in the
        /// docs-history-2026-11-10 tag.</summary>
        public string EvidenceLink { get; }
        /// <summary>The rig result; required for <see cref="MechanismProof.VerifiedByRig"/>, null otherwise.</summary>
        public string? RigResultLink { get; }

        /// <summary>M2: refuses a proof state without its evidence link, and "verified by rig" without a rig result link.</summary>
        public static TransactionMechanism Define(TransactionMechanismId id, string name, string evidence, MechanismProof proof, string evidenceLink, string? rigResultLink = null)
        {
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(evidence)) throw new ArgumentException("A mechanism needs a name and its native evidence.");
            if (string.IsNullOrWhiteSpace(evidenceLink)) throw new ArgumentException(name + ": a proof state needs its evidence link (M2).", nameof(evidenceLink));
            if (proof == MechanismProof.VerifiedByRig && string.IsNullOrWhiteSpace(rigResultLink))
                throw new ArgumentException(name + ": \"verified by rig\" needs a rig result link (M2).", nameof(rigResultLink));
            if (proof != MechanismProof.VerifiedByRig && rigResultLink != null)
                throw new ArgumentException(name + ": a rig result link is only for \"verified by rig\" (M2).", nameof(rigResultLink));
            return new TransactionMechanism(id, name, evidence, proof, evidenceLink, rigResultLink);
        }

        public static string ProofWord(MechanismProof proof) => proof switch
        {
            MechanismProof.Planned => "planned", MechanismProof.Implemented => "implemented", MechanismProof.Partial => "partial",
            MechanismProof.Stopped => "stopped", _ => "verified by rig",
        };

        /// <summary>"implemented; rig pending (docs/checks/x.md)" or "verified by rig (result link)".</summary>
        public string ProofText => Proof == MechanismProof.VerifiedByRig ? "verified by rig (" + RigResultLink + ")"
            : ProofWord(Proof) + "; rig pending (" + EvidenceLink + ")";
    }

    /// <summary>One business's verdict: served under one mechanism, or one unsupported reason. A business refused by a
    /// mechanism that is off (shop.dynamic-catalogue) names that mechanism with its reason.</summary>
    public readonly struct MechanismVerdict
    {
        internal MechanismVerdict(TransactionMechanism? mechanism, bool served, string reason)
        { Mechanism = mechanism; Served = served; Reason = reason; }
        public TransactionMechanism? Mechanism { get; }
        public bool Served { get; }
        /// <summary>Empty when served; otherwise the one unsupported reason.</summary>
        public string Reason { get; }
        public string Text => Served ? Mechanism!.Name + " (" + Mechanism.ProofText + ")"
            : Mechanism != null ? Mechanism.Name + " off: " + Reason : "unsupported: " + Reason;
    }

    public static class TransactionMechanisms
    {
        /// <summary>NativeFoodMarket.DynamicIntegrationRefusal (E5b5); a source test pins the two equal.</summary>
        public const string DynamicIntegrationRefusal = "E5b dynamic native acquisition off; original three only";

        public static readonly IReadOnlyList<TransactionMechanism> All = Array.AsReadOnly(new[]
        {
            TransactionMechanism.Define(TransactionMechanismId.ShopFullServiceBag, "shop.fullservice-bag.v1",
                "FullServiceBusinessSimulator, no entrance fee, paper-bag tag, historical products, one native order per line: direct stock and bag decrement, one completed order, receipt before effect, native daily settlement",
                MechanismProof.Implemented, "tests/A5IntegratedAcceptanceTests.cs"),
            TransactionMechanism.Define(TransactionMechanismId.VehicleStreetFoodApi, "vehicle.streetfood-api.v1",
                "Street Food Sell through VendorApi contract 1: units before and after, cents, sequence; lease and hand-back",
                MechanismProof.Implemented, "tests/VehicleMarketTests.cs"),
            TransactionMechanism.Define(TransactionMechanismId.ShopDynamicCatalogue, "shop.dynamic-catalogue",
                "Loaded-catalogue products (equipment, recipes, lifecycle): mutation notifications, holder reconciliation, held execution; on only while every E5b contract capability passed",
                MechanismProof.Implemented, "tests/DynamicAcquisitionTests.cs"),
            TransactionMechanism.Define(TransactionMechanismId.ResearchAgencyBill, "research.agency-bill.v1",
                "ChangeMoneySafe after MarketingHelper.RunDaily: one transaction per order, before the financial summary",
                MechanismProof.Implemented, "tests/ResearchChargeTests.cs"),
        });

        public static TransactionMechanism Get(TransactionMechanismId id) => All[(int)id];

        /// <summary>M1 for one scoped food business. <paramref name="scopeStatus"/> is the market's existing status for the
        /// place (NativeFoodMarket.ScopeStatus, empty when served; for a vehicle, empty when the vendor market can serve it,
        /// else its row reason). Products decide only which shop mechanism applies: historical names are the full-service
        /// bag mechanism, any other name the dynamic catalogue. No metadata input exists (M3).</summary>
        public static MechanismVerdict ForBusiness(FoodScopeChannel channel, IReadOnlyCollection<string> products, string scopeStatus)
        {
            if (products == null || products.Count == 0) return new MechanismVerdict(null, false, "no products in scope");
            scopeStatus ??= "";
            if (channel == FoodScopeChannel.Vehicle)
                return scopeStatus.Length == 0 ? new MechanismVerdict(Get(TransactionMechanismId.VehicleStreetFoodApi), true, "")
                    : new MechanismVerdict(null, false, scopeStatus);
            if (scopeStatus.StartsWith(DynamicIntegrationRefusal, StringComparison.Ordinal))
                return new MechanismVerdict(Get(TransactionMechanismId.ShopDynamicCatalogue), false, scopeStatus);
            if (scopeStatus.Length > 0) return new MechanismVerdict(null, false, scopeStatus);
            bool historical = products.All(FoodProducts.Known);
            return new MechanismVerdict(Get(historical ? TransactionMechanismId.ShopFullServiceBag : TransactionMechanismId.ShopDynamicCatalogue), true, "");
        }

        /// <summary>The <c>pc capability business &lt;place&gt;</c> text.</summary>
        public static string Describe(string placeId, string businessId, FoodScopeChannel channel, MechanismVerdict verdict)
            => placeId + " (" + (channel == FoodScopeChannel.Vehicle ? "vehicle" : "shop") + ", " + businessId + "): " + verdict.Text
                + (verdict.Mechanism == null ? "" : "\nnative evidence: " + verdict.Mechanism.NativeEvidence);
    }
}
