using System;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using PersistentCity.Core;
using PersistentCity.Persistence;
using PersistentCity.Tools.CompactFormat;
using Xunit;

namespace PersistentCity.Tests
{
    /// <summary>
    /// F7p compact-format prototype (tools/compact-format, issue #94): offline checks only. The prototype is not
    /// wired into Core, Persistence or Runtime, and writer 25 is unchanged.
    /// </summary>
    public sealed class CompactFormatPrototypeTests
    {
        [Fact]
        public void WireTreeEscapesEveryCharacterLikeTheWriter25Serializer()
        {
            var all = new StringBuilder();
            for (int c = 0; c <= 0xFFFF; c++) if (!char.IsSurrogate((char)c)) all.Append((char)c);
            all.Append("\uD83D\uDE00");
            var builder = new StringBuilder();
            WireTree.WriteString(builder, all.ToString());
            Assert.Equal(JsonConvert.SerializeObject(all.ToString()), builder.ToString());
            Assert.Equal(all.ToString(), (string)WireTree.Parse(builder.ToString())!);
        }

        [Fact]
        public void EveryCandidateRoundTripsTheRepresentativeFixtureExactly()
        {
            string writer25 = SaveCodec.Write(RepresentativeFixture.Build(957).State);
            var tree = WireTree.Parse(writer25);
            Assert.Equal(writer25, WireTree.Write(tree));
            foreach (var candidate in Candidate.All)
            {
                string payload = candidate.Encode(tree);
                var decoded = candidate.Decode(payload);
                Assert.True(WireTree.DeepEqual(tree, decoded, out string difference), candidate.Id + ": " + difference);
                Assert.Equal(writer25, WireTree.Write(decoded));
                Assert.Equal(payload, candidate.Encode(decoded));
                if (candidate.Id != "A") Assert.True(payload.Length < writer25.Length, candidate.Id + " is not smaller.");
            }
            var strict = SaveCodec.Read(WireTree.Write(Candidate.Get("C").Decode(Candidate.Get("C").Encode(tree))));
            Assert.Equal(SaveReadStatus.Loaded, strict.Status);
            Assert.Equal(writer25, SaveCodec.Write(strict.State!));
        }

        // Edge shapes the fixtures do not all reach: number spellings that must not be normalized, nulls mixed with
        // values, varying object shapes and tuple lengths, shared routes and native road-tag ranges.
        private const string EdgeCases = "{\"Numbers\":[0,-0,-1,1E-05,0.1,-0.0,12.0,9223372036854775807,-9223372036854775808,"
            + "92233720368547758070,1.7976931348623157E+308,5e3],"
            + "\"Ints\":[1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,9223372036854775807,-9223372036854775808],"
            + "\"Integral\":[1.0,2.0,-3.0,40000.0,5.0,6.0,7.0,8.0,9.0,10.0,11.0,12.0],"
            + "\"Rows\":[{\"Id\":\"a\",\"V\":1,\"Opt\":null},{\"Id\":\"b\",\"V\":2},{\"V\":3,\"Id\":\"a\",\"Extra\":[true,false]},{\"Id\":\"\\u0000\\n\\\"\\u2028x\",\"V\":null,\"Opt\":\"s\"}],"
            + "\"Tuples\":[[1,\"x\",2.5,[1,2]],[1,\"y\",3.0],[1,\"x\",null,[],{\"k\":\"x\"}]],"
            + "\"Empty\":[],\"EmptyObject\":{},\"Mixed\":[1,\"1\",true,null,{},[]],"
            + "\"Citizens\":[{\"Journey\":{\"Legs\":[{\"Minutes\":1.5,\"ServiceId\":\"native:road:v1:topo:1:2\"},{\"Minutes\":2.0,\"ServiceId\":\"native:road:v1:topo:2:7\"},"
            + "{\"Minutes\":0.5,\"ServiceId\":\"\"},{\"Minutes\":0.5,\"ServiceId\":\"native:road:fragment1:topo:07:8\"},{\"Minutes\":0.5,\"ServiceId\":\"native:road:v1:other:7:9\"}]}},"
            + "{\"Journey\":null},{\"Journey\":{\"Legs\":[{\"Minutes\":1.5,\"ServiceId\":\"native:road:v1:topo:1:2\"},{\"Minutes\":2.0,\"ServiceId\":\"native:road:v1:topo:2:7\"},"
            + "{\"Minutes\":0.5,\"ServiceId\":\"\"},{\"Minutes\":0.5,\"ServiceId\":\"native:road:fragment1:topo:07:8\"},{\"Minutes\":0.5,\"ServiceId\":\"native:road:v1:other:7:9\"}]}}],"
            + "\"Traffic\":{\"Vehicles\":[{\"Route\":[{\"Link\":1},{\"Link\":2}]},{\"Route\":[{\"Link\":1},{\"Link\":2}]},{\"Route\":[]}]},"
            + "\"Name\":\"\\u00e9\\u4e2d\\ud83d\\ude00\"}";

        [Fact]
        public void EveryCandidateKeepsExactSpellingsSharedRoutesAndRoadRanges()
        {
            var tree = WireTree.Parse(EdgeCases);
            string canonical = WireTree.Write(tree);
            Assert.Contains("1E-05", canonical);
            Assert.Contains("-0.0", canonical);
            foreach (var candidate in Candidate.All)
            {
                var stats = new ColumnarStats();
                string payload = candidate.Encode(tree, stats);
                var decoded = candidate.Decode(payload);
                Assert.True(WireTree.DeepEqual(tree, decoded, out string difference), candidate.Id + ": " + difference);
                Assert.Equal(canonical, WireTree.Write(decoded));
                Assert.Equal(payload, candidate.Encode(decoded));
                if (candidate.Id == "C" || candidate.Id == "D")
                {
                    // Two identical journeys and two identical vehicle routes share one table entry each.
                    Assert.Equal(3, stats.Routes);
                    Assert.Equal(5, stats.RouteReferences);
                    Assert.Equal(3, stats.RoadTags);
                    var citizens = (object?[])((WireObject)decoded!).Values[Array.IndexOf(((WireObject)decoded).Keys, "Citizens")]!;
                    Assert.Same(((WireObject)citizens[0]!).Values[0], ((WireObject)citizens[2]!).Values[0]);
                }
            }
        }

        [Fact]
        public void RoadTagsSplitOnlyCanonicalNodeIndices()
        {
            Assert.True(ColumnarCodec.TrySplitRoad("native:road:v1:abc:12:0", out var head, out int from, out int to));
            Assert.Equal(("native:road:v1:abc", 12, 0), (head, from, to));
            Assert.False(ColumnarCodec.TrySplitRoad("native:road:fragment1:abc:07:8", out _, out _, out _));
            Assert.False(ColumnarCodec.TrySplitRoad("native:road:v2:abc:1:2", out _, out _, out _));
            Assert.False(ColumnarCodec.TrySplitRoad("native:road:v1:abc:1:2:3", out _, out _, out _));
            Assert.False(ColumnarCodec.TrySplitRoad("native:road:v1:abc:-1:2", out _, out _, out _));
        }

        [Fact]
        public void VarintsRoundTripTheWholeLongRange()
        {
            var values = new[] { 0L, 1, -1, 31, 32, -32, 1L << 40, long.MaxValue, long.MinValue, long.MaxValue - 1, 18302466260992 };
            foreach (bool delta in new[] { false, true })
                Assert.Equal(values, Varint.Unpack(Varint.Pack(values, delta), values.Length, delta));
            Assert.False(Varint.TryCanonical("-0", out _));
            Assert.False(Varint.TryCanonical("01", out _));
            Assert.False(Varint.TryCanonical("+1", out _));
            Assert.False(Varint.TryCanonical("9223372036854775808", out _));
            Assert.True(Varint.TryCanonical("-9223372036854775808", out long min) && min == long.MinValue);
            Assert.Throws<FormatException>(() => Varint.Unpack("_", 1, false));
            Assert.Throws<FormatException>(() => Varint.Unpack("11", 1, false));
            Assert.Throws<FormatException>(() => Varint.Unpack("______________", 1, false));
        }

        [Theory]
        [InlineData("\"f\":\"pcc-c\"", "\"f\":\"pcc-x\"")]
        [InlineData("\"v\":1", "\"v\":2")]
        [InlineData("{\"f\"", "{\"z\":0,\"f\"")]
        [InlineData("\"t\":[4,", "\"t\":[4,999,")]
        [InlineData("\"t\":[4,", "\"t\":[99,")]
        public void TamperedCompactPayloadsAreRefused(string find, string replace)
        {
            string payload = Candidate.Get("C").Encode(WireTree.Parse(EdgeCases));
            Assert.Contains(find, payload);
            string tampered = payload.Replace(find, replace);
            Assert.Throws<FormatException>(() => Candidate.Get("C").Decode(tampered));
        }

        [Fact]
        public void ATruncatedOrForeignPayloadIsRefused()
        {
            var tree = WireTree.Parse(EdgeCases);
            foreach (var candidate in Candidate.All.Where(c => c.Id != "A"))
            {
                string payload = candidate.Encode(tree);
                Assert.ThrowsAny<Exception>(() => candidate.Decode(payload.Substring(0, payload.Length - 3)));
            }
            Assert.Throws<FormatException>(() => Candidate.Get("B").Decode(Candidate.Get("C").Encode(tree)));
            Assert.Throws<FormatException>(() => Candidate.Get("D").Decode(Candidate.Get("C").Encode(tree)));
        }
    }
}
