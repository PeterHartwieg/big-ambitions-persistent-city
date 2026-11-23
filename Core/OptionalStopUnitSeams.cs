using System;
using System.Globalization;
using System.Text;

namespace PersistentCity.Core
{
    public readonly struct OptionalStopUnitSeam
    {
        public OptionalStopUnitSeam(int unit, string kind, double milliseconds) { Unit = unit; Kind = kind; Milliseconds = milliseconds; }
        public int Unit { get; }
        public string Kind { get; }
        public double Milliseconds { get; }
    }

    public sealed class OptionalStopUnitSeams
    {
        public const double ThresholdMilliseconds = 5;
        public const int ListedOverThreshold = 8;
        public const int PresizedUnits = 1025;
        private double[] seams = new double[PresizedUnits], sorted = new double[PresizedUnits];
        private string[] kinds = new string[PresizedUnits];
        private double open, boundaryWall;
        private long boundaryCpu = -1;
        private bool callOpen;

        public bool RecordsCpu { get; set; }
        public double MaintenanceMilliseconds { get; private set; }
        public double MaintenanceCpuMilliseconds { get; private set; } = -1;
        public static double CpuBetween(long from, long to) => from >= 0 && to >= from ? (to - from) / 1e6 : -1;

        public bool CallOpen => callOpen;
        public int Units { get; private set; }
        public double CallSeamMilliseconds { get; private set; }
        public int SlowestUnit { get; private set; } = -1;
        public string SlowestUnitKind { get; private set; } = "";
        public double SlowestUnitMilliseconds { get; private set; }
        public double SlowestUnitCpuMilliseconds { get; private set; } = -1;
        public double SlowestUnitSeamMilliseconds => SlowestUnit < 0 ? 0 : seams[SlowestUnit];
        public double MaximumUnitSeamMilliseconds { get; private set; }
        public string MaximumUnitSeamKind { get; private set; } = "";
        public bool Completed { get; private set; }
        public double UnitSeamMilliseconds(int unit) => unit >= 0 && unit < Units ? seams[unit] : throw new ArgumentOutOfRangeException(nameof(unit));
        public string UnitKind(int unit) => unit >= 0 && unit < Units ? kinds[unit] : throw new ArgumentOutOfRangeException(nameof(unit));

        public void BeginCall()
        {
            Units = 0; open = 0; CallSeamMilliseconds = 0; SlowestUnit = -1; SlowestUnitKind = ""; SlowestUnitMilliseconds = 0; SlowestUnitCpuMilliseconds = -1;
            MaximumUnitSeamMilliseconds = 0; MaximumUnitSeamKind = ""; Completed = false; callOpen = true;
            MaintenanceMilliseconds = 0; MaintenanceCpuMilliseconds = -1; boundaryWall = 0; boundaryCpu = -1;
        }

        public void BeginCall(long cpuNanoseconds) { BeginCall(); boundaryCpu = cpuNanoseconds; }

        public void Maintained(double wallMilliseconds, long cpuNanoseconds)
        {
            if (!callOpen) return;
            MaintenanceMilliseconds = Math.Max(0, wallMilliseconds - boundaryWall);
            MaintenanceCpuMilliseconds = CpuBetween(boundaryCpu, cpuNanoseconds);
            boundaryWall = wallMilliseconds; boundaryCpu = cpuNanoseconds;
        }

        public void EndUnitAt(string kind, double wallMilliseconds, long cpuNanoseconds)
        {
            if (!callOpen) return;
            double wall = wallMilliseconds - boundaryWall, cpu = CpuBetween(boundaryCpu, cpuNanoseconds);
            boundaryWall = wallMilliseconds; boundaryCpu = cpuNanoseconds;
            EndUnit(kind, wall, cpu);
        }

        public void Seam(double milliseconds)
        {
            if (!callOpen) return;
            open += milliseconds; CallSeamMilliseconds += milliseconds;
        }

        public void EndUnit(string kind, double unitMilliseconds) => EndUnit(kind, unitMilliseconds, -1);

        public void EndUnit(string kind, double unitMilliseconds, double unitCpuMilliseconds)
        {
            if (!callOpen) return;
            if (Units == seams.Length)
            {
                Array.Resize(ref seams, seams.Length * 2); Array.Resize(ref kinds, kinds.Length * 2); Array.Resize(ref sorted, sorted.Length * 2);
            }
            seams[Units] = open; kinds[Units] = kind;
            if (unitMilliseconds > SlowestUnitMilliseconds)
            {
                SlowestUnitMilliseconds = unitMilliseconds; SlowestUnit = Units; SlowestUnitKind = kind;
                SlowestUnitCpuMilliseconds = double.IsNaN(unitCpuMilliseconds) || unitCpuMilliseconds < 0 ? -1 : unitCpuMilliseconds;
            }
            if (open > MaximumUnitSeamMilliseconds) { MaximumUnitSeamMilliseconds = open; MaximumUnitSeamKind = kind; }
            Units++; open = 0;
        }

        public void EndCall(bool completed = true)
        {
            if (!callOpen) return;
            callOpen = false; Completed = completed;
        }

        public int NextUnitOverThreshold(int from)
        {
            for (int i = Math.Max(0, from); i < Units; i++) if (seams[i] > ThresholdMilliseconds) return i;
            return -1;
        }

        public double P95UnitSeamMilliseconds()
        {
            if (Units == 0) return 0;
            for (int i = 0; i < Units; i++)
            {
                double value = seams[i];
                int j = i - 1;
                for (; j >= 0 && sorted[j] > value; j--) sorted[j + 1] = sorted[j];
                sorted[j + 1] = value;
            }
            int rank = (int)Math.Ceiling(0.95 * Units);
            return sorted[Math.Max(1, rank) - 1];
        }

        public int UnitsOverThreshold()
        {
            int count = 0;
            for (int i = 0; i < Units; i++) if (seams[i] > ThresholdMilliseconds) count++;
            return count;
        }

        public OptionalStopUnitSeam[] OverThreshold()
        {
            var result = new OptionalStopUnitSeam[UnitsOverThreshold()];
            for (int i = 0, n = 0; i < Units; i++) if (seams[i] > ThresholdMilliseconds) result[n++] = new OptionalStopUnitSeam(i, kinds[i], seams[i]);
            return result;
        }

        public string OverThresholdText()
        {
            var over = OverThreshold();
            if (over.Length == 0) return "";
            var text = new StringBuilder();
            for (int i = 0; i < over.Length && i < ListedOverThreshold; i++)
            {
                if (i > 0) text.Append("; ");
                text.Append(over[i].Kind).Append('#').Append(over[i].Unit).Append(' ')
                    .Append(over[i].Milliseconds.ToString("F3", CultureInfo.InvariantCulture)).Append(" ms");
            }
            if (over.Length > ListedOverThreshold) text.Append("; +").Append(over.Length - ListedOverThreshold).Append(" more");
            return text.ToString();
        }
    }
}
