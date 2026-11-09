namespace PersistentCity.Runtime
{
    public sealed partial class NativeFoodMarket
    {
        /// <summary>E5b4: E5b2's published loaded-name image for the product policy; null while the catalogue is dirty,
        /// being rebuilt, or the contract shadow is not running. Reads one reference.</summary>
        internal LoadedNameImage? LoadedNames => contractReader?.LoadedNames;
    }
}
