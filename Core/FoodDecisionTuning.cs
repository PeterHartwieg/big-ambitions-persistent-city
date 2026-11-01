using System;
using System.Globalization;
using System.Text;

namespace PersistentCity.Core
{
    /// <summary>Immutable effective A3 tuning image. Wire revision 1 is twelve exact binary64
    /// values in fixed order, never part of a logical draw key. No revision history is retained.</summary>
    public sealed class FoodDecisionTuning
    {
        public const int WireLength = 193;
        internal FoodChoiceSettings Settings { get; }
        private FoodDecisionTuning(FoodChoiceSettings settings) { Settings = settings; }
        public static FoodDecisionTuning Capture(FoodChoiceSettings live)
        {
            var copy = live.Copy(); copy.Clamp();
            return new FoodDecisionTuning(copy);
        }
        public string ToWire()
        {
            var text = new StringBuilder(WireLength); text.Append('1');
            void Add(double value) => text.Append(BitConverter.DoubleToInt64Bits(value).ToString("X16", CultureInfo.InvariantCulture));
            Add(Settings.FoodChoiceTemperature);
            Add(Settings.FoodChoiceWeightFloor);
            Add(Settings.FoodChoiceTasteWeight);
            Add(Settings.FoodChoicePriceWeight);
            Add(Settings.FoodChoiceDistanceWeight);
            Add(Settings.FoodChoiceDistanceScaleMinutes);
            Add(Settings.FoodChoiceQueueWeight);
            Add(Settings.FoodChoiceQualityWeight);
            Add(Settings.FoodChoiceTimePressureWeight);
            Add(Settings.FoodChoiceHealthWeight);
            Add(Settings.FoodChoiceHouseholdWeight);
            Add(Settings.FoodShortlistTemperature);
            return text.ToString();
        }
        public static bool TryParse(string? wire, out FoodDecisionTuning? tuning)
        {
            tuning = null;
            if (!IsCanonicalWire(wire)) return false;
            int at = 1;
            double Read() { double value = ReadWord(wire!, at); at += 16; return value; }
            var settings = new FoodChoiceSettings {
                FoodChoiceTemperature = Read(),
                FoodChoiceWeightFloor = Read(),
                FoodChoiceTasteWeight = Read(),
                FoodChoicePriceWeight = Read(),
                FoodChoiceDistanceWeight = Read(),
                FoodChoiceDistanceScaleMinutes = Read(),
                FoodChoiceQueueWeight = Read(),
                FoodChoiceQualityWeight = Read(),
                FoodChoiceTimePressureWeight = Read(),
                FoodChoiceHealthWeight = Read(),
                FoodChoiceHouseholdWeight = Read(),
                FoodShortlistTemperature = Read() };
            tuning = new FoodDecisionTuning(settings); return true;
        }
        /// <summary>Allocation-free streaming shape precheck; no image is created until the full graph passes.</summary>
        public static bool IsCanonicalWire(string? wire)
        {
            if (wire == null || wire.Length != WireLength || wire[0] != '1') return false;
            for (int i = 1; i < wire.Length; i++)
                if (!(wire[i] >= '0' && wire[i] <= '9' || wire[i] >= 'A' && wire[i] <= 'F')) return false;
            if (!In(ReadWord(wire, 1), 0.25, 4)) return false; // FoodChoiceTemperature
            if (!In(ReadWord(wire, 17), 0.001, 0.25)) return false; // FoodChoiceWeightFloor
            if (!In(ReadWord(wire, 33), 0, 3)) return false; // FoodChoiceTasteWeight
            if (!In(ReadWord(wire, 49), 0, 3)) return false; // FoodChoicePriceWeight
            if (!In(ReadWord(wire, 65), 0, 3)) return false; // FoodChoiceDistanceWeight
            if (!In(ReadWord(wire, 81), 2, 60)) return false; // FoodChoiceDistanceScaleMinutes
            if (!In(ReadWord(wire, 97), 0, 3)) return false; // FoodChoiceQueueWeight
            if (!In(ReadWord(wire, 113), 0, 3)) return false; // FoodChoiceQualityWeight
            if (!In(ReadWord(wire, 129), 0, 3)) return false; // FoodChoiceTimePressureWeight
            if (!In(ReadWord(wire, 145), 0, 3)) return false; // FoodChoiceHealthWeight
            if (!In(ReadWord(wire, 161), 0.1, 5)) return false; // FoodChoiceHouseholdWeight
            if (!In(ReadWord(wire, 177), 0.25, 4)) return false; // FoodShortlistTemperature
            return true;
        }
        private static double ReadWord(string wire, int at)
        {
            ulong bits = 0;
            for (int end = at + 16; at < end; at++) bits = bits * 16 + (uint)(wire[at] <= '9' ? wire[at] - '0' : wire[at] - 'A' + 10);
            return BitConverter.Int64BitsToDouble(unchecked((long)bits));
        }
        private static bool In(double value, double min, double max) => Position.Finite(value) && value >= min && value <= max;
    }
}
