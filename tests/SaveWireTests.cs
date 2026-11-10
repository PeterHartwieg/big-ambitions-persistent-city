using Newtonsoft.Json;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;

namespace PersistentCity.Tests;

/// <summary>F7d: the body-editing helper re-encodes every schema-34 body that differs from the writer's spelling only by
/// whitespace, and leaves lexical corruption (a respelled string) for the product to refuse.</summary>
public sealed class SaveWireTests
{
    [Fact]
    public void IndentedAndCompactBodiesAreReencodedAndLoadWhileARespelledStringStaysRefused()
    {
        var state = CurrentSchemaOnlyTests.City();
        string payload = SaveCodec.Write(state);
        var root = SaveWire.Parse(payload);
        foreach (string body in new[] { root.ToString(), root.ToString(Formatting.None), root.ToString(Formatting.Indented) })
        {
            string reencoded = SaveWire.Payload(body);
            Assert.StartsWith(SaveCodec.CompactPrefix, reencoded);
            Assert.Equal(payload, reencoded);
            var read = SaveCodec.Read(reencoded);
            Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
        }
        // A noncanonical escape is a lexical corruption the compact wire cannot carry: unchanged, and refused.
        string respelled = root.ToString(Formatting.None).Replace("\"Capabilities\":", "\"Capabilit\\u0069es\":");
        Assert.Same(respelled, SaveWire.Payload(respelled));
        Assert.Equal(SaveReadStatus.Invalid, SaveCodec.Read(respelled).Status);
        // JSON with another schema number passes through unchanged.
        string older = SaveWire.Text(state).Replace("{\"Schema\":" + CityState.CurrentSchema + ",", "{\"Schema\":36,");
        Assert.Same(older, SaveWire.Payload(older));
    }
}
