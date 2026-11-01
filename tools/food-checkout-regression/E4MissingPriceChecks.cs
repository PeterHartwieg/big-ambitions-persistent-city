using System;
using System.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;

static partial class Program
{
    private sealed class MissingPriceRoutes : IRoutePlanner
    {
        public Route? Plan(Place from,Place to,double minute,TravelPreferences preferences)
            => new Route { Legs={new RouteLeg {From=from.Position,To=to.Position,Mode=TravelMode.Walk,Minutes=1}} };
    }

    static void E4MissingPriceChecks()
    {
        foreach(string unpriced in new[]{Burger,Croissant})
        {
            using var f=new Fixture();
            f.Reg.retailPrices.RemoveAll(price=>price.itemName==unpriced);
            string priced=unpriced==Burger ? Croissant : Burger;
            var observed=f.Market.CaptureBusiness(f.BusinessId,Minute)!;
            Check(f.State.Shops.Single().ContainsProduct(unpriced),"missing-price fixture retains full enrolled scope");
            Check(f.Market.CanServe(f.BusinessId),"missing price retains existing native authority behavior");
            Check(observed.Offers.Count==1 && observed.Offers[0].ProductId==priced && observed.Offers[0].Units>0 && observed.MealsPerHour>0,
                "actual catalogue omits only the unpriced product; remaining stock/service are available");

            var state=Population.Create(new[]{new CitizenIdentity {Id="missing-price",Name="Consumer",Age=30,Persona=Persona.Foodie}},
                new[]{new Place {Id="home",Name="Home",Kind=PlaceKind.Home,Capacity=1},
                    new Place {Id=f.PlaceId,Name="Shop",Kind=PlaceKind.Shop,Position=new Position(1,0,0)}},
                "e4-missing-price","test",957,Minute);
            state.Citizens[0].NextEventMinute=state.Citizens[0].ActivityUntilMinute=Minute+180;
            state.Food=f.State;
            var routes=new MissingPriceRoutes();var city=new CitySimulator(state,routes);city.AttachFoodMarket(f.Market);
            state.Food.ChoiceStartsDay=(int)(Minute/1440);state.Food.AwarenessStartsDay=state.Food.ChoiceStartsDay+1;
            var meal=state.Food.Residents.Single();meal.DesiredAt=meal.DueAt=Minute;meal.ExpiresAt=Minute+120;meal.Allowance=Money.FromCents(1200);
            city=new CitySimulator(state,routes);city.AttachFoodMarket(f.Market);
            city.AdvanceTo(Minute,1);
            Check(meal.Decision!=null && meal.Decision.Shops.Count==1,
                "missing retail price must not remove an owned shop from represented demand");
            Check(meal.Decision!.Shops[0].Offers.Count==1 && meal.Decision.Shops[0].Offers[0].ProductId==priced,
                "capture retains every priced scoped offer and never invents a missing price");
            var restored=SaveCodec.Read(SaveCodec.Write(city.State));Check(restored.Status==SaveReadStatus.Loaded,"partial priced-menu capture reloads");
            city=new CitySimulator(restored.State!,routes);city.AttachFoodMarket(f.Market);
            while(!city.AdvanceTo(Minute+30,128).CaughtUp) { }
            var history=city.State.Food!.History.Single();
            Check(f.Market.NativePurchases==1 && f.Reg.unprocessedCompletedOrders.Count==1 && f.Bags==2,
                "remaining priced offer completes exactly one checkout with one shared bag after reload");
            Check(history.ProductSales.Count==1 && history.ProductSales[0].ProductId==priced && history.Purchases==1,
                "missing-price product creates no sale or history cell");
            Check(f.Stock(unpriced)==(unpriced==Burger ? 7 : 5),"unpriced product stock remains untouched");
        }
    }
}
