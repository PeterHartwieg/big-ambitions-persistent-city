using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using HarmonyLib;
using Newtonsoft.Json;
using PersistentCity.Persistence;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace PersistentCity.Runtime
{
    internal static class NativeSaveHandOff
    {
        private const string PatchId = "persistentcity.save-handoff";
        private static readonly Harmony patches = new Harmony(PatchId);
        private static MethodInfo? compress, save;
        private static long nativeStarted;

        internal static bool Installed { get; private set; }
        internal static string Status { get; private set; } = "not installed";
        internal static bool TimeNativeSave { get; set; }
        internal static long NativeSaves { get; private set; }
        internal static double LastNativeSaveMilliseconds { get; private set; }

        internal static void Install()
        {
            if (Installed) return;
            try
            {
                compress = AccessTools.Method(typeof(SaveGameSerializationHelper), nameof(SaveGameSerializationHelper.CompressBinaryData),
                    new[] { typeof(string), typeof(string) });
                if (compress == null || compress.ReturnType != typeof(bool) || !compress.IsStatic)
                { Status = "SaveGameSerializationHelper.CompressBinaryData(string, string) is missing."; return; }
                save = AccessTools.Method(typeof(SaveGameManager), nameof(SaveGameManager.Save),
                    new[] { typeof(SaveGameManager.SaveType), typeof(string), typeof(string) });
                if (save == null) { Status = "SaveGameManager.Save(SaveType, string, string) is missing."; return; }
                string? contract = SelfTest();
                if (contract != null) { Status = "Save hand-off contract check failed: " + contract; return; }
                patches.Patch(compress, prefix: new HarmonyMethod(typeof(NativeSaveHandOff), nameof(CompressPrefix)) { priority = Priority.First });
                patches.Patch(save, prefix: new HarmonyMethod(typeof(NativeSaveHandOff), nameof(SavePrefix)) { priority = Priority.First },
                    postfix: new HarmonyMethod(typeof(NativeSaveHandOff), nameof(SavePostfix)) { priority = Priority.Last });
                Installed = true;
                Status = "";
            }
            catch (Exception error) { Status = "Save hand-off install failed: " + error.Message; }
        }

        internal static string? Conflict()
        {
            if (!Installed || compress == null || save == null) return Status.Length > 0 ? Status : "Save hand-off is not installed.";
            foreach (var method in new[] { compress, save })
            {
                var info = Harmony.GetPatchInfo(method);
                if (info == null || !info.Owners.Contains(PatchId)) return "Save hand-off patch is missing on " + method.Name + ".";
                if (method == compress)
                    foreach (string owner in info.Owners)
                        if (!string.Equals(owner, PatchId, StringComparison.Ordinal)) return "Another mod patches the native save compression: " + owner + ".";
            }
            return null;
        }

        private static bool CompressPrefix(string path, string outputPath, ref bool __result)
        {
            var pending = SaveHandOff.Claim(path);
            if (pending == null) return true;
            __result = SaveHandOff.Compress(path, outputPath, pending);
            return false;
        }

        private static void SavePrefix() => nativeStarted = TimeNativeSave ? Stopwatch.GetTimestamp() : 0;

        private static void SavePostfix()
        {
            SaveHandOff.MarkSerialized();
            if (nativeStarted == 0) return;
            LastNativeSaveMilliseconds = (Stopwatch.GetTimestamp() - nativeStarted) * 1000.0 / Stopwatch.Frequency;
            NativeSaves++;
            nativeStarted = 0;
        }

        private static string? SelfTest()
        {
            var payload = new StringBuilder("{\"quote\":\"\\\"\",\"slash\":\"\\\\\",\"line\":\"a\nb\",\"name\":\"\u00e9\u0416\ud83d\ude00\",\"fill\":\"");
            payload.Append('x', 70000).Append("\"}");
            string expectedPayload = payload.ToString();
            string expectedMarker = SaveEpochStore.Success(1, 1, 61.5, "self-test", expectedPayload.Length);
            const string payloadToken = PendingSave.TokenPrefix + "state-selftest0000000000000000000000";
            const string markerToken = PendingSave.TokenPrefix + "epoch-selftest0000000000000000000000";
            var data = new Dictionary<string, string>
            {
                ["other:before"] = "kept", [SaveStore.StateKey] = payloadToken, [SaveEpochStore.Key] = markerToken, ["other:after"] = "\u0416",
            };
            string? binary = Odin(data, expectedPayload, expectedMarker, payloadToken, markerToken);
            if (binary != null) return binary;
            byte[] json = new UTF8Encoding(false).GetBytes(JsonConvert.SerializeObject(data, Formatting.None));
            var spliced = Splice(json, payloadToken, markerToken, expectedPayload, expectedMarker, out var outcome);
            if (outcome != SaveSpliceOutcome.Spliced) return "JSON splice outcome " + outcome + ".";
            var read = JsonConvert.DeserializeObject<Dictionary<string, string>>(new UTF8Encoding(false).GetString(spliced));
            return Matches(read, expectedPayload, expectedMarker) ? null : "JSON splice did not round-trip.";
        }

        private static string? Odin(Dictionary<string, string> data, string expectedPayload, string expectedMarker, string payloadToken, string markerToken)
        {
            var utility = Type.GetType("OdinSerializer.SerializationUtility, OdinSerializer");
            var format = Type.GetType("OdinSerializer.DataFormat, OdinSerializer");
            if (utility == null || format == null) return "OdinSerializer is missing.";
            var methods = utility.GetMethods(BindingFlags.Public | BindingFlags.Static);
            var serialize = methods.FirstOrDefault(m => m.Name == "SerializeValue" && m.IsGenericMethodDefinition && m.ReturnType == typeof(byte[])
                && m.GetParameters().Length == 3 && m.GetParameters()[0].ParameterType.IsGenericParameter && m.GetParameters()[1].ParameterType == format);
            var deserialize = methods.FirstOrDefault(m => m.Name == "DeserializeValue" && m.IsGenericMethodDefinition
                && m.GetParameters().Length == 3 && m.GetParameters()[0].ParameterType == typeof(byte[]) && m.GetParameters()[1].ParameterType == format);
            if (serialize == null || deserialize == null) return "OdinSerializer byte-array methods are missing.";
            object binary = Enum.Parse(format, "Binary");
            var bytes = (byte[])serialize.MakeGenericMethod(typeof(Dictionary<string, string>)).Invoke(null, new object?[] { data, binary, null });
            var spliced = Splice(bytes, payloadToken, markerToken, expectedPayload, expectedMarker, out var outcome);
            if (outcome != SaveSpliceOutcome.Spliced) return "Odin splice outcome " + outcome + ".";
            var read = (Dictionary<string, string>?)deserialize.MakeGenericMethod(typeof(Dictionary<string, string>)).Invoke(null, new object?[] { spliced, binary, null });
            return Matches(read, expectedPayload, expectedMarker) ? null : "Odin splice did not round-trip.";
        }

        private static byte[] Splice(byte[] source, string payloadToken, string markerToken, string payload, string marker, out SaveSpliceOutcome outcome)
        {
            using var input = new MemoryStream(source, false);
            using var output = new MemoryStream();
            outcome = SaveSplice.Copy(input, output, new[] { payloadToken, markerToken }, new[] { payload, marker });
            return output.ToArray();
        }

        private static bool Matches(Dictionary<string, string>? read, string payload, string marker) => read != null && read.Count == 4
            && read.TryGetValue(SaveStore.StateKey, out var p) && p == payload && read.TryGetValue(SaveEpochStore.Key, out var m) && m == marker
            && read.TryGetValue("other:before", out var before) && before == "kept" && read.TryGetValue("other:after", out var after) && after == "\u0416";
    }
}
