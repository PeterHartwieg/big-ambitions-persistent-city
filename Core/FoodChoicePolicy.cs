using System;
using System.Collections.Generic;
using System.Globalization;

namespace PersistentCity.Core
{
    /// <summary>Immutable, bounded feasibility output. No market, routes or writers are available to policy.</summary>
    public readonly struct EvaluatedFoodOffer
    {
        public EvaluatedFoodOffer(string business, string product, Money price, double travel, double queue,
            double usedTime, double window, int? quality, int? demand, MealKind kind, double memoryContribution = 0, double alternativeWeight = 1, int newTasteBand = -1, bool campaignActive = false,
            Money? ticket = null)
        { Ticket=ticket ?? price; CampaignActive=campaignActive; NewTasteBand=newTasteBand; AlternativeWeight=alternativeWeight; MemoryContribution=memoryContribution; BusinessId=business; ProductId=product; Price=price; Travel=travel; Queue=queue; UsedTime=usedTime;
          Window=window; Quality=quality; Demand=demand; Kind=kind; }
        /// <summary>F6j: main plus the extras this resident would add at this shop; equals Price without extras.</summary>
        public Money Ticket { get; }
        public bool CampaignActive { get; }
        public double MemoryContribution { get; }
        public double AlternativeWeight { get; }
        public int NewTasteBand { get; }
        public string BusinessId { get; }
        public string ProductId { get; }
        public Money Price { get; }
        public double Travel { get; }
        public double Queue { get; }
        public double UsedTime { get; }
        public double Window { get; }
        public int? Quality { get; }
        public int? Demand { get; }
        public MealKind Kind { get; }
    }

    /// <summary>Policy v1 key encoding: domain followed by |length:value for EVERY component, including
    /// invariant decimal day/kind/attempt. Prefixes are computed once, with no per-offer key strings.
    /// The household key is two empty IDs, which cannot collide with any real business/product.</summary>
    public readonly struct FoodDrawContext
    {
        public FoodDrawContext(int seed, string consumer, int day, MealKind kind, string context, int attempt)
        {
            uint Prefix(string domain)
            {
                uint h=StableRandom.Append(StableRandom.Begin(seed),domain);
                h=Field(h,consumer); h=Field(h,day.ToString(CultureInfo.InvariantCulture));
                h=Field(h,((int)kind).ToString(CultureInfo.InvariantCulture)); return Field(h,context);
            }
            ShortlistPrefix=Prefix("pc:food-shortlist:v1");
            ChoicePrefix=Field(Prefix("pc:food-choice:v1"),attempt.ToString(CultureInfo.InvariantCulture));
        }
        private uint ShortlistPrefix { get; }
        private uint ChoicePrefix { get; }
        public uint ShopHash(string business) => StableRandom.Finish(Field(ShortlistPrefix,business));
        public uint OfferHash(string business,string product) => StableRandom.Finish(Field(Field(ChoicePrefix,business),product));
        private static uint Field(uint h,string s)
            => StableRandom.Append(StableRandom.Append(StableRandom.AppendDecimal(StableRandom.Append(h,'|'),s.Length),':'),s);
        public static double Rank(uint hash,double weight) => -Math.Log((hash+1.0)/4294967298.0)/weight;
    }

    public readonly struct FoodPolicyResult
    {
        public FoodPolicyResult(int index, bool outside, MealReasonCode reason, MealFactor first=MealFactor.None, MealFactor second=MealFactor.None)
        { Index=index; Outside=outside; Reason=reason; Factor1=first; Factor2=second; }
        public int Index { get; }
        public bool Outside { get; }
        public MealReasonCode Reason { get; }
        public MealFactor Factor1 { get; }
        public MealFactor Factor2 { get; }
    }

    public static partial class FoodChoicePolicy
    {
        private static readonly double[] TasteRatios={.5,.75,1,1.5,2};
        // F6i: common native-scale meal reference (retained rig prices span roughly $9-$17).
        // A power curve gives a neutral resident the same 4/9 weight at +50%, at any price level.
        private const double MealReferenceCents=1250;
        private static double PriceFactor(double cents,ResidentTraits traits,FoodChoiceSettings settings)
            // Deep discounts must not saturate the final weight cap and erase taste/travel/memory.
            // This reward ceiling is inactive throughout the recorded $9-$17 range at default strength.
            => Math.Min(4,Math.Pow(MealReferenceCents/cents,
                2*settings.FoodChoicePriceWeight*(.5+.25*traits.Band(ResidentTrait.PriceSensitivity))));
        // F6j: the shop's expense is the whole ticket. Extras raise it relative to the main at the resident's own price
        // sensitivity; the $12.50 meal anchor applies to the main only. Never above 1, so a cheap side is never a bargain.
        private static double TicketFactor(double mainCents,double ticketCents,ResidentTraits traits,FoodChoiceSettings settings)
            => ticketCents<=mainCents ? 1 : Math.Pow(mainCents/ticketCents,
                2*settings.FoodChoicePriceWeight*(.5+.25*traits.Band(ResidentTrait.PriceSensitivity)));
        internal readonly struct DistancePreference
        {
            private readonly double weight,scale,divisor,floor,temperature;
            internal DistancePreference(ResidentTraits traits,FoodChoiceSettings settings,double weatherTravelMultiplier=1)
            {
                weight=settings.FoodChoiceDistanceWeight*weatherTravelMultiplier;scale=settings.FoodChoiceDistanceScaleMinutes;
                divisor=1+traits.Band(ResidentTrait.TravelWillingness)/2.0;
                floor=settings.FoodChoiceWeightFloor;temperature=settings.FoodShortlistTemperature;
            }
            internal double Weight(double minutes,bool shortlist,bool campaignActive=false)
            {
                // Peter, 1 Nov: all residents receive the same active-campaign preference.
                // Halve the soft distance penalty and add a modest 25% preference even at the door.
                if(campaignActive) minutes*=.5;
                double w=1/(1+weight*Math.Max(0,minutes)/scale*2/divisor);
                if(campaignActive) w*=1.25;
                return shortlist ? Positive(w,floor,temperature) : w;
            }
        }
        public static int Taste(ResidentTraits t,string product) => product==FoodProducts.Croissant ? t.Band(ResidentTrait.CroissantTaste)
            : product==FoodProducts.Burger ? t.Band(ResidentTrait.BurgerTaste) : product==FoodProducts.HotDog ? t.Band(ResidentTrait.HotDogTaste) : ResidentTraits.Neutral;
        private static int OfferTaste(EvaluatedFoodOffer offer,ResidentTraits traits) => offer.NewTasteBand<0 ? Taste(traits,offer.ProductId) : offer.NewTasteBand;

        // Bounded rational signals; unknown quality/demand are neutral. Health, loyalty and brand have no signal.
        // Meal suitability retains the verified MealRhythm table, scaled by the taste coefficient.
        // Both Weight overloads ignore allowance, retained for caller compatibility. Select and HoldPurchase
        // enforce affordability; wallet savings never scale the soft price preference.
        public static double Weight(EvaluatedFoodOffer o, ResidentTraits t, Money allowance, FoodChoiceSettings s,double weatherTravelMultiplier=1)
            => WeightCore(o,t,s,OfferTaste(o,t),MealRhythm.ProductPenalty(o.Kind,o.ProductId),
                o.NewTasteBand<0 ? s.FoodChoiceTasteWeight : s.FoodMenuNewTasteWeight,weatherTravelMultiplier);

        // Pure E5c seam only. Live callers retain the historical overload until schema-25 capture/E5b.
        public static double Weight(EvaluatedFoodOffer o, ResidentTraits t, Money allowance, FoodChoiceSettings s,
            FoodProductMetadata metadata, double weatherTravelMultiplier=1)
            => WeightCore(o,t,s,FoodProducts.KnownLegacy(o.ProductId) ? Taste(t,o.ProductId) : metadata.TasteBand,
                FoodProducts.KnownLegacy(o.ProductId) ? MealRhythm.ProductPenalty(o.Kind,o.ProductId) : metadata.Penalty(o.Kind),
                FoodProducts.KnownLegacy(o.ProductId) ? s.FoodChoiceTasteWeight : s.FoodMenuNewTasteWeight,weatherTravelMultiplier);

        private static double WeightCore(EvaluatedFoodOffer o, ResidentTraits t, FoodChoiceSettings s,
            int band, int fitPenalty, double tasteWeight, double weatherTravelMultiplier)
        {
            double taste=TasteRatios[band];
            double w=tasteWeight==1 ? taste : Math.Pow(taste,tasteWeight);
            w*=PriceFactor(o.Price.ApproximateCents(),t,s);
            w*=TicketFactor(o.Price.ApproximateCents(),o.Ticket.ApproximateCents(),t,s);
            w*=DistanceWeight(o.Travel,t,s,false,weatherTravelMultiplier,o.CampaignActive);
            w/=1+s.FoodChoiceQueueWeight*o.Queue/(2+3*t.Band(ResidentTrait.QueuePatience));
            double quality=(o.Quality.HasValue ? (o.Quality.Value-50)/100.0 : 0)
                +(o.Demand.HasValue ? (o.Demand.Value-50)/100.0 : 0);
            w*=Math.Max(.25,1+s.FoodChoiceQualityWeight*t.Band(ResidentTrait.QualityOrientation)/4.0*quality);
            w/=1+s.FoodChoiceTimePressureWeight*(.5+.25*t.Band(ResidentTrait.TimePressure))*o.UsedTime/Math.Max(1,o.Window);
            w/=1+s.FoodChoiceTasteWeight*fitPenalty/200.0;
            return ApplyMemory(Positive(w,s.FoodChoiceWeightFloor,s.FoodChoiceTemperature), o.MemoryContribution, s.FoodChoiceWeightFloor);
        }
        public static double DistanceWeight(double minutes,ResidentTraits t,FoodChoiceSettings s,bool shortlist,double weatherTravelMultiplier=1,bool campaignActive=false)
            => new DistancePreference(t,s,weatherTravelMultiplier).Weight(minutes,shortlist,campaignActive);
        public static double DistanceWeight(double minutes,ResidentTraits t,FoodChoiceSettings s,bool shortlist,bool campaignActive)
            => DistanceWeight(minutes,t,s,shortlist,1,campaignActive);
        internal static double ApplyMemory(double weight, double contribution, double floor)
            => Math.Max(floor, Math.Min(64, weight * Math.Exp(contribution)));
        private static double Positive(double w,double floor,double temperature)
        {
            if(!Position.Finite(w)) w=floor;
            double bounded=Math.Max(floor,w);
            double result=temperature==1 ? bounded : Math.Pow(bounded,1/temperature);
            return Position.Finite(result) ? Math.Max(floor,Math.Min(64,result)) : floor;
        }

        public static FoodPolicyResult Select(IReadOnlyList<EvaluatedFoodOffer> offers,ResidentTraits traits,Money allowance,
            FoodDrawContext draw,FoodChoiceSettings settings,bool fundedHousehold,double weatherTravelMultiplier=1)
        {
            if (offers.Count>MaximumShortlist*MaximumOffersPerShop || !traits.IsValid) throw new ArgumentException("Invalid policy inputs.");
            int selected=-1;
            // Already-funded cooking is the reference-price equivalent outside option (price factor 1).
            // HouseholdWeight scales its convenience/preference, without a second home-food debit.
            double best=fundedHousehold ? FoodDrawContext.Rank(draw.OfferHash("",""),
                Positive(settings.FoodChoiceHouseholdWeight*PriceFactor(MealReferenceCents,traits,settings),
                    settings.FoodChoiceWeightFloor,settings.FoodChoiceTemperature)) : double.PositiveInfinity;
            for(int i=0;i<offers.Count;i++)
            {
                var o=offers[i];
                if(o.AlternativeWeight<=0 || o.Price<=Money.Zero || o.Price>allowance) continue;
                double rank=FoodDrawContext.Rank(draw.OfferHash(o.BusinessId,o.ProductId),Weight(o,traits,allowance,settings,weatherTravelMultiplier)*o.AlternativeWeight);
                // The reserved empty household IDs precede real IDs in an exact tie.
                int tie=selected<0 ? 1 : StringComparer.Ordinal.Compare(o.BusinessId,offers[selected].BusinessId);
                if(tie==0) tie=StringComparer.Ordinal.Compare(o.ProductId,offers[selected].ProductId);
                if(rank<best || rank==best && tie<0) {best=rank;selected=i;}
            }
            if(selected<0) return new FoodPolicyResult(-1,fundedHousehold,fundedHousehold ? MealReasonCode.PrefersAlternative : MealReasonCode.NoFeasibleOffer,
                fundedHousehold ? MealFactor.Household : MealFactor.None);
            var chosen=offers[selected];
            MealFactor first=OfferTaste(chosen,traits)>2 && (chosen.NewTasteBand<0 ? settings.FoodChoiceTasteWeight : settings.FoodMenuNewTasteWeight)>0 ? MealFactor.Taste : MealFactor.None;
            MealFactor second=chosen.Quality>50 && traits.Band(ResidentTrait.QualityOrientation)>2 && settings.FoodChoiceQualityWeight>0 ? MealFactor.Quality : MealFactor.None;
            if(first==MealFactor.None) {first=second;second=MealFactor.None;}
            return new FoodPolicyResult(selected,false,MealReasonCode.Bought,first,second);
        }
    }
}
