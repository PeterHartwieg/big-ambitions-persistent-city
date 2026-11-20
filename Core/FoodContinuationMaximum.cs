namespace PersistentCity.Core
{
    public sealed class FoodContinuationMaximum
    {
        public double Milliseconds { get; private set; }
        public string Step { get; private set; } = "";

        public void Observe(bool food, double milliseconds, string step)
        {
            if (food && milliseconds > Milliseconds) { Milliseconds = milliseconds; Step = step; }
        }

        public void Reset() { Milliseconds = 0; Step = ""; }
    }
}
