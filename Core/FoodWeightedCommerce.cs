using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace PersistentCity.Core
{
    public sealed partial class FoodCommerce
    {
        internal FoodChoiceSettings ChoiceSettings { get; }
        internal FoodAwarenessSettings AwarenessSettings { get; }
        internal IFoodAwarenessProvider? MemoryProvider { get; }
        internal IFoodAwarenessProvider? CampaignProvider { get; }
        private FoodAwarenessCatalogue? awarenessCatalogue;
        private FoodAwarenessCatalogue.Builder? awarenessBuilder;
        private long awarenessRevision=-1,awarenessInvalidation,buildingRevision,buildingInvalidation;
        private List<FoodScope>? awarenessScopes,buildingScopes;
        private int awarenessCount=-1,buildingCount;
        internal void InvalidateAwareness() { awarenessInvalidation++; }
        private long CurrentAwarenessRevision => (market as IFoodAwarenessRevision)?.AwarenessRevision ?? 0;
        private bool AwarenessReady => awarenessCatalogue!=null && awarenessRevision==CurrentAwarenessRevision
            && awarenessInvalidation==publishedInvalidation && ReferenceEquals(awarenessScopes,state.Shops) && awarenessCount==state.Shops.Count;
        private long publishedInvalidation=-1;
        internal bool AwarenessCurrent => AwarenessReady;
        internal bool HasPendingCampaignPublication => (CampaignProvider as IFoodCampaignPreparation)?.HasPendingPublication == true;
        internal string ContinuationStep { get; private set; } = "";
        internal void ContinueCampaignPublication() { ContinuationStep="campaign"; ((IFoodCampaignPreparation)CampaignProvider!).ContinuePublication(); }
        internal bool NeedsAwarenessPreparation(double minute) => HasPendingCampaignPublication || rivalResumeNeedsRefresh && NeedsRivalPreparation(minute)
            || pendingDecision==null && state.ChoicePolicyVersion==FoodChoicePolicy.Version
            && (int)(minute/1440)>=state.ChoiceStartsDay && (int)(minute/1440)>=state.AwarenessStartsDay && (NeedsRivalPreparation(minute) || !AwarenessReady);
        internal void ContinueAwarenessPreparation()
        {
            ContinuationStep="awareness";
            if (HasPendingCampaignPublication) { ContinueCampaignPublication(); return; }
            if(NeedsRivalPreparation(city.State.Minute))
            {
                ContinuationStep="rival-refresh";
                var rivals=(IRivalFoodMarket)market; rivals.ContinueRivalRefresh(city.State.Minute);
                if(!rivals.NeedsRivalRefresh(city.State.Minute)) rivalResumeNeedsRefresh=false;
                return;
            }
            long revision=CurrentAwarenessRevision;
            if(awarenessBuilder==null || buildingRevision!=revision || buildingInvalidation!=awarenessInvalidation
                || !ReferenceEquals(buildingScopes,state.Shops) || buildingCount!=state.Shops.Count)
            {
                awarenessBuilder?.Dispose();buildingRevision=revision;buildingInvalidation=awarenessInvalidation;
                buildingScopes=state.Shops;buildingCount=state.Shops.Count;
                awarenessBuilder=FoodAwarenessCatalogue.BeginSparseBuild(AwarenessEntries());
            }
            ContinuationStep=AwarenessStepNames[(int)awarenessBuilder.Stage];
            if(!awarenessBuilder.Continue()) return;
            awarenessCatalogue=awarenessBuilder.Result;awarenessBuilder.Dispose();awarenessBuilder=null;
            awarenessRevision=buildingRevision;publishedInvalidation=buildingInvalidation;awarenessScopes=buildingScopes;awarenessCount=buildingCount;
        }
        private static readonly string[] AwarenessStepNames=Array.ConvertAll((FoodAwarenessCatalogue.BuildStage[])Enum.GetValues(typeof(FoodAwarenessCatalogue.BuildStage)),
            stage=>"awareness-"+stage.ToString().ToLowerInvariant());
        private FoodAwarenessCatalogue AwarenessCatalogue() => AwarenessReady ? awarenessCatalogue!
            : throw new InvalidOperationException("Awareness image must finish bounded preparation before capture.");
        private FoodScope? ScopeAtPlace(string place)
            => AwarenessReady ? awarenessCatalogue!.ScopeAtPlace(place)
                : state.Shops.FirstOrDefault(scope=>scope.PlaceId==place);
        private bool VendorCandidate(FoodScope scope) => market is IFoodCandidateMarket m ? m.IsCandidate(scope) : scope.Channel==FoodScopeChannel.Shop;
        private Position? Anchor(string id) => id.Length==0 ? (Position?)null : city.FindPlace(id).Position;
        private bool awarenessAtStop;
        private string awarenessStopPlace="";
        private Func<FoodAwarenessCatalogue.Entry,bool>? awarenessEligible;
        private bool IsAwarenessEligible(FoodAwarenessCatalogue.Entry e)
            =>(!awarenessAtStop || e.PlaceId==awarenessStopPlace) && e.Scope!.BusinessId==e.BusinessId
                && (e.Kind==FoodAwarenessAlternativeKind.External ? RivalSettings.RivalChoiceWeight>0 && RivalCandidate(e.Scope!) : VendorCandidate(e.Scope!));
        private ResidentMeal? memoryMeal;
        private CitizenState? memoryPerson;
        private Func<string,double>? memoryContributionDelegate;
        private double CurrentMemoryContribution(string id) => MemoryContribution(memoryMeal!,memoryPerson!,id);
        private FoodAwarenessImage AwarenessSample(ResidentMeal meal,CitizenState person,FoodRoutineStop stop,int day,bool atStop,FoodAwarenessDump? dump=null,
            FoodAwarenessImage? reuse=null,List<FoodAwarenessCandidate>? spareCandidates=null)
        {
            if(CampaignProvider is IFoodCampaignAwarenessProvider campaigns && campaigns.HasEvidence)
                campaigns.Prepare(meal.ResidentId,day,city.State.Minute);
            awarenessAtStop=atStop;awarenessStopPlace=stop.PlaceId;
            return AwarenessCatalogue().Sample(city.State.Seed,meal.ResidentId,day,Anchor(person.HomeId),Anchor(person.DayPlaceId),Anchor(stop.PlaceId),
                AwarenessSettings,awarenessEligible ??= IsAwarenessEligible,
                city.State.Capabilities.Active ? MemoryProvider ?? storedMemoryProvider : EmptyFoodAwarenessProvider.Instance,CampaignProvider,dump,atStop ? stop.PlaceId : null,
                reuse,spareCandidates);
        }
        internal FoodAwarenessDump? InspectAwareness(string consumerId,int? day)
        {
            if(!records.TryGetValue(consumerId,out var meal)) return null;
            var person=Owner(meal);if(person==null) return null;
            while(NeedsRivalPreparation(city.State.Minute) || !AwarenessReady) ContinueAwarenessPreparation();
            var stop=ChoiceStop(meal,person);bool atStop=meal.Decision!=null
                ? meal.Decision.ContextKey.StartsWith("stop:",StringComparison.Ordinal) : AtFoodStop(person);
            var dump=new FoodAwarenessDump {ConsumerId=consumerId,Day=day ?? meal.Day,
                HeldImage=meal.Decision?.Awareness,HeldShortlist=meal.Decision?.Shops.Select(s=>s.BusinessId).ToArray() ?? Array.Empty<string>()};
            var image=AwarenessSample(meal,person,stop,day ?? meal.Day,atStop,dump);
            var context=meal.Decision?.ContextKey ?? Context(stop,atStop);
            dump.Shortlist=AwarenessCatalogue().Rank(image,city.FindPlace(stop.PlaceId).Position,Traits(meal,person),meal.Decision?.Tuning?.Settings ?? ChoiceSettings,
                new FoodDrawContext(city.State.Seed,meal.ResidentId,day ?? meal.Day,meal.Kind,context,meal.Decision?.Attempt ?? meal.ContextAttempts+1),
                routes is IFoodWalkingPace walking ? walking.WalkingMetresPerMinute : 120, id=>MemoryContribution(meal,person,id),rivalWeight:RivalSettings.RivalChoiceWeight,weatherTravelMultiplier:meal.Decision?.WeatherTravelMultiplier ?? 1).Select(e=>e.BusinessId).ToArray();
            return dump;
        }
        private ResidentMeal? pendingDecision;
        private FoodRoutineStop? capturedStop;
        private readonly FoodRoutineStop capturedStopBuffer=new FoodRoutineStop();
        private double? choiceMilliseconds;
        private readonly Dictionary<FoodDecisionShop,FoodBusiness> evaluatedBusinesses = new Dictionary<FoodDecisionShop,FoodBusiness>();
        private readonly List<FoodBusiness> spareMaterializedBusinesses=new List<FoodBusiness>(FoodChoicePolicy.MaximumShortlist);
        private readonly List<FoodOffer> spareMaterializedOffers=new List<FoodOffer>(FoodChoicePolicy.MaximumShortlist*4);
        private readonly List<FoodDecisionShop> spareDecisionShops=new List<FoodDecisionShop>(FoodChoicePolicy.MaximumShortlist);
        private readonly List<FoodDecisionOffer> spareDecisionOffers=new List<FoodDecisionOffer>(FoodChoicePolicy.MaximumShortlist*4);
        private readonly List<OpeningWindow> spareOpeningWindows=new List<OpeningWindow>(FoodChoicePolicy.MaximumShortlist*7);
        private readonly List<FoodAwarenessCandidate> spareAwarenessCandidates=new List<FoodAwarenessCandidate>(FoodAwarenessCatalogue.MaximumSourceSlots);
        private FoodAwarenessImage? spareAwarenessImage;
        private FoodDecisionState? spareDecisionState,recycledDecision;
        private readonly List<FoodBusiness> recycledMaterializedBusinesses=new List<FoodBusiness>(FoodChoicePolicy.MaximumShortlist);
        private readonly FoodScope[] scratchScopes=CreateScratchScopes();
        private readonly FoodAwarenessCatalogue.Entry[] scratchRanked=new FoodAwarenessCatalogue.Entry[FoodChoicePolicy.MaximumShortlist];
        private readonly double[] scratchRanks=new double[FoodChoicePolicy.MaximumShortlist];
        private readonly double[] scratchContributions=new double[FoodChoicePolicy.MaximumShortlist];
        private static FoodScope[] CreateScratchScopes()
        {
            var scopes=new FoodScope[FoodChoicePolicy.MaximumShortlist];
            for(int i=0;i<scopes.Length;i++) scopes[i]=new FoodScope();
            return scopes;
        }
        private FoodBusiness CapturedBusiness(FoodDecisionShop shop)
        {
            if(!evaluatedBusinesses.TryGetValue(shop,out var business))
            {business=Materialize(shop,!city.ObservingFood);evaluatedBusinesses.Add(shop,business);}
            return business;
        }
        private void RecyclePendingDecision()
        {
            for(int i=0;i<recycledMaterializedBusinesses.Count;i++)
            {
                var b=recycledMaterializedBusinesses[i];
                for(int j=0;j<b.Offers.Count;j++) spareMaterializedOffers.Add(b.Offers[j]);
                b.Offers.Clear();b.Hours=EmptyOpeningWindows;
                spareMaterializedBusinesses.Add(b);
            }
            recycledMaterializedBusinesses.Clear();
            var d=recycledDecision;recycledDecision=null;
            if(d==null) return;
            for(int i=0;i<d.Shops.Count;i++)
            {
                var s=d.Shops[i];
                s.Outward=null;s.Return=null;
                for(int j=0;j<s.Offers.Count;j++) {var o=s.Offers[j];o.Metadata=null;spareDecisionOffers.Add(o);}
                s.Offers.Clear();
                for(int j=0;j<s.Hours.Count;j++) spareOpeningWindows.Add(s.Hours[j]);
                s.Hours.Clear();
                spareDecisionShops.Add(s);
            }
            d.Shops.Clear();
            if(d.Awareness!=null)
            {
                for(int i=0;i<d.Awareness.Union.Count;i++) spareAwarenessCandidates.Add(d.Awareness.Union[i]);
                d.Awareness.Union.Clear();
                spareAwarenessImage=d.Awareness;
                d.Awareness=null;
            }
            d.RoutesValidatedBy=null;d.Tuning=null;
            spareDecisionState=d;
        }
        private static readonly List<OpeningWindow> EmptyOpeningWindows=new List<OpeningWindow>(0);
        internal bool HasPendingDecision => pendingDecision != null;
        private bool NewChoice(ResidentMeal meal) => meal.Day>=state.ChoiceStartsDay && state.ChoicePolicyVersion==FoodChoicePolicy.Version;
        private ResidentTraits Traits(ResidentMeal meal,CitizenState person) => meal.RepresentedTraits ?? person.Traits!;

        private FoodRoutineStop ChoiceStop(ResidentMeal meal,CitizenState p,FoodRoutineStop? into=null)
        {
            if(meal.HostSlot>0) return routineStops[p.Identity.Id];
            double admission=0;
            for(int i=p.RecentVisits.Count-1;i>=0;i--)
                if(p.RecentVisits[i].PlaceId==p.PlaceId && p.RecentVisits[i].Activity==p.Activity)
                {admission=p.RecentVisits[i].AtMinute;break;}
            var stop=into ?? new FoodRoutineStop();
            stop.AnchorId=p.Identity.Id;stop.PlaceId=p.PlaceId;stop.Activity=p.Activity;
            stop.CapturedAt=admission;stop.DepartureAt=Math.Min(p.NextEventMinute,p.ActivityUntilMinute);
            return stop;
        }
        private readonly FoodRoutineStop continuationStop=new FoodRoutineStop();
        private readonly FoodConsumer quoteConsumer=new FoodConsumer();
        private TravelPreferences? quotePreferences;
        private static string Context(FoodRoutineStop stop,bool atStop)
        {
            var hash=TextDigest.Begin().Append(stop.PlaceId.Length).Append(':').Append(stop.PlaceId).Append('|')
                .Append((int)stop.Activity).Append('|').AppendRoundTrip(stop.CapturedAt).Append('|').AppendRoundTrip(stop.DepartureAt).Hash();
            return (atStop ? "stop:" : "trip:")+Convert.ToBase64String(hash);
        }
        private FoodDrawContext Draw(ResidentMeal meal,FoodDecisionState d) => new FoodDrawContext(city.State.Seed,meal.ResidentId,meal.Day,meal.Kind,d.ContextKey,d.Attempt);
        private static FoodRoutineStop CopyStopInto(FoodRoutineStop s,FoodRoutineStop target)
        {target.PlaceId=s.PlaceId;target.Activity=s.Activity;target.CapturedAt=s.CapturedAt;target.DepartureAt=s.DepartureAt;return target;}
        internal static FoodScope FreezeScope(FoodScope s)
        {
            var copy=new FoodScope {AlternativeKind=s.AlternativeKind,BusinessId=s.BusinessId,PlaceId=s.PlaceId,
                ProductId=s.ProductId,AdditionalProductIds=new List<string>(s.AdditionalProductIds),NextServiceAt=s.NextServiceAt};
            copy.UseChannel(s.Channel);
            return copy;
        }
        private static FoodScope FreezeScopeInto(FoodScope s,FoodScope copy)
        {
            copy.AlternativeKind=s.AlternativeKind;copy.BusinessId=s.BusinessId;copy.PlaceId=s.PlaceId;
            copy.ProductId=s.ProductId;copy.AdditionalProductIds.Clear();copy.AdditionalProductIds.AddRange(s.AdditionalProductIds);
            copy.NextServiceAt=s.NextServiceAt;copy.UseChannel(s.Channel);
            return copy;
        }
        private bool MatchesStop(FoodRoutineStop stop,FoodDecisionState d,bool atStop)
        {
            if(capturedStop==null)
            {
                if(Context(stop,atStop)!=d.ContextKey) return false;
                capturedStop=CopyStopInto(stop,capturedStopBuffer);
            }
            return capturedStop.PlaceId==stop.PlaceId && capturedStop.Activity==stop.Activity
                && capturedStop.CapturedAt==stop.CapturedAt && capturedStop.DepartureAt==stop.DepartureAt;
        }

        private double SearchAllowance(double minutes)
        {
            double pace=routes is IFoodWalkingPace walking ? walking.WalkingMetresPerMinute : 1000000;
            return Math.Min(1e9,Math.Max(0,minutes)*pace+.1);
        }
        private TravelPreferences FoodPreferences(double minutes) => new TravelPreferences {
            AllowTransit=false,OwnsCar=false,WalkingToleranceMetres=SearchAllowance(minutes) };
        private TravelPreferences QuotePreferences(double minutes)
        {
            double tolerance=SearchAllowance(minutes);
            var held=quotePreferences;
            if(held!=null && !held.AllowTransit && !held.OwnsCar && held.WalkingToleranceMetres==tolerance && held.FareWeightMinutes==2) return held;
            return quotePreferences=FoodPreferences(minutes);
        }
        internal TravelPreferences ReturnPreferences(CitizenState person,ResidentMeal? meal=null)
        {
            if(meal==null) records.TryGetValue(person.Identity.Id,out meal);
            return meal!=null && meal.ContextAttempts>0 ? FoodPreferences(Math.Max(meal.ReturnMinutes,meal.Deadline-meal.DesiredAt))
                : new TravelPreferences {AllowTransit=false,OwnsCar=false,WalkingToleranceMetres=person.Preferences.WalkingToleranceMetres};
        }

        private void BeginFoodDecision(ResidentMeal meal,CitizenState person,bool atStop)
        {
            RecyclePendingDecision();
            CaptureRoutineStop(person);
            var stop=ChoiceStop(meal,person,continuationStop); string context=Context(stop,atStop);
            if(meal.LastContextKey==context || meal.ContextAttempts==FoodChoicePolicy.MaximumContextsPerMeal)
            { Finish(meal,MealStatus.Missed,meal.LastContextKey==context ? "This meal situation was already considered" : "Meal decision limit reached");return; }
            double now=city.State.Minute;
            double deadline=atStop ? Math.Min(meal.ExpiresAt,stop.DepartureAt) : Math.Min(meal.ExpiresAt,
                Math.Min(stop.DepartureAt,now+(meal.HostSlot==0 ? MealRhythm.BreakMinutes(person,city.State.Seed,meal.Kind) : RepresentedBreak(person,stop,meal.Kind))));
            bool reuse=!city.ObservingFood;
            FoodDecisionState d;
            if(reuse && spareDecisionState!=null)
            {
                d=spareDecisionState;spareDecisionState=null;
                d.Weather=default;d.WeatherTravelMultiplier=1;d.Awareness=null;d.PolicyVersion=FoodChoicePolicy.Version;
                d.Result=default;d.BusinessId="";d.ProductId="";d.Outside=default;d.Reason=default;d.Factor1=default;d.Factor2=default;
                d.QuotesDone=0;d.RoutesValidated=0;d.RoutesValidatedBy=null;d.RoutesValidatedRevision=0;d.Shops.Clear();
            }
            else d=new FoodDecisionState();
            d.Tuning=FoodDecisionTuning.Capture(ChoiceSettings);d.IntentId=meal.IntentId;d.ContextKey=context;
            d.Attempt=meal.ContextAttempts+1;d.OriginAt=now;d.Deadline=deadline;
            using(var captureBatch=(market as IFoodCandidateMarket)?.BeginCapture(now))
            {
            if(meal.Day>=state.AwarenessStartsDay && !AwarenessReady) { if(reuse) { d.Tuning=null; spareDecisionState=d; } return; }
            d.Weather=city.CaptureWeather(now);d.WeatherTravelMultiplier=city.RainTravelMultiplier(d.Weather);
            long choiceStarted=city.ObservingFood ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
            var policy=ProductPolicy();
            FoodScope[] scopes;
            int scopeCount;
            double[] contributions=scratchContributions;
            if(meal.Day>=state.AwarenessStartsDay)
            {
                scopes=scratchScopes;
                scopeCount=AwareShortlistInto(meal,person,stop,d,atStop,scopes,reuse);
            }
            else if(atStop)
            {
                scopes=ScopesAtInto(stop.PlaceId,scratchScopes,out scopeCount);
            }
            else
            {
                scopes=scratchScopes;
                scopeCount=ShortlistInto(meal,person,stop,Draw(meal,d),d.Tuning.Settings,scopes,contributions,d.WeatherTravelMultiplier);
            }
            if (atStop || meal.Day>=state.AwarenessStartsDay)
            {
                if(scopeCount>contributions.Length) contributions=new double[scopeCount];
                for(int i=0;i<scopeCount;i++) contributions[i]=MemoryContribution(meal,person,scopes[i].BusinessId);
            }
            choiceMilliseconds=city.ObservingFood ? (System.Diagnostics.Stopwatch.GetTimestamp()-choiceStarted)*1000.0/System.Diagnostics.Stopwatch.Frequency : (double?)null;
            for (int scopeIndex=0; scopeIndex<scopeCount; scopeIndex++)
            {
                var scope=scopes[scopeIndex];
                var b=scope.AlternativeKind==FoodAlternativeKind.Rival ? CaptureRival(scope.BusinessId,now,deadline) : market is IFoodCandidateMarket capture ? capture.CaptureBusiness(scope.BusinessId,now) : ReadBusiness(scope.BusinessId,now);
                if(b==null || b.Id!=scope.BusinessId || b.PlaceId!=scope.PlaceId) continue;
                int scopedOffers=scope.AdditionalProductIds.Count+1;
                if(scopedOffers>d.Tuning.Settings.FoodMenuMaximumOffersPerShop || b.Offers.Count>FoodMenuPolicy.MaximumOffers) continue;
                bool supportedMenu=b.Offers.Count<=scopedOffers;
                for(int o=0;o<b.Offers.Count;o++)
                    if(!policy.Executable(b.Offers[o].ProductId) || !scope.ContainsProduct(b.Offers[o].ProductId)) supportedMenu=false;
                if(!supportedMenu) continue;
                FoodDecisionShop shop;
                if(reuse && spareDecisionShops.Count>0)
                {
                    int last=spareDecisionShops.Count-1;
                    shop=spareDecisionShops[last];spareDecisionShops.RemoveAt(last);
                    shop.Outward=null;shop.Return=null;shop.OutwardQuote=default;shop.ReturnQuote=default;
                    shop.Offers.Clear();shop.Hours.Clear();
                }
                else shop=new FoodDecisionShop();
                shop.AlternativeKind=b.AlternativeKind;shop.RivalTerms=b.RivalTerms;shop.RivalServiceMinutes=b.RivalServiceMinutes;
                shop.AlternativeWeight=b.AlternativeKind==FoodAlternativeKind.Rival ? RivalSettings.RivalChoiceWeight : 1;
                shop.MemoryContribution=contributions[scopeIndex];shop.BusinessId=b.Id;shop.PlaceId=b.PlaceId;
                shop.MealsPerHour=b.MealsPerHour ?? -1;shop.NextServiceAt=Math.Max(now,scope.NextServiceAt);
                shop.ServiceUnavailable=b.ServiceUnavailableReason.Length>0;shop.CustomerServicePercent=b.CustomerServicePercent ?? -1;
                shop.DistrictId=b.DistrictId;
                CopyHoursInto(b.Hours,shop.Hours,reuse);
                for(int o=0;o<b.Offers.Count;o++)
                {
                    var offer=b.Offers[o];
                    int held=0;
                    if(offer.Units.HasValue) reservations.TryGetValue(new ReservationSlot(b.PlaceId,offer.ProductId),out held);
                    int units=offer.Units.HasValue ? Math.Max(0,offer.Units.Value-held) : -1;
                    FoodDecisionOffer item;
                    if(reuse && spareDecisionOffers.Count>0)
                    {
                        int last=spareDecisionOffers.Count-1;
                        item=spareDecisionOffers[last];spareDecisionOffers.RemoveAt(last);
                    }
                    else item=new FoodDecisionOffer();
                    item.ProductId=offer.ProductId;item.Price=offer.Price;
                    item.Metadata=policy.Capture(offer.ProductId,Traits(meal,person),city.State.Seed,meal.ResidentId);
                    item.Units=units;item.DistrictDemandPercent=offer.DistrictDemandPercent ?? -1;
                    shop.Offers.Add(item);
                }
                d.Shops.Add(shop);
            }
            }
            meal.Memory.Weather=d.Weather;
            meal.ContextAttempts=d.Attempt;meal.LastContextKey=context;meal.Decision=d;pendingDecision=meal;
            capturedStop=CopyStopInto(stop,capturedStopBuffer);
        }

        private FoodScope[] ScopesAtInto(string placeId,FoodScope[] buffer,out int count)
        {
            count=0;
            for(int i=0;i<state.Shops.Count;i++) if(state.Shops[i].PlaceId==placeId) count++;
            if(count==0) return buffer;
            var scopes=count<=buffer.Length ? buffer : new FoodScope[count];
            int at=0;
            for(int i=0;i<state.Shops.Count;i++)
                if(state.Shops[i].PlaceId==placeId)
                {
                    scopes[at]=at<buffer.Length ? FreezeScopeInto(state.Shops[i],buffer[at]) : FreezeScope(state.Shops[i]);
                    at++;
                }
            return scopes;
        }
        private void CopyHoursInto(List<OpeningWindow> hours,List<OpeningWindow> target,bool reuse)
        {
            if(target.Capacity<hours.Count) target.Capacity=hours.Count;
            for(int i=0;i<hours.Count;i++)
            {
                var h=hours[i];
                OpeningWindow w;
                if(reuse && spareOpeningWindows.Count>0)
                {
                    int last=spareOpeningWindows.Count-1;
                    w=spareOpeningWindows[last];spareOpeningWindows.RemoveAt(last);
                }
                else w=new OpeningWindow();
                w.Day=h.Day;w.FromMinute=h.FromMinute;w.ToMinute=h.ToMinute;
                target.Add(w);
            }
        }
        private static FoodBusiness BusinessById(List<FoodBusiness> businesses,string id)
        {
            foreach(var b in businesses) if(b.Id==id) return b;
            throw new InvalidOperationException("Sequence contains no matching element");
        }
        private static FoodDecisionShop ShopById(List<FoodDecisionShop> shops,string id)
        {
            foreach(var s in shops) if(s.BusinessId==id) return s;
            throw new InvalidOperationException("Sequence contains no matching element");
        }
        private static FoodChoice ChoiceFor(List<FoodChoice> choices,string business,string product)
        {
            foreach(var c in choices) if(c.BusinessId==business && c.ProductId==product) return c;
            throw new InvalidOperationException("Sequence contains no matching element");
        }
        private IFoodProductPolicy ProductPolicy()
            => city.State.MenuPolicy==FoodNameIdentity.DynamicWire
                && market is IFoodProductPolicyMarket policies ? policies.ProductPolicy : FoodProductPolicy.OriginalThree;

        private static FoodProductMetadata? HeldDynamic(FoodDecisionShop shop,string product)
        {
            foreach(var offer in shop.Offers)
                if(offer.ProductId==product)
                {
                    var m=offer.Metadata;
                    return m==null || m.Revision!=2 ? (FoodProductMetadata?)null
                        : new FoodProductMetadata((MealClass)m.MealClass,m.TasteBand,m.BreakfastFitCents,m.LunchFitCents,m.DinnerFitCents);
                }
            return null;
        }

        private int AwareShortlistInto(ResidentMeal meal,CitizenState person,FoodRoutineStop stop,FoodDecisionState decision,bool atStop,FoodScope[] into,bool reuse)
        {
            var catalogue=AwarenessCatalogue();
            var image=reuse ? spareAwarenessImage : null;
            if(image!=null) spareAwarenessImage=null;
            decision.Awareness=AwarenessSample(meal,person,stop,meal.Day,atStop,null,image,reuse ? spareAwarenessCandidates : null);
            double pace=routes is IFoodWalkingPace walking ? walking.WalkingMetresPerMinute : 120;
            memoryMeal=meal;memoryPerson=person;
            int count;
            try
            {
                count=catalogue.RankInto(decision.Awareness,city.FindPlace(stop.PlaceId).Position,Traits(meal,person),decision.Tuning!.Settings,Draw(meal,decision),pace,
                    scratchRanked,scratchRanks,memoryContributionDelegate ??= CurrentMemoryContribution,rivalWeight:RivalSettings.RivalChoiceWeight,weatherTravelMultiplier:decision.WeatherTravelMultiplier);
            }
            finally { memoryMeal=null; memoryPerson=null; }
            for(int i=0;i<count;i++) { FreezeScopeInto(scratchRanked[i].Scope!,into[i]); scratchRanked[i]=null!; }
            return count;
        }

        private int ShortlistInto(ResidentMeal meal,CitizenState person,FoodRoutineStop stop,FoodDrawContext draw,FoodChoiceSettings tuning,FoodScope[] into,double[] contributions,double weatherTravelMultiplier)
        {
            var ranks=scratchRanks;int count=0;
            var origin=city.FindPlace(stop.PlaceId);
            var traits=Traits(meal,person);
            var distancePreference=new FoodChoicePolicy.DistancePreference(traits,tuning,weatherTravelMultiplier);
            double pace=routes is IFoodWalkingPace walking ? walking.WalkingMetresPerMinute : 120;
            for(int s=0;s<state.Shops.Count;s++)
            {
                var scope=state.Shops[s];
                if(!VendorCandidate(scope)) continue;
                double minutes=Position.Distance(origin.Position,city.FindPlace(scope.PlaceId).Position)/pace;
                double contribution=MemoryContribution(meal,person,scope.BusinessId);
                double rank=FoodDrawContext.Rank(draw.ShopHash(scope.BusinessId),FoodChoicePolicy.ApplyMemory(distancePreference.Weight(minutes,true),contribution,tuning.FoodChoiceWeightFloor));
                int insert=0;
                while(insert<count && (ranks[insert]<rank || ranks[insert]==rank
                    && StringComparer.Ordinal.Compare(scratchScopesRaw[insert].BusinessId,scope.BusinessId)<0)) insert++;
                if(insert==into.Length) continue;
                int last=Math.Min(count,into.Length-1);
                for(int j=last;j>insert;j--) {scratchScopesRaw[j]=scratchScopesRaw[j-1];ranks[j]=ranks[j-1];contributions[j]=contributions[j-1];}
                scratchScopesRaw[insert]=scope;ranks[insert]=rank;contributions[insert]=contribution;count=Math.Min(count+1,into.Length);
            }
            for(int i=0;i<count;i++) { FreezeScopeInto(scratchScopesRaw[i],into[i]); scratchScopesRaw[i]=null!; }
            return count;
        }
        private readonly FoodScope[] scratchScopesRaw=new FoodScope[FoodChoicePolicy.MaximumShortlist];

        private FoodBusiness Materialize(FoodDecisionShop s,bool reuse)
        {
            FoodBusiness business;
            if(reuse && spareMaterializedBusinesses.Count>0)
            {
                int last=spareMaterializedBusinesses.Count-1;
                business=spareMaterializedBusinesses[last];spareMaterializedBusinesses.RemoveAt(last);
                business.Offers.Clear();
            }
            else business=new FoodBusiness();
            business.AlternativeKind=s.AlternativeKind;business.RivalTerms=s.RivalTerms;business.RivalServiceMinutes=s.RivalServiceMinutes;
            business.Id=s.BusinessId;business.PlaceId=s.PlaceId;business.Name=s.PlaceId;
            business.MealsPerHour=s.MealsPerHour<0 ? (int?)null : (int)s.MealsPerHour;business.NextServiceMinute=s.NextServiceAt;
            business.ServiceUnavailableReason=s.ServiceUnavailable ? "Service unavailable" : "";business.Hours=s.Hours;
            business.ServiceEvidence=s.AlternativeKind==FoodAlternativeKind.Rival ? "Estimated availability from advertised price/hours; stock unknown" : "Captured player supply";
            business.CustomerServicePercent=s.CustomerServicePercent<0 ? (int?)null : s.CustomerServicePercent;business.DistrictId=s.DistrictId;
            if(business.Offers.Capacity<s.Offers.Count) business.Offers.Capacity=s.Offers.Count;
            for(int i=0;i<s.Offers.Count;i++)
            {
                var o=s.Offers[i];
                FoodOffer offer;
                if(reuse && spareMaterializedOffers.Count>0)
                {
                    int last=spareMaterializedOffers.Count-1;
                    offer=spareMaterializedOffers[last];spareMaterializedOffers.RemoveAt(last);
                }
                else offer=new FoodOffer();
                offer.ProductId=o.ProductId;offer.Price=o.Price;offer.Units=o.Units<0 ? (int?)null : o.Units;
                offer.DistrictDemandPercent=o.DistrictDemandPercent<0 ? (int?)null : o.DistrictDemandPercent;
                business.Offers.Add(offer);
            }
            return business;
        }

        private FoodIntent DecisionIntent(ResidentMeal meal,CitizenState person,FoodDecisionState d,FoodRoutineStop stop,FoodIntent? reuse=null)
        {
            var intent=reuse==null ? new FoodIntent() : FreshIntent(reuse);
            intent.Id=d.IntentId??meal.IntentId;intent.ConsumerId=meal.ResidentId;intent.Day=meal.Day;intent.DesiredAt=meal.DesiredAt;intent.RequestedAt=d.OriginAt;
            intent.Deadline=d.Deadline;intent.OriginPlaceId=stop.PlaceId;intent.OriginActivity=stop.Activity;intent.EatingMinutes=MealRhythm.EatingMinutes(meal.Kind);
            intent.MaximumQueueMinutes=MealRhythm.QueueMinutes(meal.Kind,stop.Activity);intent.TimeCostMultiplier=1+Math.Min(1,(d.OriginAt-meal.DesiredAt)/120);
            if(meal.Budget?.HomeFoodFundedDay==meal.Day)
            {intent.OutsideOption=FoodOutsideOption.FundedHouseholdFood;intent.OutsideOptionScoreCents=0;}
            return intent;
        }
        internal static FoodIntent FreshIntent(FoodIntent intent)
        {
            if(intent.Choices.Count!=0) throw new InvalidOperationException("A reused food intent still holds choices.");
            var choices=intent.Choices;
            intent.Id="";intent.ConsumerId="";intent.Day=0;intent.RequestedAt=0;intent.Deadline=0;intent.Status=default;
            intent.BusinessId="";intent.ProductId="";intent.ArrivalMinute=null;intent.Spent=default;intent.Outcome="";
            intent.OriginPlaceId="";intent.OriginActivity=default;intent.ContextEvidence="Fixed daily reference; no activity observation";
            intent.DesiredAt=0;intent.EatingMinutes=0;intent.MaximumQueueMinutes=0;intent.TimeCostMultiplier=1;intent.ReturnTravelMinutes=null;
            intent.ServiceEndsAt=null;intent.ReturnBy=null;intent.OutsideOption=default;intent.OutsideOptionScoreCents=null;
            intent.OutsideOptionEvidence="No outside option modeled";intent.Choices=choices;
            return intent;
        }

        private void ContinueFoodDecision(ResidentMeal meal,CitizenState person)
        {
            ContinuationStep="decision-check";
            var d=meal.Decision!;
            if(d.Tuning==null) d.Tuning=FoodDecisionTuning.Capture(ChoiceSettings);
            bool atStop=d.ContextKey.StartsWith("stop:",StringComparison.Ordinal);
            var stop=ChoiceStop(meal,person,continuationStop);
            if(!MatchesStop(stop,d,atStop) || city.State.Minute!=d.OriginAt)
            {EndDecision(meal);Finish(meal,MealStatus.Missed,"Schedule or shop changed before departure");return;}
            var origin=city.FindPlace(stop.PlaceId);
            var preferences=QuotePreferences(d.Deadline-d.OriginAt);
            if(d.Result!=FoodDecisionResult.Evaluating) {ContinuationStep="decision-commit";CommitFoodDecision(meal,person,stop,d,atStop,preferences);return;}
            bool requested=false;
            ContinuationStep="decision-quote";
            while(d.QuotesDone<2*d.Shops.Count)
            {
                var s=d.Shops[d.QuotesDone/2]; bool outward=d.QuotesDone%2==0;
                var shop=city.FindPlace(s.PlaceId);
                Route? result=null; FoodQuoteState progress;
                if(outward)
                {
                    quoteConsumer.DailyAllowance=AvailableForMeal(meal);
                    string? rejection=FoodDemand.KnownUnavailable(CapturedBusiness(s),quoteConsumer,d.OriginAt,d.Deadline);
                    bool impossible=routes is IWalkingTimeBound bound && (s.MealsPerHour>0 || s.AlternativeKind==FoodAlternativeKind.Rival)
                        && d.OriginAt+bound.MinimumWalkingMinutes(origin,shop)+bound.MinimumWalkingMinutes(shop,origin)
                            +s.ServiceMinutes+MealRhythm.EatingMinutes(meal.Kind)>d.Deadline;
                    if(rejection!=null || impossible) progress=FoodQuoteState.NotNeeded;
                    else if(atStop) {result=PresentRoute(origin.Position);progress=FoodQuoteState.Answered;}
                    else
                    {
                        if(requested) return;
                        requested=true;result=RequestFoodQuote(origin,shop,d.OriginAt,preferences);
                        progress=Usable(result,origin,shop) ? FoodQuoteState.Answered : FoodQuoteState.Refused;
                    }
                    s.Outward=progress==FoodQuoteState.Answered ? result : null;s.OutwardQuote=progress;
                }
                else
                {
                    if(s.OutwardQuote!=FoodQuoteState.Answered) progress=FoodQuoteState.NotNeeded;
                    else if(atStop) {result=PresentRoute(origin.Position);progress=FoodQuoteState.Answered;}
                    else
                    {
                        if(requested) return;
                        requested=true;result=RequestFoodQuote(shop,origin,d.OriginAt+s.Outward!.Minutes,preferences);
                        progress=Usable(result,shop,origin) ? FoodQuoteState.Answered : FoodQuoteState.Refused;
                    }
                    s.Return=progress==FoodQuoteState.Answered ? result : null;s.ReturnQuote=progress;
                }
                d.QuotesDone++;
            }
            ContinuationStep=requested ? "decision-quote-evaluate" : "decision-evaluate";
            var intent=EvaluateDecision(meal,person,d,stop,out var businesses,out var quotes);
            var evaluated=evaluatedOffers;evaluated.Clear();
            var allowance=AvailableForMeal(meal);
            foreach(var c in intent.Choices)
            {
                if(!c.ScoreCents.HasValue) continue;
                var b=BusinessById(businesses,c.BusinessId);
                var heldShop=ShopById(d.Shops,c.BusinessId);
                var ticket=CandidateTotal(meal,person,heldShop,c.ProductId,c.Price,allowance);
                double queue=Math.Max(0,b.NextServiceMinute-c.AdmissionMinute!.Value);
                var dynamic=HeldDynamic(heldShop,c.ProductId);
                evaluated.Add(new EvaluatedFoodOffer(c.BusinessId,c.ProductId,c.Price,c.TravelMinutes!.Value,queue,
                    c.ServiceEndsAt!.Value-d.OriginAt+c.ReturnMinutes!.Value+intent.EatingMinutes,d.Deadline-d.OriginAt,
                    c.CustomerServicePercent,c.DistrictDemandPercent,meal.Kind,heldShop.MemoryContribution,heldShop.AlternativeWeight,
                    dynamic.HasValue ? dynamic.Value.TasteBand : FoodMenuPolicy.Code(c.ProductId)>3 ? FoodMenuPolicy.Taste(city.State.Seed,meal.ResidentId,FoodMenuPolicy.Code(c.ProductId)) : -1,
                    campaignActive: d.Awareness?.CampaignActive(c.BusinessId)==true, ticket: ticket, metadata: dynamic));
            }
            long choiceStarted=city.ObservingFood ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
            var pick=FoodChoicePolicy.Select(evaluated,Traits(meal,person),allowance,Draw(meal,d),d.Tuning.Settings,intent.OutsideOption!=FoodOutsideOption.None,d.WeatherTravelMultiplier);
            if(choiceMilliseconds.HasValue) choiceMilliseconds+=(System.Diagnostics.Stopwatch.GetTimestamp()-choiceStarted)*1000.0/System.Diagnostics.Stopwatch.Frequency;
            d.Result=pick.Outside ? FoodDecisionResult.Outside : pick.Index>=0 ? FoodDecisionResult.Selected : FoodDecisionResult.NoFeasible;
            d.Reason=pick.Reason;d.Factor1=pick.Factor1;d.Factor2=pick.Factor2;
            if(pick.Index>=0) {d.BusinessId=evaluated[pick.Index].BusinessId;d.ProductId=evaluated[pick.Index].ProductId;}
            else if(pick.Outside) d.Outside=intent.OutsideOption;
            else d.Reason=FailureReason(intent);
            evaluated.Clear();
        }
        private static Route PresentRoute(Position point) => new Route {Legs=new List<RouteLeg> {new RouteLeg {From=point,To=point,Mode=TravelMode.Walk}}};
        private Route? RequestFoodQuote(Place from,Place to,double at,TravelPreferences preferences)
        {
            try {return routes is ISharedWalkingPlanner shared ? shared.PlanShared(from,to,at,preferences) : WalkingQuote(from,to,at,preferences);}
            catch(Exception) {return null;}
        }
        private static bool Usable(Route? r,Place from,Place to)
        {
            if(r==null || r.Legs.Count==0 || r.Legs.Count>512 || !Position.Finite(r.Minutes) || r.Minutes<=0 || r.Minutes>1440) return false;
            Position previous=from.Position;
            foreach(var leg in r.Legs)
            {
                if(leg.Mode!=TravelMode.Walk || !leg.From.IsFinite || !leg.To.IsFinite || !Position.Finite(leg.Minutes) || leg.Minutes<0
                    || Position.Distance(previous,leg.From)>.01) return false;
                previous=leg.To;
            }
            return Position.Distance(previous,to.Position)<=.01;
        }
        private FoodIntent EvaluateDecision(ResidentMeal meal,CitizenState person,FoodDecisionState d,FoodRoutineStop stop,
            out List<FoodBusiness> businesses,out List<FoodTravelQuote> quotes)
        {
            bool reuse=!city.ObservingFood;
            if(reuse) {RecycleChoices();businesses=scratchBusinesses;businesses.Clear();quotes=scratchQuotes;spareQuotes.AddRange(quotes);quotes.Clear();}
            else {businesses=new List<FoodBusiness>(d.Shops.Count);quotes=new List<FoodTravelQuote>();}
            foreach(var s in d.Shops) businesses.Add(CapturedBusiness(s));
            for(int i=0;i<d.Shops.Count;i++)
                if(d.Shops[i].AlternativeKind==FoodAlternativeKind.Rival) businesses[i].NextServiceMinute=RivalNextService(d.Shops[i],d.OriginAt);
            string intentId=d.IntentId??meal.IntentId;
            foreach(var s in d.Shops)
                if(s.Outward!=null && s.Return!=null)
                {
                    FoodTravelQuote quote;
                    if(reuse && spareQuotes.Count>0) {quote=spareQuotes[spareQuotes.Count-1];spareQuotes.RemoveAt(spareQuotes.Count-1);}
                    else quote=new FoodTravelQuote();
                    quote.BusinessId=s.BusinessId;quote.ConsumerId=meal.ResidentId;quote.IntentId=intentId;quote.OriginPlaceId=stop.PlaceId;
                    quote.Minutes=s.Outward.Minutes;quote.ReturnMinutes=s.Return.Minutes;quote.Evidence="Captured complete walking paths";
                    quotes.Add(quote);
                }
            var consumer=Consumer(person,AvailableForMeal(meal),meal);MealRhythm.Apply(consumer,meal.Kind);
            var intent=DecisionIntent(meal,person,d,stop,reuse ? spareIntent : null);
            if(reuse) recycledIntent=spareIntent=intent;
            var refusals=scratchRefusals;refusals.Clear();
            foreach(var shop in d.Shops)
                if(shop.OutwardQuote==FoodQuoteState.Refused) refusals[shop.BusinessId]="Unreachable";
                else if(shop.ReturnQuote==FoodQuoteState.Refused) refusals[shop.BusinessId]="No confirmed return path";
                else if(shop.OutwardQuote==FoodQuoteState.NotNeeded && FoodDemand.KnownUnavailable(CapturedBusiness(shop),consumer,d.OriginAt,d.Deadline)==null)
                    refusals[shop.BusinessId]="Service and return do not fit available time";
            FoodDemand.ChooseObserved(consumer,businesses,quotes,intent,d.OriginAt,refusals,meal.Kind,true,reuse ? spareChoices : null);
            refusals.Clear();
            rivalSoldOutShops.Clear();
            foreach(var c in intent.Choices)
            {
                if(!c.ScoreCents.HasValue || !c.ServiceEndsAt.HasValue) continue;
                var s=ShopById(d.Shops,c.BusinessId);
                if(s.AlternativeKind!=FoodAlternativeKind.Rival || !SoldOut(s.PlaceId,RivalType(s),c.ServiceEndsAt.Value)) continue;
                c.ScoreCents=null;c.Reason="Out of stock";rivalSoldOutShops.Add(s);
            }
            return intent;
        }
        private readonly List<FoodBusiness> scratchBusinesses=new List<FoodBusiness>(FoodChoicePolicy.MaximumShortlist);
        private readonly List<FoodTravelQuote> scratchQuotes=new List<FoodTravelQuote>(FoodChoicePolicy.MaximumShortlist);
        private readonly List<FoodTravelQuote> spareQuotes=new List<FoodTravelQuote>(FoodChoicePolicy.MaximumShortlist);
        private readonly Dictionary<string,string> scratchRefusals=new Dictionary<string,string>(StringComparer.Ordinal);
        private readonly List<FoodChoice> spareChoices=new List<FoodChoice>();
        private readonly List<EvaluatedFoodOffer> evaluatedOffers=new List<EvaluatedFoodOffer>();
        private FoodIntent? recycledIntent,spareIntent;
        private void RecycleChoices()
        {
            var intent=recycledIntent;recycledIntent=null;
            if(intent==null) return;
            spareChoices.AddRange(intent.Choices);intent.Choices.Clear();
        }
        private static MealReasonCode FailureReason(FoodIntent intent)
        {
            var choices=intent.Choices;
            foreach(var c in choices) if(c.Reason.Contains("unknown")) return MealReasonCode.InsufficientEvidence;
            foreach(var c in choices) if(c.Reason.Contains("queue")) return MealReasonCode.QueueTooLong;
            foreach(var c in choices) if(c.Reason.Contains("available time")) return MealReasonCode.NoLongerFits;
            foreach(var c in choices) if(c.Reason=="Closed or too late") return MealReasonCode.ShopClosed;
            foreach(var c in choices) if(c.Reason=="Service unavailable") return MealReasonCode.ServiceUnavailable;
            return MealReasonCode.NoFeasibleOffer;
        }
        private void EndDecision(ResidentMeal meal)
        {
            var finished=meal.Decision;
            meal.Decision=null;pendingDecision=null;capturedStop=null;
            if(!city.ObservingFood)
            {
                if(finished!=null)
                {
                    for(int i=0;i<finished.Shops.Count;i++) {finished.Shops[i].Outward=null;finished.Shops[i].Return=null;}
                    finished.RoutesValidatedBy=null;
                    recycledDecision=finished;
                }
                foreach(var business in evaluatedBusinesses.Values) recycledMaterializedBusinesses.Add(business);
            }
            evaluatedBusinesses.Clear();rivalSoldOutShops.Clear();
        }
        private bool ValidateCapturedRoute(Place from,Place to,Route route,TravelPreferences preferences,bool present)
        {
            if(present) return route.Minutes==0 && route.Legs.Count==1 && Position.Distance(route.Legs[0].From,from.Position)<=.01
                && Position.Distance(route.Legs[0].To,to.Position)<=.01;
            if(!Usable(route,from,to)) return false;
            if(!(routes is IWalkingQuoteValidity validity)) return true;
            var operation=routes as IWalkingQuoteOperation;operation?.BeginWalkingQuote();
            try {return validity.IsWalkingQuoteValid(from,to,route,preferences);}
            finally {operation?.EndWalkingQuote();}
        }
        private void CommitFoodDecision(ResidentMeal meal,CitizenState person,FoodRoutineStop stop,FoodDecisionState d,bool atStop,TravelPreferences preferences)
        {
            bool sliced=!atStop && routes is IWalkingQuoteValidity && routes is IWalkingQuoteRevision;
            if(sliced && d.Result==FoodDecisionResult.Selected)
            {
                long revision=((IWalkingQuoteRevision)routes).WalkingQuoteRevision;
                if(d.RoutesValidated>0 && (!ReferenceEquals(d.RoutesValidatedBy,routes) || d.RoutesValidatedRevision!=revision)) d.RoutesValidated=0;
                if(d.RoutesValidated<2)
                {
                    var held=ShopById(d.Shops,d.BusinessId);
                    bool outward=d.RoutesValidated==0;
                    if(!ValidateCapturedRoute(city.FindPlace(outward ? stop.PlaceId : held.PlaceId),city.FindPlace(outward ? held.PlaceId : stop.PlaceId),
                        outward ? held.Outward! : held.Return!,preferences,false))
                    {EndDecision(meal);Finish(meal,MealStatus.Missed,"Schedule or shop changed before departure");return;}
                    d.RoutesValidated++;d.RoutesValidatedBy=routes;d.RoutesValidatedRevision=((IWalkingQuoteRevision)routes).WalkingQuoteRevision;
                    return;
                }
            }
            var intent=EvaluateDecision(meal,person,d,stop,out var businesses,out var quotes);
            if(d.Result==FoodDecisionResult.Selected)
            {
                var shop=ShopById(d.Shops,d.BusinessId);
                var choice=ChoiceFor(intent.Choices,d.BusinessId,d.ProductId);
                if(!choice.ScoreCents.HasValue || (shop.AlternativeKind==FoodAlternativeKind.Rival ? !RivalCommitAllowed(meal,shop,choice.Price,d.OriginAt) : market is IFoodBusinessAuthority authority && !authority.CanServe(shop.BusinessId))
                    || !sliced && (!ValidateCapturedRoute(city.FindPlace(stop.PlaceId),city.FindPlace(shop.PlaceId),shop.Outward!,preferences,atStop)
                    || !ValidateCapturedRoute(city.FindPlace(shop.PlaceId),city.FindPlace(stop.PlaceId),shop.Return!,preferences,atStop)))
                {EndDecision(meal);Finish(meal,MealStatus.Missed,"Schedule or shop changed before departure");return;}
                double stay=choice.ServiceEndsAt!.Value-(d.OriginAt+shop.Outward!.Minutes)+intent.EatingMinutes;
                if(!atStop && meal.HostSlot==0 && !city.TryBeginMealVisit(person.Identity.Id,shop.PlaceId,shop.Outward,shop.Return!.Minutes,stay,d.Deadline,
                    deferObservation:true,capturedHours:shop.AlternativeKind==FoodAlternativeKind.Rival ? CapturedBusiness(shop).OpeningPlace : null))
                {EndDecision(meal);Finish(meal,MealStatus.Missed,"Schedule or shop changed before departure");return;}
                if(atStop && shop.AlternativeKind==FoodAlternativeKind.Rival && SoldOut(shop.PlaceId,RivalType(shop),choice.ServiceEndsAt!.Value))
                {RivalSellOutRefusals++;RivalStopStockout(meal,person,shop,d);EndDecision(meal);Finish(meal,MealStatus.Missed,"Service unavailable on arrival");return;}
                if(atStop) city.CancelOptionalStop(person.Identity.Id);
                intent.Status=FoodIntentStatus.Travelling;intent.BusinessId=shop.BusinessId;intent.ProductId=d.ProductId;
                choice.Reason="Chosen: weighted preference draw";
                meal.External=shop.AlternativeKind==FoodAlternativeKind.Rival ? "10"+shop.RivalTerms : "";
                if(shop.AlternativeKind==FoodAlternativeKind.Rival) RivalChoices++;
                meal.PlaceId=shop.PlaceId;meal.BusinessId=shop.BusinessId;meal.ProductId=d.ProductId;
                meal.Price=choice.Price;meal.ServiceMinutes=shop.ServiceMinutes;meal.ReturnMinutes=shop.Return!.Minutes;meal.Deadline=d.Deadline;
                meal.Reason="Chose "+d.ProductId;meal.ExistingStop=atStop;
                if(atStop)
                {
                    meal.ServiceAt=choice.ServiceEndsAt.Value;meal.Status=MealStatus.Serving;meal.DueAt=meal.ServiceAt;
                    if(shop.AlternativeKind==FoodAlternativeKind.Player) ScopeAtPlace(shop.PlaceId)!.NextServiceAt=meal.ServiceAt;
                    else rivalLoad.Admit(meal);
                }
                else
                {
                    meal.Status=MealStatus.Travelling;
                    if(meal.HostSlot>0)
                    {
                        meal.Excursion=new MealExcursion {OriginId=stop.PlaceId,DestinationId=shop.PlaceId,OriginActivity=stop.Activity,
                            OriginPosition=city.FindPlace(stop.PlaceId).Position,DestinationPosition=city.FindPlace(shop.PlaceId).Position,
                            DepartedAt=d.OriginAt,ArrivesAt=d.OriginAt+shop.Outward.Minutes,SharedDepartureAt=stop.DepartureAt,
                            AdmissionAt=choice.AdmissionMinute!.Value,ReturnGeometry=GeometrySignature(shop.Return),MaximumQueueMinutes=intent.MaximumQueueMinutes};
                        meal.DueAt=meal.Excursion.AdmissionAt;
                    }
                    else meal.DueAt=person.NextEventMinute;
                }
                if (city.State.Capabilities.Active) memoryStore.CommitStage(meal,ExperienceStage.Committed);
                Reserve(meal,1);
            }
            else
            {
                intent.Status=d.Result==FoodDecisionResult.Outside ? FoodIntentStatus.DeclinedPurchase : FoodIntentStatus.Unmet;
                if (atStop && d.Result==FoodDecisionResult.NoFeasible) FailedExistingStop(meal,person,d);
                Finish(meal,d.Result==FoodDecisionResult.Outside ? MealStatus.Declined : MealStatus.Missed,
                    d.Result==FoodDecisionResult.Outside ? "Chose household food" : ResidentInsight.ReasonWords(d.Reason),d.Outside);
                var outcome=meal.Today?.Find(meal.Kind);
                if(outcome!=null) {outcome.Reason=d.Reason;outcome.Factor1=d.Factor1;outcome.Factor2=d.Factor2;}
            }
            if(d.Result==FoodDecisionResult.Selected) meal.Reason=ChoiceWords(d);
            EndDecision(meal);
            RecordChoices(intent,businesses,meal.Kind);
            if(city.ObservingFood) city.ObserveFood(new FoodDecisionTrace {Consumer=Consumer(person,AvailableForMeal(meal),meal),
                Intent=intent,Businesses=businesses,Quotes=quotes,MealKind=meal.Kind,Decision=d,ChoiceMilliseconds=choiceMilliseconds,Tickets=CandidateTickets(meal,person,d,intent),
                RouteCalls=atStop ? 0 : d.Shops.Sum(s=>(s.OutwardQuote==FoodQuoteState.Answered || s.OutwardQuote==FoodQuoteState.Refused ? 1 : 0)
                    +(s.ReturnQuote==FoodQuoteState.Answered || s.ReturnQuote==FoodQuoteState.Refused ? 1 : 0))});
            if(d.Result==FoodDecisionResult.Selected && !atStop && meal.HostSlot==0) city.ObserveFoodDeparture(person);
        }
        private static string ChoiceWords(FoodDecisionState d) => "Chose food"+(d.Factor1==MealFactor.Taste ? "; likes it" : "")
            +(d.Factor1==MealFactor.Quality || d.Factor2==MealFactor.Quality ? "; values quality" : "");
    }
}

