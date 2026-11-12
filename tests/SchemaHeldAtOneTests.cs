using PersistentCity.Core;
using Xunit;

namespace PersistentCity.Tests;

public sealed class SchemaHeldAtOneTests
{
    [Fact]
    public void TheSaveSchemaIsHeldAtOne()
        => Assert.True(CityState.CurrentSchema == 1,
            "The save schema is held at 1 until release; change the format in place. See docs/decisions/2026-11-09-schema-one.md.");
}
