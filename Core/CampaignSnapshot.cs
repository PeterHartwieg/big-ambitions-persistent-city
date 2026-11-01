using System;
using System.Collections.Generic;

namespace PersistentCity.Core
{
    public enum CampaignReadStatus { Observed, Unknown, Refused }
    public enum CampaignResidentGroup { Unknown }

    public sealed class CampaignTypeGroup
    {
        public CampaignTypeGroup(int code, string name) { Code = code; Name = name; }
        public int Code { get; }
        public string Name { get; }
        public CampaignResidentGroup Group => CampaignResidentGroup.Unknown;
        public string Evidence => "Verified native channel/size; no verified resident-group targeting.";
    }

    /// <summary>Reviewed native identifiers, not a demographic reach claim. No choice consumer yet.</summary>
    public static class CampaignTypeGroups
    {
        public static IReadOnlyList<CampaignTypeGroup> Reviewed { get; } = Array.AsReadOnly(new[] {
            new CampaignTypeGroup(0, "SmallInternet"), new CampaignTypeGroup(1, "MediumInternet"),
            new CampaignTypeGroup(2, "LargeInternet"), new CampaignTypeGroup(3, "SmallBillboard"),
            new CampaignTypeGroup(4, "MediumBillboard"), new CampaignTypeGroup(5, "LargeBillboard") });
        public static string? VerifiedName(int code) => code >= 0 && code < Reviewed.Count ? Reviewed[code].Name : null;
        // The inspected native contract contains channels/sizes, but no resident targeting evidence.
        // Neither internet use nor billboard exposure follows from a persona or an assigned workplace.
        public static CampaignResidentGroup Group(int code) => code >= 0 && code < Reviewed.Count ? Reviewed[code].Group : CampaignResidentGroup.Unknown;
    }

    /// <summary>Detached observation only; nullable scalars mean Unknown, never inferred success.</summary>
    public sealed class CampaignObservation
    {
        public CampaignObservation(int? typeCode, string? typeName, bool? enabled, string? agencyId,
            CampaignReadStatus status)
        { TypeCode = typeCode; TypeName = typeName; Enabled = enabled; AgencyId = agencyId; Status = status; }
        public int? TypeCode { get; }
        public string? TypeName { get; }
        public bool? Enabled { get; }
        public string? AgencyId { get; }
        public CampaignReadStatus Status { get; }
        public CampaignResidentGroup ResidentGroup => CampaignResidentGroup.Unknown;
        public bool? Active => null;
        public double? StartMinute => null;
        public double? EndMinute => null;
        public double? DurationMinutes => null;
        public string? NativeTarget => null;
        public string UnknownEvidence => "Active lifecycle, timing and native resident targets are not exposed by the verified fields.";
    }

    public sealed class BusinessCampaignSnapshot
    {
        public BusinessCampaignSnapshot(string businessId, CampaignReadStatus status,
            IReadOnlyList<CampaignObservation> campaigns, int? trafficIndex, int? marketing, int? total)
        {
            BusinessId = businessId; Status = status;
            var copy = new CampaignObservation[campaigns.Count];
            for (int i = 0; i < copy.Length; i++) copy[i] = campaigns[i];
            Campaigns = Array.AsReadOnly(copy);
            TrafficIndex = trafficIndex; Marketing = marketing; Total = total;
        }
        public string BusinessId { get; }
        public CampaignReadStatus Status { get; }
        public IReadOnlyList<CampaignObservation> Campaigns { get; }
        public int? TrafficIndex { get; }
        public int? Marketing { get; }
        public int? Total { get; }
        public string PromotionEvidence => "Saved native indices only; not customer counts, reach, payment or current campaign effectiveness.";
    }

    public sealed class CampaignSnapshot
    {
        public CampaignSnapshot(double atMinute, long revision, IReadOnlyList<BusinessCampaignSnapshot> businesses)
        {
            AtMinute = atMinute; Revision = revision;
            var copy = new BusinessCampaignSnapshot[businesses.Count];
            for (int i = 0; i < copy.Length; i++) copy[i] = businesses[i];
            Businesses = Array.AsReadOnly(copy);
        }
        public double AtMinute { get; }
        public long Revision { get; }
        public IReadOnlyList<BusinessCampaignSnapshot> Businesses { get; }
    }
}
