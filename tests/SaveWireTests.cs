using Newtonsoft.Json;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;

namespace PersistentCity.Tests;

/// <summary>P-A: the body-editing helper respaces every current-schema body that differs from the writer's spelling only by
/// whitespace, and never respells a string.</summary>
public sealed class SaveWireTests
{
    [Fact]
    public void IndentedBodiesAreRespacedAndLoadAndARespelledStringPassesThroughUnchanged()
    {
        var state = CurrentSchemaOnlyTests.City();
        string payload = SaveCodec.Write(state);
        var root = SaveWire.Parse(payload);
        foreach (string body in new[] { root.ToString(), root.ToString(Formatting.None), root.ToString(Formatting.Indented) })
        {
            string reencoded = SaveWire.Payload(body);
            Assert.StartsWith(SaveCodec.BodyPrefix, reencoded);
            Assert.Equal(payload, reencoded);
            var read = SaveCodec.Read(reencoded);
            Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
        }
        // A noncanonical escape passes through unchanged; it decodes to the same field name, so the strict reader loads it.
        string respelled = root.ToString(Formatting.None).Replace("\"Capabilities\":", "\"Capabilit\\u0069es\":");
        Assert.Same(respelled, SaveWire.Payload(respelled));
        Assert.Equal(SaveReadStatus.Loaded, SaveCodec.Read(respelled).Status);
        // JSON with another schema number passes through unchanged.
        string older = SaveWire.Text(state).Replace("{\"Schema\":" + CityState.CurrentSchema + ",", "{\"Schema\":36,");
        Assert.Same(older, SaveWire.Payload(older));
    }
}
