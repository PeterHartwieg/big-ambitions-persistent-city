using PersistentCity.Core;
using PersistentCity.Runtime;

static partial class Program
{
    static (CitySimulator City, NativeFoodMarket Market) PlayerControl(IEnumerable<CityBuildingController> fixtures)
    {
        var state=State(1); state.Places.Add(new Place {Id="player-shop",Kind=PlaceKind.Shop,Position=new(10,0,0)});
        state.Citizens[0].NextEventMinute=state.Citizens[0].ActivityUntilMinute=2870;
        state.Food=new FoodCommerceState {Shops=new() {new FoodScope {BusinessId="player",PlaceId="player-shop",ProductId=FoodProducts.Croissant}}};
        var market=new NativeFoodMarket(fixtures) {Players=new[] {new FoodBusiness {Id="player",PlaceId="player-shop",MealsPerHour=60,
            Hours=Enumerable.Range(0,7).Select(d=>new OpeningWindow {Day=d,FromMinute=0,ToMinute=1440}).ToList(),
            Offers=new() {new FoodOffer {ProductId=FoodProducts.Croissant,Price=Money.FromCents(100),Units=100}}}}};
        var city=new CitySimulator(state,new Paths());city.AttachFoodMarket(market);
        state.Food.ChoiceStartsDay=state.Food.AwarenessStartsDay=state.Food.RivalStartsDay=1;
        return (city,market);
    }
    static IEnumerable<CityBuildingController> ThrowingControllers()
    { yield return Fixtures(1)[0]; throw new InvalidOperationException("Injected rival metadata enumeration failure"); }
    static void ReviewCase(string name)
    {
        SaveGameManager.Current=new();
        if(name=="review-non-food")
        {
            var fixtures=Fixtures(1024);
            foreach(var c in fixtures) c.buildingRegistration.retailPrices[0].itemName="clothing";
            fixtures[1].buildingRegistration.retailPrices[0].itemName=null!;
            fixtures[2].buildingRegistration.retailPrices[0].itemName="";
            var (city,market)=PlayerControl(fixtures);
            foreach(var c in fixtures.Skip(1)) city.State.Places.Add(new Place {Id="address:Test%20Street:"+c.buildingRegistration.StreetNumber+":shop",Kind=PlaceKind.Shop,Position=new(1,0,0)});
            var meal=city.State.Food!.Residents.Single();var dump=city.InspectFoodAwareness(meal.ResidentId)!;
            Check(dump.Shortlist.SequenceEqual(new[]{"player"}),"non-food rivals cannot displace the player shortlist");
            Check(!market.RivalScopes.Any(s=>s!=null),"non-food rivals publish no food scope");
            Check(market.RivalDetailReads==0,"non-food rivals consume zero detail reads");
            Advance(city,meal.DesiredAt+10);Check(market.PlayerBuys==1,"player commerce survives non-food rivals");
            long revision=market.RivalRevision;
            fixtures[0].buildingRegistration.retailPrices[0].itemName=FoodProducts.Croissant;
            Drain(market,city.State.Minute+60);
            Check(market.RivalRevision!=revision&&market.RivalScopes.Count(s=>s!=null)==1,"in-place advertised food addition revises metadata");
            var scope=market.RivalScopes.Single(s=>s!=null);Check(market.IsRivalCandidate(scope),"new food advertiser is admitted");
            revision=market.RivalRevision;fixtures[0].buildingRegistration.retailPrices[0].itemName="electronics";
            Drain(market,city.State.Minute+120);
            Check(market.RivalRevision!=revision&&!market.IsRivalCandidate(scope)&&!market.RivalScopes.Any(s=>s!=null),"food withdrawal removes metadata authority");
            market.CloseRivals();return;
        }
        if(name=="review-refresh-failure")
        {
            var (city,market)=PlayerControl(ThrowingControllers());
            var warnings=new List<string>();market.rivalWarning=warnings.Add;
            var meal=city.State.Food!.Residents.Single();Advance(city,meal.DesiredAt+10);
            Check(market.PlayerBuys==1,"player checkout continues after rival catalogue failure");
            Check(!market.NeedsRivalRefresh(city.State.Minute+1000)&&!market.RivalScopes.Any(s=>s!=null),"failed rival source stays unavailable");
            Check(market.ReadRival("unknown")==null,"failed rival source never supplies a target");
            market.ContinueRivalRefresh(city.State.Minute+1000);
            Check(warnings.Count==1&&market.RivalUnavailable.Length>0,"failure logs once with no retry");
            Advance(city,city.State.Minute+20);Check(city.State.Citizens[0].MealOuting==null,"player control returns after rival failure");
            market.CloseRivals();return;
        }
        if(name=="review-saved-failure")
        {
            var fixtures=Fixtures(1);var state=State(1);state.Food=new();
            state.Citizens[0].NextEventMinute=state.Citizens[0].ActivityUntilMinute=2870;
            var market=new NativeFoodMarket(fixtures);var city=new CitySimulator(state,new Paths());city.AttachFoodMarket(market);
            state.Food.ChoiceStartsDay=state.Food.AwarenessStartsDay=state.Food.RivalStartsDay=1;
            var meal=state.Food.Residents.Single();Advance(city,meal.DesiredAt);Check(meal.Status==MealStatus.Travelling,"rival committed before failure");
            var read=PersistentCity.Persistence.SaveCodec.Read(PersistentCity.Persistence.SaveCodec.Write(state));
            Check(read.Status==PersistentCity.Persistence.SaveReadStatus.Loaded,read.Error);
            market.CloseRivals();market=new NativeFoodMarket(ThrowingControllers());city=new CitySimulator(read.State!,new Paths());city.AttachFoodMarket(market);
            Advance(city,city.State.Minute+30);meal=city.State.Food!.Residents.Single();
            Check(meal.Status==MealStatus.Missed&&meal.Purchases==0&&meal.LegacyExternalFoodSpent==Money.Zero,"cold saved rival refuses without debit after catalogue failure");
            Check(city.State.Citizens[0].MealOuting==null&&market.NativeWrites==0,"failed saved rival keeps its safe return");
            market.CloseRivals();return;
        }
        throw new ArgumentException("Unknown review case: "+name);
    }
}
