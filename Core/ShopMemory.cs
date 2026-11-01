using System;
using System.Collections.Generic;
using System.Linq;

namespace PersistentCity.Core
{
    /// <summary>Revision 1 executes bounded B experience/loyalty; unknown revisions refuse.</summary>
    public sealed class PopulationCapabilities
    {
        public string Code { get; set; } = "B";
        public int Revision { get; set; } = 1;
        public bool Active { get; set; }
        public void RequireExecutable()
        {
            if (Code != "B" || Revision != 1)
                throw new InvalidOperationException("Unsupported active population capability or revision; preserve the payload and suspend simulation.");
        }
    }

    public sealed class BusinessReference
    {
        public int Slot { get; set; }
        public string BusinessId { get; set; } = "";
        public string PlaceId { get; set; } = "";
    }
    public enum ShopExperience { Served = 1, Closed, Stockout, Queue }
    public enum ExperienceStage { Committed = 1, Reached, Failed, Served }
    public sealed class ExperienceWatermark
    {
        public int Day { get; set; }
        public MealKind Meal { get; set; }
        public int Context { get; set; }
        public ExperienceStage Stage { get; set; }
    }
    public sealed class ShopMemory
    {
        public int BusinessRef { get; set; }
        public int Day { get; set; }
        public int Served { get; set; }
        public int Affinity { get; set; }
        public ShopExperience Outcome { get; set; }
    }
    public sealed class ConsumerMemory
    {
        public WeatherSample Weather { get; set; }
        public ExperienceWatermark? Watermark { get; set; }
        public List<ShopMemory> Entries { get; set; } = new List<ShopMemory>();
    }

    /// <summary>Canonical ASCII contract; no capture, decay, eviction or choice contribution in B0.</summary>
    public static class MemoryWire
    {
        public const string Alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        public const int MaximumDay = 1000000000 / 1440;
        public static string Number(int value, int width)
        {
            if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));
            var chars = new char[width];
            for (int i = width - 1; i >= 0; i--) { chars[i] = Alphabet[value % 36]; value /= 36; }
            if (value != 0) throw new ArgumentOutOfRangeException(nameof(value));
            return new string(chars);
        }
        public static int Number(string text, int offset, int width)
        {
            int value = 0;
            for (int i = offset; i < offset + width; i++)
            {
                int digit = Alphabet.IndexOf(text[i]);
                if (digit < 0) throw new FormatException("Noncanonical base-36 alphabet.");
                value = checked(value * 36 + digit);
            }
            return value;
        }
        public static string Encode(ConsumerMemory memory)
        {
            Check(memory, MaximumDay, null);
            var w = memory.Watermark;
            var text = new System.Text.StringBuilder(48).Append('1');
            if (w == null) text.Append("-------");
            else text.Append(Number(w.Day, 4)).Append(Number((int)w.Meal, 1)).Append(Number(w.Context, 1)).Append(Number((int)w.Stage, 1));
            foreach (var e in memory.Entries)
                text.Append(Number(e.BusinessRef, 3)).Append(Number(e.Day, 4)).Append(Number(e.Served, 1))
                    .Append(Number(e.Affinity + 8, 1)).Append(Number((int)e.Outcome, 1));
            return text.ToString();
        }
        public static ConsumerMemory Decode(string text)
        {
            // Bound length and arity before creating any entry or splitting strings.
            CheckWire(text);
            var memory = new ConsumerMemory();
            if (string.CompareOrdinal(text, 1, "-------", 0, 7) != 0) memory.Watermark = new ExperienceWatermark {
                Day = Number(text, 1, 4), Meal = (MealKind)Number(text, 5, 1), Context = Number(text, 6, 1), Stage = (ExperienceStage)Number(text, 7, 1) };
            for (int i = 8; i < text.Length; i += 10) memory.Entries.Add(new ShopMemory {
                BusinessRef = Number(text, i, 3), Day = Number(text, i + 3, 4), Served = Number(text, i + 7, 1),
                Affinity = Number(text, i + 8, 1) - 8, Outcome = (ShopExperience)Number(text, i + 9, 1) });
            Check(memory, MaximumDay, null);
            return memory;
        }
        /// <summary>Streaming shape precheck without allocating the four entry objects a second time.</summary>
        public static void CheckWire(string text)
        {
            if (text == null || text.Length < 8 || text.Length > 48 || (text.Length - 8) % 10 != 0 || text[0] != '1')
                throw new FormatException("Invalid memory length/version.");
            if (string.CompareOrdinal(text, 1, "-------", 0, 7) != 0)
            {
                int day = Number(text, 1, 4), meal = Number(text, 5, 1), context = Number(text, 6, 1), stage = Number(text, 7, 1);
                if (day > MaximumDay || meal > 3 || context < 1 || context > 4 || stage < 1 || stage > 4)
                    throw new FormatException("Invalid experience watermark.");
            }
            int previous = -1;
            for (int i = 8; i < text.Length; i += 10)
            {
                int business = Number(text, i, 3), day = Number(text, i + 3, 4), served = Number(text, i + 7, 1),
                    affinity = Number(text, i + 8, 1), outcome = Number(text, i + 9, 1);
                if (business <= previous || business >= 20000 || day > MaximumDay || affinity > 16 || outcome < 1 || outcome > 4 || outcome == 1 && served == 0)
                    throw new FormatException("Invalid shop memory/reference.");
                previous = business;
            }
        }
        public static void Check(ConsumerMemory memory, int day, ISet<int>? references)
        {
            if (memory == null || memory.Entries == null || memory.Entries.Count > 4) throw new FormatException("Invalid memory arity.");
            var w = memory.Watermark;
            if (w != null && (w.Day < 0 || w.Day > day || w.Day > MaximumDay || (int)w.Meal < 0 || (int)w.Meal > 3
                || w.Context < 1 || w.Context > 4 || (int)w.Stage < 1 || (int)w.Stage > 4)) throw new FormatException("Invalid experience watermark.");
            int previous = -1;
            foreach (var e in memory.Entries)
            {
                if (e == null || e.BusinessRef <= previous || e.BusinessRef >= 20000 || e.Day < 0 || e.Day > day || e.Day > MaximumDay
                    || e.Served < 0 || e.Served > 35 || e.Affinity < -8 || e.Affinity > 8 || (int)e.Outcome < 1 || (int)e.Outcome > 4
                    || e.Outcome == ShopExperience.Served && e.Served == 0 || references != null && !references.Contains(e.BusinessRef))
                    throw new FormatException("Invalid shop memory/reference.");
                previous = e.BusinessRef;
            }
        }
    }

}
