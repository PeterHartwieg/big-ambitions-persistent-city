using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;
using Xunit.Abstractions;

namespace PersistentCity.Tests;

/// <summary>
/// P-A single-pass save: one writer straight from the snapshot inside the save callback, one strict reader straight from
/// the save string. Round trips at the representative 957-resident city (1,914 persons) and at 3,000 persons; corrupt,
/// truncated, other-schema and retired compact payloads are refused through every entry point and never overwritten;
/// the wallets the payload carries and the native receipts beside it come from one capture.
/// </summary>
public sealed class SaveRoundTripTests
{
    private readonly ITestOutputHelper output;
    public SaveRoundTripTests(ITestOutputHelper output) { this.output = output; }

    private const string ReceiptKey = "persistentcity:food-sale-v1:round-trip";

    /// <summary>The 1-resident city (<see cref="CurrentSchemaOnlyTests.City"/>) as the F7d columnar writer stored it before
    /// P-A (the reader corpus's old <c>minimal.json</c>). Development builds wrote this format at schema 1; it is refused as
    /// an older development city.</summary>
    private static string RetiredCompactMinimal() => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "ReaderCorpus", "retired-pcc-c-minimal.json"));

    // ---- Round trip ----

    [Theory]
    [InlineData("representative-957x2", 1914)]
    [InlineData("rush-1500x2", 3000)]
    public void TheWriterRoundTripsExactlyAtTheRepresentativeAndThreeThousandPersonCities(string fixture, int persons)
    {
        var state = F7cFixtures.Build(fixture).State;
        Assert.Equal(persons, state.Citizens.Count + state.Visitors.Count);
        var cost = new SaveCostRecorder();
        cost.Begin();
        string payload = SaveCodec.Write(state, cost);
        cost.End(true, "");
        Assert.StartsWith(SaveCodec.BodyPrefix, payload);
        Assert.Null(SaveCodec.DevelopmentSchemaOf(payload));
        SaveCodec.RequireExecutableCapabilities(payload);

        var read = SaveCodec.Read(payload);
        Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
        Assert.Equal(CityState.CurrentSchema, read.SourceSchema);
        var loaded = read.State!;
        Assert.Equal(state.Citizens.Select(c => c.Identity.Id), loaded.Citizens.Select(c => c.Identity.Id));
        Assert.Equal(state.Visitors.Select(v => v.Id), loaded.Visitors.Select(v => v.Id));
        Assert.Equal(Wallets(state), Wallets(loaded));
        // Read then write is byte-identical, through the direct writer and through a snapshot of the loaded city.
        Assert.Equal(payload, SaveCodec.Write(loaded));
        Assert.Equal(payload, SaveCodec.Encode(SaveCodec.Capture(loaded)));
        // The strict read interns every string value once per distinct text.
        var homes = new HashSet<string>(ReferenceEqualityComparer.Instance);
        foreach (var c in loaded.Citizens) homes.Add(c.HomeId);
        Assert.Equal(loaded.Citizens.Select(c => c.HomeId).Distinct(StringComparer.Ordinal).Count(), homes.Count);

        // Capture, validation and encoding are timed separately; the phases account for the whole write.
        foreach (var phase in new[] { SaveCostPhase.Synchronize, SaveCostPhase.Capture, SaveCostPhase.Validate, SaveCostPhase.Encode })
            Assert.True(cost.Covered(phase), phase + " not timed");
        Assert.Equal(cost.TotalTicks, Enum.GetValues<SaveCostPhase>().Sum(p => cost.PhaseTicks(p)));
        Assert.Equal(payload.Length, cost.PayloadCharacters);
        Assert.Equal(System.Text.Encoding.UTF8.GetByteCount(payload), cost.PayloadUtf8Bytes);
        output.WriteLine($"{fixture}: {payload.Length} chars; capture {cost.Milliseconds(SaveCostPhase.Capture):F2} ms, "
            + $"validate {cost.Milliseconds(SaveCostPhase.Validate):F2} ms, encode {cost.Milliseconds(SaveCostPhase.Encode):F2} ms");
    }

    [Fact]
    public void AFreshCityIsPublishedInTheBodyFormatAndReopens()
    {
        var state = CurrentSchemaOnlyTests.City();
        var data = new Dictionary<string, string>(StringComparer.Ordinal) { ["other"] = "kept" };
        var session = SaveSession.Open(data, state.PopulationKey);
        Assert.Equal(SaveReadStatus.Missing, session.InitialReadResult.Status);
        Assert.True(session.WriteTo(data, state).Success);
        Assert.StartsWith(SaveCodec.BodyPrefix, data[SaveStore.StateKey]);
        var reopened = SaveSession.Open(data, state.PopulationKey);
        Assert.True(reopened.InitialReadResult.Status == SaveReadStatus.Loaded, reopened.InitialReadResult.Error);
        Assert.Equal("kept", data["other"]);
    }

    // ---- Refusal without overwrite ----

    public static IEnumerable<object[]> Corruptions() => new[]
    {
        "truncated by one character", "truncated in the middle", "trailing content", "unknown enum", "broken token",
        "duplicate field", "empty",
    }.Select(c => new object[] { c });

    [Theory]
    [MemberData(nameof(Corruptions))]
    public void CorruptAndTruncatedPayloadsAreRefusedAndKept(string corruption)
    {
        string valid = SaveCodec.Write(ChangeJournalTests.Fixture(5150));
        string payload = corruption switch
        {
            "truncated by one character" => valid.Substring(0, valid.Length - 1),
            "truncated in the middle" => valid.Substring(0, valid.Length / 2),
            "trailing content" => valid + "{}",
            "unknown enum" => Once(valid, "\"Phase\":0,", "\"Phase\":99,"),
            "broken token" => Once(valid, "\"Minute\":", "\"Minute\":-x"),
            "duplicate field" => valid.Replace(SaveCodec.BodyPrefix, "{\"Schema\":1,\"Schema\":1,\"Capabilities\":"),
            _ => "",
        };
        Assert.NotEqual(valid, payload);
        RefusedAndKept(payload, SaveReadStatus.Invalid);
    }

    [Theory]
    [InlineData(0)] [InlineData(2)] [InlineData(36)] [InlineData(37)]
    public void AnotherSchemaNumberIsRefusedWithStartANewCityAndKept(int schema)
    {
        string payload = SaveCodec.Write(ChangeJournalTests.Fixture(5150))
            .Replace(SaveCodec.BodyPrefix, "{\"Schema\":" + schema + ",\"Capabilities\":");
        Assert.Equal(schema, SaveCodec.DevelopmentSchemaOf(payload));
        var read = RefusedAndKept(payload, SaveReadStatus.DevelopmentSchema);
        Assert.Equal(schema, read.SourceSchema);
        Assert.Equal(SaveCodec.DevelopmentSchemaMessage + " (schema " + schema + ")", read.Error);
        Assert.Contains("start a new city", Assert.Throws<SaveValidationException>(() => SaveCodec.RequireExecutableCapabilities(payload)).Message);
    }

    [Fact]
    public void ARetiredCompactCityIsRefusedWithStartANewCityAndKept()
    {
        string payload = RetiredCompactMinimal();
        Assert.StartsWith("{\"Schema\":1,\"f\":\"pcc-c\",", payload);
        // The native authority bootstrap treats it as an older development city, so it takes no food ownership.
        Assert.Equal(CityState.CurrentSchema, SaveCodec.DevelopmentSchemaOf(payload));
        var read = RefusedAndKept(payload, SaveReadStatus.DevelopmentSchema);
        Assert.Contains("start a new city", read.Error);
        Assert.DoesNotContain("Invalid", read.Error);
        Assert.Contains("start a new city", Assert.Throws<SaveValidationException>(() => SaveCodec.RequireExecutableCapabilities(payload)).Message);
    }

    [Fact]
    public void OneCharacterOverTheInputCapIsRefusedAndKept()
    {
        string payload = SaveCodec.BodyPrefix + new string('0', SaveCodec.MaxInputCharacters + 1 - SaveCodec.BodyPrefix.Length);
        var read = RefusedAndKept(payload, SaveReadStatus.Invalid);
        Assert.Contains("input size limit", read.Error);
    }

    // ---- One capture ----

    /// <summary>
    /// Inside the save callback the city is captured once and encoded from that capture on the same thread. A sale that
    /// paid before the callback is in both the wallet the payload carries and the native receipt beside it; a sale after
    /// the callback returned is in neither: the published payload never changes and its wallet still matches the receipts
    /// the callback published with it.
    /// </summary>
    [Fact]
    public void WalletsAndNativeReceiptsComeFromOneCapture()
    {
        var state = F7cFixtures.Build("rush-957x2").State;
        var data = new Dictionary<string, string>(StringComparer.Ordinal) { ["rlo:state"] = "owned by RLO" };
        var session = SaveSession.Open(data, state.PopulationKey);
        var customer = state.Food!.Residents.First(m => m.Budget != null);
        var wallet = customer.Budget!;
        Money price = Money.FromCents(825);

        var paid = Sale(data, customer.ResidentId, 1, wallet, price);
        Money balance = wallet.Balance, spent = wallet.PreparedFoodSpent;
        Assert.True(session.WriteTo(data, state).Success);
        string published = data[SaveStore.StateKey];
        var receipts = data.Keys.Where(FoodSaleReceipts.IsReceiptKey).ToList();
        var later = Sale(data, customer.ResidentId, 2, wallet, price);

        Assert.Same(published, data[SaveStore.StateKey]);
        Assert.Equal(new[] { FoodSaleReceipts.Prefix + paid.IntentId }, receipts);
        Assert.Equal(FoodReceiptStatus.Paid, FoodSaleReceipts.ReadStatus(data, paid));
        Assert.Equal(FoodReceiptStatus.Paid, FoodSaleReceipts.ReadStatus(data, later));
        var saved = Wallet(published, customer.ResidentId);
        Assert.Equal(balance, saved.Balance);
        Assert.Equal(spent, saved.PreparedFoodSpent);
        Assert.Equal(wallet.Balance + price, saved.Balance);

        // The same holds for an explicit snapshot: encoding reads the capture, never the live wallet.
        var snapshot = SaveCodec.Capture(state);
        Money captured = wallet.Balance;
        Sale(data, customer.ResidentId, 3, wallet, price);
        Assert.Equal(captured, Wallet(SaveCodec.Encode(snapshot), customer.ResidentId).Balance);
        Assert.Equal(wallet.Balance + price, captured);
    }

    /// <summary>One paid native sale: the receipt is confirmed in the native save data and the wallet pays the price.</summary>
    private static ResidentMeal Sale(IDictionary<string, string> data, string resident, int day, ResidentBudget wallet, Money price)
    {
        var meal = new ResidentMeal
        {
            ResidentId = resident, Day = day, Kind = MealKind.Lunch,
            BusinessId = "business:shop:round-trip", ProductId = FoodProducts.HotDog, Price = price,
        };
        FoodSaleReceipts.Begin(data, meal);
        FoodSaleReceipts.Confirm(data, meal);
        wallet.Balance -= price; wallet.PreparedFoodSpent += price;
        return meal;
    }

    private static ResidentBudget Wallet(string payload, string resident)
    {
        var read = SaveCodec.Read(payload);
        Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
        return read.State!.Food!.Residents.Single(m => m.ResidentId == resident).Budget!;
    }

    private static List<(string, long, long)> Wallets(CityState state) => state.Food!.Residents
        .Where(m => m.Budget != null).Select(m => (m.ResidentId, m.Budget!.Balance.Units, m.Budget.PreparedFoodSpent.Units)).ToList();

    private static string Once(string text, string find, string replace)
    {
        int at = text.IndexOf(find, StringComparison.Ordinal);
        Assert.True(at >= 0, "missing " + find);
        return text.Substring(0, at) + replace + text.Substring(at + find.Length);
    }

    /// <summary>The product refuses <paramref name="payload"/> through the codec, the store and the session, and keeps the
    /// payload and every other key unchanged.</summary>
    private static SaveReadResult RefusedAndKept(string payload, SaveReadStatus status)
    {
        var replacement = CurrentSchemaOnlyTests.City();
        var data = new Dictionary<string, string>(StringComparer.Ordinal)
        { [SaveStore.StateKey] = payload, ["rlo:state"] = "owned by RLO", [ReceiptKey] = "pending", ["othermod:state"] = "foreign" };
        var before = new Dictionary<string, string>(data);
        var read = SaveCodec.Read(payload);
        Assert.True(read.Status == status, read.Status + " " + read.Error);
        Assert.Null(read.State);
        Assert.Equal(status, SaveStore.ReadFrom(data, replacement.PopulationKey).Status);
        var session = SaveSession.Open(data, replacement.PopulationKey);
        Assert.Equal(status, session.InitialReadResult.Status);
        Assert.False(session.WriteTo(data, replacement).Success);
        Assert.False(SaveStore.WriteTo(data, replacement).Success);
        Assert.Equal(before, data);
        Assert.Same(payload, data[SaveStore.StateKey]);
        return read;
    }
}
