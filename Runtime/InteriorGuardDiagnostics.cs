namespace PersistentCity.Runtime
{
    public sealed partial class PopulationRuntime
    {
        public string InteriorGuardDiagnostics => "ThirdPartyInteriorGuard: "
            + (NativeInteriorGuard.Verdict.Detected ? "detected" : "not detected")
            + "; rule=" + NativeInteriorGuard.Verdict.Reason
            + "; effective view=" + (NativeInteriorCityView.Configured ? "on" : "off")
            + "; active=" + NativeInteriorCityView.State.Active;
    }
}
