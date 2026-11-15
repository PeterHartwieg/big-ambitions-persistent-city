namespace PersistentCity.Core
{
    public enum AmbientRespawnAction { Done, SpawnOne, SetDesired }

    public static class AmbientRespawnSteps
    {
        public static AmbientRespawnAction Next(int active, int desired, int lastSet, bool inside, int previousActive)
        {
            if (lastSet != desired || inside) return AmbientRespawnAction.Done;
            if (active >= desired) return AmbientRespawnAction.SetDesired;
            if (previousActive >= 0 && active <= previousActive) return AmbientRespawnAction.SetDesired;
            return AmbientRespawnAction.SpawnOne;
        }
    }
}
