using System;

namespace PersistentCity.Core
{
    /// <summary>
    /// GR-9 (G6): bodies fade in where they appear and fade out where they leave at a mode change (a walker reaching its
    /// car, a car reaching its parking point, a walker boarding the subway), instead of popping. Fades run on the follow
    /// clock, which stands while the city clock does. A body whose materials cannot fade keeps the old pop.
    /// </summary>
    public static class BodyFade
    {
        public const double FadeInSeconds = 0.75;
        /// <summary>A body leaving at a mode change fades as quickly as a corrected one (<see cref="WalkerFollow.FadeOutSeconds"/>).</summary>
        public const double HandoffFadeSeconds = WalkerFollow.FadeOutSeconds;

        /// <summary>Alpha of a body that started fading in <paramref name="elapsedSeconds"/> ago: 0 at the start, 1 once done.</summary>
        public static float In(double elapsedSeconds)
            => !Position.Finite(elapsedSeconds) ? 1f : (float)Math.Max(0, Math.Min(1, elapsedSeconds / FadeInSeconds));

        /// <summary>Alpha of a body that started fading out <paramref name="elapsedSeconds"/> ago: 1 at the start, 0 once done.</summary>
        public static float Out(double elapsedSeconds)
            => !Position.Finite(elapsedSeconds) ? 0f : (float)Math.Max(0, Math.Min(1, 1 - elapsedSeconds / HandoffFadeSeconds));
    }
}
