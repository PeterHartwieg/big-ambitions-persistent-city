using System;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;

namespace PersistentCity.Tests;

public sealed class Schema18ReviewTests
{
    public sealed class ExtendedDto
    {
        public int Existing { get; set; }
        public int Added { get; set; }
        public int Computed => Existing + Added;
    }

    [Theory]
    [InlineData("Existing")]
    [InlineData("Existing Existing")]
    public void TupleLayoutRefusesMissingOrDuplicateWritableFields(string fields)
    {
        var layout = typeof(SaveCodec).GetNestedType("TupleLayout", BindingFlags.NonPublic)!;
        var failure = Assert.Throws<TargetInvocationException>(() => Activator.CreateInstance(layout,
            BindingFlags.NonPublic | BindingFlags.Instance, null, new object[] { typeof(ExtendedDto), fields, 2, "" }, null));
        Assert.IsType<InvalidOperationException>(failure.InnerException);
        Assert.Contains("writable properties", failure.InnerException!.Message);
    }

    [Theory]
    [InlineData(typeof(ResidentBudget), 20, CityState.EconomicLedgerSchema)]
    [InlineData(typeof(ResidentBudget), 19, 25)] [InlineData(typeof(ResidentBudget), 19, 26)]
    [InlineData(typeof(ResidentMeal), 36, 25)] [InlineData(typeof(ResidentMeal), 34, 26)]
    public void PublishedTupleLayoutsCoverEveryWritablePropertyExactlyOnce(Type dto, int expected, int schema)
    {
        var layout = typeof(SaveCodec).GetNestedType("TupleLayout", BindingFlags.NonPublic)!;
        var instance = layout.GetMethod("For")!.Invoke(null, new object[] { dto, schema })!;
        var fields = (PropertyInfo[])layout.GetField("Properties")!.GetValue(instance)!;
        // The allowance-only external account uses the existing Budget slot's scalar variant. Schema 26 derives a
        // roster record's anchor and slot from the person table (one record per person).
        var writable = dto.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.GetSetMethod() != null
            && !(dto == typeof(ResidentMeal) && p.Name == "LegacyExternalFoodSpent")
            && !(dto == typeof(ResidentBudget) && schema < SaveCodec.EconomicLedgerSchema && p.Name == "Ledger")
            && !(dto == typeof(ResidentMeal) && schema >= SaveCodec.PersonSchema && (p.Name == "AnchorId" || p.Name == "Slot")));
        Assert.Equal(expected, fields.Length);
        Assert.Equal(fields.Length, fields.Select(p => p.Name).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(writable.Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal), fields.Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void EarlyProbeStopsAtBothHeadersAndFullDecodeStillRejectsDamagedTail(bool capabilitiesFirst)
    {
        const string capability = "\"Capabilities\":{\"Active\":false,\"Code\":\"B\",\"Revision\":1}";
        // Schema 23 and later also carry the menu-policy header before the population graph.
        string menu = "\"MenuPolicy\":\"" + FoodNameIdentity.Wire + "\",";
        string current = "\"Schema\":" + CityState.CurrentSchema + ",";
        string prefix = capabilitiesFirst ? "{" + capability + "," + current + menu : "{" + current + capability + "," + menu;
        string damaged = prefix + "\"UnreadBody\":[";
        SaveCodec.RequireExecutableCapabilities(damaged);
        Assert.Equal(SaveReadStatus.Invalid, DevelopmentChain.Read(damaged).Status);
    }

    [Fact]
    public void WriterPutsBothBootstrapHeadersBeforePopulationAndOldOrderStillLoads()
    {
        var city = Population.Create(new[] { new CitizenIdentity { Id = "r", Name = "Resident", Age = 30 } },
            new[] { new Place { Id = "home", Name = "Home", Kind = PlaceKind.Home, Capacity = 1 } }, "probe-review", "test", 11, 0);
        string current = DevelopmentChain.Write(city);
        Assert.StartsWith("{\"Schema\":"+CityState.CurrentSchema+",\"Capabilities\":", current);
        var root = SaveWire.Parse(current);
        var oldOrder = new JObject(root.Properties().OrderBy(p => p.Name, StringComparer.Ordinal).Select(p => new JProperty(p.Name, p.Value.DeepClone())));
        string previous = oldOrder.ToString(Formatting.None);
        SaveCodec.RequireExecutableCapabilities(previous);
        var read = DevelopmentChain.Read(previous);
        Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
        Assert.Equal(current, DevelopmentChain.Write(read.State!));
    }

    [Theory]
    [InlineData("\"Schema\":18,\"Schema\":18,")]
    [InlineData("\"Schema\":0x12,")]
    [InlineData("\"Schema\":17,")]
    [InlineData("\"Schema\":24,")] [InlineData("\"Schema\":25,")] [InlineData("\"Schema\":26,")] [InlineData("\"Schema\":27,")] [InlineData("\"Schema\":29,")]
    public void ProbeRefusesDuplicateNoncanonicalLegacyOrFutureHeader(string schema)
    {
        Assert.Throws<SaveValidationException>(() => SaveCodec.RequireExecutableCapabilities("{" + schema + "\"Capabilities\":{\"Active\":false,\"Code\":\"B\",\"Revision\":1}}"));
    }

    [Theory]
    [InlineData("{\"Active\":false,\"Code\":\"B\",\"Revision\":01}")]
    [InlineData("{\"Active\":false,\"Code\":\"B\",\"Revision\":1,\"Revision\":1}")]
    [InlineData("{\"Active\":false,\"Code\":\"B\"}")]
    [InlineData("{\"Active\":false,\"Code\":\"B\",\"Revision\":1,\"Extra\":0}")]
    [InlineData("{\"Active\":false,\"Code\":\"C\",\"Revision\":1}")]
    [InlineData("{\"Active\":false,\"Code\":\"B\",\"Revision\":2}")]
    public void ProbeKeepsStrictCapabilityHeaderValidation(string capability)
        => Assert.Throws<SaveValidationException>(() => SaveCodec.RequireExecutableCapabilities("{\"Schema\":28,\"Capabilities\":" + capability + "}"));

    [Fact]
    public void ProbeSupportsPrettyHeadersAndAllPublishedLegacySchemas()
    {
        SaveCodec.RequireExecutableCapabilities("{\r\n \"Capabilities\": {\r \"Code\":\"B\",\n\"Active\":false,\r\n\"Revision\":1},\r\n\"Schema\":" + CityState.CurrentSchema + ",\r\n\"MenuPolicy\":\"" + FoodNameIdentity.Wire + "\",\"UnreadBody\":[");
        // F5c-lite: every older development schema, in each integer grammar the legacy probe accepted, is refused
        // with the development message, so the native authority bootstrap never takes PC ownership.
        foreach (string schema in new[] { "0xD", "13", "17", "18", "25", "26", "27", "28", "29" })
            Assert.Contains("older development build", Assert.Throws<SaveValidationException>(() => SaveCodec.RequireExecutableCapabilities(
                "{\"Unrelated\":[],\"Schema\":" + schema + ",\"Capabilities\":{\"Active\":false,\"Code\":\"B\",\"Revision\":1}}")).Message);
    }
}
