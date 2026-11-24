using System.Runtime.CompilerServices;
using PersistentCity.Core;

namespace PersistentCity.Tests;

internal static class StrictRetimePose
{
    [ModuleInitializer]
    internal static void Enable() => TravelTiming.StrictPoseDefault = true;
}
