using PersistentCity.Core;
using PersistentCity.Persistence;
namespace PersistentCity.Tests;
// Named-field fixtures deliberately exercise the frozen source contracts. Current-wire
// tuple tests live in Schema18Tests; ordinary simulation/reload tests use SaveCodec.
internal static class LegacyWire
{
    internal static string Write(CityState state)
    {
        int schema = state.Schema;
        try { if (schema == 18) state.Schema = 17; return LegacyPopulationCodec.Write(state); }
        finally { state.Schema = schema; }
    }
}
