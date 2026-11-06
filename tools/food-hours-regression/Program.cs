using System;
using System.Collections.Generic;
using System.Linq;
using PersistentCity.Core;
using PersistentCity.Runtime;

var place = new Place { Id = "shop", Kind = PlaceKind.Shop, OpenMinute = 480, CloseMinute = 1080 };
var reg = new Entities.BuildingRegistration { scheduleDays = new() { new Entities.ScheduleDay { day = 1, isOpen = true,
    openingHourSlots = new() { new Entities.Slot { startingHour = 8, endingHour = 18 } } } } };
var market = new NativeFoodMarket(place, reg);
market.RefreshRoutineHours(600);
var hours = place.WeeklyHours;
Require(!DailyRoutine.Open(place, 1440 + 19 * 60), "Initial hours should close at 18:00");
market.RefreshRoutineHours(601);
Require(ReferenceEquals(hours, place.WeeklyHours), "Unchanged hours should not rebuild the saved schedule");
reg.scheduleDays[0].openingHourSlots[0].endingHour = 22;
market.RefreshRoutineHours(602);
Require(DailyRoutine.Open(place, 1440 + 19 * 60), "A native extension must affect the same place used by routing/admission");
Require(place.Id == "shop", "Changing hours cannot change place identity");
reg.temporarilyClosed = true; market.RefreshRoutineHours(602, true);
Require(!DailyRoutine.NextOpen(place, 1440 + 602, 1440 + 1400).HasValue, "A native closure must not leave fallback daytime hours");
reg.temporarilyClosed = false; market.RefreshRoutineHours(603);
Require(DailyRoutine.Open(place, 1440 + 19 * 60), "Reopening must restore the current native schedule");
SaveGameManager.Current = new object(); reg.scheduleDays[0].openingHourSlots[0].endingHour = 12;
market.RefreshRoutineHours(604, true);
Require(DailyRoutine.Open(place, 1440 + 19 * 60), "A market from another save cannot rewrite this place");
Console.WriteLine("PASS live enrolled-shop hours: extension, closure, reopening, stable identity, unchanged schedule allocation and save ownership.");
foreach (string type in new[] { "ba:businesstype_school", "ba:businesstype_gym", "ba:businesstype_nightclub", "ba:businesstype_cinema", "ba:businesstype_theater" })
{
    reg.businessTypeName = type;
    CityManager.Instance.cityBuildingControllers = new() { new CityBuildingController { buildingRegistration = reg } };
    var places = GamePlaces.Read();
    var dormant = places.Single(p => p.Kind == PlaceKind.Shop);
    Require(dormant.Id == "address:Test:7:shop" && dormant.WeeklyHours.Count == 0 && dormant.OpenMinute == dormant.CloseMinute,
        "Established retail study/leisure must retain a closed stable Shop entrance");
    Require(places.Count(p => p.Kind == PlaceKind.Work) == 1 && places.Count(p => p.Kind == PlaceKind.Study || p.Kind == PlaceKind.Leisure) == 1,
        "Dormant Shop must not replace or duplicate original work/study/leisure roles");
    var convertedMarket = new NativeFoodMarket(dormant, reg);
    convertedMarket.RefreshRoutineHours(700, true);
    Require(dormant.WeeklyHours.Count == 0, "Native hourly refresh must not reopen dormant Shop for study/leisure");
    reg.businessTypeName = "ba:businesstype_coffeeshop";
    convertedMarket.RefreshRoutineHours(701, true);
    Require(dormant.Id == "address:Test:7:shop" && dormant.WeeklyHours.Count == 1,
        "Food conversion must activate the same saved Shop entrance without a new identity");
    reg.businessTypeName = type; convertedMarket.RefreshRoutineHours(702, true);
    Require(dormant.WeeklyHours.Count == 0, "Returning to study/leisure must close duplicate shop activity again");
}
// F5a census sink: same places, one fact record per established business, field reads only.
foreach (var (type, player, rival) in new[] { ("ba:businesstype_school", false, ""), ("ba:businesstype_coffeeshop", true, ""), ("ba:businesstype_coffeeshop", false, "rival-a") })
{
    reg.businessTypeName = type; reg.RentedByPlayer = player; reg.businessOwnerRivalId = rival;
    reg.aiEmployees = rival.Length > 0 ? new List<object> { new(), new(), new() } : null;
    string before = Fingerprint(GamePlaces.Read());
    var census = new List<CensusWorkplace>();
    string withCensus = Fingerprint(GamePlaces.Read(census));
    Require(before == withCensus, "The census sink must not change any returned place");
    Require(census.Count == 1 && census[0].WorkPlaceId == "address:Test:7:work" && census[0].AreaSquareMetres == 30,
        "Census records the business area from the same catalogue read as homes");
    Require(census[0].StudyPlaceId == (type == "ba:businesstype_school" ? "address:Test:7:study" : ""), "Only a school pairs a study place");
    Require(census[0].PlayerOwned == player, "Census flags player-owned businesses");
    Require(census[0].NativeStaff == (rival.Length > 0 ? 3 : -1), "Rival AI staff list length is informational; unknown otherwise");
    Require(reg.RentedByPlayer == player && reg.businessTypeName == type && (reg.aiEmployees?.Count ?? 0) == (rival.Length > 0 ? 3 : 0),
        "Census read must leave the native registration unchanged");
}
reg.businessTypeName = "ba:businesstype_coffeeshop"; reg.RentedByPlayer = false; reg.businessOwnerRivalId = null; reg.aiEmployees = null;
Console.WriteLine("PASS actual GamePlaces census sink: identical places, area/school/player/rival-staff facts, unchanged registration.");
reg.HasEstablishedBusiness = false;
var vacant = GamePlaces.Read();
Require(vacant.Count == 1 && vacant[0].Kind == PlaceKind.Shop && vacant[0].WeeklyHours.Count == 0,
    "Vacant retail keeps dormant entrance without inventing a Work assignment");
Console.WriteLine("PASS actual GamePlaces + NativeFoodHours: five retail study/leisure roles stay dormant, convert in place, return dormant; vacant retail has no fake Work role.");
static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
static string Fingerprint(List<Place> places) => string.Join("|", places.Select(p => p.Id + ":" + p.Kind + ":" + p.Capacity + ":" + p.WeeklyHours.Count + ":" + p.EvidenceSource));
public static class SaveGameManager { public static object Current = new object(); }
namespace Entities
{
    public sealed class BuildingRegistration
    {
        public bool temporarilyClosed, AvailableForRent; public bool HasEstablishedBusiness = true;
        public string businessTypeName = "ba:businesstype_coffeeshop", StreetName = "Test", BusinessName = "Test shop";
        public int StreetNumber = 7; public List<ScheduleDay> scheduleDays = new();
        public bool RentedByPlayer; public string? businessOwnerRivalId; public List<object>? aiEmployees;
    }
    public sealed class ScheduleDay { public int day; public bool isOpen; public List<Slot> openingHourSlots = new(); }
    public sealed class Slot { public int startingHour, endingHour; }
}
namespace PersistentCity.Runtime
{
    public sealed partial class NativeFoodMarket
    {
        private readonly object save = SaveGameManager.Current;
        private readonly Dictionary<string, Entities.BuildingRegistration> registrations = new();
        public NativeFoodMarket(Place place, Entities.BuildingRegistration registration)
        { routinePlaces.Add(place.Id, place); registrations.Add(place.Id, registration); }
    }
}
