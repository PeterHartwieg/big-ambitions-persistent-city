using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

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
        /// <summary>The street as shown to the player; the localized key when a localizer is supplied.</summary>
        public string DisplayStreet { get; set; } = "";
        public int Number { get; set; }
        public bool CanNavigate => Street.Length > 0;
        public string AddressText => CanNavigate ? (DisplayStreet.Length > 0 ? DisplayStreet : Street) + " " + Number.ToString(CultureInfo.InvariantCulture) : "";
        public string Label => !CanNavigate ? Name : Name.Length == 0 || Name == AddressText ? AddressText : Name + ", " + AddressText;

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

        /// <param name="localize">Maps a native street key to display text; identity when null.</param>
        public static ResidentAddress From(Place place, Func<string, string>? localize = null)
        {
            var address = new ResidentAddress { PlaceId = place.Id, Name = place.Name };
            if (!TryParse(place.Id, out string street, out int number)) return address;
            address.Street = street; address.Number = number;
            string shown = localize == null ? street : localize(street);
            address.DisplayStreet = string.IsNullOrWhiteSpace(shown) ? street : shown;
            // Homes and unnamed businesses are named by their raw native address; show it localized.
            if (place.Name == street + " " + number.ToString(CultureInfo.InvariantCulture)) address.Name = address.AddressText;
            return address;
        }
    }

    /// <summary>
    /// Read-only view of one resident for the inspector, built from facts the city already stores.
    /// Capture never changes the city, draws no random number, plans no route and reads no market.
    /// Only the latest meal record is stored before schema 17, so earlier meals today are reported
    /// as not recorded rather than inferred.
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
        public string Story { get; private set; } = "";
        public string Heading { get; private set; } = "";
        public string HeadingReason { get; private set; } = "";
        public IReadOnlyList<ResidentMealInsight> Meals { get; private set; } = Array.Empty<ResidentMealInsight>();
        public string MealNote { get; private set; } = "";
        public double Minute { get; private set; }

        public static string DefaultProductName(string productId) => productId == FoodProducts.Croissant ? "croissant"
            : productId == FoodProducts.Burger ? "burger" : productId == FoodProducts.HotDog ? "hot dog"
            : productId.Length == 0 ? "food" : productId;

        public static ResidentInsight Capture(CityState city, CitizenState person, ResidentMeal? meal,
            Func<string, Place?> findPlace, Func<string, string>? productName = null, Func<string, string>? localize = null)
        {
            if (city == null) throw new ArgumentNullException(nameof(city));
            if (person == null) throw new ArgumentNullException(nameof(person));
            if (findPlace == null) throw new ArgumentNullException(nameof(findPlace));
            productName ??= DefaultProductName;
            Func<string, string> label = id =>
            {
                var place = Find(findPlace, id);
                return place == null ? "an unknown place" : ResidentAddress.From(place, localize).Label;
            };
            double now = city.Minute;
            var insight = new ResidentInsight { ResidentId = person.Identity.Id, Name = person.Identity.Name, Age = person.Identity.Age, Minute = now };
            var home = Find(findPlace, person.HomeId);
            insight.Home = home == null ? null : ResidentAddress.From(home, localize);
            var day = Find(findPlace, person.DayPlaceId);
            insight.Work = day == null ? null : ResidentAddress.From(day, localize);
            insight.WorkRole = day == null ? person.Identity.Persona == Persona.Retiree ? "Retired" : "No regular workplace"
                : day.Kind == PlaceKind.Study ? "Studies at" : "Works at";
            insight.Personality = DescribePersonality(person, meal, productName);
            var mealToday = meal != null && meal.Slot == 0 && meal.ResidentId == person.Identity.Id ? meal : null;
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
            // Words for facts that already change behaviour: persona rules, travel preferences and,
            // where a consumer has one, a food preference. No new traits are generated here.
            var words = new List<string>(6);
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
            if (meal != null && meal.PreferredProductId.Length > 0) words.Add("likes " + productName(meal.PreferredProductId) + "s");
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
                    ? "to buy " + productName(meal.ProductId) + " for " + KindName(meal.Kind).ToLowerInvariant()
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
            if (meal.Kind == MealKind.Legacy)
            {
                note = "This city uses the earlier single daily meal: " + (meal.Day == today ? Describe(meal, label, productName, now) : "unknown") + ".";
                return meals;
            }
            if (meal.Day > today)
            {
                for (int i = 0; i < 3; i++) { meals[i].State = ResidentMealState.NotRecorded; meals[i].Text = "not recorded"; }
                return meals;
            }
            if (meal.Day < today) return meals;
            int current = meal.Kind - MealKind.Breakfast;
            for (int i = 0; i < current; i++) { meals[i].State = ResidentMealState.NotRecorded; meals[i].Text = "not recorded (only the latest meal is stored)"; }
            meals[current].State = State(meal.Status);
            meals[current].Text = Describe(meal, label, productName, now);
            return meals;
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
                case MealStatus.Travelling: return "on the way to " + shop + " for " + product;
                case MealStatus.Serving: return "waiting for " + product + " at " + shop;
                case MealStatus.Paid: return "bought " + product + " at " + shop;
                case MealStatus.Declined: return "chose food from home instead of buying";
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
    }
}
