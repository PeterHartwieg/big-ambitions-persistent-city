using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using PersistentCity.Core;
using PersistentCity.Persistence;
using PersistentCity.Runtime;
using Xunit;

public sealed class MarketResearchTests
{
    private static int?[] Values(MarketResearchReport r) => r.FixtureCells.Select(c => c.Value).ToArray();
    private static MarketResearchCell Cell(MarketResearchReport r, MarketResearchMetric metric, MarketResearchBand band = MarketResearchBand.All)
        => r.FixtureCells.Single(c => c.Metric == metric && c.Band == band);

    [Fact]
    public void ManualCohortsMatchAndEachConsumerCountsOnceInOverlappingUsualLocations()
    {
        var source = new ResearchFixture(40);
        var report = ResearchFixture.Finish(source, 16, scope: new MarketResearchScope("address", new Position(0,0,0), 50));
        Assert.Equal(40, source.Reads); Assert.Equal(40, Cell(report, MarketResearchMetric.Consumers).Value);
        Assert.Equal(26, Cell(report, MarketResearchMetric.RepresentedConsumers).Value);
        Assert.Equal(20, Cell(report, MarketResearchMetric.BreakfastOpportunities).Value);
        Assert.Equal(20, Cell(report, MarketResearchMetric.LunchOpportunities).Value);
        Assert.Equal(20, Cell(report, MarketResearchMetric.DinnerOpportunities).Value);
        Assert.Equal(20, Cell(report, MarketResearchMetric.HouseholdFood, MarketResearchBand.Funded).Value);
        Assert.Equal(20, Cell(report, MarketResearchMetric.HouseholdFood, MarketResearchBand.Unfunded).Value);
        Assert.Equal(14, Cell(report, MarketResearchMetric.DiscretionaryBudget, MarketResearchBand.Low).Value);
        Assert.Equal(13, Cell(report, MarketResearchMetric.DiscretionaryBudget, MarketResearchBand.Neutral).Value);
        Assert.Equal(13, Cell(report, MarketResearchMetric.DiscretionaryBudget, MarketResearchBand.High).Value);
        Assert.Equal(14, Cell(report, MarketResearchMetric.UsualDayGroup, MarketResearchBand.Work).Value);
        foreach (var metric in Enum.GetValues<MarketResearchMetric>().Where(m => m >= MarketResearchMetric.CroissantTaste && m <= MarketResearchMetric.TimePressure))
            foreach (var cell in report.FixtureCells.Where(c => c.Metric == metric)) Assert.Equal(8, cell.Value);
        Assert.Equal(3, report.LogicalDay); Assert.Contains("estimate", report.Evidence); Assert.Contains("represented", report.Evidence);
    }

    [Theory]
    [InlineData(16)] [InlineData(256)] [InlineData(1024)]
    public void PagingAndJsonCursorRestartAreIdenticalAndPublishOnlyWhenComplete(int unit)
    {
        var source = new ResearchFixture(2501);
        var expected = ResearchFixture.Finish(source); source.Reads = 0;
        var job = new MarketResearchEngine(source, new MarketResearchScope(), MarketResearchTier.InDepth);
        Assert.Null(job.Completed); Assert.Equal(unit, job.Continue(source,unit)); Assert.Null(job.Completed);
        string wire = JsonConvert.SerializeObject(job.Checkpoint()); Assert.True(wire.Length < 1024);
        var checkpoint = JsonConvert.DeserializeObject<MarketResearchCursor>(wire)!;
        job = new MarketResearchEngine(source, new MarketResearchScope(), MarketResearchTier.InDepth, checkpoint);
        // Mutating a previously exported checkpoint cannot alter the job.
        checkpoint.Counters[0] = 9999;
        while (job.Completed == null) Assert.InRange(job.Continue(source,unit), 1,unit);
        // Restore reads the last processed row once to validate the cursor; it does not count it again.
        Assert.Equal(2502, source.Reads); Assert.Equal(Values(expected),Values(job.Completed));
        Assert.Equal(expected.Id,job.Completed.Id); Assert.Equal(0,job.Continue(source));
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    public void AnySnapshotRevisionChangeDropsPartialAndCompletedImages(int kind)
    {
        var source = new ResearchFixture(40);
        var job = new MarketResearchEngine(source,new MarketResearchScope(),MarketResearchTier.InDepth);
        job.Continue(source,16); var old = job.Checkpoint();
        source.Identity = new MarketResearchIdentity(kind==0 ? "replacement" : "fixture", kind==1 ? 4 : 3,
            kind==2 ? 8 : 7, kind==3 ? 12 : 11, kind==4 ? 14 : 13);
        job.Continue(source,16); Assert.Equal(16,job.Checkpoint().Next); Assert.Null(job.Completed);
        while(job.Completed==null) job.Continue(source,16);
        Assert.Equal(40,Cell(job.Completed,MarketResearchMetric.Consumers).Value);
        var restarted = new MarketResearchEngine(source,new MarketResearchScope(),MarketResearchTier.InDepth,old);
        Assert.Equal(0,restarted.Checkpoint().Next);
        source.Identity = new MarketResearchIdentity("third",3,7,11,13);
        job.Continue(source,16); Assert.Null(job.Completed); Assert.Equal(16,job.Checkpoint().Next);
    }

    [Fact]
    public void MidUnitReplacementCannotPublishMixedSnapshot()
    {
        var source = new ResearchFixture(16);
        var job = new MarketResearchEngine(source,new MarketResearchScope(),MarketResearchTier.Basic);
        source.OnRead = i => { if(i==8) source.Identity = new MarketResearchIdentity("changed",3,7,11,13); };
        job.Continue(source); Assert.Null(job.Completed); Assert.Equal(0,job.Checkpoint().Next);
        source.OnRead = null; job.Continue(source); Assert.Equal(16,Cell(job.Completed!,MarketResearchMetric.Consumers).Value);
    }

    private sealed class Rows : IMarketResearchSnapshot
    {
        internal readonly MarketResearchConsumer[] Items;
        internal Rows(params MarketResearchConsumer[] items) { Items=items; }
        public MarketResearchIdentity Identity { get; } = new MarketResearchIdentity("rows",3,1,1,1);
        public int Count => Items.Length;
        public MarketResearchConsumer Read(int i) => Items[i];
    }
    private static MarketResearchConsumer Row(string id, Position? home, Position? work, bool unknown=false)
        => new MarketResearchConsumer(id,false,home,work,Activity.Work,unknown ? null : "2222222222", unknown ? null : (byte?)0,
            unknown ? null : (bool?)false,unknown ? null : (int?)0);

    [Fact]
    public void CatchmentIncludesExactStraightLineBoundaryOnceAndDoesNotGuessUnknownLocations()
    {
        var source = new Rows(Row("a",new Position(30,0,40),new Position(0,0,0)),
            Row("b",new Position(50.00001,0,0),new Position(0,0,50.00001)),
            Row("c",null,new Position(-50,0,0)), Row("d",null,null),Row("e",new Position(0,51,0),null));
        var report = ResearchFixture.Finish(source,scope:new MarketResearchScope("a",new Position(0,0,0),50));
        Assert.Equal(2,Cell(report,MarketResearchMetric.Consumers).Value);
        Assert.Equal(3,Cell(report,MarketResearchMetric.UsualLocationUnknown).Value);
        Assert.Contains("lower bound", report.Evidence);
    }

    [Fact]
    public void MissingInputsAndUnavailableSiblingFeaturesRemainUnknownRatherThanZero()
    {
        var report = ResearchFixture.Finish(new Rows(Row("a",null,null,true)));
        Assert.Null(Cell(report,MarketResearchMetric.BreakfastOpportunities).Value);
        Assert.Null(Cell(report,MarketResearchMetric.PriceSensitivity,MarketResearchBand.Neutral).Value);
        Assert.Equal(1,Cell(report,MarketResearchMetric.DiscretionaryBudget,MarketResearchBand.Unknown).Value);
        Assert.Equal(1,Cell(report,MarketResearchMetric.HouseholdFood,MarketResearchBand.Unknown).Value);
        Assert.Null(Cell(report,MarketResearchMetric.LoyaltyDistribution,MarketResearchBand.Unknown).Value);
        Assert.Null(Cell(report,MarketResearchMetric.RivalMarketShare,MarketResearchBand.Unknown).Value);
        var empty = ResearchFixture.Finish(new Rows());
        Assert.Equal(0,Cell(empty,MarketResearchMetric.BreakfastOpportunities).Value);
        Assert.Equal(0,Cell(empty,MarketResearchMetric.PriceSensitivity,MarketResearchBand.Neutral).Value);
    }

    [Theory]
    [InlineData(MarketResearchTier.Basic,13)] [InlineData(MarketResearchTier.Detailed,62)] [InlineData(MarketResearchTier.InDepth,64)]
    public void TierProjectionHasFixedUniqueBoundedCells(MarketResearchTier tier,int expected)
    {
        var report = ResearchFixture.Finish(new ResearchFixture(40),tier:tier);
        Assert.Equal(expected,report.FixtureCells.Count); Assert.True(expected<=64);
        Assert.Equal(expected,report.FixtureCells.Select(c=>(c.Metric,c.Band)).Distinct().Count());
        Assert.All(report.FixtureCells,c=>Assert.InRange(c.Label.Length,1,64));
        if(tier==MarketResearchTier.Basic) Assert.DoesNotContain(report.FixtureCells,c=>c.Metric==MarketResearchMetric.PriceSensitivity);
        if(tier!=MarketResearchTier.InDepth) Assert.DoesNotContain(report.FixtureCells,c=>c.Metric==MarketResearchMetric.RivalMarketShare);
    }

    private sealed class FakeEntitlement : IMarketResearchEntitlement
    {
        internal MarketResearchGrant? Grant;
        public bool TryGet(string id,out MarketResearchGrant? grant) { grant=Grant; return true; }
    }
    [Fact]
    public void EveryUnpaidUnknownUndeliveredOrReplacedGrantRevealsNothing()
    {
        var report=ResearchFixture.Finish(new ResearchFixture(40)); var fake=new FakeEntitlement();
        for(int mask=0;mask<32;mask++)
        {
            fake.Grant=new MarketResearchGrant { EntitlementId=(mask&1)!=0 ? "e" : "other", ReportId=(mask&2)!=0 ? report.Id : "other",
                Paid=(mask&4)!=0, Delivered=(mask&8)!=0, Replaced=(mask&16)!=0 };
            bool valid=mask==15;
            Assert.Equal(valid,report.TryRead(fake,"e",out var cells));
            if(!valid) Assert.Null(cells); else Assert.NotNull(cells);
        }
        fake.Grant=null; Assert.False(report.TryRead(fake,"e",out var absent)); Assert.Null(absent);
        Assert.False(report.TryRead(null,"e",out absent)); Assert.Null(absent);
        Assert.False(report.TryRead(fake,"",out absent)); Assert.Null(absent);
        Assert.DoesNotContain("Value",JsonConvert.SerializeObject(report));
        Assert.DoesNotContain("FixtureCells",JsonConvert.SerializeObject(report));
        foreach(string id in new[]{"","e",report.Id})
        { Assert.False(NoPaymentPath.Instance.TryGet(id,out var grant)); Assert.Null(grant);
          Assert.False(report.TryRead(NoPaymentPath.Instance,id,out absent)); Assert.Null(absent); }
    }

    [Fact]
    public void ReplacedReportAndChangedScopeCannotReuseOldEntitlementOrCursor()
    {
        var source=new ResearchFixture(40); var a=ResearchFixture.Finish(source);
        var b=ResearchFixture.Finish(source,scope:new MarketResearchScope("address",new Position(0,0,0),50));
        var grant=new FakeEntitlement { Grant=new MarketResearchGrant { EntitlementId="e",ReportId=a.Id,Paid=true,Delivered=true } };
        Assert.False(b.TryRead(grant,"e",out var cells)); Assert.Null(cells);
        var job=new MarketResearchEngine(source,new MarketResearchScope(),MarketResearchTier.Basic); job.Continue(source,16);
        Assert.Equal(0,new MarketResearchEngine(source,new MarketResearchScope(),MarketResearchTier.Detailed,job.Checkpoint()).Checkpoint().Next);
    }

    [Fact]
    public void DuplicateIdsAndMalformedCursorsFailClosed()
    {
        var source=new Rows(Row("a",null,null),Row("a",null,null));
        var job=new MarketResearchEngine(source,new MarketResearchScope(),MarketResearchTier.Basic);
        Assert.Throws<ArgumentException>(()=>job.Continue(source)); Assert.Null(job.Completed);
        Assert.Throws<InvalidOperationException>(()=>job.Continue(source));
        var good=new ResearchFixture(40); job=new MarketResearchEngine(good,new MarketResearchScope(),MarketResearchTier.Basic);
        var c=job.Checkpoint(); c.Counters=new int[65];
        Assert.Throws<ArgumentException>(()=>new MarketResearchEngine(good,new MarketResearchScope(),MarketResearchTier.Basic,c));
        c=job.Checkpoint(); c.Next=-1;
        Assert.Throws<ArgumentException>(()=>new MarketResearchEngine(good,new MarketResearchScope(),MarketResearchTier.Basic,c));
        c=job.Checkpoint(); c.Counters[0]=1;
        Assert.Throws<ArgumentException>(()=>new MarketResearchEngine(good,new MarketResearchScope(),MarketResearchTier.Basic,c));
    }

    [Fact]
    public void SettingsAreFlatClampedOnceAndCannotEnableResearch()
    {
        var settings=JsonConvert.DeserializeObject<PrototypeSettings>("{\"MarketResearchCatchmentMetres\":1,\"MarketResearchConsumersPerUnit\":9999,\"MarketResearchAvailable\":true}")!;
        var logs=new List<string>(); settings.MarketResearch.Clamp(logs.Add); settings.MarketResearch.Clamp(logs.Add);
        Assert.Equal(50,settings.MarketResearchCatchmentMetres); Assert.Equal(1024,settings.MarketResearchConsumersPerUnit);
        Assert.Equal(2,logs.Count); Assert.False(settings.MarketResearch.Available);
        Assert.False(typeof(MarketResearchSettings).GetProperty("Available")!.CanWrite);
        settings=new PrototypeSettings(); Assert.Equal(500,settings.MarketResearchCatchmentMetres); Assert.Equal(256,settings.MarketResearchConsumersPerUnit);
    }

    [Theory]
    [InlineData("500.0",500,500)]
    [InlineData("\"5e2\"",500,500)]
    [InlineData("null",500,256)]
    [InlineData("2147483648",2000,1024)]
    [InlineData("-2147483649",50,16)]
    [InlineData("\"1e300\"",2000,1024)]
    [InlineData("\"-1e300\"",50,16)]
    [InlineData("500.5",500,256)]
    [InlineData("\"NaN\"",500,256)]
    [InlineData("\"invalid\"",500,256)]
    [InlineData("true",500,256)]
    [InlineData("{}",500,256)]
    [InlineData("[1,2]",500,256)]
    public void ResearchSettingTokensNormalizeWithoutRejectingUnrelatedSettings(string token,int radius,int unit)
    {
        string json="{\"MarketResearchCatchmentMetres\":"+token+",\"MarketResearchConsumersPerUnit\":"+token
            +",\"Enabled\":false,\"MaximumVehicles\":7}";
        var settings=JsonConvert.DeserializeObject<PrototypeSettings>(json)!;
        var logs=new List<string>(); settings.MarketResearch.Clamp(logs.Add); settings.MarketResearch.Clamp(logs.Add);
        Assert.Equal(radius,settings.MarketResearchCatchmentMetres); Assert.Equal(unit,settings.MarketResearchConsumersPerUnit);
        Assert.False(settings.Enabled); Assert.Equal(7,settings.MaximumVehicles); Assert.False(settings.MarketResearch.Available);
        Assert.Equal(2,logs.Count); Assert.All(logs,line=>Assert.InRange(line.Length,1,128));
        var roundTrip=JsonConvert.DeserializeObject<PrototypeSettings>(JsonConvert.SerializeObject(settings))!;
        Assert.Equal(radius,roundTrip.MarketResearchCatchmentMetres); Assert.Equal(unit,roundTrip.MarketResearchConsumersPerUnit);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void RestoredCursorRejectsNextOrLastIdThatDoesNotMatchTheFrozenSource(bool changeNext)
    {
        var source=new ResearchFixture(40);
        var job=new MarketResearchEngine(source,new MarketResearchScope(),MarketResearchTier.InDepth);
        job.Continue(source,16);
        var cursor=JsonConvert.DeserializeObject<MarketResearchCursor>(JsonConvert.SerializeObject(job.Checkpoint()))!;
        if(changeNext) cursor.Next++; else cursor.LastId="consumer:000005";
        var error=Assert.Throws<ArgumentException>(()=>new MarketResearchEngine(source,new MarketResearchScope(),MarketResearchTier.InDepth,cursor));
        Assert.Equal("Invalid research cursor.",error.Message);
    }

    [Fact]
    public void NoPublicEngineNativeDependencyOrSavedFieldAndZeroWorkWhenUnrequested()
    {
        Assert.False(typeof(MarketResearchEngine).IsPublic);
        var source=new ResearchFixture(40); Assert.Equal(0,source.Reads);
        Assert.DoesNotContain(typeof(IMarketResearchSnapshot).GetMethods(),m=>m.GetParameters().Any(p=>p.ParameterType==typeof(IRoutePlanner)));
        Assert.DoesNotContain(typeof(MarketResearchEngine).Assembly.GetReferencedAssemblies(),a=>a.Name!.Contains("Unity") || a.Name.Contains("Rlo"));
        Assert.DoesNotContain(typeof(CityState).GetProperties(),p=>p.Name.Contains("Research"));
        Assert.DoesNotContain(typeof(CityState).GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic),p=>p.Name.Contains("Research"));
    }

    [Fact]
    public void PopulationPayloadBytesStayIdenticalAcrossResearchAndCursorRestart()
    {
        var city=EconomyScenario.Create(40,11); string before=SaveCodec.Write(city);
        var source=new ResearchFixture(40); var job=new MarketResearchEngine(source,new MarketResearchScope(),MarketResearchTier.InDepth);
        job.Continue(source,16);
        var c=JsonConvert.DeserializeObject<MarketResearchCursor>(JsonConvert.SerializeObject(job.Checkpoint()))!;
        job=new MarketResearchEngine(source,new MarketResearchScope(),MarketResearchTier.InDepth,c);
        while(job.Completed==null) job.Continue(source,16);
        Assert.Equal(before,SaveCodec.Write(city)); Assert.Equal(23,CityState.CurrentSchema); Assert.Equal(23,city.Schema);
    }

    [Fact]
    public void UnitClampsAndChangingCadenceDoNotChangeResults()
    {
        var source=new ResearchFixture(2501); var job=new MarketResearchEngine(source,new MarketResearchScope(),MarketResearchTier.InDepth);
        Assert.Equal(16,job.Continue(source,int.MinValue)); Assert.Equal(1024,job.Continue(source,int.MaxValue));
        while(job.Completed==null) job.Continue(source,256);
        Assert.Equal(Values(ResearchFixture.Finish(source)),Values(job.Completed));
    }

    [Theory]
    [InlineData("")] [InlineData("012340123")] [InlineData("01234012345")] [InlineData("0123401235")] [InlineData("012340123/る")]
    public void DirectTraitValidationPreservesA2Bounds(string bands)
    {
        Assert.Equal(new ResidentTraits { Bands=bands }.IsValid,MarketResearchConsumer.ValidTraits(bands));
    }

    [Fact]
    public void CatchmentAndTierChangesRejectPreviousEntitlement()
    {
        var source=new ResearchFixture(40); var report=ResearchFixture.Finish(source);
        var auth=new FakeEntitlement { Grant=new MarketResearchGrant { EntitlementId="e",ReportId=report.Id,Paid=true,Delivered=true } };
        Assert.True(report.TryRead(auth,"e",out _));
        auth.Grant.Replaced=true; Assert.False(report.TryRead(auth,"e",out var cells)); Assert.Null(cells);
        auth.Grant.Replaced=false;
        foreach(var other in new[]{ResearchFixture.Finish(source,tier:MarketResearchTier.Basic),
            ResearchFixture.Finish(source,scope:new MarketResearchScope("a",new Position(0,0,0),50)),
            ResearchFixture.Finish(source,scope:new MarketResearchScope("a",new Position(0,0,0),51))})
        { Assert.False(other.TryRead(auth,"e",out cells)); Assert.Null(cells); }
    }

    private sealed class Oversized : IMarketResearchSnapshot
    {
        public MarketResearchIdentity Identity { get; }=new MarketResearchIdentity("oversized",1,1,1,1);
        public int Count => 150001;
        public MarketResearchConsumer Read(int i) => throw new Exception("Must reject before reading");
    }
    [Fact]
    public void HardCapsAndBadReplacementFailBeforeReadsAndDiscardCompletedImage()
    {
        var src=new ResearchFixture(40); var job=new MarketResearchEngine(src,new MarketResearchScope(),MarketResearchTier.InDepth);
        while(job.Completed==null) job.Continue(src);
        Assert.Throws<ArgumentException>(()=>job.Continue(new Oversized())); Assert.Null(job.Completed);
        Assert.Throws<ArgumentException>(()=>new MarketResearchEngine(new Oversized(),new MarketResearchScope(),MarketResearchTier.Basic));
        Assert.Throws<ArgumentException>(()=>new MarketResearchScope("address",new Position(0,0,0),49));
        Assert.Throws<ArgumentException>(()=>new MarketResearchScope("address",new Position(0,0,0),2001));
        Assert.Throws<ArgumentException>(()=>new MarketResearchScope("address",new Position(double.NaN,0,0),500));
        Assert.Throws<ArgumentException>(()=>new MarketResearchEngine(src,new MarketResearchScope(),(MarketResearchTier)3));
    }
}
