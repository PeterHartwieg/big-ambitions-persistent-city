namespace PersistentCity.Runtime
{
    public sealed partial class PopulationRuntime
    {
        /// <summary>Read-only cached bridge line. Does not refresh detection or F0 eligibility.</summary>
        public string InteriorGuardDiagnostics => "ThirdPartyInteriorGuard: "
            + (NativeInteriorGuard.Verdict.Detected ? "detected" : "not detected")
            + "; rule=" + NativeInteriorGuard.Verdict.Reason
            + "; effective view=" + (NativeInteriorCityView.Configured ? "on" : "off")
            + "; active=" + NativeInteriorCityView.State.Active;
    }
}
