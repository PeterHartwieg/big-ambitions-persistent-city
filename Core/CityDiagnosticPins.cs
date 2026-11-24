using System;
using System.Collections.Generic;

namespace PersistentCity.Core
{
    public sealed class FoodDecisionRecorder
    {
        public const int Capacity = 50;
        public const int Retained = 1000;
        private readonly Queue<string> lines = new Queue<string>();
        private (FoodDecisionTrace? Trace, Position? Origin)[]? rows;
        private int rowStart, rowCount;
        private readonly Func<FoodDecisionTrace, string?> format;
        private readonly Func<FoodDecisionTrace, string>? brief;
        private readonly Action<Exception> failed;
        private CitySimulator? observed;

        public FoodDecisionRecorder(Func<FoodDecisionTrace, string?> format, Action<Exception> failed, Func<FoodDecisionTrace, string>? brief = null)
        {
            this.format = format ?? throw new ArgumentNullException(nameof(format));
            this.failed = failed ?? throw new ArgumentNullException(nameof(failed));
            this.brief = brief;
        }

        public bool Recording => observed != null;
        public int Count => rowCount;
        public IEnumerable<string> Lines => lines;
        internal CitySimulator? Observed => observed;

        public IReadOnlyList<string> Near(double x, double z, double radiusMetres, int count)
        {
            var found = new List<string>();
            if (count <= 0 || !(radiusMetres >= 0) || rows == null) return found;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = rowCount - 1; i >= 0 && found.Count < count; i--)
            {
                var row = rows[(rowStart + i) % Retained];
                if (row.Origin == null || row.Trace == null) continue;
                double dx = row.Origin.Value.X - x, dz = row.Origin.Value.Z - z;
                double distance = Math.Sqrt(dx * dx + dz * dz);
                if (distance > radiusMetres || !seen.Add(row.Trace.Consumer.Id)) continue;
                found.Add(distance.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " m | " + (brief?.Invoke(row.Trace) ?? format(row.Trace)));
            }
            found.Reverse();
            return found;
        }

        public void Start(CitySimulator city)
        {
            if (city == null) throw new ArgumentNullException(nameof(city));
            if (ReferenceEquals(observed, city)) return;
            Stop();
            rows ??= new (FoodDecisionTrace?, Position?)[Retained];
            observed = city;
            city.FoodDecisionObserved += Record;
        }

        public void Stop()
        {
            if (observed != null) observed.FoodDecisionObserved -= Record;
            observed = null;
        }

        public void Rebind(CitySimulator city)
        {
            var current = observed;
            if (current == null || city == null || ReferenceEquals(current, city)) return;
            current.FoodDecisionObserved -= Record;
            observed = city;
            city.FoodDecisionObserved += Record;
        }

        public void Detach() { Stop(); lines.Clear(); rows = null; rowStart = rowCount = 0; }

        private void Record(FoodDecisionTrace trace)
        {
            try
            {
                string? line = format(trace);
                if (line == null) return;
                if (lines.Count == Capacity) lines.Dequeue();
                lines.Enqueue(line);
                var buffer = rows!;
                var row = (trace, observed?.FindPosition(trace.Consumer.OriginPlaceId));
                if (rowCount < Retained) buffer[(rowStart + rowCount++) % Retained] = row;
                else { buffer[rowStart] = row; rowStart = (rowStart + 1) % Retained; }
            }
            catch (Exception error) { Stop(); failed(error); }
        }
    }

    public static class FoodDecisionQuery
    {
        public const double DefaultRadiusMetres = 50;
        public const int DefaultCount = 50;
        public const string Usage = "pc food decisions near <vehicle-id|x,z>[,radius m][,count]";

        public static bool TryParse(string value, out string vehicle, out double x, out double z, out double radius, out int count, out string error)
        {
            vehicle = ""; x = z = double.NaN; radius = DefaultRadiusMetres; count = DefaultCount; error = "";
            var parts = (value ?? "").Split(',');
            int next;
            if (parts.Length >= 2 && Number(parts[0], out x) && Number(parts[1], out z)) next = 2;
            else
            {
                vehicle = parts[0].Trim();
                x = z = double.NaN;
                if (vehicle.Length == 0) { error = "near needs a vendor vehicle id or <x>,<z>"; return false; }
                next = 1;
            }
            if (parts.Length > next + 2) { error = "near takes at most a radius and a count after the target"; return false; }
            if (parts.Length > next && (!Number(parts[next], out radius) || radius < 0))
            { error = "the radius must be a non-negative number of metres"; return false; }
            if (parts.Length > next + 1 && (!int.TryParse(parts[next + 1].Trim(), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out count)
                || count < 1 || count > FoodDecisionRecorder.Retained))
            { error = "the count must be a whole number from 1 to " + FoodDecisionRecorder.Retained.ToString(System.Globalization.CultureInfo.InvariantCulture); return false; }
            return true;
        }

        private static bool Number(string text, out double value)
            => double.TryParse(text.Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out value) && Position.Finite(value);
    }

    public sealed class ResearchSnapshotCache
    {
        public const double LifetimeSeconds = .25;
        private ResearchLedger? snapshot;
        private CitySimulator? city;
        private double until;

        internal CitySimulator? City => city;

        public ResearchLedger? Get(CitySimulator? current, double nowSeconds)
        {
            if (current == null) { Forget(); return null; }
            if (!ReferenceEquals(current, city) || nowSeconds >= until)
            { snapshot = CityViewReports.ResearchSnapshot(current.View); city = current; until = nowSeconds + LifetimeSeconds; }
            return snapshot;
        }

        public void Forget() { snapshot = null; city = null; }
    }
}
