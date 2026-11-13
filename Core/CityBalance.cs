namespace PersistentCity.Core
{
    public static class CityBalance
    {
        public const double GameClockFactor = 1d / 3;
        public const int ConsumerRepresentationFactor = 2;
        public const double DailyJobLossChance = 0.001;
        public const double JobLossPerMiss = 0.5;
        public const double MaximumJobLossChance = 0.01;
        public const double DailyJobFindingChance = 0.10;
        public const int WorkplaceRouteProbes = 2;
        public const int SupportIncomePercent = 60;
        public const int PensionIncomePercent = 70;
        public const double UnrentableHomeUsableShare = 0.6;
        public const double UnrentableHomeSquareMetresPerPerson = 30;
        public const double StoreyHeightMetres = 3.5;
        public const int CityResidentTotal = 1000;
    }
}
