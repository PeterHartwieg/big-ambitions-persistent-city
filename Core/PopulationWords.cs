using System.Globalization;
using System.Text;

namespace PersistentCity.Core
{
    public static class PopulationWords
    {
        public const string Households = "Residents who share a home form one household.";
        public const string Workplaces = "Residents and commuters take the seats at the city's workplaces. Residents without a workplace live on a pension or on support income.";
        public const string Visitors = "Visitors live outside the city and arrive and leave by subway. Commuters come on weekdays, usually to work; day visitors come to shop and for leisure, mostly at weekends. Visitors are not shown on the street yet.";
        public const string NoStation = "No visitors come to this city: it has no subway station to arrive through.";
        public const string Wallets = "Each person has their own wallet for food at home and prepared meals; residents also pay their bills from it.";

        public static string Headline(int day, int residents, int visitors, int peopleWithMeals)
            => "Day " + N(day) + " · " + N(residents) + (residents == 1 ? " resident · " : " residents · ")
                + N(visitors) + (visitors == 1 ? " visitor · " : " visitors · ") + N(peopleWithMeals) + (peopleWithMeals == 1 ? " person with meals" : " people with meals");

        public static string Lines(int visitors, int portals)
        {
            var text = new StringBuilder(512);
            text.Append(Households).Append('\n').Append(Workplaces).Append('\n');
            if (visitors > 0) text.Append(Visitors).Append('\n');
            else if (portals == 0) text.Append(NoStation).Append('\n');
            return text.Append(Wallets).ToString();
        }

        private static string N(int value) => value.ToString(CultureInfo.InvariantCulture);
    }
}
