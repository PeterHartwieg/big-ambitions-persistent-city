using System;
using System.Globalization;

namespace PersistentCity.Core
{
    public static class PlanningSliceAnatomy
    {
        public static string Text(double sliceMilliseconds, double jobMilliseconds, string unitStage, double unitMilliseconds,
            double unitNavMeshMilliseconds, int unitNavMeshQueries, long unitNavMeshSamples)
        {
            double outside = Math.Max(0, sliceMilliseconds - jobMilliseconds);
            double unitOther = Math.Max(0, unitMilliseconds - unitNavMeshMilliseconds);
            return "Slowest planning slice: " + Ms(sliceMilliseconds) + " ms, route job " + Ms(jobMilliseconds) + " ms, outside the route job "
                + Ms(outside) + " ms; its slowest unit " + (unitStage.Length == 0 ? "none" : unitStage) + " " + Ms(unitMilliseconds)
                + " ms, NavMesh paths " + Ms(unitNavMeshMilliseconds) + " ms over " + unitNavMeshQueries.ToString(CultureInfo.InvariantCulture)
                + " queries, other work " + Ms(unitOther) + " ms, " + unitNavMeshSamples.ToString(CultureInfo.InvariantCulture) + " NavMesh samples.";
        }

        private static string Ms(double value) => value.ToString("F3", CultureInfo.InvariantCulture);
    }
}
