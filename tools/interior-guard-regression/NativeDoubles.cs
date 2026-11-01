using System.Reflection;
using PersistentCity.Core;

// Our own metadata-only fixtures. No foreign mod or game binary is loaded here.
public class BuildingManager
{
    public void EnterBuilding() { }
    public void ExitFromBuilding() { }
}
namespace HarmonyLib
{
    public sealed class Patches { public string[] Owners = Array.Empty<string>(); }
    public static class Harmony
    {
        public static int Reads;
        public static bool Throw;
        public static string[] Owners = Array.Empty<string>();
        public static Patches? GetPatchInfo(MethodBase method)
        {
            Reads++;
            if (Throw) throw new InvalidOperationException("fixture metadata failure");
            return Owners.Length == 0 ? null : new Patches { Owners = Owners };
        }
    }
}
namespace PersistentCity.Runtime
{
    internal static class NativeInteriorCityView
    {
        public static bool Configured;
        public static readonly View State = new();
        internal sealed class View { public bool Active => false; }
    }
}
