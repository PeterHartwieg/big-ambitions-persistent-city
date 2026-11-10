using System;
using System.Globalization;
using System.Linq;
using PersistentCity.FailureMatrix;

// F8g: prints the matrix table (docs/perf/f8bg.md, F8g) and exits non-zero on any failed arm.
int days = args.Length > 0 ? int.Parse(args[0], CultureInfo.InvariantCulture) : 2;
var timer = System.Diagnostics.Stopwatch.StartNew();
var arms = CapabilityMatrix.Run(days, Console.WriteLine);
Console.WriteLine();
Console.Write(CapabilityMatrix.Table(arms));
Console.WriteLine("matrix: " + arms.Count(a => a.Passed) + " of " + arms.Count + " arms passed, " + days + " simulated days per economy arm, "
    + CapabilityMatrix.Residents + " residents x2, wall " + timer.Elapsed.TotalSeconds.ToString("F1", CultureInfo.InvariantCulture) + " s (offline, no mod-work)");
return arms.All(a => a.Passed) ? 0 : 1;
