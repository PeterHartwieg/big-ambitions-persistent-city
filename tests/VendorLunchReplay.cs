using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;
using PersistentCity.Runtime;

namespace PersistentCity.Tests;

public static class VendorLunchReplay
{
    public const double GameMinutesPerSecond = 30 / 98.6;
    public const long Day = 2;
    public const long OpenAt = Day * 1440 + 9 * 60;
    public const double LunchFrom = Day * 1440 + 11 * 60 + 50, LunchTo = Day * 1440 + 12 * 60 + 10;
    public const double DayFrom = OpenAt - 60, DayTo = (Day + 1) * 1440 + 30;

    public sealed class Step
    {
        public double Total, Prepare, Stops, Advance, Sync, UnitMax, Physical, Budget = double.NaN;
        public int Bodies, PathQueries, Samples, Acquisitions;
        public long PrepareBytes, StopsBytes, AdvanceBytes, PhysicalBytes;
        public int Units, CitizenEvents;
        public bool PrepareReturned, Saturated;
        public string UnitKind = "";
    }

    public sealed class Result
    {
        public readonly List<Step> Frames = new List<Step>();
        public string SaveSha256 = "";
        public double NativeFrom, NativeTo, RealSeconds;
        public long Purchases, VendorPurchases, AllocatedBytes, RouteCalls, LatePathStarts, CarriedPaths, PathSteps;
        public int VendorMeals, Collections;
        public double GameMinutesPerRealSecond => RealSeconds <= 0 ? 0 : (NativeTo - NativeFrom) / RealSeconds;
        public static double Rank(IEnumerable<double> values, double fraction)
        {
            var sorted = values.OrderBy(v => v).ToList();
            return sorted.Count == 0 ? 0 : sorted[Math.Max(0, Math.Min(sorted.Count - 1, (int)Math.Ceiling(fraction * sorted.Count) - 1))];
        }
        public double P95(Func<Step, double> part) => Rank(Frames.Select(part), .95);
        public double Max(Func<Step, double> part) => Frames.Count == 0 ? 0 : Frames.Max(part);
        public double Mean(Func<Step, double> part) => Frames.Count == 0 ? 0 : Frames.Average(part);
        public double SaturatedShare => Frames.Count == 0 ? 0 : Frames.Count(f => f.Saturated) / (double)Frames.Count;
    }

    public sealed class Rig
    {
        public readonly CityState State;
        public readonly CitySimulator City;
        public readonly StreetFoodVendorMarket Vendors;
        public readonly Dictionary<string, string> Receipts = new Dictionary<string, string>(StringComparer.Ordinal);
        public readonly UpdateCityBudget Budget = new UpdateCityBudget();
        public readonly AdvanceAdmission Admission = new AdvanceAdmission();
        public bool Forecasting = true;
        public bool Fits(double elapsed, double budget) => Forecasting ? Admission.AdmitsEvent(elapsed, budget) : elapsed < budget;
        public bool AdmitsPlanning(double elapsed, double budget) => !Forecasting || Admission.AdmitsPlanning(elapsed, budget);
        public bool Opened { get; private set; }
        public double Native;
        internal PhysicalPopulation? Bodies;
        public Position Stand;

        public readonly SlowPlanner Planner;

        public Rig(int vehicles = 2, int residents = 1000, bool resumable = false, bool openInFrames = false)
        {
            Planner = resumable ? new ResumableSlowPlanner() : new SlowPlanner();
            StreetFoodHooks.Reset(); UnityEngine.AI.NavMesh.Reset(); AmbientPedestrians.Reserved.Clear();
            PhysicalPedestrianBody.Unreachable = null; PhysicalPedestrians.VisiblePoint = null; PhysicalPedestrians.AppearOnScreen = false;
            State = RushSpikeReplay.Create(residents);
            foreach (var scope in State.Food!.Shops) { scope.ProductId = FoodProducts.Croissant; scope.AdditionalProductIds.Clear(); }
            var stand = Stand = Busiest(State);
            for (int i = 0; i < vehicles; i++)
            {
                var v = StreetFoodHooks.Add("v" + i.ToString("D2"), x: (float)stand.X + i * 8, items: new[] { FoodProducts.HotDog, FoodProducts.Burger });
                v.Z = (float)stand.Z + 2; v.Units[0] = v.Units[1] = 400; v.MealsPerHour = 60;
            }
            City = new CitySimulator(State, Planner);
            Native = State.Minute;
            Vendors = new StreetFoodVendorMarket(City, new VehicleMarketTests.Api(), "Street Food vendor API missing.", new VendorNavMeshSampler(),
                Receipts, () => Native, () => true);
            var markets = new FoodMarkets(State.Food, new Shops(State.Food, Receipts), Vendors, id => City.FindPlace(id).Position);
            City.AttachFoodMarket(markets); City.EnableHouseholdFinances();
            City.AttachOptionalStops(new RushSpikeReplay.Stops(), OptionalStopPolicy.ApprovedChoicePercentage);
            RunTo(OpenAt - 60);
            if (openInFrames) return;
            RunTo(OpenAt);
            OpenVehicles();
            RunTo(OpenAt + 1);
        }

        private void OpenVehicles()
        {
            foreach (var v in StreetFoodHooks.Vehicles) StreetFoodHooks.Open(v.Id, 600);
            Opened = true;
        }

        public void OpenWhenDue()
        {
            if (!Opened && Native >= OpenAt && City.IsCaughtUpTo(AccountingClock.Target(Native))) OpenVehicles();
        }

        private static Position Busiest(CityState state)
        {
            return state.Places.Where(p => p.Kind == PlaceKind.Work && Math.Abs(p.Position.X) < 900 && Math.Abs(p.Position.Z) < 900)
                .GroupBy(p => p.Position).OrderByDescending(g => g.Count()).ThenBy(g => g.Key.X).ThenBy(g => g.Key.Z).First().Key;
        }

        public void RunTo(double minute)
        {
            int stalled = 0;
            while (Native < minute)
            {
                double before = Native;
                Move((float)Math.Min(.25, minute - Native));
                if (Native == before && ++stalled > 64) throw new InvalidOperationException("Replay clock stalled at " + Native + " with the city at " + State.Minute + ".");
                if (Native != before) stalled = 0;
                while (Vendors.Prepare((long)Native)) { }
                while (!City.AdvanceTo(AccountingClock.Target(Native), 256).CaughtUp) { }
                City.SynchronizeSnapshot();
            }
        }

        public void AttachBodies()
        {
            NativeTravelPace.Focus = Stand; UnityEngine.Time.Next(3);
            Bodies = new PhysicalPopulation(new UnityEngine.Transform { Component = new PopulationRuntime { vendorMarket = Vendors } }, City,
                new MultimodalRoutes(), typeof(VendorLunchReplay).Assembly, new VendorBodyTests.Log(), 96, 24, 120, new DwellSpots());
        }

        public void WalkBodies(float seconds)
        {
            var pool = PhysicalPedestrians.Last;
            if (Bodies == null || pool == null) return;
            foreach (var body in pool.Active.Values)
            {
                if (body.Agent.isStopped) continue;
                var at = body.Position; double d = Position.Distance(at, body.Target), step = 1.4 * seconds;
                body.Character.transform.position = PhysicalPedestrianBody.Vector(d <= step ? body.Target : Position.Lerp(at, body.Target, step / d));
            }
        }

        public double End = double.PositiveInfinity;
        public void Tick(double seconds) => Move((float)Math.Min(GameMinutesPerSecond * seconds, (Opened ? End : Math.Min(End, OpenAt)) - Native));

        private void Move(float requested)
        {
            StreetFoodHooks.Now = (long)Math.Floor(Native); StreetFoodHooks.Expire();
            bool ready = City.IsCaughtUpTo(AccountingClock.Target(Native));
            float allowed = AccountingClock.Limit(ready, Native, requested);
            Native = Math.Floor(Native / 60) * 60 + (float)((float)(Native % 60) + allowed);
            StreetFoodHooks.Now = (long)Math.Floor(Native);
            if (!City.BeginAccountingObservations(Native)) throw new InvalidOperationException("Accounting epoch refused at " + Native + ".");
        }
    }

    public class SlowPlanner : IRoutePlanner, ICitizenRoutePlanner, ITrafficRoutePlanner
    {
        private readonly RushSpikeReplay.Planner inner = new RushSpikeReplay.Planner();
        public double CitizenRouteMilliseconds;
        public TrafficNetwork? TrafficNetwork => inner.TrafficNetwork;
        public void SetTrafficForecast(Func<int, double, TrafficLinkForecast>? forecast) => inner.SetTrafficForecast(forecast);
        public bool IsTrafficLinkClosed(int linkId) => inner.IsTrafficLinkClosed(linkId);
        public long Calls;
        protected void Spend() => Spend(CitizenRouteMilliseconds);
        protected void Spend(double milliseconds)
        {
            Calls++;
            if (!(milliseconds > 0)) return;
            long until = Stopwatch.GetTimestamp() + (long)(milliseconds * Stopwatch.Frequency / 1000);
            while (Stopwatch.GetTimestamp() < until) { }
        }
        public Route? Plan(Place from, Place to, double minute, TravelPreferences preferences) { Spend(); return inner.Plan(from, to, minute, preferences); }
        public Route? Plan(Place from, Place to, double minute, CitizenState citizen) { Spend(); return inner.Plan(from, to, minute, citizen); }
        protected Route? Answer(RouteRequest request)
            => request.Citizen != null ? inner.Plan(request.From, request.To, request.DepartureMinute, request.Citizen)
                : inner.Plan(request.From, request.To, request.DepartureMinute, request.Preferences);
    }

    public sealed class ResumableSlowPlanner : SlowPlanner, IResumableRoutePlanner
    {
        public long Frame, Deadline = long.MaxValue, LatePathStarts, CarriedPaths, PathSteps, HeldFrame = -1;
        public double SliceUnitMilliseconds, PathMilliseconds, PreparationMilliseconds, ReserveMilliseconds = .05;
        public bool FrameWorkDone, PathReserve = true;
        private long unitFrame = -1;
        private int frameUnits, framePaths;
        public IRoutePlanningJob Begin(RouteRequest request) => new Job(this, request);

        private sealed class Job : IRoutePlanningJob
        {
            private readonly ResumableSlowPlanner owner;
            private readonly RouteRequest request;
            private readonly long begun;
            private int stage;
            public Job(ResumableSlowPlanner owner, RouteRequest request) { this.owner = owner; this.request = request; begun = owner.Frame; }
            public Route? Result { get; private set; }

            public bool Continue(double budgetMilliseconds)
            {
                long started = Stopwatch.GetTimestamp();
                long deadline = double.IsPositiveInfinity(budgetMilliseconds) ? long.MaxValue : started + (long)(budgetMilliseconds * Stopwatch.Frequency / 1000);
                bool first = true, finite = !double.IsPositiveInfinity(budgetMilliseconds);
                if (finite && owner.unitFrame != owner.Frame) { owner.unitFrame = owner.Frame; owner.frameUnits = 0; owner.framePaths = 0; }
                while (stage < 2)
                {
                    if (!first && Stopwatch.GetTimestamp() >= deadline) return false;
                    if (finite && stage == 1 && owner.PathReserve && !PathAdmission.Admits(owner.frameUnits > 0, owner.framePaths, int.MaxValue,
                        (deadline - Stopwatch.GetTimestamp()) * 1000.0 / Stopwatch.Frequency, owner.ReserveMilliseconds))
                    { owner.HeldFrame = owner.Frame; return false; }
                    first = false;
                    if (finite) owner.frameUnits++;
                    long unit = Stopwatch.GetTimestamp();
                    if (stage == 0) owner.Spend(owner.PreparationMilliseconds);
                    else
                    {
                        owner.PathSteps++;
                        if (owner.Frame != begun) owner.CarriedPaths++;
                        double cost = owner.CitizenRouteMilliseconds + owner.PathMilliseconds;
                        if (cost > 0 && (owner.FrameWorkDone || owner.frameUnits > 1) && (owner.Deadline - unit) * 1000.0 / Stopwatch.Frequency < cost) owner.LatePathStarts++;
                        owner.Spend(cost);
                        Result = owner.Answer(request);
                        if (finite) owner.framePaths++;
                        double observed = (Stopwatch.GetTimestamp() - unit) * 1000.0 / Stopwatch.Frequency;
                        owner.ReserveMilliseconds = Math.Max(.05, Math.Max(observed * 1.25, owner.ReserveMilliseconds * .95));
                    }
                    stage++;
                    owner.SliceUnitMilliseconds = Math.Max(owner.SliceUnitMilliseconds, (Stopwatch.GetTimestamp() - unit) * 1000.0 / Stopwatch.Frequency);
                }
                return true;
            }

            public void Dispose() { }
        }
    }

    private sealed class Shops : IFoodMarket, ILocalFoodMarket, IFoodCandidateMarket, IFoodBusinessAuthority,
        IFoodFailureMarket, IFoodSaleEvidence, IRivalFoodMarket, IFoodAwarenessRevision
    {
        private readonly FoodCommerceState state;
        private readonly IDictionary<string, string> receipts;
        public Shops(FoodCommerceState state, IDictionary<string, string> receipts) { this.state = state; this.receipts = receipts; }
        public double EarliestMinute => 0;
        public long AwarenessRevision => 1;
        private IEnumerable<FoodScope> Scopes => state.Shops.Where(s => s.Channel == FoodScopeChannel.Shop);
        public IReadOnlyList<FoodBusiness> Read(double minute) => Scopes.Select(s => ReadBusiness(s.BusinessId, minute)!).ToArray();
        public FoodBusiness? ReadBusiness(string id, double minute)
        {
            var scope = Scopes.FirstOrDefault(s => s.BusinessId == id);
            if (scope == null) return null;
            return new FoodBusiness { Id = scope.BusinessId, PlaceId = scope.PlaceId, Name = scope.PlaceId, MealsPerHour = 600,
                Hours = Enumerable.Range(0, 7).Select(d => new OpeningWindow { Day = d, FromMinute = 360, ToMinute = 1380 }).ToList(),
                Offers = new List<FoodOffer> { new FoodOffer { ProductId = FoodProducts.Croissant, Price = Money.FromCents(650), Units = 100000 } } };
        }
        public IReadOnlyList<FoodBusiness> ReadLocal(Place p, double at, double tolerance, int max) => Read(at).Take(max).ToArray();
        public bool CanServe(string id) => Scopes.Any(s => s.BusinessId == id);
        public bool IsConfirmedPaid(ResidentMeal m) => FoodSaleReceipts.ReadStatus(receipts, m) == FoodReceiptStatus.Paid;
        public FoodSale Buy(ResidentMeal m, double at) { FoodSaleReceipts.Begin(receipts, m); FoodSaleReceipts.Confirm(receipts, m); return new FoodSale(true, "shop double confirmed"); }
        public bool IsCandidate(FoodScope s) => s.Channel == FoodScopeChannel.Shop;
        public FoodBusiness? CaptureBusiness(string id, double at) => ReadBusiness(id, at);
        public FoodBusiness? ReadFailureBusiness(string id, string p, double at) => ReadBusiness(id, at);
        public IDisposable BeginCapture(double at) => new Handle();
        private sealed class Handle : IDisposable { public void Dispose() { } }
        public long RivalRevision => 1;
        public IEnumerable<FoodScope> RivalScopes => Array.Empty<FoodScope>();
        public void CloseRivals() { }
        public bool NeedsRivalRefresh(double at) => false;
        public void ContinueRivalRefresh(double at) { }
        public bool IsRivalCandidate(FoodScope s) => false;
        public bool RivalAtPlace(string p) => false;
        public FoodAlternativeObservation? ReadRival(string id) => null;
    }

    private static double Ms(long from, long to) => (to - from) * 1000.0 / Stopwatch.Frequency;

    public static Step Frame(Rig rig, double seconds)
    {
        var step = new Step();
        long started = Stopwatch.GetTimestamp();
        double Elapsed() => Ms(started, Stopwatch.GetTimestamp());
        rig.OpenWhenDue();
        rig.Tick(seconds);
        if (rig.Bodies != null) { UnityEngine.Time.Next((float)seconds); rig.WalkBodies((float)seconds); }
        long bytes = GC.GetAllocatedBytesForCurrentThread();
        long Bytes() { long now = GC.GetAllocatedBytesForCurrentThread(), used = now - bytes; bytes = now; return used; }
        started = Stopwatch.GetTimestamp();
        double target = rig.Native;
        bool pending = rig.Vendors.Prepare((long)target);
        long prepared = Stopwatch.GetTimestamp();
        step.PrepareBytes = Bytes();
        step.Prepare = Ms(started, prepared);
        if (pending) { step.PrepareReturned = true; step.Total = Elapsed(); return step; }
        rig.City.RefreshOptionalStops();
        long stops = Stopwatch.GetTimestamp();
        step.Stops = Ms(prepared, stops);
        step.StopsBytes = Bytes();
        double advanceTarget = Math.Max(AccountingClock.Target(target), rig.State.Minute);
        var budget = new PopulationFrameBudget(seconds, false);
        double timeBudget = step.Budget = rig.Budget.Advance(budget.Milliseconds, Elapsed());
        long advanceStarted = Stopwatch.GetTimestamp();
        var resumable = rig.Planner as ResumableSlowPlanner;
        if (resumable != null) { resumable.Frame++; resumable.FrameWorkDone = false; resumable.Deadline = advanceStarted + (long)(timeBudget * Stopwatch.Frequency / 1000); }
        double elapsed = 0, unitStarted = 0;
        bool caughtUp = false;
        try
        {
            for (int i = 0; i < budget.Units; i++)
            {
                if (rig.City.PlanningPending)
                {
                    if (!rig.AdmitsPlanning(elapsed, timeBudget)) break;
                    int sliceCollections = GC.CollectionCount(0);
                    long sliceCpu = ThreadCpuClock.Available ? ThreadCpuClock.Nanoseconds() : -1;
                    long sliceStarted = Stopwatch.GetTimestamp();
                    if (resumable != null) resumable.SliceUnitMilliseconds = 0;
                    bool finished = rig.City.ContinuePlanning(Math.Max(0, timeBudget - elapsed));
                    elapsed = Ms(advanceStarted, Stopwatch.GetTimestamp());
                    if (resumable != null) resumable.FrameWorkDone = true;
                    long sliceCpuEnd = sliceCpu >= 0 ? ThreadCpuClock.Nanoseconds() : -1;
                    rig.Admission.ObservePlanning(resumable != null ? resumable.SliceUnitMilliseconds : Ms(sliceStarted, Stopwatch.GetTimestamp()),
                        sliceCpu >= 0 && sliceCpuEnd >= sliceCpu ? (sliceCpuEnd - sliceCpu) / 1e6 : -1, GC.CollectionCount(0) != sliceCollections);
                    Unit("route-planning");
                    if (!finished) { if (elapsed >= timeBudget || resumable?.HeldFrame == resumable?.Frame) break; continue; }
                    step.CitizenEvents++;
                    if (step.CitizenEvents >= budget.CitizenEvents || !rig.Fits(elapsed, timeBudget)) break;
                    continue;
                }
                int eventCollections = GC.CollectionCount(0);
                double sliceStart = Ms(advanceStarted, Stopwatch.GetTimestamp());
                var result = rig.City.AdvanceFrameSlice(advanceTarget, 1);
                step.Units += result.Events; step.CitizenEvents += result.CitizenEvents;
                elapsed = Ms(advanceStarted, Stopwatch.GetTimestamp());
                if (resumable != null) resumable.FrameWorkDone = true;
                rig.Admission.ObserveEvent(elapsed - sliceStart, -1, GC.CollectionCount(0) != eventCollections);
                Unit(result.PlanningPending ? "citizen-route-begin" : result.TrafficEvents > 0 ? "traffic"
                    : result.FoodContinuationEvents > 0 ? "food-continuation" : result.CitizenEvents > 0 ? "citizen" : "other");
                if (result.PlanningPending) { if (elapsed >= timeBudget) break; continue; }
                if (result.CaughtUp) { caughtUp = true; break; }
                if (result.ThroughRoutePending || step.CitizenEvents >= budget.CitizenEvents || !rig.Fits(elapsed, timeBudget)) break;
            }
        }
        finally
        {
            rig.Admission.EndFrame();
            long synced = Stopwatch.GetTimestamp();
            step.Advance = Ms(advanceStarted, synced);
            rig.City.SynchronizeSnapshot();
            step.Sync = Ms(synced, Stopwatch.GetTimestamp());
            step.AdvanceBytes = Bytes();
        }
        step.Saturated = !caughtUp && !rig.City.IsCaughtUpTo(advanceTarget);
        if (rig.Bodies != null)
        {
            long paths = PhysicalPedestrianBody.PathQueries; int samples = UnityEngine.AI.NavMesh.Samples, acquired = PhysicalPedestrians.Last?.Acquisitions.Count ?? 0;
            long physical = Stopwatch.GetTimestamp();
            rig.Bodies.Update();
            step.PathQueries = (int)(PhysicalPedestrianBody.PathQueries - paths); step.Samples = UnityEngine.AI.NavMesh.Samples - samples;
            step.Acquisitions = (PhysicalPedestrians.Last?.Acquisitions.Count ?? 0) - acquired;
            step.Physical = Ms(physical, Stopwatch.GetTimestamp());
            rig.Budget.ObservePhysical(step.Physical);
            step.PhysicalBytes = Bytes();
            step.Bodies = PhysicalPedestrians.Last?.Active.Count ?? 0;
        }
        step.Total = Elapsed();
        return step;

        void Unit(string kind)
        {
            if (elapsed - unitStarted > step.UnitMax) { step.UnitMax = elapsed - unitStarted; step.UnitKind = kind; }
            unitStarted = elapsed;
        }
    }

    public static Action<string>? Progress;

    public static Result Run(double seconds, double from = LunchFrom, double to = LunchTo, int vehicles = 2, int maximumFrames = 200000, bool bodies = false,
        double routeMilliseconds = 0, bool forecast = true, bool resumable = false, double pathMilliseconds = 0, double preparationMilliseconds = 0, bool pathReserve = true)
    {
        long began = Stopwatch.GetTimestamp();
        bool openInFrames = from <= OpenAt;
        var rig = new Rig(vehicles, resumable: resumable, openInFrames: openInFrames) { Forecasting = forecast };
        if (bodies) rig.AttachBodies();
        rig.Planner.CitizenRouteMilliseconds = 0;
        Progress?.Invoke("rig ready " + Ms(began, Stopwatch.GetTimestamp()) + " ms at " + rig.Native);
        rig.RunTo(from);
        Progress?.Invoke("preroll done " + Ms(began, Stopwatch.GetTimestamp()) + " ms at " + rig.Native);
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        rig.End = to;
        rig.Planner.CitizenRouteMilliseconds = routeMilliseconds;
        if (rig.Planner is ResumableSlowPlanner counted) { counted.PathMilliseconds = pathMilliseconds; counted.PreparationMilliseconds = preparationMilliseconds; counted.PathReserve = pathReserve; counted.LatePathStarts = counted.CarriedPaths = counted.PathSteps = 0; }
        long routeCalls = rig.Planner.Calls;
        var result = new Result { NativeFrom = rig.Native };
        int collections = GC.CollectionCount(0);
        long bytes = GC.GetAllocatedBytesForCurrentThread();
        while ((rig.Native < to || !rig.City.IsCaughtUpTo(AccountingClock.Target(rig.Native))) && result.Frames.Count < maximumFrames)
        {
            bool last = rig.Native >= to;
            result.Frames.Add(Frame(rig, last ? 0 : seconds));
            if (!last) result.RealSeconds += seconds;
        }
        result.AllocatedBytes = GC.GetAllocatedBytesForCurrentThread() - bytes;
        result.Collections = GC.CollectionCount(0) - collections;
        result.NativeTo = rig.Native;
        result.RouteCalls = rig.Planner.Calls - routeCalls;
        if (rig.Planner is ResumableSlowPlanner slow) { result.LatePathStarts = slow.LatePathStarts; result.CarriedPaths = slow.CarriedPaths; result.PathSteps = slow.PathSteps; }
        Finish(rig, result);
        rig.Bodies?.Dispose();
        return result;
    }

    public static Result RunUninterrupted(double from = LunchFrom, double to = LunchTo, int vehicles = 2)
    {
        var rig = new Rig(vehicles, openInFrames: to <= OpenAt);
        rig.RunTo(from);
        var result = new Result { NativeFrom = rig.Native };
        rig.RunTo(to);
        result.NativeTo = rig.Native;
        Finish(rig, result);
        return result;
    }

    private static void Finish(Rig rig, Result result)
    {
        var meals = rig.State.Food!.Residents;
        result.Purchases = meals.Sum(m => m.Purchases);
        result.VendorMeals = meals.Count(m => VendorNames.IsVendorPlace(m.PlaceId));
        result.VendorPurchases = rig.Vendors.Purchases;
        result.SaveSha256 = RushSpikeReplay.Sha(DevelopmentChain.Write(rig.State));
    }
}
