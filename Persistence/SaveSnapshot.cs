using System;
using System.Collections.Generic;
using PersistentCity.Core;

namespace PersistentCity.Persistence
{
    /// <summary>
    /// Independent parts of one capture (F7c). Each copies its own part of the live graph and nothing else, so any
    /// order produces the same snapshot and the same bytes. Visitors are one part because a visitor person and its
    /// routine point at each other.
    /// </summary>
    internal enum SnapshotSection { Header, Places, Residents, Visitors, Traffic, Food, Research, Vendors, Economy }

    /// <summary>
    /// One save epoch's detached, immutable projection of the state the wire carries (F7 plan section 3.2, slice F7c).
    /// It is captured on the main thread inside the save callback, after the synchronize step, and then validated
    /// and encoded. It holds no native object and no list or record the runtime can reach; committed journey routes
    /// are frozen (<see cref="Route.Commit"/>) and shared by reference. The current writer encodes it with the same
    /// wire DTO types as the live model, so the bytes are the ones the writer produced from the live graph.
    /// Never cached across saves: <see cref="SaveSession"/> publishes a snapshot at most once.
    /// </summary>
    public sealed class SaveSnapshot
    {
        internal static readonly SnapshotSection[] DefaultOrder =
        {
            SnapshotSection.Header, SnapshotSection.Places, SnapshotSection.Residents, SnapshotSection.Visitors,
            SnapshotSection.Traffic, SnapshotSection.Food, SnapshotSection.Research, SnapshotSection.Vendors, SnapshotSection.Economy,
        };

        private SaveSnapshot(CityState wire, int schema, int sharedRoutes, int copiedRoutes)
        {
            Wire = wire; Schema = schema; Minute = wire.Minute; PopulationKey = wire.PopulationKey ?? "";
            SharedRoutes = sharedRoutes; CopiedRoutes = copiedRoutes;
        }

        /// <summary>The detached graph. Only validation and the encoder read it; it is never handed to the runtime.</summary>
        internal CityState Wire { get; }
        /// <summary>The writer schema this snapshot was captured for.</summary>
        public int Schema { get; }
        public double Minute { get; }
        public string PopulationKey { get; }
        /// <summary>Committed journey routes shared by reference instead of copied.</summary>
        public int SharedRoutes { get; }
        /// <summary>Uncommitted routes copied leg by leg: food quotes and journeys that entered by load or by direct construction.</summary>
        public int CopiedRoutes { get; }
        /// <summary>Set once a session published this snapshot's payload.</summary>
        internal bool Published { get; set; }

        internal static SaveSnapshot Capture(CityState live, int schema, IReadOnlyList<SnapshotSection>? order = null)
        {
            var copier = new Copier();
            var wire = new CityState();
            var done = new bool[DefaultOrder.Length];
            foreach (var section in order ?? DefaultOrder)
            {
                if (done[(int)section]) throw new ArgumentException("A snapshot section is captured once.");
                done[(int)section] = true;
                copier.Section(section, live, wire);
            }
            foreach (bool section in done) if (!section) throw new ArgumentException("A snapshot needs every section.");
            return new SaveSnapshot(wire, schema, copier.SharedRoutes, copier.CopiedRoutes);
        }

        // Copies tolerate null records and lists and keep out-of-range values unchanged, so validation reports
        // the same refusal it reported for the live graph.
        private sealed class Copier
        {
            public int SharedRoutes, CopiedRoutes;
            // Language version 9 allocates a delegate for every method-group conversion and for every lambda that calls
            // an instance member, so the per-record copies are bound once per capture.
            private static readonly Func<OpeningWindow, OpeningWindow> WindowCopy = Window;
            private static readonly Func<VisitRecord, VisitRecord> VisitCopy = Visit;
            private static readonly Func<CitizenIdentity, CitizenIdentity> IdentityCopy = Identity;
            private static readonly Func<FoodScope, FoodScope> ScopeCopy = Scope;
            private readonly Func<FoodDecisionShop, FoodDecisionShop> decisionShop;
            public Copier() { decisionShop = DecisionShop; }

            public void Section(SnapshotSection section, CityState live, CityState wire)
            {
                switch (section)
                {
                    case SnapshotSection.Header:
                        wire.MenuPolicy = live.MenuPolicy; wire.ProductNames = Values(live.ProductNames)!;
                        wire.NamePolicyStartsDay = live.NamePolicyStartsDay; wire.WeatherStartsDay = live.WeatherStartsDay;
                        wire.Capabilities = live.Capabilities == null ? null! : new PopulationCapabilities
                        { Code = live.Capabilities.Code, Revision = live.Capabilities.Revision, Active = live.Capabilities.Active };
                        wire.Schema = live.Schema; wire.TravelTimingVersion = live.TravelTimingVersion; wire.TravelClockRate = live.TravelClockRate;
                        wire.TrafficTopologyId = live.TrafficTopologyId; wire.RloVersion = live.RloVersion; wire.PopulationKey = live.PopulationKey;
                        wire.Seed = live.Seed; wire.Minute = live.Minute; wire.NextResidentSequence = live.NextResidentSequence;
                        wire.PopulationTarget = live.PopulationTarget; wire.InitialImportedResidents = live.InitialImportedResidents;
                        wire.NextVisitorSequence = live.NextVisitorSequence; wire.IdentityPolicyVersion = live.IdentityPolicyVersion;
                        wire.Portals = Values(live.Portals)!;
                        return;
                    case SnapshotSection.Places: wire.Places = List(live.Places, Place)!; return;
                    case SnapshotSection.Residents:
                        wire.Citizens = List(live.Citizens, c => Citizen(c, new CitizenState()))!;
                        wire.SourceIdentities = List(live.SourceIdentities, IdentityCopy)!;
                        return;
                    case SnapshotSection.Visitors: Visitors(live, wire); return;
                    case SnapshotSection.Traffic: wire.Traffic = live.Traffic == null ? null : Traffic(live.Traffic); return;
                    case SnapshotSection.Food: wire.Food = live.Food == null ? null : Food(live.Food); return;
                    case SnapshotSection.Research: wire.Research = live.Research?.CopyForSave()!; return;
                    case SnapshotSection.Economy: wire.Economy = SaveCodec.CopyEconomy(live.Economy)!; return;
                    case SnapshotSection.Vendors: wire.Vendors = SaveCodec.CopyVendors30(live.Vendors)!; return;
                    default: throw new ArgumentOutOfRangeException(nameof(section));
                }
            }

            private static List<T>? List<T>(List<T>? source, Func<T, T> copy) where T : class
            {
                if (source == null) return null;
                var result = new List<T>(source.Count);
                foreach (var item in source) result.Add(item == null ? null! : copy(item));
                return result;
            }

            private static List<T>? Values<T>(List<T>? source) => source == null ? null : new List<T>(source);
            private static long[]? Values(long[]? source) => source == null ? null : (long[])source.Clone();

            // ---- City ----

            private static Place Place(Place p) => new Place
            {
                Id = p.Id, Name = p.Name, District = p.District, Kind = p.Kind, Position = p.Position, Capacity = p.Capacity,
                OpenMinute = p.OpenMinute, CloseMinute = p.CloseMinute, WeeklyHours = List(p.WeeklyHours, WindowCopy)!,
                AttractionWeight = p.AttractionWeight, NativeTrafficIndex = p.NativeTrafficIndex, EvidenceSource = p.EvidenceSource,
                ResidentialAreaSquareMetres = p.ResidentialAreaSquareMetres,
            };

            private static OpeningWindow Window(OpeningWindow w) => new OpeningWindow { Day = w.Day, FromMinute = w.FromMinute, ToMinute = w.ToMinute };

            private static CitizenIdentity Identity(CitizenIdentity i) => new CitizenIdentity
            { Id = i.Id, Name = i.Name, District = i.District, Age = i.Age, Female = i.Female, Persona = i.Persona, AppearanceSeed = i.AppearanceSeed };

            private T Citizen<T>(CitizenState c, T copy) where T : CitizenState
            {
                copy.W = c.W; // immutable
                copy.Identity = c.Identity == null ? null! : Identity(c.Identity);
                copy.HomeId = c.HomeId; copy.DayPlaceId = c.DayPlaceId; copy.ShopId = c.ShopId; copy.LeisureId = c.LeisureId;
                copy.Preferences = c.Preferences == null ? null! : new TravelPreferences { OwnsCar = c.Preferences.OwnsCar,
                    AllowTransit = c.Preferences.AllowTransit, WalkingToleranceMetres = c.Preferences.WalkingToleranceMetres,
                    FareWeightMinutes = c.Preferences.FareWeightMinutes };
                var prep = c.CommutePreparation;
                copy.CommutePreparation = prep == null ? null! : new CommutePreparation { PlaceId = prep.PlaceId, WorkStartsAt = prep.WorkStartsAt,
                    WorkEndsAt = prep.WorkEndsAt, PreparedAt = prep.PreparedAt, TravelMinutes = prep.TravelMinutes };
                var outing = c.MealOuting;
                copy.MealOuting = outing == null ? null : new MealOuting { OriginId = outing.OriginId, OriginActivity = outing.OriginActivity,
                    ResumeUntil = outing.ResumeUntil, ResumeEvent = outing.ResumeEvent, Returning = outing.Returning };
                var stop = c.OptionalStop;
                copy.OptionalStop = stop == null ? null : new OptionalStopState { Kind = stop.Kind, PlaceId = stop.PlaceId, SpotId = stop.SpotId,
                    TargetId = stop.TargetId, Position = stop.Position, Facing = stop.Facing, ReturnPosition = stop.ReturnPosition,
                    StartMinute = stop.StartMinute, EndMinute = stop.EndMinute, VisitMinute = stop.VisitMinute,
                    ResumeEventMinute = stop.ResumeEventMinute, Cancelled = stop.Cancelled, Members = Values(stop.Members)! };
                copy.OptionalStopChoice = c.OptionalStopChoice;
                copy.Car = c.Car == null ? null : new CarState { ModelId = c.Car.ModelId, PaintSeed = c.Car.PaintSeed, Id = c.Car.Id, ParkedPosition = c.Car.ParkedPosition };
                copy.JoinedMinute = c.JoinedMinute; copy.LastEmploymentCheckDay = c.LastEmploymentCheckDay; copy.JobChanges = c.JobChanges;
                copy.EmploymentStartDay = c.EmploymentStartDay; copy.Accrual = c.Accrual;
                copy.Phase = c.Phase; copy.Activity = c.Activity; copy.PlaceId = c.PlaceId; copy.DestinationId = c.DestinationId;
                copy.DestinationActivity = c.DestinationActivity; copy.Journey = Route(c.Journey);
                copy.DepartureMinute = c.DepartureMinute; copy.NextEventMinute = c.NextEventMinute; copy.ActivityUntilMinute = c.ActivityUntilMinute;
                copy.CompletedJourneys = c.CompletedJourneys; copy.MissedActivities = c.MissedActivities;
                copy.RecentVisits = List(c.RecentVisits, VisitCopy)!;
                copy.Traits = Traits(c.Traits);
                // Derived from the immutable identity and the seed; DailyRoutine rebuilds it when either differs.
                copy.RoutineKeys = c.RoutineKeys;
                copy.VisitorCarried = c.VisitorCarried;
                return copy;
            }

            private static VisitRecord Visit(VisitRecord v) => new VisitRecord { AtMinute = v.AtMinute, PlaceId = v.PlaceId, Activity = v.Activity };

            private static ResidentTraits? Traits(ResidentTraits? t) => t == null ? null : new ResidentTraits { Version = t.Version, Bands = t.Bands };

            /// <summary>A committed journey is immutable (its leg list and legs refuse every edit) and shared; any other
            /// route is copied leg by leg.</summary>
            private Route? Route(Route? route)
            {
                if (route == null) return null;
                if (route.IsCommitted) { SharedRoutes++; return route; }
                CopiedRoutes++;
                var copy = new Route();
                foreach (var leg in route.Legs)
                    copy.Legs.Add(leg == null ? null! : new RouteLeg { From = leg.From, To = leg.To, Mode = leg.Mode, Minutes = leg.Minutes, ServiceId = leg.ServiceId });
                return copy;
            }

            private void Visitors(CityState live, CityState wire)
            {
                var routines = new Dictionary<VisitorRoutine, VisitorRoutine>(ReferenceComparer<VisitorRoutine>.Instance);
                VisitorRoutine Routine(VisitorRoutine r)
                {
                    if (routines.TryGetValue(r, out var known)) return known;
                    var copy = Citizen(r, new VisitorRoutine());
                    routines.Add(r, copy);
                    return copy;
                }
                var persons = new Dictionary<VisitorPerson, VisitorPerson>(ReferenceComparer<VisitorPerson>.Instance);
                wire.Visitors = List(live.Visitors, v =>
                {
                    var stay = v.Stay;
                    var job = v.Employment;
                    var copy = new VisitorPerson
                    {
                        Id = v.Id,
                        Stay = stay == null ? null! : new VisitorStay { OriginPortalId = stay.OriginPortalId, Pattern = stay.Pattern,
                            Presence = stay.Presence, FirstDay = stay.FirstDay, NextStayStart = stay.NextStayStart },
                        Employment = job == null ? null : new Employment { PersonId = job.PersonId, WorkplaceId = job.WorkplaceId, Kind = job.Kind,
                            StartDay = job.StartDay, EndDay = job.EndDay, ScheduleCode = job.ScheduleCode, ShiftDraw = job.ShiftDraw,
                            NativeEmployment = job.NativeEmployment },
                        CarriedIncomeSource = v.CarriedIncomeSource,
                        LastArrival = v.LastArrival == null ? null : Visit(v.LastArrival),
                        Routine = v.Routine == null ? null : Routine(v.Routine),
                    };
                    persons.Add(v, copy);
                    return copy;
                })!;
                wire.VisitorRoutines = List(live.VisitorRoutines, Routine)!;
                // Relink each copied routine to its copied person, as the live graph links them.
                foreach (var pair in routines)
                    pair.Value.Visitor = pair.Key.Visitor == null ? null : persons.TryGetValue(pair.Key.Visitor, out var person) ? person : null;
            }

            // ---- Traffic ----

            private static TrafficKernelState Traffic(TrafficKernelState t) => new TrafficKernelState
            {
                Schema = t.Schema, Clock = t.Clock, RequestedTime = t.RequestedTime, NextEventOrder = t.NextEventOrder, NextVehicleOrder = t.NextVehicleOrder,
                Definitions = List(t.Definitions, d => new TrafficLinkDefinition { Id = d.Id, Capacity = d.Capacity, FreeFlowMinutes = d.FreeFlowMinutes,
                    HeadwayMinutes = d.HeadwayMinutes, Signal = d.Signal == null ? null : new TrafficSignalSchedule { CycleMinutes = d.Signal.CycleMinutes,
                        GreenStartMinutes = d.Signal.GreenStartMinutes, GreenDurationMinutes = d.Signal.GreenDurationMinutes, PhaseOffsetMinutes = d.Signal.PhaseOffsetMinutes } })!,
                Links = List(t.Links, l => new TrafficLinkState { LinkId = l.LinkId, Closed = l.Closed, NextDischargeAt = l.NextDischargeAt,
                    MergeCursor = l.MergeCursor, Vehicles = Values(l.Vehicles)!, WaitingSources = Values(l.WaitingSources)!, IncomingLinks = Values(l.IncomingLinks)! })!,
                Vehicles = List(t.Vehicles, v => new TrafficVehicleState { VehicleId = v.VehicleId, Order = v.Order,
                    Route = List(v.Route, r => new TrafficRouteLeg { LinkId = r.LinkId, StartNominalMinutes = r.StartNominalMinutes,
                        EndNominalMinutes = r.EndNominalMinutes, FreeFlowMinutes = r.FreeFlowMinutes, StartLinkFraction = r.StartLinkFraction,
                        EndLinkFraction = r.EndLinkFraction })!,
                    RouteIndex = v.RouteIndex, EnqueuedAt = v.EnqueuedAt, EnteredAt = v.EnteredAt, ReadyAt = v.ReadyAt, MotionStartedAt = v.MotionStartedAt,
                    MotionStartFraction = v.MotionStartFraction, NominalMinutes = v.NominalMinutes, PhysicalControl = v.PhysicalControl,
                    Completed = v.Completed, CompletedAt = v.CompletedAt })!,
                Events = List(t.Events, e => new TrafficEventState { LinkId = e.LinkId, At = e.At, Order = e.Order })!,
            };

            // ---- Food ----

            private FoodCommerceState Food(FoodCommerceState f) => new FoodCommerceState
            {
                Outflow = f.Outflow == null ? null : Outflow(f.Outflow),
                BusinessReferences = List(f.BusinessReferences, b => new BusinessReference { Slot = b.Slot, BusinessId = b.BusinessId, PlaceId = b.PlaceId })!,
                RepresentationFactor = f.RepresentationFactor,
                RoutineStops = List(f.RoutineStops, s => new FoodRoutineStop { AnchorId = s.AnchorId, PlaceId = s.PlaceId, Activity = s.Activity,
                    CapturedAt = s.CapturedAt, DepartureAt = s.DepartureAt })!,
                History = List(f.History, ShopDay)!,
                HouseholdFinanceVersion = f.HouseholdFinanceVersion, MealRhythmVersion = f.MealRhythmVersion,
                Shops = List(f.Shops, ScopeCopy)!,
                Residents = List(f.Residents, Meal)!,
                Districts = List(f.Districts, d => new FoodDistrictBudgetMix { DistrictId = d.DistrictId, WorkingPercent = d.WorkingPercent,
                    MiddlePercent = d.MiddlePercent, UpperPercent = d.UpperPercent, Evidence = d.Evidence })!,
                ChoicePolicyVersion = f.ChoicePolicyVersion, ChoiceStartsDay = f.ChoiceStartsDay, AwarenessStartsDay = f.AwarenessStartsDay,
                RivalStartsDay = f.RivalStartsDay,
                Campaigns = Values(f.Campaigns)!, // FoodCampaignImage is immutable
            };

            private static FoodScope Scope(FoodScope s)
            {
                var copy = new FoodScope { AlternativeKind = s.AlternativeKind, PlaceId = s.PlaceId, ProductId = s.ProductId,
                    AdditionalProductIds = Values(s.AdditionalProductIds)!, BusinessId = s.BusinessId, NextServiceAt = s.NextServiceAt };
                copy.UseChannel(s.Channel);
                return copy;
            }

            private static RivalOutflowHistory Outflow(RivalOutflowHistory o)
            {
                var copy = new RivalOutflowHistory { StartDay = o.StartDay, ThroughDay = o.ThroughDay, PartialStart = o.PartialStart,
                    Baseline = o.Baseline, ExpiredUnits = o.ExpiredUnits, ExpiredCount = o.ExpiredCount };
                foreach (var row in o.Rows)
                    copy.Rows.Add(row == null ? null! : new RivalOutflowHistory.Row { Day = row.Day, Units = row.Units, Count = row.Count });
                return copy;
            }

            private static FoodShopDay ShopDay(FoodShopDay d) => new FoodShopDay
            {
                StockoutObserved = d.StockoutObserved,
                PeriodCauses = d.PeriodCauses == null ? null! : new MealPeriodCauses { Counts = d.PeriodCauses.Counts == null ? null! : (int[])d.PeriodCauses.Counts.Clone() },
                Day = d.Day, PlaceId = d.PlaceId, BusinessId = d.BusinessId, OffersConsidered = d.OffersConsidered, ChosenOffers = d.ChosenOffers,
                PriceRejections = d.PriceRejections, AccessRejections = d.AccessRejections, ClosedRejections = d.ClosedRejections,
                StockRejections = d.StockRejections, StaffRejections = d.StaffRejections, ServiceRejections = d.ServiceRejections,
                UnknownEvidence = d.UnknownEvidence, PreferredAlternatives = d.PreferredAlternatives, Purchases = d.Purchases,
                ExistingStopPurchases = d.ExistingStopPurchases, Revenue = d.Revenue, PurchaseHours = Values(d.PurchaseHours)!,
                PurchaseMeals = Values(d.PurchaseMeals)!,
                ProductSales = List(d.ProductSales, s =>
                {
                    var copy = new FoodProductSales { ProductId = s.ProductId, Purchases = s.Purchases, Revenue = s.Revenue, PurchaseMeals = Values(s.PurchaseMeals)! };
                    if (s.HasOpaqueIdentity()) copy.PreserveOpaqueIdentity();
                    return copy;
                })!,
                BuyerDistricts = List(d.BuyerDistricts, b => new FoodBuyerDistrict { DistrictId = b.DistrictId, Purchases = b.Purchases })!,
            };

            private ResidentMeal Meal(ResidentMeal m)
            {
                // A new record has no panel counters, so every setter below only stores its value.
                var copy = new ResidentMeal
                {
                    ResidentId = m.ResidentId, AnchorId = m.AnchorId, Slot = m.Slot, PreferredProductId = m.PreferredProductId,
                    PreferenceBonusCents = m.PreferenceBonusCents, NeedsHomeResync = m.NeedsHomeResync, Excursion = Excursion(m.Excursion),
                    Day = m.Day, Kind = m.Kind, DayAllowanceUsed = m.DayAllowanceUsed, DesiredAt = m.DesiredAt, ExpiresAt = m.ExpiresAt,
                    DueAt = m.DueAt, Status = m.Status, PlaceId = m.PlaceId, BusinessId = m.BusinessId, ProductId = m.ProductId,
                    Reason = m.Reason, Allowance = m.Allowance, Price = m.Price, ServiceMinutes = m.ServiceMinutes, ServiceAt = m.ServiceAt,
                    Deadline = m.Deadline, ReturnMinutes = m.ReturnMinutes, Budget = Budget(m.Budget), ExistingStop = m.ExistingStop,
                    ExistingStopPurchases = m.ExistingStopPurchases, Purchases = m.Purchases, Spent = m.Spent,
                    RepresentedTraits = Traits(m.RepresentedTraits), Today = Today(m.Today), Decision = Decision(m.Decision),
                    LastContextKey = m.LastContextKey, ContextAttempts = m.ContextAttempts, Memory = Memory(m.Memory)!,
                    External = m.External, LegacyExternalFoodSpent = m.LegacyExternalFoodSpent,
                };
                m.CopyHostTo(copy);
                return copy;
            }

            private static MealExcursion? Excursion(MealExcursion? e) => e == null ? null : new MealExcursion
            {
                OriginId = e.OriginId, DestinationId = e.DestinationId, OriginActivity = e.OriginActivity, OriginPosition = e.OriginPosition,
                DestinationPosition = e.DestinationPosition, DepartedAt = e.DepartedAt, ArrivesAt = e.ArrivesAt, AdmissionAt = e.AdmissionAt,
                SharedDepartureAt = e.SharedDepartureAt, ReturnStartsAt = e.ReturnStartsAt, ReturnsAt = e.ReturnsAt,
                MaximumQueueMinutes = e.MaximumQueueMinutes, ReturnGeometry = e.ReturnGeometry, Returning = e.Returning,
                Unresolved = e.Unresolved, UnresolvedReason = e.UnresolvedReason,
            };

            private static ResidentBudget? Budget(ResidentBudget? b) => b == null ? null : new ResidentBudget
            {
                Ledger = b.Ledger, IncomeSource = b.IncomeSource, BaseIncomeCents = b.BaseIncomeCents, DailyIncomeCents = b.DailyIncomeCents,
                DailyHousingCents = b.DailyHousingCents, DailyEssentialsCents = b.DailyEssentialsCents, DailyHomeFoodCents = b.DailyHomeFoodCents,
                OpeningBalance = b.OpeningBalance, Balance = b.Balance, IncomeReceived = b.IncomeReceived, EssentialsPaid = b.EssentialsPaid,
                HomeFoodPaid = b.HomeFoodPaid, PreparedFoodSpent = b.PreparedFoodSpent, ExternalFoodSpent = b.ExternalFoodSpent,
                HeldPurchase = b.HeldPurchase, Arrears = b.Arrears, LastPostedDay = b.LastPostedDay, HomeFoodFundedDay = b.HomeFoodFundedDay,
                DaysWithoutHomeFood = b.DaysWithoutHomeFood, NextPostingMinute = b.NextPostingMinute,
            };

            private static ResidentFoodToday? Today(ResidentFoodToday? t) => t == null ? null : new ResidentFoodToday
            {
                Day = t.Day, Unrecorded = t.Unrecorded,
                Meals = List(t.Meals, r => new MealOutcomeRecord { TargetKind = r.TargetKind, Kind = r.Kind, Status = r.Status, Reason = r.Reason,
                    PlaceId = r.PlaceId, ProductId = r.ProductId, Outside = r.Outside, Factor1 = r.Factor1, Factor2 = r.Factor2,
                    At = r.At, CheckoutAt = r.CheckoutAt })!,
            };

            private static ConsumerMemory? Memory(ConsumerMemory? m) => m == null ? null : new ConsumerMemory
            {
                Weather = m.Weather,
                Watermark = m.Watermark == null ? null! : new ExperienceWatermark { Day = m.Watermark.Day, Meal = m.Watermark.Meal,
                    Context = m.Watermark.Context, Stage = m.Watermark.Stage },
                Entries = List(m.Entries, e => new ShopMemory { BusinessRef = e.BusinessRef, Day = e.Day, Served = e.Served,
                    Affinity = e.Affinity, Outcome = e.Outcome })!,
            };

            private FoodDecisionState? Decision(FoodDecisionState? d) => d == null ? null : new FoodDecisionState
            {
                Weather = d.Weather, WeatherTravelMultiplier = d.WeatherTravelMultiplier, Tuning = d.Tuning, // tuning is immutable
                Awareness = d.Awareness == null ? null : new FoodAwarenessImage { CatalogueRevision = d.Awareness.CatalogueRevision,
                    Union = List(d.Awareness.Union, c => new FoodAwarenessCandidate { AlternativeKind = c.AlternativeKind, BusinessId = c.BusinessId,
                        Sources = c.Sources, Eligible = c.Eligible })! },
                IntentId = d.IntentId, PolicyVersion = d.PolicyVersion, ContextKey = d.ContextKey, Attempt = d.Attempt, Result = d.Result,
                BusinessId = d.BusinessId, ProductId = d.ProductId, Outside = d.Outside, Reason = d.Reason, Factor1 = d.Factor1, Factor2 = d.Factor2,
                OriginAt = d.OriginAt, Deadline = d.Deadline, QuotesDone = d.QuotesDone,
                Shops = List(d.Shops, decisionShop)!,
            };

            private FoodDecisionShop DecisionShop(FoodDecisionShop s) => new FoodDecisionShop
            {
                AlternativeKind = s.AlternativeKind, RivalTerms = s.RivalTerms, RivalServiceMinutes = s.RivalServiceMinutes,
                AlternativeWeight = s.AlternativeWeight, MemoryContribution = s.MemoryContribution, BusinessId = s.BusinessId, PlaceId = s.PlaceId,
                MealsPerHour = s.MealsPerHour, ServiceUnavailable = s.ServiceUnavailable, NextServiceAt = s.NextServiceAt,
                Hours = List(s.Hours, WindowCopy)!, CustomerServicePercent = s.CustomerServicePercent, DistrictId = s.DistrictId,
                Offers = List(s.Offers, o => new FoodDecisionOffer { Metadata = o.Metadata, ProductId = o.ProductId, Price = o.Price,
                    Units = o.Units, DistrictDemandPercent = o.DistrictDemandPercent })!, // metadata is immutable
                OutwardQuote = s.OutwardQuote, ReturnQuote = s.ReturnQuote, Outward = Route(s.Outward), Return = Route(s.Return),
            };
        }

        private sealed class ReferenceComparer<T> : IEqualityComparer<T> where T : class
        {
            public static readonly ReferenceComparer<T> Instance = new ReferenceComparer<T>();
            public bool Equals(T? x, T? y) => ReferenceEquals(x, y);
            public int GetHashCode(T obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }
    }
}
