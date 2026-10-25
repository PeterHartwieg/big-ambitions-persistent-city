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
reg.HasEstablishedBusiness = false;
var vacant = GamePlaces.Read();
Require(vacant.Count == 1 && vacant[0].Kind == PlaceKind.Shop && vacant[0].WeeklyHours.Count == 0,
    "Vacant retail keeps dormant entrance without inventing a Work assignment");
Console.WriteLine("PASS actual GamePlaces + NativeFoodHours: five retail study/leisure roles stay dormant, convert in place, return dormant; vacant retail has no fake Work role.");
static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
public static class SaveGameManager { public static object Current = new object(); }
namespace Entities
{
    public sealed class BuildingRegistration
    {
        public bool temporarilyClosed, AvailableForRent; public bool HasEstablishedBusiness = true;
        public string businessTypeName = "ba:businesstype_coffeeshop", StreetName = "Test", BusinessName = "Test shop";
        public int StreetNumber = 7; public List<ScheduleDay> scheduleDays = new();
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
