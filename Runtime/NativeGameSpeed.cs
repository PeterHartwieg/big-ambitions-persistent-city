using System;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UI;

namespace PersistentCity.Runtime
{
    internal static class NativeGameSpeed
    {
        internal static readonly FieldInfo? SpeedField = AccessTools.Field(typeof(GameSpeedController), "currentGameSpeed");
        private static Func<GameSpeedController, GameSpeed>? readSpeed;
        private static bool prepared;

        // Called once when F0 is enabled, never from a body/frame query. A private-field
        // contract mismatch or unsupported code generation leaves the feature disabled.
        public static bool Prepare()
        {
            if (prepared) return readSpeed != null;
            prepared = true;
            try
            {
                if (SpeedField == null || SpeedField.IsStatic || SpeedField.FieldType != typeof(GameSpeed)
                    || SpeedField.DeclaringType == null || !SpeedField.DeclaringType.IsAssignableFrom(typeof(GameSpeedController))) return false;
                var method = new DynamicMethod("PersistentCityReadGameSpeed", typeof(GameSpeed), new[] { typeof(GameSpeedController) }, typeof(NativeGameSpeed).Module, true);
                var il = method.GetILGenerator();
                il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldfld, SpeedField); il.Emit(OpCodes.Ret);
                readSpeed = (Func<GameSpeedController, GameSpeed>)method.CreateDelegate(typeof(Func<GameSpeedController, GameSpeed>));
                return true;
            }
            catch { return false; }
        }

        public static bool IsFastForward(GameSpeedController? controller)
            => controller != null && (readSpeed == null || readSpeed(controller)?.speed == TimeSpeed.FastForward);
        public static bool CurrentFastForward => IsFastForward(InstanceBehavior<UIs>.Instance?.gameSpeed);

        // Preserve the previous off-path accessor, including its missing-field/null behavior.
        public static bool LegacyFastForward
        {
            get
            {
                var ui = InstanceBehavior<UIs>.Instance;
                var controller = ui == null ? null : ui.gameSpeed;
                var speed = controller == null ? null : SpeedField?.GetValue(controller) as GameSpeed;
                return speed != null && speed.speed == TimeSpeed.FastForward;
            }
        }
    }
}
