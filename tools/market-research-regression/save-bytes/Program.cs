using System.IO;
using PersistentCity.Core;
using PersistentCity.Persistence;

// Same fixture source compiled against each head, then compared as literal UTF-8 bytes.
var state=EconomyScenario.Create(40,11);
Directory.CreateDirectory(args[0]);
File.WriteAllText(Path.Combine(args[0],"population.json"),SaveCodec.Write(state));
state.Food=EconomyScenario.Food();
var sim=new CitySimulator(state,new NoRoutes());
sim.AttachFoodMarket(new NoFood()); sim.EnableHouseholdFinances();
File.WriteAllText(Path.Combine(args[0],"food.json"),SaveCodec.Write(state));

sealed class NoRoutes : IRoutePlanner
{
    public Route? Plan(Place a,Place b,double at,TravelPreferences p) => throw new System.Exception("Unexpected route request");
}
sealed class NoFood : IFoodMarket
{
    public double EarliestMinute => 0;
    public System.Collections.Generic.IReadOnlyList<FoodBusiness> Read(double at) => System.Array.Empty<FoodBusiness>();
    public FoodSale Buy(ResidentMeal meal,double at) => throw new System.Exception("Unexpected checkout");
}
