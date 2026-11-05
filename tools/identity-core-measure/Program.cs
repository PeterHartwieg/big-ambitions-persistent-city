using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using PersistentCity.Core;

// F5b offline measurement: ownership validator cost at 1,914 / 10,000 / 100,000 persons and derived identity
// cost per lookup (docs/plans/f5-population.md section 4 F5b row). Component timings on this PC only; no game,
// no native mod-work or whole-frame claim. Usage: dotnet run -c Release [-- golden]
internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "golden") { Golden(); return 0; }
        Console.WriteLine("F5b identity core measurement (offline component timings, Release, .NET " + Environment.Version + ")");
        bool ok = true;
        foreach (int persons in new[] { 1914, 10000, 100000 }) ok &= Validator(persons);
        ok &= Identity(400);
        ok &= Identity(5000);
        Intents();
        Console.WriteLine(ok ? "PASS" : "FAIL");
        return ok ? 0 : 1;
    }

    private static CitizenIdentity[] Donors(int count)
    {
        var personas = (Persona[])Enum.GetValues(typeof(Persona));
        return Enumerable.Range(0, count).Select(i => new CitizenIdentity
        {
            Id = "c" + i.ToString(CultureInfo.InvariantCulture), Name = "Given" + i + " Family" + (i * 7 % count),
            Age = 18 + i * 13 % 65, Female = i % 2 == 0, Persona = personas[i % personas.Length], AppearanceSeed = 1000 + i
        }).ToArray();
    }

    // A factor-2 style city: half residents (c<n>/pc:<n>), half visitors (consumer:<anchor>:1); every person has a
    // wallet, a roster record and three intents; residents hold one employment unless the workplace is player-owned.
    private static OwnershipInput City(int count)
    {
        int residents = count / 2;
        var people = new List<PersonFacts>();
        var homes = Enumerable.Range(0, Math.Max(1, residents / 3)).Select(i => "home:" + i.ToString("D6", CultureInfo.InvariantCulture)).ToArray();
        var portals = Enumerable.Range(0, 12).Select(i => "station:" + i.ToString("D2", CultureInfo.InvariantCulture)).ToArray();
        var workplaces = Enumerable.Range(0, 1000).Select(i => new WorkplaceFacts("work:" + i.ToString("D4", CultureInfo.InvariantCulture),
            i % 10 == 0 ? PlaceKind.Study : PlaceKind.Work, i % 97 == 1)).ToArray();
        for (int i = 0; i < residents; i++)
        {
            string id = i < 400 ? "c" + i.ToString(CultureInfo.InvariantCulture) : "pc:" + (i - 399).ToString(CultureInfo.InvariantCulture);
            people.Add(new PersonFacts(id, PersonKind.Resident, homes[i % homes.Length]));
            people.Add(new PersonFacts(FoodRepresentation.ConsumerId(id, 1), PersonKind.Visitor, "",
                new VisitorStay { OriginPortalId = portals[i % portals.Length], Pattern = VisitorPattern.Commuter, FirstDay = 3, NextStayStart = 5000 }));
        }
        people.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
        var jobs = new List<Employment>();
        for (int i = 0; i < people.Count; i++)
        {
            if (people[i].Kind != PersonKind.Resident) continue;
            var place = workplaces[i % workplaces.Length];
            if (place.PlayerOwned) continue;
            jobs.Add(new Employment { PersonId = people[i].Id, WorkplaceId = place.Id, StartDay = 1,
                Kind = place.Kind == PlaceKind.Study ? EmploymentKind.Study : EmploymentKind.Work });
        }
        var kinds = new[] { MealKind.Breakfast, MealKind.Lunch, MealKind.Dinner };
        return new OwnershipInput
        {
            Persons = people.ToArray(), FinancesEnabled = true, FoodEnabled = true,
            Wallets = people.Select(p => new WalletFacts(MoneyOwnerKind.Person, p.Id)).ToArray(),
            Roster = people.Select(p => new RosterFacts(p.Id, p.Id, 0)).ToArray(),
            Intents = people.SelectMany(p => kinds.Select(k => new DemandUnitFacts(MealRhythm.IntentId(p.Id, 9, k), p.Id))).ToArray(),
            Employments = jobs.ToArray(),
            Wages = jobs.Where(j => j.Kind == EmploymentKind.Work).Select(j => new WagePosting(j.PersonId, 9, j.WorkplaceId)).ToArray(),
            HomePlaceIds = homes, PortalIds = portals, Workplaces = workplaces
        };
    }

    private static bool Validator(int count)
    {
        var input = City(count);
        var validator = new OwnershipValidator();
        if (!validator.Validate(input)) { Console.WriteLine("validator fixture invalid: " + validator.Violations[0]); return false; }
        validator.Validate(input);
        var times = new List<double>();
        long bytes = 0;
        int runs = count >= 100000 ? 30 : 100;
        var watch = new Stopwatch();
        for (int r = 0; r < runs; r++)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            watch.Restart();
            bool valid = validator.Validate(input);
            watch.Stop();
            bytes += GC.GetAllocatedBytesForCurrentThread() - before;
            if (!valid) return false;
            times.Add(watch.Elapsed.TotalMilliseconds);
        }
        Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
            "validator persons={0} intents={1} employments={2} wages={3} runs={4} p50={5:F4} ms p95={6:F4} ms max={7:F4} ms warm-alloc={8:F1} B/call",
            count, input.Intents.Count, input.Employments.Count, input.Wages.Count, runs, P(times, .5), P(times, .95), times.Max(), bytes / (double)runs));
        if (bytes != 0) Console.WriteLine("validator allocated " + bytes + " B over " + runs + " warm calls");
        return bytes == 0;
    }

    private static bool Identity(int donorCount)
    {
        var donors = Donors(donorCount);
        var build = Stopwatch.StartNew();
        var policy = IdentityPolicy.V1(donors);
        build.Stop();
        var ids = Enumerable.Range(1, 100000).Select(i => i % 2 == 0 ? PersonIds.Visitor(i) : FoodRepresentation.ConsumerId("pc:" + i, 1)).ToArray();
        long sink = 0;
        foreach (var id in ids.Take(1000)) { sink += policy.Draw(7, id).Persona; sink += policy.Derive(7, id).Age; }
        var watch = new Stopwatch();
        long before = GC.GetAllocatedBytesForCurrentThread();
        watch.Start();
        foreach (var id in ids) sink += policy.Draw(7, id).Appearance;
        watch.Stop();
        long drawBytes = GC.GetAllocatedBytesForCurrentThread() - before;
        double drawNs = watch.Elapsed.TotalMilliseconds * 1e6 / ids.Length;
        before = GC.GetAllocatedBytesForCurrentThread();
        watch.Restart();
        foreach (var id in ids) sink += policy.Derive(7, id).Name.Length;
        watch.Stop();
        long deriveBytes = GC.GetAllocatedBytesForCurrentThread() - before;
        double deriveNs = watch.Elapsed.TotalMilliseconds * 1e6 / ids.Length;
        // Matched alternative: the same three hashes keyed by concatenated strings (StableRandom.Hash(id + purpose)).
        before = GC.GetAllocatedBytesForCurrentThread();
        watch.Restart();
        foreach (var id in ids)
            sink += StableRandom.Hash(id + ":identity:v1:appearance", 7) % (uint)donorCount + StableRandom.Hash(id + ":identity:v1:surname", 7) % (uint)donorCount
                + StableRandom.Hash(id + ":identity:v1:persona", 7) % 3;
        watch.Stop();
        long concatBytes = GC.GetAllocatedBytesForCurrentThread() - before;
        double concatNs = watch.Elapsed.TotalMilliseconds * 1e6 / ids.Length;
        Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
            "identity donors={0} build={1:F4} ms lookups={2} draw={3:F1} ns {4} B/lookup derive={5:F1} ns {6:F1} B/lookup concat-key-hashes={7:F1} ns {8:F1} B/lookup (sink {9})",
            donorCount, build.Elapsed.TotalMilliseconds, ids.Length, drawNs, drawBytes / ids.Length, deriveNs, deriveBytes / (double)ids.Length,
            concatNs, concatBytes / (double)ids.Length, sink % 10));
        if (drawBytes != 0) Console.WriteLine("draw allocated " + drawBytes + " B");
        return drawBytes == 0;
    }

    // Matched alternative for intent ownership: Uri.EscapeDataString per intent vs the allocation-free comparison.
    private static void Intents()
    {
        var input = City(10000);
        var intents = input.Intents;
        int matched = 0;
        var watch = new Stopwatch();
        long before = GC.GetAllocatedBytesForCurrentThread();
        watch.Start();
        for (int i = 0; i < intents.Count; i++)
            if (intents[i].IntentId.StartsWith("food:" + Uri.EscapeDataString(intents[i].OwnerId) + ":", StringComparison.Ordinal)) matched++;
        watch.Stop();
        long escapeBytes = GC.GetAllocatedBytesForCurrentThread() - before;
        double escapeMs = watch.Elapsed.TotalMilliseconds;
        int derived = 0;
        before = GC.GetAllocatedBytesForCurrentThread();
        watch.Restart();
        for (int i = 0; i < intents.Count; i++)
            if (OwnershipValidator.DerivedFrom(intents[i].IntentId, intents[i].OwnerId)) derived++;
        watch.Stop();
        long derivedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
        Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
            "intent-derivation intents={0} escape-per-intent={1:F4} ms {2:F1} B/intent matched={3}; allocation-free={4:F4} ms {5:F1} B/intent matched={6}",
            intents.Count, escapeMs, escapeBytes / (double)intents.Count, matched, watch.Elapsed.TotalMilliseconds, derivedBytes / (double)intents.Count, derived));
    }

    private static double P(List<double> values, double q)
    {
        var sorted = values.OrderBy(v => v).ToArray();
        return sorted[Math.Min(sorted.Length - 1, (int)Math.Ceiling(q * sorted.Length) - 1)];
    }

    private static void Golden()
    {
        var policy = IdentityPolicy.V1(GoldenDonors());
        foreach (int seed in new[] { 0, 12345, -7 })
            foreach (var id in new[] { "consumer:c0:1", "consumer:pc%3A12:2", "pv:1", "pv:42" })
            {
                var d = policy.Derive(seed, id);
                Console.WriteLine("(" + seed + ", \"" + id + "\", \"" + d.Name + "\", " + d.Age + ", " + (d.Female ? "true" : "false")
                    + ", Persona." + d.Persona + ", " + d.AppearanceSeed + "),");
            }
    }

    private static CitizenIdentity[] GoldenDonors() => new[]
    {
        new CitizenIdentity { Id = "c0", Name = "Ava Stone", Age = 34, Female = true, Persona = Persona.Commuter, AppearanceSeed = 101, District = "Midtown" },
        new CitizenIdentity { Id = "c1", Name = "Ben Ortiz", Age = 19, Female = false, Persona = Persona.Student, AppearanceSeed = 102, District = "Garment" },
        new CitizenIdentity { Id = "c2", Name = "Cleo Marsh", Age = 70, Female = true, Persona = Persona.Retiree, AppearanceSeed = 103, District = "Midtown" },
        new CitizenIdentity { Id = "c3", Name = "Dan Hale", Age = 41, Female = false, Persona = Persona.Parent, AppearanceSeed = 104, District = "Kitchen" },
        new CitizenIdentity { Id = "c4", Name = "Eve Park", Age = 28, Female = true, Persona = Persona.Foodie, AppearanceSeed = 105, District = "Murray Hill" },
        new CitizenIdentity { Id = "c5", Name = "Finn Lowe Jr", Age = 52, Female = false, Persona = Persona.NightOwl, AppearanceSeed = 106, District = "Midtown" },
        new CitizenIdentity { Id = "c6", Name = "Gia Russo", Age = 23, Female = true, Persona = Persona.Sporty, AppearanceSeed = 107, District = "Garment" },
        new CitizenIdentity { Id = "c7", Name = "Hal Brook", Age = 66, Female = false, Persona = Persona.Grumpy, AppearanceSeed = 108, District = "Murray Hill" },
    };
}
