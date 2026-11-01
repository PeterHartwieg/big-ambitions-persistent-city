using PersistentCity.Core;

namespace PersistentCity.Runtime;

// Only the unrelated native player adapter is replaced. D2's actual scheduler-facing
// adapter and D1's actual read-only reader are compiled unchanged against field doubles.
public sealed partial class NativeFoodMarket : IFoodMarket, IFoodCandidateMarket, IFoodAwarenessRevision
{
    readonly CityBuildingController[] controllers;
    readonly RivalFoodSettings rivalSettings;
    public NativeFoodMarket(CityBuildingController[] controllers, RivalFoodSettings? settings = null)
    { this.controllers = controllers; rivalSettings = settings ?? new(); }
    public double EarliestMinute => 0;
    public long AwarenessRevision => RivalRevision;
    public int NativeWrites;
    public IReadOnlyList<FoodBusiness> Read(double at) => throw new Exception("Forbidden all-shop/native supply read");
    public FoodSale Buy(ResidentMeal meal, double at) { NativeWrites++; throw new Exception("Forbidden rival checkout"); }
    public bool IsCandidate(FoodScope scope) => false;
    public FoodBusiness? CaptureBusiness(string id, double at) => throw new Exception("Forbidden rival native capture");
    sealed class Batch : IDisposable { public void Dispose() { } }
    public IDisposable BeginCapture(double minute) => new Batch();
}
