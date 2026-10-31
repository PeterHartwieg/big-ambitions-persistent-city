using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Reflection;
using System.Reflection.Emit;
using PersistentCity.Core;
using PersistentCity.Runtime;
using Xunit;

namespace Entities
{
    public enum MarketingTypeName { SmallInternet, MediumInternet, LargeInternet, SmallBillboard, MediumBillboard, LargeBillboard, None }
    public sealed class MarketingCampaign
    {
        public MarketingTypeName marketingTypeName = MarketingTypeName.SmallInternet;
        public bool enabled = true;
        public Address agencyAddress = new Address();
        public void Start() => throw new Exception("Native writer called");
        public void Stop() => throw new Exception("Native writer called");
    }
}

public sealed class Address { public string streetName = "Agency Street"; public int streetNumber = 7; }
public sealed class Promotion { public int trafficIndex = 20; public int marketing = 30; public int total = 50; }

namespace PersistentCity.Tests
{
    public sealed class NativeCampaignReaderTests
    {
        public sealed class Registration
        {
            public List<Entities.MarketingCampaign?>? marketingCampaigns = new List<Entities.MarketingCampaign?> { new Entities.MarketingCampaign() };
            public Promotion? promotion = new Promotion();
            public object Provider => throw new Exception("Getter called");
            public float Money { get => throw new Exception("Money getter called"); set => throw new Exception("Money setter called"); }
            public void UpdatePromotion() => throw new Exception("Native writer called");
            public void GetDailyMarketingExpenses() => throw new Exception("Helper called");
        }
        public sealed class PropertiesOnly
        {
            public object marketingCampaigns => throw new Exception("Campaign getter called");
            public object promotion => throw new Exception("Promotion getter called");
        }
        public sealed class WrongTypes { public string marketingCampaigns = "changed"; public string promotion = "changed"; }
        public sealed class WrongList { public List<object> marketingCampaigns = new List<object> { new Entities.MarketingCampaign() }; }
        public sealed class DisguisedList { public object marketingCampaigns = new List<Entities.MarketingCampaign> { new Entities.MarketingCampaign() }; }
        public sealed class BadPromotion { public float trafficIndex = 100; public string marketing = "100"; public int total = -1; }
        public sealed class ChangedPromotion
        {
            public List<Entities.MarketingCampaign> marketingCampaigns = new List<Entities.MarketingCampaign> { new Entities.MarketingCampaign() };
            public BadPromotion promotion = new BadPromotion();
        }

        private static CampaignSnapshot Read(object? input)
        {
            var refresh = new NativeCampaignReader().Begin(new[] { new CampaignBusinessSource("business:one", input) }, 60, 1);
            Assert.Null(refresh.Snapshot);
            Assert.Equal(1, refresh.Step(1, 60, 1));
            return Assert.IsType<CampaignSnapshot>(refresh.Snapshot);
        }

        [Fact]
        public void CopiesOnlyVerifiedFieldsAndLeavesUnsupportedFactsUnknown()
        {
            var reg = new Registration();
            var business = Read(reg).Businesses.Single();
            var campaign = business.Campaigns.Single();
            Assert.Equal(CampaignReadStatus.Observed, business.Status);
            Assert.Equal(0, campaign.TypeCode); Assert.Equal("SmallInternet", campaign.TypeName);
            Assert.True(campaign.Enabled); Assert.Equal("address:Agency%20Street:7", campaign.AgencyId);
            Assert.Equal(20, business.TrafficIndex); Assert.Equal(30, business.Marketing); Assert.Equal(50, business.Total);
            Assert.Null(campaign.Active); Assert.Null(campaign.StartMinute); Assert.Null(campaign.EndMinute);
            Assert.Null(campaign.DurationMinutes); Assert.Null(campaign.NativeTarget);
            Assert.Equal(CampaignResidentGroup.Unknown, campaign.ResidentGroup);
        }

        [Fact]
        public void MissingChangedAndPropertyMembersStayUnknownWithoutCallingGetters()
        {
            foreach (object? source in new object?[] { null, new object(), new PropertiesOnly(), new WrongTypes(), new WrongList(), new DisguisedList() })
            {
                var business = Read(source).Businesses.Single();
                Assert.Equal(CampaignReadStatus.Unknown, business.Status); Assert.Empty(business.Campaigns);
            }
            var changed = Read(new ChangedPromotion()).Businesses.Single();
            Assert.Equal(CampaignReadStatus.Unknown, changed.Status);
            Assert.Null(changed.TrafficIndex); Assert.Null(changed.Marketing); Assert.Null(changed.Total);
        }

        [Theory]
        [InlineData(true)] [InlineData(false)]
        public void ChangedNativeEnumOrEnabledFieldCannotBecomeObserved(bool changeEnum)
        {
            // A separate assembly can expose the same native full names with an incompatible contract.
            var module = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("ChangedCampaign" + changeEnum), AssemblyBuilderAccess.Run)
                .DefineDynamicModule("main");
            var enumBuilder = module.DefineEnum("Entities.MarketingTypeName", TypeAttributes.Public, typeof(int));
            for (int i = 0; i < 7; i++) enumBuilder.DefineLiteral(i == 6 ? "None" : CampaignTypeGroups.VerifiedName(i)!, changeEnum ? i + 1 : i);
            var enumType = enumBuilder.CreateTypeInfo()!.AsType();
            var campaignBuilder = module.DefineType("Entities.MarketingCampaign", TypeAttributes.Public);
            campaignBuilder.DefineField("marketingTypeName", enumType, FieldAttributes.Public);
            campaignBuilder.DefineField("enabled", changeEnum ? typeof(bool) : typeof(string), FieldAttributes.Public);
            campaignBuilder.DefineField("agencyAddress", typeof(Address), FieldAttributes.Public);
            var campaignType = campaignBuilder.CreateTypeInfo()!.AsType();
            object campaign = Activator.CreateInstance(campaignType)!;
            campaignType.GetField("enabled")!.SetValue(campaign, changeEnum ? (object)true : "true");
            campaignType.GetField("agencyAddress")!.SetValue(campaign, new Address());
            var listType = typeof(List<>).MakeGenericType(campaignType);
            var list = (System.Collections.IList)Activator.CreateInstance(listType)!; list.Add(campaign);
            var registrationBuilder = module.DefineType("Registration", TypeAttributes.Public);
            registrationBuilder.DefineField("marketingCampaigns", listType, FieldAttributes.Public);
            var registrationType = registrationBuilder.CreateTypeInfo()!.AsType();
            object registration = Activator.CreateInstance(registrationType)!;
            registrationType.GetField("marketingCampaigns")!.SetValue(registration, list);
            var observed = Read(registration).Businesses[0].Campaigns[0];
            Assert.Equal(CampaignReadStatus.Unknown, observed.Status);
            if (changeEnum) Assert.Null(observed.TypeCode); else Assert.Null(observed.Enabled);
        }

        [Fact]
        public void DisabledMalformedAndUnsupportedCampaignsNeverLookActive()
        {
            var reg = new Registration();
            reg.marketingCampaigns![0]!.enabled = false;
            Assert.Equal(CampaignReadStatus.Unknown, Read(reg).Businesses[0].Campaigns[0].Status);
            Assert.False(Read(reg).Businesses[0].Campaigns[0].Enabled);
            reg.marketingCampaigns[0]!.enabled = true;
            reg.marketingCampaigns[0]!.agencyAddress.streetName = "";
            Assert.Null(Read(reg).Businesses[0].Campaigns[0].AgencyId);
            reg.marketingCampaigns[0]!.marketingTypeName = (Entities.MarketingTypeName)99;
            var unsupported = Read(reg).Businesses[0].Campaigns[0];
            Assert.Equal(99, unsupported.TypeCode); Assert.Null(unsupported.TypeName);
            Assert.Equal(CampaignReadStatus.Unknown, unsupported.Status);
            reg.marketingCampaigns[0]!.marketingTypeName = Entities.MarketingTypeName.None;
            Assert.Null(Read(reg).Businesses[0].Campaigns[0].TypeName);
            reg.marketingCampaigns[0] = null;
            Assert.Null(Read(reg).Businesses[0].Campaigns[0].Enabled);
            reg.marketingCampaigns = null;
            Assert.Equal(CampaignReadStatus.Unknown, Read(reg).Businesses[0].Status);
        }

        [Fact]
        public void RefusesAllDuplicatesEvenAcrossAgenciesAndCapsMalformedLists()
        {
            var reg = new Registration();
            reg.marketingCampaigns!.Add(new Entities.MarketingCampaign { agencyAddress = new Address { streetNumber = 8 } });
            Assert.All(Read(reg).Businesses[0].Campaigns, c => Assert.Equal(CampaignReadStatus.Refused, c.Status));
            while (reg.marketingCampaigns.Count <= NativeCampaignReader.MaximumCampaignsPerBusiness) reg.marketingCampaigns.Add(null);
            Assert.Equal(CampaignReadStatus.Refused, Read(reg).Businesses[0].Status);
            Assert.Empty(Read(reg).Businesses[0].Campaigns);
        }

        [Fact]
        public void UnknownPromotionIsNotZeroOrFavourableAndCopiesAreDetached()
        {
            var reg = new Registration();
            var snapshot = Read(reg);
            reg.marketingCampaigns![0]!.enabled = false;
            reg.marketingCampaigns[0]!.agencyAddress.streetName = "Changed";
            reg.marketingCampaigns.Clear(); reg.promotion!.total = 101;
            Assert.True(snapshot.Businesses[0].Campaigns[0].Enabled);
            Assert.Equal("address:Agency%20Street:7", snapshot.Businesses[0].Campaigns[0].AgencyId);
            Assert.Equal(50, snapshot.Businesses[0].Total);
            Assert.Null(Read(reg).Businesses[0].Total);
            Assert.Equal(CampaignReadStatus.Unknown, Read(reg).Businesses[0].Status);
            reg.promotion = null;
            Assert.Null(Read(reg).Businesses[0].TrafficIndex);
            Assert.Throws<NotSupportedException>(() => ((IList<CampaignObservation>)snapshot.Businesses[0].Campaigns).Clear());
        }

        [Fact]
        public void ObservedEmptyIsDistinctFromUnknownListAndZeroPromotionIsOnlyAnIndex()
        {
            var reg = new Registration(); reg.marketingCampaigns!.Clear();
            reg.promotion!.trafficIndex = 0; reg.promotion.marketing = 0; reg.promotion.total = 0;
            var empty = Read(reg).Businesses[0];
            Assert.Equal(CampaignReadStatus.Observed, empty.Status); Assert.Empty(empty.Campaigns);
            Assert.Equal(0, empty.Total);
            reg.marketingCampaigns = null;
            Assert.Equal(CampaignReadStatus.Unknown, Read(reg).Businesses[0].Status);
        }

        [Fact]
        public void ReflectionMetadataCacheRefusesFurtherShapesAtItsHardBound()
        {
            var module = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("CampaignShapes"), AssemblyBuilderAccess.Run).DefineDynamicModule("main");
            var reader = new NativeCampaignReader();
            for (int i = 0; i < 40; i++)
            {
                var shape = module.DefineType("Registration" + i, TypeAttributes.Public);
                shape.DefineField("marketingCampaigns", typeof(List<Entities.MarketingCampaign>), FieldAttributes.Public);
                var type = shape.CreateTypeInfo()!.AsType(); var source = Activator.CreateInstance(type)!;
                type.GetField("marketingCampaigns")!.SetValue(source, new List<Entities.MarketingCampaign>());
                var refresh = reader.Begin(new[] { new CampaignBusinessSource("a", source) }, 0, 0);
                refresh.Step(1, 0, 0);
                Assert.NotNull(refresh.Snapshot);
                Assert.Equal(CampaignReadStatus.Unknown, refresh.Snapshot!.Businesses[0].Status);
                // Empty lists still need known promotion to establish an entirely Observed business.
            }
            var final = reader.Begin(new[] { new CampaignBusinessSource("b", new Registration()) }, 0, 0);
            final.Step(1, 0, 0);
            Assert.Empty(final.Snapshot!.Businesses[0].Campaigns);
            Assert.Null(final.Snapshot.Businesses[0].TrafficIndex);
        }

        [Theory]
        [InlineData(1)] [InlineData(8)] [InlineData(NativeCampaignReader.MaximumBusinesses)]
        public void CursorIsBoundedCompleteAndMatchesSingleBoundaryRead(int count)
        {
            var sources = Enumerable.Range(0, count).Select(i => new CampaignBusinessSource("b:" + i, new Registration())).ToArray();
            var reader = new NativeCampaignReader();
            var refresh = reader.Begin(sources, 120, 42);
            sources[0] = new CampaignBusinessSource("replacement", null);
            while (refresh.Cursor < count)
            {
                int before = refresh.Cursor;
                int read = refresh.Step(1, 120, 42);
                Assert.Equal(1, read); Assert.Equal(before + 1, refresh.Cursor);
                if (refresh.Cursor < count) Assert.Null(refresh.Snapshot);
            }
            Assert.Equal(count, refresh.Snapshot!.Businesses.Count);
            Assert.Equal("b:0", refresh.Snapshot.Businesses[0].BusinessId);
            Assert.Equal(42, refresh.Snapshot.Revision); Assert.Equal(120, refresh.Snapshot.AtMinute);
            Assert.Equal(0, refresh.Step(8, 120, 42));
        }

        [Fact]
        public void ChangedBoundaryDiscardsPartialAndCompletedImagesAndInvalidScopesRefuse()
        {
            var reader = new NativeCampaignReader();
            var sources = new[] { new CampaignBusinessSource("a", new Registration()), new CampaignBusinessSource("b", new Registration()) };
            var refresh = reader.Begin(sources, 60, 1);
            refresh.Step(1, 60, 1); Assert.Equal(0, refresh.Step(1, 61, 1));
            Assert.True(refresh.Refused); Assert.Null(refresh.Snapshot); Assert.Equal(0, refresh.Step(1, 60, 1));
            var changedRevision = reader.Begin(sources, 60, 1);
            changedRevision.Step(8, 60, 1); changedRevision.Step(8, 60, 2);
            Assert.True(changedRevision.Refused); Assert.Null(changedRevision.Snapshot);
            Assert.Throws<ArgumentException>(() => reader.Begin(Array.Empty<CampaignBusinessSource>(), 0, 0));
            Assert.Throws<ArgumentException>(() => reader.Begin(Enumerable.Repeat(sources[0], 1025).ToArray(), 0, 0));
            Assert.Throws<ArgumentException>(() => reader.Begin(new[] { sources[0], sources[0] }, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => reader.Begin(sources, 0, 0).Step(65, 0, 0));
        }

        [Fact]
        public void RecognizesOnlySixVerifiedNativeTypesWithoutInventingDemographics()
        {
            for (int code = 0; code < 6; code++)
            {
                var reg = new Registration(); reg.marketingCampaigns![0]!.marketingTypeName = (Entities.MarketingTypeName)code;
                Assert.Equal(CampaignTypeGroups.VerifiedName(code), Read(reg).Businesses[0].Campaigns[0].TypeName);
                Assert.NotNull(CampaignTypeGroups.VerifiedName(code));
                Assert.Equal(CampaignResidentGroup.Unknown, CampaignTypeGroups.Group(code));
            }
            foreach (int code in new[] { -1, 6, 99, int.MaxValue })
            { Assert.Null(CampaignTypeGroups.VerifiedName(code)); Assert.Equal(CampaignResidentGroup.Unknown, CampaignTypeGroups.Group(code)); }
        }

        [Fact]
        public void SourceHasNoNativeMutationProviderReflectionInvocationOrSchedulingPath()
        {
            string root = Path.GetFullPath(Path.Combine(SourceDirectory(), ".."));
            string source = File.ReadAllText(Path.Combine(root, "Runtime", "NativeCampaignReader.cs"))
                + File.ReadAllText(Path.Combine(root, "Runtime", "NativeCampaignDiagnostics.cs"));
            foreach (string forbidden in new[] { ".SetValue(", ".Invoke(", "GetProperty(", "GetMethod(", "dynamic ", "ChangeMoney", "UpdatePromotion(",
                "GetDailyMarketingExpenses(", "GetMarketingEfficiency(", "GetBuildingRegistration(", "CustomerEntriesHelper", "MarketingHelper",
                "BusinessHelper", "MarketingTypeSettings.Get", "GetEntriesByAddress", "Harmony", "void Update(", "marketingCampaigns.Add", "marketingCampaigns.Remove" })
                Assert.DoesNotContain(forbidden, source);
            Assert.Contains("field.GetValue(source)", source);
        }
        private static string SourceDirectory([CallerFilePath] string path = "") => Path.GetDirectoryName(path)!;
    }
}
