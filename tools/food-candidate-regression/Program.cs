using System.Diagnostics;
using Newtonsoft.Json;
using PersistentCity.Core;
using PersistentCity.Runtime;
using Entities;

static class Program
{
    static void Require(bool condition,string message) {if(!condition) throw new Exception(message);}
    static void Main()
    {
        foreach(int frames in new[]{0,1,2,5})
        foreach(bool reload in new[]{false,true})
        {
            SaveGameManager.Current=new NativeSave();NativeFoodOwnership.Reset();
            var controllers=Enumerable.Range(0,131).Select(i=>new CityBuildingController {buildingRegistration=new BuildingRegistration {StreetNumber=i}}).ToArray();
            var state=new FoodCommerceState();var market=new NativeFoodMarket(state,controllers);
            // Equivalent to AfterNativeTick's final drained transition, after its last metadata refresh.
            SaveGameManager.Current.Hour++;
            foreach(var c in controllers)
            {
                var reg=c.buildingRegistration!;var row=NativeFoodOwnership.Find(reg)!;
                Require(row.Boundary(NativeFoodOwnership.Hour,true,false,true),"acquisition boundary");market.ActivateScope(row,reg);
            }
            var last=state.Shops.Single(s=>s.PlaceId.EndsWith(":130:shop",StringComparison.Ordinal));
            Require(market.IsCandidate(last),"acquisition must publish eligibility without another discovery frame");
            controllers[129].Accessible=false;
            controllers[128].buildingRegistration!.Eligible=false;
            for(int f=0;f<frames;f++) market.RefreshDiscovery();
            if(reload)
            {
                NativeFoodOwnership.Reload();
                state=JsonConvert.DeserializeObject<FoodCommerceState>(JsonConvert.SerializeObject(state))!;
                market=new NativeFoodMarket(state,controllers);
            }
            using(market.BeginCapture(2160))
            {
                var eligible=state.Shops.Where(market.IsCandidate).Select(s=>s.PlaceId).ToArray();
                Require(eligible.Length==129,"withdrawal/access image must be independent of >64-controller cursor and reload");
                Require(!eligible.Any(id=>id.EndsWith(":129:shop",StringComparison.Ordinal)||id.EndsWith(":128:shop",StringComparison.Ordinal)),"withdrawn shops cannot consume sample slots");
                Require(eligible.Contains(last.PlaceId),"newly acquired distant controller remains eligible");
                Require(market.DetailedReads==0,"preparation must not read detailed supply");
            }
        }
        // Exercise the actual capture/hours implementation, with only the native detailed reader stubbed.
        SaveGameManager.Current=new NativeSave();NativeFoodOwnership.Reset();
        var shops=Enumerable.Range(0,131).Select(i=>new CityBuildingController {buildingRegistration=new BuildingRegistration {StreetNumber=i}}).ToArray();
        var food=new FoodCommerceState();var sample=new NativeFoodMarket(food,shops);
        SaveGameManager.Current.Hour++;
        foreach(var c in shops) {var row=NativeFoodOwnership.Find(c.buildingRegistration!)!;row.Boundary(NativeFoodOwnership.Hour,true,false,true);sample.ActivateScope(row,c.buildingRegistration!);}
        var ids=food.Shops.Take(8).Select(s=>s.BusinessId).ToArray();
        long before=sample.HoursRefreshPasses;var oldImage=ids.Select(id=>sample.ReadBusiness(id,2160)).ToArray();long oldPasses=sample.HoursRefreshPasses-before;
        string oldEvidence=JsonConvert.SerializeObject(oldImage);
        before=sample.HoursRefreshPasses;FoodBusiness?[] image;
        using(sample.BeginCapture(2160)) image=ids.Select(id=>sample.CaptureBusiness(id,2160)).ToArray();
        Require(oldPasses==8 && sample.HoursRefreshPasses-before==0,"captured native hours need no catalogue-wide refresh");
        Require(oldEvidence==JsonConvert.SerializeObject(image),"batch must preserve captured evidence");
        int reads=sample.DetailedReads;
        try {using(sample.BeginCapture(2160)) {sample.ThrowRead=true;sample.CaptureBusiness(ids[0],2160);}} catch(InvalidOperationException) {}
        sample.ThrowRead=false;
        using(sample.BeginCapture(2160)) Require(sample.CaptureBusiness(ids[0],2160)!=null,"failed capture must release its batch");
        Require(sample.DetailedReads==reads+2,"only requested detailed reads");
        var oldTimes=new List<double>();var newTimes=new List<double>();
        for(int i=0;i<80;i++)
        {
            void Run(bool batch,List<double> times)
            {
                long start=Stopwatch.GetTimestamp();
                if(batch) {using(sample.BeginCapture(2160)) foreach(string id in ids) sample.CaptureBusiness(id,2160);}
                else foreach(string id in ids) sample.ReadBusiness(id,2160);
                if(i>=16) times.Add(Stopwatch.GetElapsedTime(start).TotalMilliseconds);
            }
            if(i%2==0) {Run(false,oldTimes);Run(true,newTimes);} else {Run(true,newTimes);Run(false,oldTimes);}
        }
        oldTimes.Sort();newTimes.Sort();
        Console.WriteLine(JsonConvert.SerializeObject(new {checks="actual NativeFoodCandidates + NativeFoodHours, synthetic native surfaces",controllers=131,discoveryFrames=new[]{0,1,2,5},reload=true,oldHoursPasses=oldPasses,newHoursPasses=0,oldMedianMs=oldTimes[oldTimes.Count/2],newMedianMs=newTimes[newTimes.Count/2]}));
    }
}

public sealed class NativeSave {public int Day=1,Hour=8,Minute;}
public static class SaveGameManager {public static NativeSave Current=new();}
public sealed class CityBuildingController {public BuildingRegistration? buildingRegistration;public bool Accessible=true;}
namespace Entities
{
    public sealed class BuildingRegistration
    {
        public bool RentedByPlayer=true,Eligible=true,HasEstablishedBusiness=true,temporarilyClosed;
        public string StreetName="Test",businessTypeName="food";public int StreetNumber;
        public List<string> cachedAvailableProducts=new(){FoodProducts.Croissant};
        public List<ScheduleDay> scheduleDays=new(){new() {day=1,isOpen=true,openingHourSlots=new(){new() {startingHour=0,endingHour=24}}}};
    }
    public sealed class ScheduleDay {public int day;public bool isOpen;public List<Slot> openingHourSlots=new();}
    public sealed class Slot {public int startingHour,endingHour;}
}
namespace PersistentCity.Runtime
{
    public static class GamePlaces
    {
        public static bool BuildingAccessible(CityBuildingController c)=>c.Accessible;
        public static bool IsRetailShopActivity(string type)=>type=="food";
    }
    public static class NativeFoodOwnership
    {
        static Dictionary<string,FoodAuthority> rows=new();
        public static long Hour=>SaveGameManager.Current.Day*24L+SaveGameManager.Current.Hour;
        public static void Reset()=>rows.Clear();
        public static void Reload()=>rows=JsonConvert.DeserializeObject<Dictionary<string,FoodAuthority>>(JsonConvert.SerializeObject(rows))!;
        public static FoodAuthority? Find(BuildingRegistration r)=>rows.GetValueOrDefault(NativeFoodMarket.PlaceId(r));
        public static FoodAuthority Observe(BuildingRegistration r,bool eligible)
        {
            string id=NativeFoodMarket.PlaceId(r);
            if(!rows.TryGetValue(id,out var row)) rows[id]=row=new FoodAuthority {PlaceId=id};
            row.Observe("business:"+id,eligible,Hour);return row;
        }
        public static void Retire(string id) {if(rows.TryGetValue(id,out var row)) row.Observe(row.Signature,false,Hour);}
    }
    public sealed partial class NativeFoodMarket
    {
        readonly object save=SaveGameManager.Current;
        readonly FoodCommerceState state;
        readonly Dictionary<string,BuildingRegistration> registrations=new();
        readonly Dictionary<string,CityBuildingController> shopControllers=new();
        readonly Dictionary<string,object> decisionSupply=new();
        long lastOwnershipRefresh=-1;
        public int DetailedReads;public bool ThrowRead;
        public double EarliestMinute=>SaveGameManager.Current.Day*1440+SaveGameManager.Current.Hour*60;
        public static string PlaceId(BuildingRegistration r)=>"address:"+r.StreetName+":"+r.StreetNumber+":shop";
        public static bool Supported(BuildingRegistration r)=>r.RentedByPlayer&&r.Eligible;
        public NativeFoodMarket(FoodCommerceState state,CityBuildingController[] controllers)
        {
            this.state=state;this.controllers=controllers;
            discoveryRegistrations=new BuildingRegistration?[controllers.Length];discoveryStreets=new string?[controllers.Length];
            discoveryNumbers=new int[controllers.Length];discoveryPlaces=new string?[controllers.Length];
            foreach(var c in controllers) if(c.buildingRegistration is {} r) routinePlaces[PlaceId(r)]=new Place {Id=PlaceId(r),Kind=PlaceKind.Shop};
            DiscoverShops(controllers.Length);
        }
        public FoodBusiness? ReadBusiness(string id,double minute)
        {var s=state.Shops.Single(s=>s.BusinessId==id);return ReadSelected(minute,new[]{s.PlaceId}).SingleOrDefault();}
        private IReadOnlyList<FoodBusiness> ReadSelected(double minute,string[] places,bool capture=false)
        {
            RefreshReadMetadata(minute);DetailedReads++;
            if(ThrowRead) throw new InvalidOperationException("Read failed");
            return places.Select(p=>new FoodBusiness {Id=state.Shops.Single(s=>s.PlaceId==p).BusinessId,PlaceId=p,Name=p,MealsPerHour=60,
                Hours=routinePlaces[p].WeeklyHours,Offers=new(){new() {ProductId=FoodProducts.Croissant,Price=Money.FromCents(100),Units=100}}}).ToArray();
        }
    }
}
