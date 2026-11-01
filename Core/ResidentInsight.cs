using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace PersistentCity.Core
{
    public enum ResidentMealState { Unknown, NotRecorded, InProgress, Bought, HouseholdFood, Skipped, Unconfirmed }

    public sealed class ResidentMealInsight
    {
        public MealKind Kind { get; set; }
        public ResidentMealState State { get; set; }
        public string Text { get; set; } = "";
    }

    /// <summary>A place the inspector can link to. Street and number come from the native address
    /// encoded in the place ID; places without one (outdoor spots) are shown but not navigable.</summary>
    public sealed class ResidentAddress
    {
        public string PlaceId { get; set; } = "";
        public string Name { get; set; } = "";
        /// <summary>The native street key exactly as in the place ID (e.g. "ba:street_thirdstreet"), used to find the building on the map.</summary>
        public string Street { get; set; } = "";
        public int Number { get; set; }
        public bool CanNavigate => Street.Length > 0;
        public string AddressText { get; private set; } = "";
        public string Label => !CanNavigate ? Name : Name.Length == 0 || Name == AddressText ? AddressText
            : Name.EndsWith(", " + AddressText, StringComparison.Ordinal) ? Name : Name + ", " + AddressText;

        /// <summary>Decode GamePlaces' "address:&lt;escaped street&gt;:&lt;number&gt;:&lt;kind&gt;" form.</summary>
        public static bool TryParse(string placeId, out string street, out int number)
        {
            street = ""; number = 0;
            if (placeId == null || !placeId.StartsWith("address:", StringComparison.Ordinal)) return false;
            var parts = placeId.Split(':');
            if (parts.Length != 4 || parts[1].Length == 0
                || !int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out number)) { number = 0; return false; }
            street = Uri.UnescapeDataString(parts[1]);
            return street.Trim().Length > 0;
        }

        /// <param name="formatAddress">Formats the complete native address, including number/street order. Display only.</param>
        public static ResidentAddress From(Place place, Func<string, int, string>? formatAddress = null)
        {
            var address = new ResidentAddress { PlaceId = place.Id, Name = Readable(place.Name) };
            if (!TryParse(place.Id, out string street, out int number)) return address;
            address.Street = street; address.Number = number;
            string? shown = null;
            // Native street data can be absent during loading or unknown to this game version.
            // A failed display callback must never break a popup or change its map arguments.
            try { shown = formatAddress?.Invoke(street, number); }
            catch (Exception) { }
            string digits = number.ToString(CultureInfo.InvariantCulture);
            address.AddressText = !string.IsNullOrWhiteSpace(shown) && !HasStreetKey(shown!) ? shown!
                : HasStreetKey(street) ? digits + " " + Readable(street) : street + " " + digits;
            // Homes and unnamed businesses are named by their raw native address.
            address.Name = Readable(place.Name.Replace(street + " " + digits, address.AddressText));
            return address;
        }

        private static bool HasStreetKey(string text) => text.IndexOf("ba:street_", StringComparison.OrdinalIgnoreCase) >= 0;

        private static string Readable(string text)
        {
            if (!HasStreetKey(text)) return text;
            return Regex.Replace(text, @"ba:street_([\w]*)", match =>
            {
                string words = match.Groups[1].Value.Replace('_', ' ');
                words = Regex.Replace(words, @"(?<=\w)(street|avenue|road|lane|drive|boulevard)$", " $1", RegexOptions.IgnoreCase);
                words = words.Trim();
                return words.Length == 0 ? "Unknown street" : CultureInfo.InvariantCulture.TextInfo.ToTitleCase(words);
            }, RegexOptions.IgnoreCase);
        }
    }

    /// <summary>
    /// Read-only view of one resident for the inspector, built from facts the city already stores.
    /// Capture never changes the city, draws no random number, plans no route and reads no market.
    /// Today's meals come from the recorded current-day outcomes plus the live meal in progress; a meal
    /// with no record is "unknown", and meals that ended before the schema-17 upgrade are "not recorded".
    /// </summary>
    public sealed class ResidentInsight
    {
        public string ResidentId { get; private set; } = "";
        public string Name { get; private set; } = "";
        public int Age { get; private set; }
        public ResidentAddress? Home { get; private set; }
        public ResidentAddress? Work { get; private set; }
        public string WorkRole { get; private set; } = "";
        public IReadOnlyList<string> Personality { get; private set; } = Array.Empty<string>();
        public IReadOnlyList<string> MemoryWords { get; private set; } = Array.Empty<string>();
        public string Story { get; private set; } = "";
        public string Heading { get; private set; } = "";
        public string HeadingReason { get; private set; } = "";
        public IReadOnlyList<ResidentMealInsight> Meals { get; private set; } = Array.Empty<ResidentMealInsight>();
        public string MealNote { get; private set; } = "";
        public double Minute { get; private set; }

        private const string UnknownPlace = "an unknown place";

        public static string DefaultProductName(string productId) => productId == FoodProducts.Croissant ? "croissant"
            : productId == FoodProducts.Burger ? "burger" : productId == FoodProducts.HotDog ? "hot dog"
            : productId.Length == 0 ? "food" : productId;

        public static ResidentInsight Capture(CityState city, CitizenState person, ResidentMeal? meal,
            Func<string, Place?> findPlace, Func<string, string>? productName = null, Func<string, int, string>? formatAddress = null,
            FoodMemorySettings? memorySettings = null, Func<int, BusinessReference?>? memoryReference = null,
            Func<string, string?>? shopName = null, Func<FoodScope, bool>? currentOwnShop = null)
        {
            if (city == null) throw new ArgumentNullException(nameof(city));
            if (person == null) throw new ArgumentNullException(nameof(person));
            if (findPlace == null) throw new ArgumentNullException(nameof(findPlace));
            productName ??= DefaultProductName;
            Func<string, string> label = id =>
            {
                var place = Find(findPlace, id);
                return place == null ? UnknownPlace : ResidentAddress.From(place, formatAddress).Label;
            };
            double now = city.Minute;
            var insight = new ResidentInsight { ResidentId = person.Identity.Id, Name = person.Identity.Name, Age = person.Identity.Age, Minute = now };
            var home = Find(findPlace, person.HomeId);
            insight.Home = home == null ? null : ResidentAddress.From(home, formatAddress);
            var day = Find(findPlace, person.DayPlaceId);
            insight.Work = day == null ? null : ResidentAddress.From(day, formatAddress);
            insight.WorkRole = day == null ? person.Identity.Persona == Persona.Retiree ? "Retired" : "No regular workplace"
                : day.Kind == PlaceKind.Study ? "Studies at" : "Works at";
            insight.Personality = DescribePersonality(person, meal, productName);
            var mealToday = meal != null && meal.Slot == 0 && meal.ResidentId == person.Identity.Id ? meal : null;
            if (mealToday != null && city.Food != null)
                insight.MemoryWords = ResidentMemoryWords.Capture(city.Food, mealToday, person.Traits, (int)(now / 1440),
                    memorySettings ?? new FoodMemorySettings(),
                    memoryReference ?? (slot => city.Food.BusinessReferences.Find(r => r.Slot == slot)),
                    id => shopName?.Invoke(id) ?? label(id), currentOwnShop);
            insight.Story = TellStory(person, insight, Doing(person, mealToday, label, productName, now));
            DescribeHeading(person, mealToday, label, productName, now, out string heading, out string reason);
            insight.Heading = heading; insight.HeadingReason = reason;
            insight.Meals = TodaysMeals(city, mealToday, label, productName, now, out string note);
            insight.MealNote = note;
            return insight;
        }

        /// <summary>Popup text in display order: the resident's own story, where they are heading
        /// and why, then today's three meals. Addresses and personality are rendered separately.</summary>
        public string Text()
        {
            var text = new StringBuilder(512);
            text.Append(Story).Append('\n').Append('\n');
            foreach (string words in MemoryWords) text.Append(words).Append('.').Append('\n');
            if (MemoryWords.Count > 0) text.Append('\n');
            text.Append(Heading);
            if (HeadingReason.Length > 0) text.Append(" (").Append(HeadingReason).Append(')');
            text.Append('\n').Append('\n').Append("Today's meals").Append('\n');
            foreach (var meal in Meals) text.Append(KindName(meal.Kind)).Append(": ").Append(meal.Text).Append('\n');
            if (MealNote.Length > 0) text.Append(MealNote).Append('\n');
            return text.ToString();
        }

        public static string KindName(MealKind kind) => kind == MealKind.Breakfast ? "Breakfast" : kind == MealKind.Lunch ? "Lunch"
            : kind == MealKind.Dinner ? "Dinner" : "Meal";

        public static string Clock(double minute)
        {
            if (!Position.Finite(minute)) return "--:--";
            int within = (int)Math.Floor(minute - Math.Floor(minute / 1440) * 1440);
            if (within >= 1440) within = 1439;
            return (within / 60).ToString("00", CultureInfo.InvariantCulture) + ":" + (within % 60).ToString("00", CultureInfo.InvariantCulture);
        }

        private static Place? Find(Func<string, Place?> findPlace, string id) => string.IsNullOrEmpty(id) ? null : findPlace(id);

        private static IReadOnlyList<string> DescribePersonality(CitizenState person, ResidentMeal? meal, Func<string, string> productName)
        {
            // Words for persona rules, travel preferences and the saved lifestyle traits; never numbers.
            var words = new List<string>(12);
            switch (person.Identity.Persona)
            {
                case Persona.Commuter: words.Add("keeps a regular workday"); break;
                case Persona.Foodie: words.Add("loves eating out"); words.Add("rarely packs lunch"); break;
                case Persona.Parent: words.Add("early riser"); words.Add("often packs lunch"); break;
                case Persona.Student: words.Add("on a student budget"); words.Add("often packs lunch"); break;
                case Persona.Retiree: words.Add("early riser"); words.Add("no workday"); break;
                case Persona.Sporty: words.Add("active"); words.Add("goes out most evenings"); break;
                case Persona.Grumpy: words.Add("grumpy"); break;
                case Persona.NightOwl: words.Add("night owl"); words.Add("works late"); break;
            }
            double walk = person.Preferences.WalkingToleranceMetres;
            if (walk >= 1500) words.Add("happy to walk far");
            else if (walk < 900) words.Add("prefers short walks");
            if (person.Preferences.OwnsCar) words.Add("owns a car");
            else if (!person.Preferences.AllowTransit) words.Add("avoids public transport");
            if (person.Preferences.FareWeightMinutes >= 4) words.Add("minds fares");
            if (person.Traits != null) words.AddRange(ResidentTraits.Words(person.Traits, productName));
            return words;
        }

        private static string PersonaNoun(Persona persona)
        {
            switch (persona)
            {
                case Persona.Commuter: return "a commuter";
                case Persona.Foodie: return "a foodie";
                case Persona.Parent: return "a parent";
                case Persona.Student: return "a student";
                case Persona.Retiree: return "retired";
                case Persona.Sporty: return "sporty";
                case Persona.Grumpy: return "a bit grumpy";
                default: return "a night owl";
            }
        }

        private static string TellStory(CitizenState person, ResidentInsight insight, string doing)
        {
            var text = new StringBuilder(256);
            text.Append(insight.Name.Length > 0 ? insight.Name : "This resident");
            if (insight.Age > 0) text.Append(", ").Append(insight.Age.ToString(CultureInfo.InvariantCulture));
            text.Append(", is ").Append(PersonaNoun(person.Identity.Persona)).Append('.');
            text.Append(" Lives at ").Append(insight.Home?.Label ?? "an unknown home").Append('.');
            if (insight.Work != null) text.Append(' ').Append(insight.WorkRole).Append(' ').Append(insight.Work.Label).Append('.');
            else text.Append(' ').Append(insight.WorkRole).Append('.');
            text.Append(" Right now: ").Append(doing).Append('.');
            return text.ToString();
        }

        private static string ActivityAt(Activity activity, string place) => activity == Activity.Home ? "at home"
            : activity == Activity.Work ? "at work at " + place : activity == Activity.Study ? "studying at " + place
            : activity == Activity.Shop ? "shopping at " + place : "spending free time at " + place;

        private static string Mode(TravelMode mode) => mode == TravelMode.Car ? "driving" : mode == TravelMode.Bus ? "riding the bus"
            : mode == TravelMode.Subway ? "riding the subway" : "walking";

        private static string StopWords(DwellKind kind)
        {
            switch (kind)
            {
                case DwellKind.Break: return "taking a short break";
                case DwellKind.WindowShopping: return "window shopping";
                case DwellKind.ChatOutsideVenue: return "chatting outside";
                case DwellKind.Audience: return "watching a street performance";
                case DwellKind.Resting: return "resting on a bench";
                default: return "stopped briefly";
            }
        }

        private static string Doing(CitizenState person, ResidentMeal? meal, Func<string, string> label, Func<string, string> productName, double now)
        {
            var stop = person.OptionalStop;
            if (stop != null && stop.ActiveAt(now)) return StopWords(stop.Kind) + " near " + label(stop.PlaceId);
            switch (person.Phase)
            {
                case CitizenPhase.Travelling:
                {
                    var journey = person.Journey;
                    TravelMode mode = TravelMode.Walk;
                    if (journey != null && journey.Legs.Count > 0)
                    {
                        double elapsed = now - person.DepartureMinute;
                        mode = journey.Legs[journey.Legs.Count - 1].Mode;
                        foreach (var leg in journey.Legs)
                        {
                            if (elapsed <= leg.Minutes) { mode = leg.Mode; break; }
                            elapsed -= leg.Minutes;
                        }
                    }
                    return Mode(mode) + " to " + label(person.DestinationId);
                }
                case CitizenPhase.WaitingForRoute: return "waiting to leave " + label(person.PlaceId);
                case CitizenPhase.WaitingForOpening: return "waiting for " + label(person.DestinationId) + " to open";
            }
            if (meal != null && person.MealOuting != null && meal.PlaceId == person.PlaceId
                && (meal.Status == MealStatus.Travelling || meal.Status == MealStatus.Serving))
                return "getting " + KindName(meal.Kind).ToLowerInvariant() + " (" + productName(meal.ProductId) + ") at " + label(person.PlaceId);
            string at = ActivityAt(person.Activity, label(person.PlaceId));
            return person.ActivityUntilMinute > now && person.ActivityUntilMinute - now < 1440 ? at + " until " + Clock(person.ActivityUntilMinute) : at;
        }

        private static void DescribeHeading(CitizenState person, ResidentMeal? meal, Func<string, string> label,
            Func<string, string> productName, double now, out string heading, out string reason)
        {
            bool moving = person.Phase != CitizenPhase.AtPlace && person.DestinationId.Length > 0;
            var stop = person.OptionalStop;
            if (!moving)
            {
                heading = "Not travelling: " + ActivityAt(person.Activity, label(person.PlaceId));
                reason = stop != null && stop.ActiveAt(now) ? StopWords(stop.Kind) + " until " + Clock(stop.EndMinute)
                    : person.ActivityUntilMinute > now && person.ActivityUntilMinute - now < 1440 ? "stays until " + Clock(person.ActivityUntilMinute) : "";
                return;
            }
            string destination = label(person.DestinationId);
            heading = "Heading to " + destination;
            if (person.Phase == CitizenPhase.Travelling && person.NextEventMinute > now && person.NextEventMinute - now < 1440)
                heading += ", arriving about " + Clock(person.NextEventMinute);
            string until = person.ActivityUntilMinute > now && person.ActivityUntilMinute - now < 1440 ? " until " + Clock(person.ActivityUntilMinute) : "";
            var outing = person.MealOuting;
            if (outing != null && outing.Returning)
                reason = "going back to " + ActivityName(outing.OriginActivity) + " after a meal break";
            else if (outing != null)
                reason = meal != null && meal.PlaceId == person.DestinationId && meal.Status == MealStatus.Travelling
                    ? "to buy " + productName(meal.ProductId) + " for " + KindName(meal.Kind).ToLowerInvariant() + ChoiceWords(meal)
                    : "to get something to eat";
            else
                switch (person.DestinationActivity)
                {
                    case Activity.Work: reason = "for work" + until; break;
                    case Activity.Study: reason = "for classes" + until; break;
                    case Activity.Shop: reason = "to go shopping" + until; break;
                    case Activity.Leisure: reason = "for some free time" + until; break;
                    default: reason = "to go home"; break;
                }
            if (person.Phase == CitizenPhase.WaitingForOpening) reason += "; waiting for it to open";
            else if (person.Phase == CitizenPhase.WaitingForRoute) reason += "; waiting for a route";
            if (stop != null && stop.ActiveAt(now)) reason += "; " + StopWords(stop.Kind) + " on the way";
        }

        private static string ActivityName(Activity activity) => activity == Activity.Work ? "work" : activity == Activity.Study ? "class"
            : activity == Activity.Shop ? "shopping" : activity == Activity.Leisure ? "their free time" : "home";

        private static IReadOnlyList<ResidentMealInsight> TodaysMeals(CityState city, ResidentMeal? meal, Func<string, string> label,
            Func<string, string> productName, double now, out string note)
        {
            var meals = new ResidentMealInsight[3];
            for (int i = 0; i < 3; i++)
                meals[i] = new ResidentMealInsight { Kind = MealKind.Breakfast + i, State = ResidentMealState.Unknown, Text = "unknown" };
            note = "";
            int today = (int)Math.Floor(now / 1440);
            if (city.Food == null) { note = "Meal data is unavailable: resident food demand is not active in this city."; return meals; }
            if (meal == null) { note = "No meal record for this resident yet."; return meals; }
            // A record of an earlier day is not today's: the view is empty until this consumer's next outcome.
            var recorded = meal.Today != null && meal.Today.Day == today ? meal.Today : null;
            if (meal.Kind == MealKind.Legacy)
            {
                var single = new ResidentMealInsight { Kind = MealKind.Legacy, State = ResidentMealState.Unknown, Text = "unknown" };
                Fill(single, meal, recorded, today, label, productName, now);
                note = "This city uses the earlier single daily meal: " + single.Text + ".";
                return meals;
            }
            foreach (var slot in meals) Fill(slot, meal, recorded, today, label, productName, now);
            return meals;
        }

        // Precedence: a meal under way now, then its recorded outcome, then what the live record shows.
        private static void Fill(ResidentMealInsight slot, ResidentMeal meal, ResidentFoodToday? recorded, int today,
            Func<string, string> label, Func<string, string> productName, double now)
        {
            bool current = meal.Day == today && meal.Kind == slot.Kind;
            var outcome = recorded?.Find(slot.Kind);
            if (current && (meal.Status == MealStatus.Travelling || meal.Status == MealStatus.Serving || meal.Status == MealStatus.Pending && outcome == null))
            { slot.State = State(meal.Status); slot.Text = Describe(meal, label, productName, now); return; }
            if (outcome != null)
            {
                slot.State = State(outcome.Status);
                slot.Text = current && outcome.Reason==MealReasonCode.Other && meal.ContextAttempts>0
                    && (meal.Reason=="Meal decision limit reached" || meal.Reason=="This meal situation was already considered")
                    ? "skipped: "+Sentence(meal.Reason) : Describe(outcome, label, productName);
                return;
            }
            if (recorded != null && MealOutcomes.Index(slot.Kind) < recorded.Unrecorded)
            { slot.State = ResidentMealState.NotRecorded; slot.Text = "not recorded (before the upgrade)"; return; }
            if (current) { slot.State = State(meal.Status); slot.Text = Describe(meal, label, productName, now); return; }
            // Without a record, a meal that ended earlier today cannot be reconstructed.
            if (meal.Today == null && (meal.Day > today || meal.Day == today && slot.Kind < meal.Kind))
            { slot.State = ResidentMealState.NotRecorded; slot.Text = "not recorded"; }
        }

        private static ResidentMealState State(MealOutcomeStatus status) => status == MealOutcomeStatus.Bought ? ResidentMealState.Bought
            : status == MealOutcomeStatus.ChoseAlternative ? ResidentMealState.HouseholdFood
            : status == MealOutcomeStatus.Skipped ? ResidentMealState.Skipped : ResidentMealState.Unconfirmed;

        private static string Describe(MealOutcomeRecord outcome, Func<string, string> label, Func<string, string> productName)
        {
            string shop = outcome.PlaceId.Length == 0 ? "a shop" : label(outcome.PlaceId);
            if (shop == UnknownPlace) shop = "a shop no longer available";
            string product = productName(outcome.ProductId);
            switch (outcome.Status)
            {
                case MealOutcomeStatus.Bought: return "bought " + product + " at " + shop + FactorWords(outcome);
                // A choice, never a claim that the food was eaten.
                case MealOutcomeStatus.ChoseAlternative: return Alternative(outcome.Outside);
                case MealOutcomeStatus.Unconfirmed: return "purchase of " + product + " at " + shop + " not yet confirmed";
                default: return "skipped: " + ReasonWords(outcome.Reason);
            }
        }

        public static string Alternative(FoodOutsideOption outside) => outside == FoodOutsideOption.FundedHouseholdFood ? "chose household food"
            : outside == FoodOutsideOption.HomePreparation ? "chose to prepare food at home"
            : outside == FoodOutsideOption.PackedMeal ? "chose a packed meal" : "chose food from home instead of buying";

        private static string FactorWords(MealOutcomeRecord outcome) => (outcome.Factor1==MealFactor.Taste ? "; likes it" : "")
            +(outcome.Factor1==MealFactor.Quality || outcome.Factor2==MealFactor.Quality ? "; values quality" : "");

        public static string ReasonWords(MealReasonCode reason)
        {
            switch (reason)
            {
                case MealReasonCode.NoSuitableStop: return "no suitable stop in the meal window";
                case MealReasonCode.NoFeasibleOffer: return "no shop could serve in time or within budget";
                case MealReasonCode.InsufficientEvidence: return "not enough stock, service or route information";
                case MealReasonCode.ServiceUnavailable: return "service unavailable on arrival";
                case MealReasonCode.ShopClosed: return "the shop was closed";
                case MealReasonCode.QueueTooLong: return "the queue was too long";
                case MealReasonCode.NoLongerFits: return "travel, queue or service would not fit the break";
                case MealReasonCode.Interrupted: return "the visit was interrupted";
                case MealReasonCode.LeftBeforeService: return "left before being served";
                case MealReasonCode.ScheduleChanged: return "the schedule changed before leaving";
                case MealReasonCode.BusinessWithdrew: return "the shop no longer accepted the order";
                case MealReasonCode.NativeClockPassed: return "the game clock passed the meal window";
                case MealReasonCode.MoneyRange: return "the purchase exceeded the money range";
                case MealReasonCode.FundsUnavailable: return "not enough money at checkout";
                case MealReasonCode.CheckoutRefused: return "the shop refused the checkout";
                default: return "no reason recorded";
            }
        }

        private static ResidentMealState State(MealStatus status) => status == MealStatus.Travelling || status == MealStatus.Serving ? ResidentMealState.InProgress
            : status == MealStatus.Paid ? ResidentMealState.Bought : status == MealStatus.Declined ? ResidentMealState.HouseholdFood
            : status == MealStatus.Missed ? ResidentMealState.Skipped : status == MealStatus.Uncertain ? ResidentMealState.Unconfirmed
            : ResidentMealState.Unknown;

        private static string Describe(ResidentMeal meal, Func<string, string> label, Func<string, string> productName, double now)
        {
            string shop = meal.PlaceId.Length == 0 ? "a shop" : label(meal.PlaceId);
            string product = productName(meal.ProductId);
            switch (meal.Status)
            {
                case MealStatus.Travelling: return "on the way to " + shop + " for " + product + ChoiceWords(meal);
                case MealStatus.Serving: return "waiting for " + product + " at " + shop + ChoiceWords(meal);
                case MealStatus.Paid: return "bought " + product + " at " + shop;
                case MealStatus.Declined: return Alternative(meal.Today?.Day == meal.Day ? meal.Today.Find(meal.Kind)?.Outside ?? FoodOutsideOption.None : FoodOutsideOption.None);
                case MealStatus.Missed: return "skipped: " + Sentence(meal.Reason);
                case MealStatus.Uncertain: return "purchase of " + product + " at " + shop + " not yet confirmed";
                default: return now < meal.DesiredAt ? "unknown (not decided yet)" : "unknown (deciding)";
            }
        }

        private static string Sentence(string reason)
        {
            if (string.IsNullOrWhiteSpace(reason)) return "no reason recorded";
            reason = reason.Trim();
            return reason.Length > 1 && char.IsUpper(reason[0]) && !char.IsUpper(reason[1])
                ? char.ToLowerInvariant(reason[0]) + reason.Substring(1) : reason;
        }
        private static string ChoiceWords(ResidentMeal meal) => meal.ContextAttempts>0 && meal.Reason.StartsWith("Chose food",StringComparison.Ordinal)
            ? meal.Reason.Substring("Chose food".Length) : "";
    }
}
