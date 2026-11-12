using System;

namespace PersistentCity.Core
{
    public sealed class FoodPanelRefresh
    {
        private double next;
        public string Text { get; private set; } = "";
        public long Builds { get; private set; }
        public void Invalidate() => next = double.NegativeInfinity;
        public void Reset() { Text = ""; Builds = 0; Invalidate(); }
        public void Update(bool visible, double now, Func<string> build)
        {
            if (!visible || now < next) return;
            Text = build(); Builds++; next = now + 3;
        }
    }
}
