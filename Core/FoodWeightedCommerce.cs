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
        private FoodAwarenessImage AwarenessSample(ResidentMeal meal,CitizenState person,FoodRoutineStop stop,int day,bool atStop,FoodAwarenessDump? dump=null)
        {
            if(CampaignProvider is IFoodCampaignAwarenessProvider campaigns && campaigns.HasEvidence)
                campaigns.Prepare(meal.ResidentId,day,city.State.Minute);
            awarenessAtStop=atStop;awarenessStopPlace=stop.PlaceId;
            return AwarenessCatalogue().Sample(city.State.Seed,meal.ResidentId,day,Anchor(person.HomeId),Anchor(person.DayPlaceId),Anchor(stop.PlaceId),
                AwarenessSettings,awarenessEligible ??= AwarenessEligible,
                city.State.Capabilities.Active ? MemoryProvider ?? storedMemoryProvider : EmptyFoodAwarenessProvider.Instance,CampaignProvider,dump,atStop ? stop.PlaceId : null);
        }
        private bool awarenessAtStop;
        private string? awarenessStopPlace;
        private Func<FoodAwarenessCatalogue.Entry,bool>? awarenessEligible;
        private bool AwarenessEligible(FoodAwarenessCatalogue.Entry e) => (!awarenessAtStop || e.PlaceId==awarenessStopPlace) && e.Scope!.BusinessId==e.BusinessId
            && (e.Kind==FoodAwarenessAlternativeKind.External ? RivalSettings.RivalChoiceWeight>0 && RivalCandidate(e.Scope!) : VendorCandidate(e.Scope!));
        private ResidentMeal? contributionMeal;
        private CitizenState? contributionPerson;
        private Func<string,double>? contribution;
        private Func<string,double> Contribution(ResidentMeal meal,CitizenState person)
        {
            contributionMeal=meal;contributionPerson=person;
            return contribution ??= id=>MemoryContribution(contributionMeal!,contributionPerson!,id);
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
        private FoodDecisionTuning? heldTuning;
        private FoodDecisionTuning CaptureTuning() => heldTuning = FoodDecisionTuning.Capture(ChoiceSettings, heldTuning);
        private ResidentMeal? pendingDecision;
        private FoodRoutineStop? capturedStop;
        private double? choiceMilliseconds;
        private readonly Dictionary<FoodDecisionShop,FoodBusiness> evaluatedBusinesses = new Dictionary<FoodDecisionShop,FoodBusiness>();
        private FoodBusiness CapturedBusiness(FoodDecisionShop shop)
        {
            if(!evaluatedBusinesses.TryGetValue(shop,out var business))
            {business=Materialize(shop);evaluatedBusinesses.Add(shop,business);}
            return business;
        }
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
        private static FoodRoutineStop CopyStop(FoodRoutineStop s) => new FoodRoutineStop {PlaceId=s.PlaceId,Activity=s.Activity,CapturedAt=s.CapturedAt,DepartureAt=s.DepartureAt};
        internal static FoodScope FreezeScope(FoodScope s)
        {
            var copy=new FoodScope {AlternativeKind=s.AlternativeKind,BusinessId=s.BusinessId,PlaceId=s.PlaceId,
                ProductId=s.ProductId,AdditionalProductIds=new List<string>(s.AdditionalProductIds),NextServiceAt=s.NextServiceAt};
            copy.UseChannel(s.Channel);
            return copy;
        }
        private bool MatchesStop(FoodRoutineStop stop,FoodDecisionState d,bool atStop)
        {
            if(capturedStop==null)
            {
                if(Context(stop,atStop)!=d.ContextKey) return false;
                capturedStop=CopyStop(stop);
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
            CaptureRoutineStop(person);
            var stop=ChoiceStop(meal,person); string context=Context(stop,atStop);
            if(meal.LastContextKey==context || meal.ContextAttempts==FoodChoicePolicy.MaximumContextsPerMeal)
            { Finish(meal,MealStatus.Missed,meal.LastContextKey==context ? "This meal situation was already considered" : "Meal decision limit reached");return; }
            double now=city.State.Minute;
            double deadline=atStop ? Math.Min(meal.ExpiresAt,stop.DepartureAt) : Math.Min(meal.ExpiresAt,
                Math.Min(stop.DepartureAt,now+(meal.HostSlot==0 ? MealRhythm.BreakMinutes(person,city.State.Seed,meal.Kind) : RepresentedBreak(person,stop,meal.Kind))));
            var d=new FoodDecisionState {Tuning=CaptureTuning(),IntentId=meal.IntentId,ContextKey=context,Attempt=meal.ContextAttempts+1,OriginAt=now,Deadline=deadline};
            using(var captureBatch=(market as IFoodCandidateMarket)?.BeginCapture(now))
            {
            if(meal.Day>=state.AwarenessStartsDay && !AwarenessReady) return;
            d.Weather=city.CaptureWeather(now);d.WeatherTravelMultiplier=city.RainTravelMultiplier(d.Weather);
            long choiceStarted=city.ObservingFood ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
            double[] contributions=Array.Empty<double>();
            var policy=ProductPolicy();
            var scopes=meal.Day>=state.AwarenessStartsDay ? AwareShortlist(meal,person,stop,d,atStop)
                : atStop ? ScopesAt(stop.PlaceId)
                : Shortlist(meal,person,stop,Draw(meal,d),d.Tuning.Settings,out contributions,d.WeatherTravelMultiplier);
            if (atStop || meal.Day>=state.AwarenessStartsDay)
            {
                contributions=scopes.Length==0 ? Array.Empty<double>() : new double[scopes.Length];
                for(int i=0;i<scopes.Length;i++) contributions[i]=MemoryContribution(meal,person,scopes[i].BusinessId);
            }
            choiceMilliseconds=city.ObservingFood ? (System.Diagnostics.Stopwatch.GetTimestamp()-choiceStarted)*1000.0/System.Diagnostics.Stopwatch.Frequency : (double?)null;
            for (int scopeIndex=0; scopeIndex<scopes.Length; scopeIndex++)
            {
                var scope=scopes[scopeIndex];
                if(scope.AlternativeKind==FoodAlternativeKind.Rival)
                {
                    var rival=CaptureRival(scope.BusinessId,now,deadline);
                    var rivalShop=rival==null ? null : RivalDecisionShop(scope,rival,d,policy,contributions[scopeIndex],now,meal,person);
                    if(rivalShop!=null) d.Shops.Add(rivalShop);
                    continue;
                }
                var b=market is IFoodCandidateMarket capture ? capture.CaptureBusiness(scope.BusinessId,now) : ReadBusiness(scope.BusinessId,now);
                if(b==null || b.Id!=scope.BusinessId || b.PlaceId!=scope.PlaceId) continue;
                int scopedOffers=scope.AdditionalProductIds.Count+1;
                if(scopedOffers>d.Tuning.Settings.FoodMenuMaximumOffersPerShop || b.Offers.Count>FoodMenuPolicy.MaximumOffers) continue;
                bool supportedMenu=b.Offers.Count<=scopedOffers;
                foreach(var offer in b.Offers)
                    if(!policy.Executable(offer.ProductId) || !scope.ContainsProduct(offer.ProductId)) supportedMenu=false;
                if(!supportedMenu) continue;
                var shop=new FoodDecisionShop {AlternativeKind=b.AlternativeKind,RivalTerms=b.RivalTerms,RivalServiceMinutes=b.RivalServiceMinutes,AlternativeWeight=b.AlternativeKind==FoodAlternativeKind.Rival ? RivalSettings.RivalChoiceWeight : 1,MemoryContribution=contributions[scopeIndex],BusinessId=b.Id,PlaceId=b.PlaceId,MealsPerHour=b.MealsPerHour ?? -1,
                    NextServiceAt=Math.Max(now,scope.NextServiceAt),ServiceUnavailable=b.ServiceUnavailableReason.Length>0,
                    CustomerServicePercent=b.CustomerServicePercent ?? -1,DistrictId=b.DistrictId,
                    Hours=CopyHours(b.Hours)};
                foreach(var offer in b.Offers)
                {
                    int held=0;
                    if(offer.Units.HasValue) reservations.TryGetValue(ReservationKey(b.PlaceId,offer.ProductId),out held);
                    int units=offer.Units.HasValue ? Math.Max(0,offer.Units.Value-held) : -1;
                    shop.Offers.Add(new FoodDecisionOffer {ProductId=offer.ProductId,Price=offer.Price,
                        Metadata=policy.Capture(offer.ProductId,Traits(meal,person),city.State.Seed,meal.ResidentId),
                        Units=units,DistrictDemandPercent=offer.DistrictDemandPercent ?? -1});
                }
                d.Shops.Add(shop);
            }
            }
            meal.Memory.Weather=d.Weather;
            meal.ContextAttempts=d.Attempt;meal.LastContextKey=context;meal.Decision=d;pendingDecision=meal;
            capturedStop=CopyStop(stop);
        }

        private FoodScope[] ScopesAt(string placeId)
        {
            int count=0;
            foreach(var s in state.Shops) if(s.PlaceId==placeId) count++;
            if(count==0) return Array.Empty<FoodScope>();
            var scopes=new FoodScope[count];int at=0;
            foreach(var s in state.Shops) if(s.PlaceId==placeId) scopes[at++]=FreezeScope(s);
            return scopes;
        }
        private FoodDecisionShop? RivalDecisionShop(FoodScope scope,FoodAlternativeObservation o,FoodDecisionState d,IFoodProductPolicy policy,double contribution,double now,ResidentMeal meal,CitizenState person)
        {
            if(o.Id!=scope.BusinessId || o.PlaceId!=scope.PlaceId) return null;
            var offers=o.Offers;
            int scopedOffers=scope.AdditionalProductIds.Count+1;
            if(scopedOffers>d.Tuning!.Settings.FoodMenuMaximumOffersPerShop || offers.Count>FoodMenuPolicy.MaximumOffers) return null;
            bool supportedMenu=offers.Count<=scopedOffers;
            for(int i=0;i<offers.Count;i++)
                if(!policy.Executable(offers[i].ProductId) || !scope.ContainsProduct(offers[i].ProductId)) supportedMenu=false;
            if(!supportedMenu) return null;
            var shop=new FoodDecisionShop {AlternativeKind=FoodAlternativeKind.Rival,RivalTerms=RivalFoodTerms.Fingerprint(o),RivalServiceMinutes=RivalSettings.RivalAssumedServiceMinutes,
                AlternativeWeight=RivalSettings.RivalChoiceWeight,MemoryContribution=contribution,BusinessId=o.Id,PlaceId=o.PlaceId,MealsPerHour=-1,
                NextServiceAt=Math.Max(now,scope.NextServiceAt),ServiceUnavailable=false,CustomerServicePercent=o.CustomerServicePercent ?? -1,DistrictId="",
                Hours=RivalFoodTerms.CopyHours(o)};
            for(int i=0;i<offers.Count;i++)
                shop.Offers.Add(new FoodDecisionOffer {ProductId=offers[i].ProductId,Price=offers[i].Price,
                    Metadata=policy.Capture(offers[i].ProductId,Traits(meal,person),city.State.Seed,meal.ResidentId),
                    Units=-1,DistrictDemandPercent=-1});
            return shop;
        }
        private static List<OpeningWindow> CopyHours(List<OpeningWindow> hours)
        {
            var copy=new List<OpeningWindow>(hours.Count);
            foreach(var h in hours) copy.Add(new OpeningWindow {Day=h.Day,FromMinute=h.FromMinute,ToMinute=h.ToMinute});
            return copy;
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

        private FoodScope[] AwareShortlist(ResidentMeal meal,CitizenState person,FoodRoutineStop stop,FoodDecisionState decision,bool atStop)
        {
            var catalogue=AwarenessCatalogue();
            decision.Awareness=AwarenessSample(meal,person,stop,meal.Day,atStop);
            double pace=routes is IFoodWalkingPace walking ? walking.WalkingMetresPerMinute : 120;
            var ranked=catalogue.Rank(decision.Awareness,city.FindPlace(stop.PlaceId).Position,Traits(meal,person),decision.Tuning!.Settings,Draw(meal,decision),pace,Contribution(meal,person),rivalWeight:RivalSettings.RivalChoiceWeight,weatherTravelMultiplier:decision.WeatherTravelMultiplier);
            if(ranked.Length==0) return Array.Empty<FoodScope>();
            var scopes=new FoodScope[ranked.Length];
            for(int i=0;i<ranked.Length;i++) scopes[i]=FreezeScope(ranked[i].Scope!);
            return scopes;
        }

        private FoodScope[] Shortlist(ResidentMeal meal,CitizenState person,FoodRoutineStop stop,FoodDrawContext draw,FoodChoiceSettings tuning,out double[] contributions,double weatherTravelMultiplier)
        {
            var selected=new FoodScope[FoodChoicePolicy.MaximumShortlist];
            contributions=new double[FoodChoicePolicy.MaximumShortlist];
            var ranks=new double[FoodChoicePolicy.MaximumShortlist];int count=0;
            var origin=city.FindPlace(stop.PlaceId);
            var traits=Traits(meal,person);
            var distancePreference=new FoodChoicePolicy.DistancePreference(traits,tuning,weatherTravelMultiplier);
            double pace=routes is IFoodWalkingPace walking ? walking.WalkingMetresPerMinute : 120;
            foreach(var scope in state.Shops)
            {
                if(!VendorCandidate(scope)) continue;
                double minutes=Position.Distance(origin.Position,city.FindPlace(scope.PlaceId).Position)/pace;
                double contribution=MemoryContribution(meal,person,scope.BusinessId);
                double rank=FoodDrawContext.Rank(draw.ShopHash(scope.BusinessId),FoodChoicePolicy.ApplyMemory(distancePreference.Weight(minutes,true),contribution,tuning.FoodChoiceWeightFloor));
                int insert=0;
                while(insert<count && (ranks[insert]<rank || ranks[insert]==rank
                    && StringComparer.Ordinal.Compare(selected[insert].BusinessId,scope.BusinessId)<0)) insert++;
                if(insert==selected.Length) continue;
                int last=Math.Min(count,selected.Length-1);
                for(int j=last;j>insert;j--) {selected[j]=selected[j-1];ranks[j]=ranks[j-1];contributions[j]=contributions[j-1];}
                selected[insert]=scope;ranks[insert]=rank;contributions[insert]=contribution;count=Math.Min(count+1,selected.Length);
            }
            if(count!=selected.Length) Array.Resize(ref selected,count);
            for(int i=0;i<selected.Length;i++) selected[i]=FreezeScope(selected[i]);
            return selected;
        }

        private static FoodBusiness Materialize(FoodDecisionShop s) => new FoodBusiness {AlternativeKind=s.AlternativeKind,RivalTerms=s.RivalTerms,RivalServiceMinutes=s.RivalServiceMinutes,Id=s.BusinessId,PlaceId=s.PlaceId,Name=s.PlaceId,
            MealsPerHour=s.MealsPerHour<0 ? (int?)null : (int)s.MealsPerHour,NextServiceMinute=s.NextServiceAt,
            ServiceUnavailableReason=s.ServiceUnavailable ? "Service unavailable" : "",Hours=s.Hours,
            ServiceEvidence=s.AlternativeKind==FoodAlternativeKind.Rival ? "Estimated availability from advertised price/hours; stock unknown" : "Captured player supply",
            CustomerServicePercent=s.CustomerServicePercent<0 ? (int?)null : s.CustomerServicePercent,DistrictId=s.DistrictId,
            Offers=MaterializeOffers(s.Offers)};
        private static List<FoodOffer> MaterializeOffers(List<FoodDecisionOffer> offers)
        {
            var list=new List<FoodOffer>(offers.Count);
            foreach(var o in offers) list.Add(new FoodOffer {ProductId=o.ProductId,Price=o.Price,Units=o.Units<0 ? (int?)null : o.Units,
                DistrictDemandPercent=o.DistrictDemandPercent<0 ? (int?)null : o.DistrictDemandPercent});
            return list;
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
            if(d.Tuning==null) d.Tuning=CaptureTuning();
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
            try {return WalkingQuote(from,to,at,preferences);}
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
        private void EndDecision(ResidentMeal meal) {meal.Decision=null;pendingDecision=null;capturedStop=null;evaluatedBusinesses.Clear();rivalSoldOutShops.Clear();}
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

