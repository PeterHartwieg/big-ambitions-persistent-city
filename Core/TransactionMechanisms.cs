using System;
using System.Collections.Generic;
using System.Linq;

namespace PersistentCity.Core
{

    public enum TransactionMechanismId : byte { ShopFullServiceBag, VehicleStreetFoodApi, ShopDynamicCatalogue, ResearchAgencyBill }

    public enum MechanismProof : byte { Planned, Implemented, Partial, Stopped, VerifiedByRig }

    public sealed class TransactionMechanism
    {
        private TransactionMechanism(TransactionMechanismId id, string name, string evidence, MechanismProof proof, string evidenceLink, string? rigResultLink)
        { Id = id; Name = name; NativeEvidence = evidence; Proof = proof; EvidenceLink = evidenceLink; RigResultLink = rigResultLink; }
        public TransactionMechanismId Id { get; }
        public string Name { get; }
        public string NativeEvidence { get; }
        public MechanismProof Proof { get; }
        public string EvidenceLink { get; }
        public string? RigResultLink { get; }

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

        public string ProofText => Proof == MechanismProof.VerifiedByRig ? "verified by rig (" + RigResultLink + ")"
            : ProofWord(Proof) + "; rig pending (" + EvidenceLink + ")";
    }

    public readonly struct MechanismVerdict
    {
        internal MechanismVerdict(TransactionMechanism? mechanism, bool served, string reason)
        { Mechanism = mechanism; Served = served; Reason = reason; }
        public TransactionMechanism? Mechanism { get; }
        public bool Served { get; }
        public string Reason { get; }
        public string Text => Served ? Mechanism!.Name + " (" + Mechanism.ProofText + ")"
            : Mechanism != null ? Mechanism.Name + " off: " + Reason : "unsupported: " + Reason;
    }

    public static class TransactionMechanisms
    {
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

        public static string Describe(string placeId, string businessId, FoodScopeChannel channel, MechanismVerdict verdict)
            => placeId + " (" + (channel == FoodScopeChannel.Vehicle ? "vehicle" : "shop") + ", " + businessId + "): " + verdict.Text
                + (verdict.Mechanism == null ? "" : "\nnative evidence: " + verdict.Mechanism.NativeEvidence);
    }
}
