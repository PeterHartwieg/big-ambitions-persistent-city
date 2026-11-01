using PersistentCity.Core;

namespace PersistentCity.Runtime;

// Only the unrelated native player adapter is replaced. D2's actual scheduler-facing
// adapter and D1's actual read-only reader are compiled unchanged against field doubles.
public sealed partial class NativeFoodMarket : IFoodMarket, IFoodCandidateMarket, IFoodAwarenessRevision, ILocalFoodMarket
{
    readonly IEnumerable<CityBuildingController> controllers;
    readonly RivalFoodSettings rivalSettings;
    internal Action<string>? rivalWarning;
    public NativeFoodMarket(IEnumerable<CityBuildingController> controllers, RivalFoodSettings? settings = null)
    { this.controllers = controllers; rivalSettings = settings ?? new(); }
    public double EarliestMinute => 0;
    public long AwarenessRevision => RivalRevision;
    public int NativeWrites;
    public FoodBusiness[] Players = Array.Empty<FoodBusiness>();
    public int PlayerBuys;
    public IReadOnlyList<FoodBusiness> Read(double at) => throw new Exception("Forbidden all-shop/native supply read");
    public FoodSale Buy(ResidentMeal meal, double at)
    {
        if(meal.TargetKind == FoodAlternativeKind.Player && Players.Any(p=>p.Id==meal.BusinessId))
        { PlayerBuys++; return new FoodSale(true,"Native player control"); }
        NativeWrites++; throw new Exception("Forbidden rival checkout");
    }
    public bool IsCandidate(FoodScope scope) => Players.Any(p=>p.Id==scope.BusinessId);
    public FoodBusiness? CaptureBusiness(string id, double at) => Players.SingleOrDefault(p=>p.Id==id)
        ?? throw new Exception("Forbidden rival native capture");
    public FoodBusiness? ReadBusiness(string id, double at) => CaptureBusiness(id,at);
    public IReadOnlyList<FoodBusiness> ReadLocal(Place origin, double minute, double walkingToleranceMetres, int maximumCandidates)
        => throw new Exception("Forbidden all-shop read");
    sealed class Batch : IDisposable { public void Dispose() { } }
    public IDisposable BeginCapture(double minute) => new Batch();
}
