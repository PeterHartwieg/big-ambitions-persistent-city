namespace PersistentCity.Runtime
{
    public sealed partial class NativeFoodMarket
    {
        internal LoadedNameImage? LoadedNames => contractReader?.LoadedNames;
    }
}
