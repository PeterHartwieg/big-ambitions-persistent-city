using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PersistentCity.Core;
using Xunit;
using Xunit.Abstractions;

namespace PersistentCity.Tests;

[Collection(StreetFoodHooks.Collection)]
public sealed class FoodDecisionNearTests
{
    private readonly ITestOutputHelper output;
    public FoodDecisionNearTests(ITestOutputHelper output) { this.output = output; }

    [Theory]
    [InlineData("cart-a", "cart-a", double.NaN, double.NaN, FoodDecisionQuery.DefaultRadiusMetres, FoodDecisionQuery.DefaultCount)]
    [InlineData("cart-a,30", "cart-a", double.NaN, double.NaN, 30, FoodDecisionQuery.DefaultCount)]
    [InlineData("truck:b,12.5,20", "truck:b", double.NaN, double.NaN, 12.5, 20)]
    [InlineData("-310.5,268.25", "", -310.5, 268.25, FoodDecisionQuery.DefaultRadiusMetres, FoodDecisionQuery.DefaultCount)]
    [InlineData("310,268,0", "", 310, 268, 0, FoodDecisionQuery.DefaultCount)]
    [InlineData(" 310 , 268 , 40 , 1000 ", "", 310, 268, 40, 1000)]
    public void TheNearFilterReadsAVehicleOrAPositionWithOptionalRadiusAndCount(string value, string vehicle, double x, double z, double radius, int count)
    {
        Assert.True(FoodDecisionQuery.TryParse(value, out var v, out double px, out double pz, out double r, out int n, out string error), error);
        Assert.Equal(vehicle, v);
        Assert.Equal(x, px); Assert.Equal(z, pz);
        Assert.Equal(radius, r); Assert.Equal(count, n);
    }

    [Theory]
    [InlineData("")]
    [InlineData(",30")]
    [InlineData("cart-a,-1")]
    [InlineData("cart-a,wide")]
    [InlineData("cart-a,30,0")]
    [InlineData("cart-a,30,1001")]
    [InlineData("cart-a,30,2.5")]
    [InlineData("310,268,30,50,9")]
    [InlineData("310,268,NaN")]
    public void TheNearFilterRefusesAMalformedTarget(string value)
    {
        Assert.False(FoodDecisionQuery.TryParse(value, out _, out _, out _, out _, out _, out string error));
        Assert.NotEmpty(error);
    }

    [Fact]
    public void SourceTagsNameEveryAwarenessSourceInAFixedOrder()
    {
        Assert.Equal("", FoodAwarenessTags.Format(0));
        Assert.Equal("routine", FoodAwarenessTags.Format(FoodAwarenessSource.Routine));
        Assert.Equal("routine+discovery+memory", FoodAwarenessTags.Format(FoodAwarenessSource.Memory | FoodAwarenessSource.Discovery | FoodAwarenessSource.Routine));
        Assert.Equal("routine+discovery+memory+home+origin+campaign+active-campaign", FoodAwarenessTags.Format((FoodAwarenessSource)127));
    }

    [Fact]
    public void NearSamplesTheLatestDecisionOfEachResidentAroundAPointAndTagsEveryCandidate()
    {
        var (_, city, _) = Gr5AdvanceAllocationTests.City();
        while (!city.AdvanceTo(2 * 1440 + 11 * 60, 256).CaughtUp) { }
        var origins = new List<(string Resident, Position Origin)>();
        long bytes = 0, briefBytes = 0;
        int briefCalls = 0;
        var recorder = new FoodDecisionRecorder(trace =>
        {
            var at = city.FindPosition(trace.Consumer.OriginPlaceId);
            if (at.HasValue) origins.Add((trace.Consumer.Id, at.Value));
            string line = JsonConvert.SerializeObject(new { resident = trace.Consumer.Id, trace.Intent.Id, trace.MealKind, trace.Intent.BusinessId,
                vendors = trace.Businesses.Where(b => VendorNames.IsVendorPlace(b.PlaceId)), trace.ChoiceCosts, trace.Decision, trace.RouteChecks });
            bytes += line.Length * 2;
            return line;
        }, error => throw error, trace =>
        {
            string line = JsonConvert.SerializeObject(new { resident = trace.Consumer.Id, trace.Consumer.OriginPlaceId, trace.Intent.Id, trace.MealKind, trace.Intent.BusinessId,
                trace.ChoiceCosts, result = trace.Decision?.Result, reason = trace.Decision?.Reason, chosen = trace.Decision?.BusinessId, product = trace.Decision?.ProductId });
            briefBytes += line.Length * 2; briefCalls++;
            return line;
        });
        recorder.Start(city);
        for (double minute = 2 * 1440 + 11 * 60; minute < 2 * 1440 + 14 * 60; minute += 1.0 / 30) city.AdvanceTo(minute, 64);
        recorder.Stop();
        int recorded = origins.Count;
        Assert.Equal(0, briefCalls);
        recorder.Near(0, 0, double.MaxValue, FoodDecisionRecorder.Retained);
        output.WriteLine(recorded + " decisions, full row " + (bytes / Math.Max(1, recorded)) + " B, compact row formatted on read " + (briefBytes / Math.Max(1, briefCalls)) + " B, "
            + recorder.Count + " traces retained and " + recorder.Lines.Count() + " full rows ("
            + (bytes / Math.Max(1, recorded) * recorder.Lines.Count() / 1e6).ToString("0.00") + " MB)");
        Assert.True(recorded > FoodDecisionRecorder.Capacity, "only " + recorded + " decisions");
        Assert.Equal(Math.Min(recorded, FoodDecisionRecorder.Retained), recorder.Count);
        Assert.Equal(Math.Min(recorded, FoodDecisionRecorder.Capacity), recorder.Lines.Count());

        var retained = origins.Skip(Math.Max(0, recorded - FoodDecisionRecorder.Retained)).ToList();
        var centre = retained[retained.Count / 2].Origin;
        foreach (double radius in new[] { 0.0, 25, 80, 400 })
            foreach (int count in new[] { 1, 5, 50 })
            {
                var expected = new List<string>();
                var seen = new HashSet<string>(StringComparer.Ordinal);
                for (int i = retained.Count - 1; i >= 0 && expected.Count < count; i--)
                {
                    double dx = retained[i].Origin.X - centre.X, dz = retained[i].Origin.Z - centre.Z;
                    if (Math.Sqrt(dx * dx + dz * dz) <= radius && seen.Add(retained[i].Resident)) expected.Add(retained[i].Resident);
                }
                expected.Reverse();
                var near = recorder.Near(centre.X, centre.Z, radius, count);
                var residents = near.Select(l => (string)JObject.Parse(l.Substring(l.IndexOf("| ", StringComparison.Ordinal) + 2))["resident"]!).ToList();
                Assert.Equal(expected, residents);
                foreach (var line in near)
                    Assert.InRange(double.Parse(line.Substring(0, line.IndexOf(" m |", StringComparison.Ordinal)), System.Globalization.CultureInfo.InvariantCulture), 0, radius + 0.05);
            }
        Assert.NotEmpty(recorder.Near(centre.X, centre.Z, 0, 1));
        foreach (var line in recorder.Near(centre.X, centre.Z, 400, 50))
            Assert.All((JArray)JObject.Parse(line.Substring(line.IndexOf("| ", StringComparison.Ordinal) + 2))["ChoiceCosts"]!, cost => Assert.NotNull(cost["Sources"]));

        int tagged = 0;
        foreach (var line in recorder.Lines)
        {
            var row = JObject.Parse(line);
            var union = row["Decision"]?["Awareness"]?["Union"] as JArray;
            foreach (var cost in (JArray)row["ChoiceCosts"]!)
            {
                var sources = cost["Sources"]!;
                if (union == null) { Assert.Equal(JTokenType.Null, sources.Type); continue; }
                var candidate = union.FirstOrDefault(c => (string?)c["BusinessId"] == (string?)cost["BusinessId"]);
                Assert.Equal(candidate == null ? "" : FoodAwarenessTags.Format((FoodAwarenessSource)(int)candidate["Sources"]!), (string?)sources);
                tagged++;
            }
        }
        output.WriteLine(tagged + " tagged candidate rows in the last " + FoodDecisionRecorder.Capacity);
        Assert.True(tagged > 0, "no decision carried an awareness image");
    }
}
