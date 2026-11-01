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
        // The next logical event stays unprocessed while the shared image builds. A held decision
        // already owns its inputs and is never delayed/rerolled by a later catalogue publication.
        internal bool NeedsAwarenessPreparation(double minute) => pendingDecision==null && state.ChoicePolicyVersion==FoodChoicePolicy.Version
            && (int)(minute/1440)>=state.ChoiceStartsDay && (int)(minute/1440)>=state.AwarenessStartsDay && !AwarenessReady;
        internal void ContinueAwarenessPreparation()
        {
            long revision=CurrentAwarenessRevision;
            if(awarenessBuilder==null || buildingRevision!=revision || buildingInvalidation!=awarenessInvalidation
                || !ReferenceEquals(buildingScopes,state.Shops) || buildingCount!=state.Shops.Count)
            {
                awarenessBuilder?.Dispose();buildingRevision=revision;buildingInvalidation=awarenessInvalidation;
                buildingScopes=state.Shops;buildingCount=state.Shops.Count;
                awarenessBuilder=FoodAwarenessCatalogue.BeginBuild(state.Shops,city.FindPosition);
            }
            if(!awarenessBuilder.Continue()) return;
            awarenessCatalogue=awarenessBuilder.Result;awarenessBuilder.Dispose();awarenessBuilder=null;
            awarenessRevision=buildingRevision;publishedInvalidation=buildingInvalidation;awarenessScopes=buildingScopes;awarenessCount=buildingCount;
        }
        private FoodAwarenessCatalogue AwarenessCatalogue() => AwarenessReady ? awarenessCatalogue!
            : throw new InvalidOperationException("Awareness image must finish bounded preparation before capture.");
        // Before activation no index is prepared by normal advancement. Retain the live
        // existing-stop lookup until the published index is current, including after an
        // on-demand diagnostic followed by a late scope acquisition.
        private FoodScope? ScopeAtPlace(string place)
            => AwarenessReady ? awarenessCatalogue!.ScopeAtPlace(place)
                : state.Shops.FirstOrDefault(scope=>scope.PlaceId==place);
        private Position? Anchor(string id) => id.Length==0 ? (Position?)null : city.FindPlace(id).Position;
        private FoodAwarenessImage AwarenessSample(ResidentMeal meal,CitizenState person,FoodRoutineStop stop,int day,bool atStop,FoodAwarenessDump? dump=null)
        {
            return AwarenessCatalogue().Sample(city.State.Seed,meal.ResidentId,day,Anchor(person.HomeId),Anchor(person.DayPlaceId),Anchor(stop.PlaceId),
                AwarenessSettings,e=>(!atStop || e.PlaceId==stop.PlaceId) && e.Scope!.BusinessId==e.BusinessId
                    && (!(market is IFoodCandidateMarket m) || m.IsCandidate(e.Scope!)),MemoryProvider ?? storedMemoryProvider,CampaignProvider,dump,atStop ? stop.PlaceId : null);
        }
        internal FoodAwarenessDump? InspectAwareness(string consumerId,int? day)
        {
            if(!records.TryGetValue(consumerId,out var meal)) return null;
            var person=city.FindCitizen(meal.AnchorId);if(person==null) return null;
            // Explicit on-demand inspection may finish the transient index; normal events never drain it inline.
            while(!AwarenessReady) ContinueAwarenessPreparation();
            var stop=ChoiceStop(meal,person);bool atStop=meal.Decision!=null
                ? meal.Decision.ContextKey.StartsWith("stop:",StringComparison.Ordinal) : AtFoodStop(person);
            var dump=new FoodAwarenessDump {ConsumerId=consumerId,Day=day ?? meal.Day,
                HeldImage=meal.Decision?.Awareness,HeldShortlist=meal.Decision?.Shops.Select(s=>s.BusinessId).ToArray() ?? Array.Empty<string>()};
            var image=AwarenessSample(meal,person,stop,day ?? meal.Day,atStop,dump);
            var context=meal.Decision?.ContextKey ?? Context(stop,atStop);
            dump.Shortlist=AwarenessCatalogue().Rank(image,city.FindPlace(stop.PlaceId).Position,Traits(meal,person),meal.Decision?.Tuning?.Settings ?? ChoiceSettings,
                new FoodDrawContext(city.State.Seed,meal.ResidentId,day ?? meal.Day,meal.Kind,context,meal.Decision?.Attempt ?? meal.ContextAttempts+1),
                routes is IFoodWalkingPace walking ? walking.WalkingMetresPerMinute : 120, id=>MemoryContribution(meal,person,id)).Select(e=>e.BusinessId).ToArray();
            return dump;
        }
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

        private FoodRoutineStop ChoiceStop(ResidentMeal meal,CitizenState p)
        {
            if(meal.Slot>0) return routineStops[p.Identity.Id];
            double admission=0;
            for(int i=p.RecentVisits.Count-1;i>=0;i--)
                if(p.RecentVisits[i].PlaceId==p.PlaceId && p.RecentVisits[i].Activity==p.Activity)
                {admission=p.RecentVisits[i].AtMinute;break;}
            return new FoodRoutineStop {AnchorId=p.Identity.Id,PlaceId=p.PlaceId,Activity=p.Activity,
                CapturedAt=admission,DepartureAt=Math.Min(p.NextEventMinute,p.ActivityUntilMinute)};
        }
        // SHA-256 is only a compact, collision-resistant representation of the canonical logical stay,
        // not a draw. It keeps even maximum-length place IDs inside schema 17's 256-character latch.
        private static string Context(FoodRoutineStop stop,bool atStop)
        {
            string text=stop.PlaceId.Length.ToString(CultureInfo.InvariantCulture)+":"+stop.PlaceId+"|"
                +((int)stop.Activity).ToString(CultureInfo.InvariantCulture)+"|"+stop.CapturedAt.ToString("R",CultureInfo.InvariantCulture)
                +"|"+stop.DepartureAt.ToString("R",CultureInfo.InvariantCulture);
            using(var sha=SHA256.Create()) return (atStop ? "stop:" : "trip:")+Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(text)));
        }
        private FoodDrawContext Draw(ResidentMeal meal,FoodDecisionState d) => new FoodDrawContext(city.State.Seed,meal.ResidentId,meal.Day,meal.Kind,d.ContextKey,d.Attempt);
        private static FoodRoutineStop CopyStop(FoodRoutineStop s) => new FoodRoutineStop {PlaceId=s.PlaceId,Activity=s.Activity,CapturedAt=s.CapturedAt,DepartureAt=s.DepartureAt};
        private static FoodScope FreezeScope(FoodScope s) => new FoodScope {BusinessId=s.BusinessId,PlaceId=s.PlaceId,
            ProductId=s.ProductId,AdditionalProductIds=new List<string>(s.AdditionalProductIds),NextServiceAt=s.NextServiceAt};
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
            // Native planners supply their actual current pace. Pure external planners with no pace contract
            // receive a finite search ceiling; their route duration and deadline remain authoritative.
            double pace=routes is IFoodWalkingPace walking ? walking.WalkingMetresPerMinute : 1000000;
            return Math.Min(1e9,Math.Max(0,minutes)*pace+.1);
        }
        private TravelPreferences FoodPreferences(double minutes) => new TravelPreferences {
            AllowTransit=false,OwnsCar=false,WalkingToleranceMetres=SearchAllowance(minutes) };
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
                Math.Min(stop.DepartureAt,now+(meal.Slot==0 ? MealRhythm.BreakMinutes(person,city.State.Seed,meal.Kind) : RepresentedBreak(person,stop,meal.Kind))));
            var d=new FoodDecisionState {Tuning=FoodDecisionTuning.Capture(ChoiceSettings),IntentId=meal.IntentId,ContextKey=context,Attempt=meal.ContextAttempts+1,OriginAt=now,Deadline=deadline};
            // Capture into a private image first. Exceptions cannot publish a partial sample or consume an attempt.
            using(var captureBatch=(market as IFoodCandidateMarket)?.BeginCapture(now))
            {
            // A provider may publish replacement metadata at its capture boundary. Retry this
            // unconsumed event after bounded preparation; never sample a stale/partial image.
            if(meal.Day>=state.AwarenessStartsDay && !AwarenessReady) return;
            long choiceStarted=city.ObservingFood ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
            double[] contributions=Array.Empty<double>();
            var scopes=meal.Day>=state.AwarenessStartsDay ? AwareShortlist(meal,person,stop,d,atStop)
                : atStop ? state.Shops.Where(s=>s.PlaceId==stop.PlaceId).Select(FreezeScope).ToArray()
                : Shortlist(meal,person,stop,Draw(meal,d),d.Tuning.Settings,out contributions);
            if (atStop || meal.Day>=state.AwarenessStartsDay) contributions=scopes.Select(s=>MemoryContribution(meal,person,s.BusinessId)).ToArray();
            choiceMilliseconds=city.ObservingFood ? (System.Diagnostics.Stopwatch.GetTimestamp()-choiceStarted)*1000.0/System.Diagnostics.Stopwatch.Frequency : (double?)null;
            for (int scopeIndex=0; scopeIndex<scopes.Length; scopeIndex++)
            {
                var scope=scopes[scopeIndex];
                var b=market is IFoodCandidateMarket capture ? capture.CaptureBusiness(scope.BusinessId,now) : ReadBusiness(scope.BusinessId,now);
                if(b==null || b.Id!=scope.BusinessId || b.PlaceId!=scope.PlaceId) continue; // no refill or replacement identity
                // A smaller configured menu cannot silently discard offers or steal native demand.
                // Held images retain their captured tuning. The native owner reconciles at its boundary.
                int scopedOffers=scope.AdditionalProductIds.Count+1;
                if(scopedOffers>d.Tuning.Settings.FoodMenuMaximumOffersPerShop || b.Offers.Count>FoodMenuPolicy.MaximumOffers) continue;
                // The catalogue has always omitted scoped products without saved retail prices.
                // Preserve that priced subset; never invent a price or trim a scope to fit a limit.
                bool supportedMenu=b.Offers.Count<=scopedOffers;
                foreach(var offer in b.Offers)
                    if(!FoodProducts.Known(offer.ProductId) || !scope.ContainsProduct(offer.ProductId)) supportedMenu=false;
                if(!supportedMenu) continue; // unsupported or out-of-scope offers reject the whole shop
                // Market inputs belong to the provider: capture before subtracting reservations.
                var shop=new FoodDecisionShop {MemoryContribution=contributions[scopeIndex],BusinessId=b.Id,PlaceId=b.PlaceId,MealsPerHour=b.MealsPerHour ?? -1,
                    NextServiceAt=Math.Max(now,scope.NextServiceAt),ServiceUnavailable=b.ServiceUnavailableReason.Length>0,
                    CustomerServicePercent=b.CustomerServicePercent ?? -1,DistrictId=b.DistrictId,
                    Hours=b.Hours.Select(h=>new OpeningWindow {Day=h.Day,FromMinute=h.FromMinute,ToMinute=h.ToMinute}).ToList()};
                foreach(var offer in b.Offers)
                {
                    reservations.TryGetValue(ReservationKey(b.PlaceId,offer.ProductId),out int held);
                    int units=offer.Units.HasValue ? Math.Max(0,offer.Units.Value-held) : -1;
                    shop.Offers.Add(new FoodDecisionOffer {ProductId=offer.ProductId,Price=offer.Price,
                        Units=units,DistrictDemandPercent=offer.DistrictDemandPercent ?? -1});
                }
                d.Shops.Add(shop);
            }
            }
            meal.ContextAttempts=d.Attempt;meal.LastContextKey=context;meal.Decision=d;pendingDecision=meal;
            capturedStop=CopyStop(stop);
            // No reservations, optional-stop cancellation, purchases or outcomes before commit.
        }

        private FoodScope[] AwareShortlist(ResidentMeal meal,CitizenState person,FoodRoutineStop stop,FoodDecisionState decision,bool atStop)
        {
            var catalogue=AwarenessCatalogue();
            decision.Awareness=AwarenessSample(meal,person,stop,meal.Day,atStop);
            double pace=routes is IFoodWalkingPace walking ? walking.WalkingMetresPerMinute : 120;
            return catalogue.Rank(decision.Awareness,city.FindPlace(stop.PlaceId).Position,Traits(meal,person),decision.Tuning!.Settings,Draw(meal,decision),pace,id=>MemoryContribution(meal,person,id))
                .Select(e=>FreezeScope(e.Scope!)).ToArray();
        }

        private FoodScope[] Shortlist(ResidentMeal meal,CitizenState person,FoodRoutineStop stop,FoodDrawContext draw,FoodChoiceSettings tuning,out double[] contributions)
        {
            var selected=new FoodScope[FoodChoicePolicy.MaximumShortlist];
            contributions=new double[FoodChoicePolicy.MaximumShortlist];
            var ranks=new double[FoodChoicePolicy.MaximumShortlist];int count=0;
            var origin=city.FindPlace(stop.PlaceId);
            var traits=Traits(meal,person);
            var distancePreference=new FoodChoicePolicy.DistancePreference(traits,tuning);
            double pace=routes is IFoodWalkingPace walking ? walking.WalkingMetresPerMinute : 120;
            foreach(var scope in state.Shops)
            {
                if(market is IFoodCandidateMarket candidates && !candidates.IsCandidate(scope)) continue;
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
            // Supply observation may refresh a mutable native scope catalogue. Sampled identities/menu
            // bounds must stay the ones that actually won the metadata draw, even if a shop is replaced.
            for(int i=0;i<selected.Length;i++) selected[i]=FreezeScope(selected[i]);
            return selected;
        }

        private static FoodBusiness Materialize(FoodDecisionShop s) => new FoodBusiness {Id=s.BusinessId,PlaceId=s.PlaceId,Name=s.PlaceId,
            MealsPerHour=s.MealsPerHour<0 ? (int?)null : (int)s.MealsPerHour,NextServiceMinute=s.NextServiceAt,
            ServiceUnavailableReason=s.ServiceUnavailable ? "Service unavailable" : "",Hours=s.Hours,
            CustomerServicePercent=s.CustomerServicePercent<0 ? (int?)null : s.CustomerServicePercent,DistrictId=s.DistrictId,
            Offers=s.Offers.Select(o=>new FoodOffer {ProductId=o.ProductId,Price=o.Price,Units=o.Units<0 ? (int?)null : o.Units,
                DistrictDemandPercent=o.DistrictDemandPercent<0 ? (int?)null : o.DistrictDemandPercent}).ToList()};

        private FoodIntent DecisionIntent(ResidentMeal meal,CitizenState person,FoodDecisionState d,FoodRoutineStop stop)
        {
            var intent=new FoodIntent {Id=meal.IntentId,ConsumerId=meal.ResidentId,Day=meal.Day,DesiredAt=meal.DesiredAt,RequestedAt=d.OriginAt,
                Deadline=d.Deadline,OriginPlaceId=stop.PlaceId,OriginActivity=stop.Activity,EatingMinutes=MealRhythm.EatingMinutes(meal.Kind),
                MaximumQueueMinutes=MealRhythm.QueueMinutes(meal.Kind,stop.Activity),TimeCostMultiplier=1+Math.Min(1,(d.OriginAt-meal.DesiredAt)/120)};
            // Only an actually paid basket is a household alternative under the live new policy.
            if(meal.Budget?.HomeFoodFundedDay==meal.Day)
            {intent.OutsideOption=FoodOutsideOption.FundedHouseholdFood;intent.OutsideOptionScoreCents=0;}
            return intent;
        }

        private void ContinueFoodDecision(ResidentMeal meal,CitizenState person)
        {
            var d=meal.Decision!;
            if(d.Tuning==null) d.Tuning=FoodDecisionTuning.Capture(ChoiceSettings); // legacy image: capture exactly once
            bool atStop=d.ContextKey.StartsWith("stop:",StringComparison.Ordinal);
            var stop=ChoiceStop(meal,person);
            if(!MatchesStop(stop,d,atStop) || city.State.Minute!=d.OriginAt)
            {EndDecision(meal);Finish(meal,MealStatus.Missed,"Schedule or shop changed before departure");return;}
            var origin=city.FindPlace(stop.PlaceId);
            var preferences=FoodPreferences(d.Deadline-d.OriginAt);
            if(d.Result!=FoodDecisionResult.Evaluating) {CommitFoodDecision(meal,person,stop,d,atStop,preferences);return;}
            bool requested=false;
            while(d.QuotesDone<2*d.Shops.Count)
            {
                var s=d.Shops[d.QuotesDone/2]; bool outward=d.QuotesDone%2==0;
                var shop=city.FindPlace(s.PlaceId);
                Route? result=null; FoodQuoteState progress;
                if(outward)
                {
                    var consumer=Consumer(person,AvailableForMeal(meal),meal);
                    string? rejection=FoodDemand.KnownUnavailable(CapturedBusiness(s),consumer,d.OriginAt,d.Deadline);
                    bool impossible=routes is IWalkingTimeBound bound && s.MealsPerHour>0
                        && d.OriginAt+bound.MinimumWalkingMinutes(origin,shop)+bound.MinimumWalkingMinutes(shop,origin)
                            +60/s.MealsPerHour+MealRhythm.EatingMinutes(meal.Kind)>d.Deadline;
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
            var intent=EvaluateDecision(meal,person,d,stop,out var businesses,out var quotes);
            var evaluated=new List<EvaluatedFoodOffer>();
            foreach(var c in intent.Choices)
            {
                if(!c.ScoreCents.HasValue) continue;
                var b=businesses.First(x=>x.Id==c.BusinessId);
                double queue=Math.Max(0,b.NextServiceMinute-c.AdmissionMinute!.Value);
                evaluated.Add(new EvaluatedFoodOffer(c.BusinessId,c.ProductId,c.Price,c.TravelMinutes!.Value,queue,
                    c.ServiceEndsAt!.Value-d.OriginAt+c.ReturnMinutes!.Value+intent.EatingMinutes,d.Deadline-d.OriginAt,
                    c.CustomerServicePercent,c.DistrictDemandPercent,meal.Kind,d.Shops.First(s=>s.BusinessId==c.BusinessId).MemoryContribution,
                    FoodMenuPolicy.Code(c.ProductId)>3 ? FoodMenuPolicy.Taste(city.State.Seed,meal.ResidentId,FoodMenuPolicy.Code(c.ProductId)) : -1));
            }
            long choiceStarted=city.ObservingFood ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
            var pick=FoodChoicePolicy.Select(evaluated,Traits(meal,person),AvailableForMeal(meal),Draw(meal,d),d.Tuning.Settings,intent.OutsideOption!=FoodOutsideOption.None);
            if(choiceMilliseconds.HasValue) choiceMilliseconds+=(System.Diagnostics.Stopwatch.GetTimestamp()-choiceStarted)*1000.0/System.Diagnostics.Stopwatch.Frequency;
            d.Result=pick.Outside ? FoodDecisionResult.Outside : pick.Index>=0 ? FoodDecisionResult.Selected : FoodDecisionResult.NoFeasible;
            d.Reason=pick.Reason;d.Factor1=pick.Factor1;d.Factor2=pick.Factor2;
            if(pick.Index>=0) {d.BusinessId=evaluated[pick.Index].BusinessId;d.ProductId=evaluated[pick.Index].ProductId;}
            else if(pick.Outside) d.Outside=intent.OutsideOption;
            else d.Reason=FailureReason(intent);
            // The selected result is a save boundary. No draw is performed when this image resumes.
        }
        private static Route PresentRoute(Position point) => new Route {Legs=new List<RouteLeg> {new RouteLeg {From=point,To=point,Mode=TravelMode.Walk}}};
        private Route? RequestFoodQuote(Place from,Place to,double at,TravelPreferences preferences)
        {
            try {return WalkingQuote(from,to,at,preferences);}
            catch(Exception) {return null;} // settle this slot as refused; never repeat it or consume another context
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
            businesses=d.Shops.Select(CapturedBusiness).ToList();quotes=new List<FoodTravelQuote>();
            foreach(var s in d.Shops)
                if(s.Outward!=null && s.Return!=null) quotes.Add(new FoodTravelQuote {BusinessId=s.BusinessId,ConsumerId=meal.ResidentId,
                    IntentId=meal.IntentId,OriginPlaceId=stop.PlaceId,Minutes=s.Outward.Minutes,ReturnMinutes=s.Return.Minutes,Evidence="Captured complete walking paths"});
            var consumer=Consumer(person,AvailableForMeal(meal),meal);MealRhythm.Apply(consumer,meal.Kind);
            var intent=DecisionIntent(meal,person,d,stop);
            var refusals=new Dictionary<string,string>(StringComparer.Ordinal);
            foreach(var shop in d.Shops)
                if(shop.OutwardQuote==FoodQuoteState.Refused) refusals[shop.BusinessId]="Unreachable";
                else if(shop.ReturnQuote==FoodQuoteState.Refused) refusals[shop.BusinessId]="No confirmed return path";
                else if(shop.OutwardQuote==FoodQuoteState.NotNeeded && FoodDemand.KnownUnavailable(CapturedBusiness(shop),consumer,d.OriginAt,d.Deadline)==null)
                    refusals[shop.BusinessId]="Service and return do not fit available time";
            FoodDemand.ChooseObserved(consumer,businesses,quotes,intent,d.OriginAt,refusals,meal.Kind,evaluateOnly:true);
            return intent;
        }
        private static MealReasonCode FailureReason(FoodIntent intent)
        {
            if(intent.Choices.Any(c=>c.Reason.Contains("unknown"))) return MealReasonCode.InsufficientEvidence;
            if(intent.Choices.Any(c=>c.Reason.Contains("queue"))) return MealReasonCode.QueueTooLong;
            if(intent.Choices.Any(c=>c.Reason.Contains("available time"))) return MealReasonCode.NoLongerFits;
            if(intent.Choices.Any(c=>c.Reason=="Closed or too late")) return MealReasonCode.ShopClosed;
            if(intent.Choices.Any(c=>c.Reason=="Service unavailable")) return MealReasonCode.ServiceUnavailable;
            return MealReasonCode.NoFeasibleOffer;
        }
        private void EndDecision(ResidentMeal meal) {meal.Decision=null;pendingDecision=null;capturedStop=null;evaluatedBusinesses.Clear();}
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
            var intent=EvaluateDecision(meal,person,d,stop,out var businesses,out var quotes);
            if(d.Result==FoodDecisionResult.Selected)
            {
                var shop=d.Shops.First(s=>s.BusinessId==d.BusinessId);
                var choice=intent.Choices.First(c=>c.BusinessId==d.BusinessId && c.ProductId==d.ProductId);
                if(!choice.ScoreCents.HasValue || market is IFoodBusinessAuthority authority && !authority.CanServe(shop.BusinessId)
                    || !ValidateCapturedRoute(city.FindPlace(stop.PlaceId),city.FindPlace(shop.PlaceId),shop.Outward!,preferences,atStop)
                    || !ValidateCapturedRoute(city.FindPlace(shop.PlaceId),city.FindPlace(stop.PlaceId),shop.Return!,preferences,atStop))
                {EndDecision(meal);Finish(meal,MealStatus.Missed,"Schedule or shop changed before departure");return;}
                double stay=choice.ServiceEndsAt!.Value-(d.OriginAt+shop.Outward!.Minutes)+intent.EatingMinutes;
                if(!atStop && meal.Slot==0 && !city.TryBeginMealVisit(person.Identity.Id,shop.PlaceId,shop.Outward,shop.Return!.Minutes,stay,d.Deadline,deferObservation:true))
                {EndDecision(meal);Finish(meal,MealStatus.Missed,"Schedule or shop changed before departure");return;}
                // Optional presentation changes only after a winner and its feasibility are committed.
                if(atStop) city.CancelOptionalStop(person.Identity.Id);
                intent.Status=FoodIntentStatus.Travelling;intent.BusinessId=shop.BusinessId;intent.ProductId=d.ProductId;
                choice.Reason="Chosen: weighted preference draw";
                meal.PlaceId=shop.PlaceId;meal.BusinessId=shop.BusinessId;meal.ProductId=d.ProductId;
                meal.Price=choice.Price;meal.ServiceMinutes=60/shop.MealsPerHour;meal.ReturnMinutes=shop.Return!.Minutes;meal.Deadline=d.Deadline;
                meal.Reason="Chose "+d.ProductId;meal.ExistingStop=atStop;
                if(atStop)
                {
                    meal.ServiceAt=choice.ServiceEndsAt.Value;meal.Status=MealStatus.Serving;meal.DueAt=meal.ServiceAt;
                    ScopeAtPlace(shop.PlaceId)!.NextServiceAt=meal.ServiceAt;
                }
                else
                {
                    meal.Status=MealStatus.Travelling;
                    if(meal.Slot>0)
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
            RecordChoices(intent,businesses);
            if(city.ObservingFood) city.ObserveFood(new FoodDecisionTrace {Consumer=Consumer(person,AvailableForMeal(meal),meal),
                Intent=intent,Businesses=businesses,Quotes=quotes,MealKind=meal.Kind,Decision=d,ChoiceMilliseconds=choiceMilliseconds,
                RouteCalls=atStop ? 0 : d.Shops.Sum(s=>(s.OutwardQuote==FoodQuoteState.Answered || s.OutwardQuote==FoodQuoteState.Refused ? 1 : 0)
                    +(s.ReturnQuote==FoodQuoteState.Answered || s.ReturnQuote==FoodQuoteState.Refused ? 1 : 0))});
            // A compact choice explanation lives in the existing Reason field until checkout; outcomes preserve factors.
            if(d.Result==FoodDecisionResult.Selected && !atStop && meal.Slot==0) city.ObserveFoodDeparture(person);
        }
        private static string ChoiceWords(FoodDecisionState d) => "Chose food"+(d.Factor1==MealFactor.Taste ? "; likes it" : "")
            +(d.Factor1==MealFactor.Quality || d.Factor2==MealFactor.Quality ? "; values quality" : "");
    }
}

