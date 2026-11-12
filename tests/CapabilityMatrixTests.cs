using System.Collections.Generic;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using PersistentCity.FailureMatrix;
using PersistentCity.Compatibility;
using PersistentCity.Core;
using Xunit;
using Xunit.Abstractions;

namespace PersistentCity.Tests
{
    [Collection(F8aSerial.Collection)]
    public sealed class CapabilityMatrixTests
    {
        private readonly ITestOutputHelper output;
        public CapabilityMatrixTests(ITestOutputHelper output) => this.output = output;

        [Fact]
        public void EveryCoreArmPasses()
        {
            var arms = CapabilityMatrix.Run(1, output.WriteLine);
            Assert.Equal(new[] { "control", "presentation off vs never present", "food.checkout.shop off", "required refused", "rlo.donor transient window",
                "research contracts off one at a time", "food.dynamic off vs on" }, arms.Select(a => a.Name));
            Assert.All(arms, a => Assert.True(a.Passed, a.Name + ": " + a.Detail));
        }

        [Fact]
        public void EveryOptionalCapabilityHasAnArm()
        {
            var names = CapabilityMatrix.Run(1).Select(a => a.Name).ToHashSet(StringComparer.Ordinal);
            var tests = typeof(CapabilityMatrixTests).Assembly.GetTypes()
                .SelectMany(t => t.GetMethods().Where(m => m.GetCustomAttributes().Any(x => x is FactAttribute)).Select(m => t.Name + "." + m.Name))
                .ToHashSet(StringComparer.Ordinal);
            foreach (var definition in NativeCapabilities.All.Where(d => d.Requirement != CapabilityRequirement.Required))
            {
                Assert.True(CapabilityMatrix.Coverage.TryGetValue(definition.Id, out var arm), definition.Name + " has no arm");
                var test = Regex.Match(arm!, @"\b(?<check>\w+Tests\.\w+)\b");
                bool found = test.Success ? tests.Contains(test.Groups["check"].Value) : names.Any(n => arm!.StartsWith(n, StringComparison.Ordinal));
                Assert.True(found, definition.Name + ": " + arm);
            }
        }

        private static long Day2At(int hour, int minute = 0) => 2 * 1440 + hour * 60 + minute;

        private static void Drive(VendorCoordinator vendors, VendorTable table, long from, long to)
        {
            for (long minute = from; minute <= to; minute++)
            {
                StreetFoodHooks.Now = minute; StreetFoodHooks.Expire();
                vendors.Poll(minute);
                while (vendors.Continue(minute)) { }
                table.CheckInvariant();
            }
        }

        private static string Rows(VendorTable table) => JsonConvert.SerializeObject(table.Rows.Select(r => new { r.VehicleId, r.BusinessId, r.PlaceId, r.Generation, r.Kind, r.State }));

        [Fact]
        public void StreetFoodAbsentThenReturned()
        {
            StreetFoodHooks.Reset();
            try
            {
                StreetFoodHooks.Add("A");
                var rig = new StreetFoodVendorTests.Rig();
                rig.Run(Day2At(8, 5)); rig.RunTo(Day2At(9, 5)); StreetFoodHooks.Open("A"); rig.Run(Day2At(9, 5));
                Assert.True(rig.Serves("A"));
                string shops = rig.Shops(), rows = Rows(rig.Table);
                string scopes = JsonConvert.SerializeObject(rig.City.State.Food!.Shops);
                string people = string.Join("|", rig.City.State.Citizens.Select(c => c.Identity.Id + ":" + c.Identity.Name));
                string business = rig.Row("A").BusinessId;

                Assert.Null(StreetFoodBridge.Open(null, out string reason));
                var absent = new VendorCoordinator(rig.Table, null, reason, rig.City, rig.Stands);
                Drive(absent, rig.Table, Day2At(9, 6), Day2At(11, 6));
                Assert.False(absent.CanServe(business, Day2At(11, 6)));
                Assert.Contains("Street Food is not loaded", absent.Reason);
                Assert.Equal(shops, rig.Shops());
                Assert.Equal(rows, Rows(rig.Table));
                Assert.Equal(scopes, JsonConvert.SerializeObject(rig.City.State.Food.Shops));
                Assert.Equal(people, string.Join("|", rig.City.State.Citizens.Select(c => c.Identity.Id + ":" + c.Identity.Name)));
                var inputs = CapabilityReportTests.Healthy(); inputs.StreetFoodLoaded = false;
                Assert.Equal(CapabilityState.Absent, CapabilitySnapshot.Build(inputs).State(CapabilityId.VendorStreetFood));
                Assert.False(CapabilitySnapshot.Build(inputs).IsEnabled(CapabilityScope.VehicleScopes));
                Assert.True(CapabilitySnapshot.Build(inputs).IsEnabled(CapabilityScope.ShopOwnership));

                var returned = new VendorCoordinator(rig.Table, StreetFoodBridge.Bind(typeof(FoodTrucks.ModApi.VendorApi)), "", rig.City, rig.Stands);
                StreetFoodHooks.Revision++;
                Drive(returned, rig.Table, Day2At(11, 7), Day2At(12, 7));
                StreetFoodHooks.Close("A"); StreetFoodHooks.Open("A");
                Drive(returned, rig.Table, Day2At(12, 8), Day2At(12, 20));
                Assert.True(returned.CanServe(business, Day2At(12, 20)), returned.Reason);
                Assert.Equal(shops, rig.Shops());
                Assert.Equal(business, rig.Row("A").BusinessId);
            }
            finally { StreetFoodHooks.Reset(); }
        }

        [Theory]
        [InlineData(1, "Off")]
        [InlineData(3, "Refused")]
        [InlineData(4, "Failed")]
        public void ABoundStreetFoodWhoseSnapshotIsRefusedReportsOff(int status, string word)
        {
            StreetFoodHooks.Reset();
            try
            {
                StreetFoodHooks.Add("A");
                var rig = new StreetFoodVendorTests.Rig();
                rig.Run(Day2At(8, 5));
                var inputs = CapabilityReportTests.Healthy(); inputs.ReadVendor(true, rig.Vendors);
                Assert.Equal(CapabilityState.Active, CapabilitySnapshot.Build(inputs).State(CapabilityId.VendorStreetFood));
                StreetFoodHooks.ReadStatus = status; StreetFoodHooks.Revision++;
                rig.Run(Day2At(8, 6));
                inputs = CapabilityReportTests.Healthy(); inputs.ReadVendor(true, rig.Vendors);
                Assert.True(inputs.VendorApiBound);
                var registry = CapabilitySnapshot.Build(inputs);
                Assert.Equal(CapabilityState.Off, registry.State(CapabilityId.VendorStreetFood));
                Assert.StartsWith("snapshot " + word + ": ", registry.Reason(CapabilityId.VendorStreetFood));
                Assert.False(rig.Serves("A"));
                var fresh = new VendorCoordinator(new VendorTable(), StreetFoodBridge.Bind(typeof(FoodTrucks.ModApi.VendorApi)), "", rig.City, rig.Stands);
                inputs.ReadVendor(true, fresh);
                Assert.Equal(CapabilityState.NotChecked, CapabilitySnapshot.Build(inputs).State(CapabilityId.VendorStreetFood));
            }
            finally { StreetFoodHooks.Reset(); }
        }

        internal static readonly IReadOnlyDictionary<string, string[]> Requirements = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["A1"] = new[] { "NativeCapabilityTests.DefinitionsFollowTheD2Table", "CapabilityReportTests.ReportFromDoublesForEveryState", "HookConflictReportTests.InventoryCountsAndCapabilityMapping" },
            ["A2"] = new[] { "RloContinuityTests.ATransientWindowContinuesSavesTheReadyBytesAndChangesNoIdentity", "RloContinuityTests.ReflectionFallbackClassesGoingPublicAsStopAndFlareOffAsTransient" },
            ["A3"] = new[] { "RloContinuityTests.ATransientWindowContinuesSavesTheReadyBytesAndChangesNoIdentity", "RloContinuityTests.ReadyButDifferentStopsWithThePayloadKept" },
            ["A4"] = new[] { "TransactionMechanismTests.VerifiedByRigWithoutAResultLinkIsRefused", "TransactionMechanismTests.EveryScopedBusinessHasOneMechanismOrOneUnsupportedReason" },
            ["A5"] = new[] { "HookConflictReportTests.RealHarmonyOnStandInMethodsIsClassed", "tools/capability-contracts" },
            ["A6"] = new[] { "CapabilityMatrixTests.EveryCoreArmPasses", "NativeFoodContractTests.CheckoutOffWithdrawsAnOwnedShopAtTheNextBoundaryAndKeepsItsReceipts", "CapabilityReportTests.HealthyInputsReportEveryReadCapabilityActive" },
            ["A7"] = new[] { "RloContinuityTests.ATransientWindowContinuesSavesTheReadyBytesAndChangesNoIdentity", "NativeFoodContractTests.CheckoutOffRequestsNoNewOwnership" },
            ["A8"] = new[] { "RloContinuityTests.ATransientWindowContinuesSavesTheReadyBytesAndChangesNoIdentity", "CapabilityMatrixTests.EveryCoreArmPasses" },
            ["A9"] = new[] { "NativeFoodContractTests.EachChangedAuthorityMemberGivesANamedStartRefusal", "NativeFoodContractTests.EachChangedCheckoutMemberRefusesCheckoutOnly" },
            ["A10"] = new[] { "TransactionMechanismTests.EveryScopedBusinessHasOneMechanismOrOneUnsupportedReason" },
            ["A11"] = new[] { "TransactionMechanismTests.MetadataRegistrationGrantsNoMechanism" },
            ["A12"] = new[] { "HookConflictReportTests.InventoryCountsAndCapabilityMapping", "HookConflictReportTests.ForeignOwnersAreJoinedToTheInventoryAndOurOwnersAreNot" },
            ["A13"] = new[] { "CapabilityReportTests.HealthyInputsReportEveryReadCapabilityActive", "TransactionMechanismTests.VerifiedByRigWithoutAResultLinkIsRefused" },
            ["A14"] = new[] { "CapabilityMatrixTests.EveryCoreArmPasses", "HookConflictReportTests.ReportOnlyChangesNoCapability" },
            ["A15"] = new[] { "CapabilityReportTests.ReportFromDoublesForEveryState", "CapabilityReportTests.CurrentWriterGoldenHashIsUnchanged" },
            ["A16"] = new[] { "CapabilityReportTests.ReportFromDoublesForEveryState", "RloContinuityTests.EveryBridgeStatusMapsToItsReasonClass", "NativeFoodContractTests.EachChangedAuthorityMemberGivesANamedStartRefusal" },
            ["A17"] = new[] { "NativeFoodContractTests.FaultPointShapesAreTheRenameAndSignatureVariants", "CapabilityMatrixTests.StreetFoodAbsentThenReturned", "CapabilityMatrixTests.EveryCoreArmPasses" },
        };

        [Fact]
        public void EveryRequirementHasANamedPassingCheck()
        {
            var tests = typeof(CapabilityMatrixTests).Assembly.GetTypes()
                .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                    .Where(m => m.GetCustomAttributes().Any(a => a is FactAttribute)).Select(m => t.Name + "." + m.Name))
                .ToHashSet(StringComparer.Ordinal);
            Assert.Equal(Enumerable.Range(1, 17).Select(i => "A" + i), Requirements.Keys);
            foreach (var (requirement, checks) in Requirements)
            {
                Assert.NotEmpty(checks);
                foreach (string check in checks)
                {
                    Assert.False(check.StartsWith("docs/", StringComparison.Ordinal), requirement + " names a docs file: " + check);
                    if (check.StartsWith("tools/", StringComparison.Ordinal)) { Assert.True(Directory.Exists(Path.Combine(CapabilityReportTests.Root(), check)), check); continue; }
                    Assert.True(tests.Contains(check), requirement + ": no test " + check);
                }
            }
        }
    }
}
