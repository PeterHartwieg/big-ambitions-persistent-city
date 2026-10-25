namespace PersistentCity.Core
{
    // One developer tuning profile; no user-facing settings or per-save policy switches.
    public static class CityBalance
    {
        public const double GameClockFactor = 1d / 3;
        public const int ConsumerRepresentationFactor = 2;
        public const double DailyJobLossChance = 0.001;
        public const double DailyJobFindingChance = 0.10;
        public const int WorkplaceRouteProbes = 2;
        public const int SupportIncomePercent = 60;
        public const int PensionIncomePercent = 70;
    }
}
