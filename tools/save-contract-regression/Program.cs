using System;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using PersistentCity.Core;
using PersistentCity.Persistence;
using PersistentCity.Runtime;
static class Program
{
    private static CityState City(int count, int seed = 11)
    {
        var identities = Enumerable.Range(0, count).Select(i => new CitizenIdentity
        {
            Id = "rlo:test:c" + i.ToString("D3"), Name = "Citizen " + i, Age = 20 + i % 50, District = i % 2 == 0 ? "a" : "b",
            Persona = (Persona)(i % 8), AppearanceSeed = i + 1
        });
        var places = new[]
        {
            new Place { Id = "home-a", Name = "Home A", District = "a", Kind = PlaceKind.Home, Capacity = count, Position = new(0, 0, 0) },
            new Place { Id = "home-b", Name = "Home B", District = "b", Kind = PlaceKind.Home, Capacity = count, Position = new(900, 0, 0) },
            new Place { Id = "work-1", Name = "Work 1", District = "a", Kind = PlaceKind.Work, Capacity = count / 3 + 1, Position = new(300, 0, 400) },
            new Place { Id = "work-2", Name = "Work 2", District = "b", Kind = PlaceKind.Work, Capacity = count / 3 + 1, Position = new(1500, 0, -200) },
            new Place { Id = "work-3", Name = "Work 3", District = "a", Kind = PlaceKind.Work, Capacity = count / 3 + 1, Position = new(-600, 0, 900) },
            new Place { Id = "school", Name = "School", District = "a", Kind = PlaceKind.Study, Capacity = count, Position = new(200, 0, -700) },
            new Place { Id = "shop-1", Name = "Shop 1", District = "a", Kind = PlaceKind.Shop, Position = new(150, 0, 120), OpenMinute = 480, CloseMinute = 1200 },
            new Place { Id = "shop-2", Name = "Shop 2", District = "b", Kind = PlaceKind.Shop, Position = new(1000, 0, 300), OpenMinute = 540, CloseMinute = 1260 },
            new Place { Id = "shop-3", Name = "Shop 3", District = "a", Kind = PlaceKind.Shop, Position = new(-400, 0, -300), OpenMinute = 600, CloseMinute = 1080 },
            new Place { Id = "leisure-1", Name = "Leisure 1", District = "a", Kind = PlaceKind.Leisure, Position = new(60, 0, 250), OpenMinute = 600, CloseMinute = 1380 },
            new Place { Id = "leisure-2", Name = "Leisure 2", District = "b", Kind = PlaceKind.Leisure, Position = new(1200, 0, 600), OpenMinute = 660, CloseMinute = 1320 },
            new Place { Id = "leisure-3", Name = "Leisure 3", District = "a", Kind = PlaceKind.Leisure, Position = new(-200, 0, 800), OpenMinute = 720, CloseMinute = 1440 }
        };
        var city = Population.Create(identities, places, "resumable-test", "0.40.4", seed, 1440);
        // Some workers start unemployed so the daily employment check probes workplaces during the run.
        for (int i = 0; i < city.Citizens.Count; i += 5) city.Citizens[i].DayPlaceId = "";
        return city;
    }


    static void Main()
    {
        var city = City(120);
        string expected = SaveCodec.Write(city);
        foreach (string mode in new[] { "off", "on" })
        {
            var settings = JsonConvert.DeserializeObject<PrototypeSettings>("{\"InteriorCityView\":\"" + mode + "\"}")!;
            var view = new InteriorViewState();
            var entry = new object();
            foreach (int cause in new[] { 0, 1, 2, 4, 8, 16 })
            {
                view.Refresh(settings.InteriorCityView == "on", new TravelVisibility(cause == 1, cause == 2, cause == 4, true, cause == 8, cause == 16),
                    entry, cause * 3, () => InteriorViewEligibility.Check(true, false, new Position(11, 0, 22), 0));
                view.Suspend(); // save boundary: no runtime field enters the payload
                if (SaveCodec.Write(city) != expected || SaveCodec.Write(SaveCodec.Read(expected).State!) != expected)
                    throw new Exception("F0 runtime view changed save bytes in mode " + mode);
            }
        }
        Console.WriteLine("PASS F0 off/on runtime state + hidden/save/reload payloads byte-identical (12 transitions); no persistence source changes.");
        SaveCodec.Read(expected);
        long start = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 20; i++)
            if (SaveCodec.Write(city) != expected) throw new Exception("write changed bytes");
        long writeBytes = GC.GetAllocatedBytesForCurrentThread() - start;
        start = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 20; i++)
            if (SaveCodec.Read(expected).Status != SaveReadStatus.Loaded) throw new Exception("read failed");
        long readBytes = GC.GetAllocatedBytesForCurrentThread() - start;
        var refusals = new List<string>();
        foreach (string payload in new[] { "{}", "{\"Schema\":999}", "{\"Schema\":13,", expected.Replace("\"Citizens\":", "\"UnknownCitizens\":") })
        {
            var result = SaveCodec.Read(payload);
            refusals.Add(result.Status + ":" + result.Error);
        }
        Console.WriteLine(JsonConvert.SerializeObject(new { digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(expected))),
            length = expected.Length, refusals, writeBytes, readBytes }));
    }
}
