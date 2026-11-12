using System;

namespace PersistentCity.Core
{
    public static class BodyFade
    {
        public const double FadeInSeconds = 0.75;
        public const double HandoffFadeSeconds = WalkerFollow.FadeOutSeconds;

        public static float In(double elapsedSeconds)
            => !Position.Finite(elapsedSeconds) ? 1f : (float)Math.Max(0, Math.Min(1, elapsedSeconds / FadeInSeconds));

        public static float Out(double elapsedSeconds)
            => !Position.Finite(elapsedSeconds) ? 0f : (float)Math.Max(0, Math.Min(1, 1 - elapsedSeconds / HandoffFadeSeconds));
    }
}
