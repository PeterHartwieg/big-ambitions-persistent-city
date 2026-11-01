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
            BindingFlags.NonPublic | BindingFlags.Instance, null, new object[] { typeof(ExtendedDto), fields }, null));
        Assert.IsType<InvalidOperationException>(failure.InnerException);
        Assert.Contains("writable properties", failure.InnerException!.Message);
    }

    [Theory]
    [InlineData(typeof(ResidentBudget), 18)]
    [InlineData(typeof(ResidentMeal), 35)]
    public void PublishedTupleLayoutsCoverEveryWritablePropertyExactlyOnce(Type dto, int expected)
    {
        var layout = typeof(SaveCodec).GetNestedType("TupleLayout", BindingFlags.NonPublic)!;
        var instance = layout.GetMethod("For")!.Invoke(null, new object[] { dto })!;
        var fields = (PropertyInfo[])layout.GetField("Properties")!.GetValue(instance)!;
        var writable = dto.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.GetSetMethod() != null);
        Assert.Equal(expected, fields.Length);
        Assert.Equal(fields.Length, fields.Select(p => p.Name).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(writable.Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal), fields.Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void EarlyProbeStopsAtBothHeadersAndFullDecodeStillRejectsDamagedTail(bool capabilitiesFirst)
    {
        const string capability = "\"Capabilities\":{\"Active\":false,\"Code\":\"B\",\"Revision\":1}";
        string prefix = capabilitiesFirst ? "{" + capability + ",\"Schema\":18," : "{\"Schema\":18," + capability + ",";
        string damaged = prefix + "\"UnreadBody\":[";
        SaveCodec.RequireExecutableCapabilities(damaged);
        Assert.Equal(SaveReadStatus.Invalid, SaveCodec.Read(damaged).Status);
    }

    [Fact]
    public void WriterPutsBothBootstrapHeadersBeforePopulationAndOldOrderStillLoads()
    {
        var city = Population.Create(new[] { new CitizenIdentity { Id = "r", Name = "Resident", Age = 30 } },
            new[] { new Place { Id = "home", Name = "Home", Kind = PlaceKind.Home, Capacity = 1 } }, "probe-review", "test", 11, 0);
        string current = SaveCodec.Write(city);
        Assert.StartsWith("{\"Schema\":23,\"Capabilities\":", current);
        var root = JObject.Parse(current);
        var oldOrder = new JObject(root.Properties().OrderBy(p => p.Name, StringComparer.Ordinal).Select(p => new JProperty(p.Name, p.Value.DeepClone())));
        string previous = oldOrder.ToString(Formatting.None);
        SaveCodec.RequireExecutableCapabilities(previous);
        var read = SaveCodec.Read(previous);
        Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
        Assert.Equal(current, SaveCodec.Write(read.State!));
    }

    [Theory]
    [InlineData("\"Schema\":18,\"Schema\":18,")]
    [InlineData("\"Schema\":0x12,")]
    [InlineData("\"Schema\":17,")]
    [InlineData("\"Schema\":21,")]
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
        => Assert.Throws<SaveValidationException>(() => SaveCodec.RequireExecutableCapabilities("{\"Schema\":18,\"Capabilities\":" + capability + "}"));

    [Fact]
    public void ProbeSupportsPrettyHeadersAndAllPublishedLegacySchemas()
    {
        SaveCodec.RequireExecutableCapabilities("{\r\n \"Capabilities\": {\r \"Code\":\"B\",\n\"Active\":false,\r\n\"Revision\":1},\r\n\"Schema\":18,\"UnreadBody\":[");
        SaveCodec.RequireExecutableCapabilities("{\"Schema\":0xD}"); // Legacy reader retains its original integer grammar.
        for (int schema = 13; schema <= 17; schema++) SaveCodec.RequireExecutableCapabilities("{\"Unrelated\":[],\"Schema\":" + schema + "}");
    }
}
