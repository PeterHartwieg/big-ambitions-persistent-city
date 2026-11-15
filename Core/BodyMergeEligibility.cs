using System;
using System.Text;

namespace PersistentCity.Core
{
    public enum BodyMergeFallback
    {
        None,
        FastOff,
        NotPlaying,
        AlreadyMerged,
        LegacyAnimation,
        FullBlendshapes,
        OtherMethod,
        IgnoreListMissing,
        ConsoleLogs,
        ParamsMissing,
        NoAnimator,
        NoSources,
        DifferentRootBones,
        BonesMissing,
        ShortBindposes,
        NoGroups,
        CollectError,
        MergeError,
    }

    public enum BodyMergeBlendshapes { Disabled, Enabled, FullSupport }

    public readonly struct BodyMergeSettings
    {
        public BodyMergeSettings(bool playing, bool merged, bool legacyAnimation, BodyMergeBlendshapes blendshapes, bool oneMeshPerMaterial,
            bool ignoreList, bool consoleLogs, bool parameters, bool animator)
        {
            Playing = playing; Merged = merged; LegacyAnimation = legacyAnimation; Blendshapes = blendshapes; OneMeshPerMaterial = oneMeshPerMaterial;
            IgnoreList = ignoreList; ConsoleLogs = consoleLogs; Parameters = parameters; Animator = animator;
        }

        public bool Playing { get; }
        public bool Merged { get; }
        public bool LegacyAnimation { get; }
        public BodyMergeBlendshapes Blendshapes { get; }
        public bool OneMeshPerMaterial { get; }
        public bool IgnoreList { get; }
        public bool ConsoleLogs { get; }
        public bool Parameters { get; }
        public bool Animator { get; }
    }

    public readonly struct BodyMergeSource
    {
        public BodyMergeSource(bool renderer, bool enabled, bool mesh, int blendshapes, int materials, int submeshes, bool nullMaterial)
        {
            Renderer = renderer; Enabled = enabled; Mesh = mesh; Blendshapes = blendshapes; Materials = materials; Submeshes = submeshes; NullMaterial = nullMaterial;
        }

        public bool Renderer { get; }
        public bool Enabled { get; }
        public bool Mesh { get; }
        public int Blendshapes { get; }
        public int Materials { get; }
        public int Submeshes { get; }
        public bool NullMaterial { get; }
    }

    public static class BodyMergeEligibility
    {
        public static BodyMergeFallback Check(in BodyMergeSettings settings)
        {
            if (!settings.Playing) return BodyMergeFallback.NotPlaying;
            if (settings.Merged) return BodyMergeFallback.AlreadyMerged;
            if (settings.LegacyAnimation) return BodyMergeFallback.LegacyAnimation;
            if (settings.Blendshapes == BodyMergeBlendshapes.FullSupport) return BodyMergeFallback.FullBlendshapes;
            if (!settings.OneMeshPerMaterial) return BodyMergeFallback.OtherMethod;
            if (!settings.IgnoreList) return BodyMergeFallback.IgnoreListMissing;
            if (settings.ConsoleLogs) return BodyMergeFallback.ConsoleLogs;
            if (!settings.Parameters) return BodyMergeFallback.ParamsMissing;
            if (!settings.Animator) return BodyMergeFallback.NoAnimator;
            return BodyMergeFallback.None;
        }

        public static bool Item(bool combinedManager, bool ignored) => !combinedManager && !ignored;

        public static bool Source(in BodyMergeSource source, BodyMergeBlendshapes blendshapes) =>
            source.Renderer && source.Enabled && source.Mesh
            && !(source.Blendshapes > 0 && blendshapes == BodyMergeBlendshapes.Disabled)
            && source.Materials != 0 && source.Materials == source.Submeshes && !source.NullMaterial;
    }

    public sealed class BodyMergeFallbacks
    {
        private static readonly string[] Names =
        {
            "none", "fast off", "not playing", "already merged", "legacy animation", "full blendshapes", "other method", "ignore list missing",
            "console logs", "params missing", "no animator", "no sources", "different root bones", "bones missing", "short bindposes", "no groups",
            "collect error", "merge error",
        };
        private readonly long[] counts = new long[Names.Length];

        public long Total { get; private set; }

        public void Count(BodyMergeFallback reason)
        {
            int at = (int)reason;
            if (at <= 0 || at >= counts.Length) throw new ArgumentOutOfRangeException(nameof(reason));
            counts[at]++;
            Total++;
        }

        public long this[BodyMergeFallback reason] => counts[(int)reason];

        public string Describe()
        {
            if (Total == 0) return "none";
            var text = new StringBuilder();
            for (int i = 1; i < counts.Length; i++)
            {
                if (counts[i] == 0) continue;
                if (text.Length != 0) text.Append(", ");
                text.Append(Names[i]).Append(' ').Append(counts[i]);
            }
            return text.ToString();
        }
    }
}
