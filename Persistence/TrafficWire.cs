using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using PersistentCity.Core;

namespace PersistentCity.Persistence
{
    public static partial class SaveCodec
    {
        private const char TrafficItem = ';', TrafficField = ',', TrafficElement = ' ';
        private const int MaxTrafficIdLength = 256;
        [ThreadStatic] private static StringBuilder? trafficBuffer;

        internal static string TrafficDefinitionsWire(List<TrafficLinkDefinition> definitions)
        {
            var text = TrafficBuffer();
            int previousId = -1;
            long headway = 0;
            for (int i = 0; i < definitions.Count; i++)
            {
                var definition = definitions[i];
                Require(definition != null && definition.Id >= 0 && definition.Capacity >= 1, "Invalid traffic definition.");
                text.Append(TrafficItem);
                if (definition!.Id != previousId + 1) AppendWireInt(text, definition.Id);
                text.Append(TrafficField);
                if (definition.Capacity != 1) AppendWireInt(text, definition.Capacity);
                text.Append(TrafficField);
                AppendBits(text, definition.FreeFlowMinutes);
                long bits = BitConverter.DoubleToInt64Bits(definition.HeadwayMinutes);
                bool sameHeadway = i > 0 && bits == headway;
                var signal = definition.Signal;
                if (signal != null || !sameHeadway) text.Append(TrafficField);
                if (!sameHeadway) AppendBits(text, definition.HeadwayMinutes);
                if (signal != null)
                {
                    text.Append(TrafficField); AppendBits(text, signal.CycleMinutes);
                    text.Append(TrafficField); AppendBits(text, signal.GreenStartMinutes);
                    text.Append(TrafficField); AppendBits(text, signal.GreenDurationMinutes);
                    text.Append(TrafficField); AppendBits(text, signal.PhaseOffsetMinutes);
                }
                headway = bits; previousId = definition.Id;
            }
            return TrafficText(text);
        }

        internal static string TrafficLinksWire(List<TrafficLinkState> links)
        {
            var text = TrafficBuffer();
            int previousId = -1;
            long discharge = 0;
            for (int i = 0; i < links.Count; i++)
            {
                var link = links[i];
                Require(link != null && link.LinkId >= 0 && link.MergeCursor >= -1 && link.Vehicles != null && link.WaitingSources != null
                    && link.IncomingLinks != null, "Invalid traffic link state.");
                text.Append(TrafficItem);
                int start = text.Length;
                if (link!.LinkId != previousId + 1) AppendWireInt(text, link.LinkId);
                text.Append(TrafficField);
                if (link.Closed) text.Append('1');
                text.Append(TrafficField);
                long bits = BitConverter.DoubleToInt64Bits(link.NextDischargeAt);
                if (i == 0 || bits != discharge) AppendBits(text, link.NextDischargeAt);
                text.Append(TrafficField);
                if (link.MergeCursor >= 0) AppendWireInt(text, link.MergeCursor);
                text.Append(TrafficField);
                for (int k = 0; k < link.IncomingLinks.Count; k++)
                {
                    Require(link.IncomingLinks[k] >= 0, "Invalid incoming traffic link.");
                    if (k > 0) text.Append(TrafficElement);
                    AppendWireInt(text, link.IncomingLinks[k]);
                }
                text.Append(TrafficField);
                AppendIds(text, link.Vehicles);
                text.Append(TrafficField);
                AppendIds(text, link.WaitingSources);
                int end = text.Length;
                while (end > start && text[end - 1] == TrafficField) end--;
                text.Length = end;
                discharge = bits; previousId = link.LinkId;
            }
            return TrafficText(text);
        }

        internal static List<TrafficLinkDefinition> ReadTrafficDefinitions(string wire, ref int records)
        {
            var result = new List<TrafficLinkDefinition>();
            if (!TrafficHeader(wire)) return result;
            var f = new int[16];
            int position = 2, previousId = -1;
            long headway = 0;
            while (true)
            {
                int end = ItemEnd(wire, position);
                int count = TrafficFields(wire, position, end, f, 8);
                Require(count == 3 || count == 4 || count == 8, "Invalid traffic definition fields.");
                Require(result.Count < MaxTrafficLinks, "Oversized collection.");
                Charge(ref records);
                bool first = result.Count == 0;
                var definition = new TrafficLinkDefinition { Id = WireId(wire, f[0], f[1], previousId) };
                definition.Capacity = f[3] > f[2] ? WireInt(wire, f[2], f[3]) : 1;
                Require(f[3] == f[2] || definition.Capacity > 1, "Noncanonical traffic capacity.");
                definition.FreeFlowMinutes = WireBits(wire, f[4], f[5]);
                long bits = headway;
                if (count > 3 && f[7] > f[6])
                {
                    bits = BitConverter.DoubleToInt64Bits(WireBits(wire, f[6], f[7]));
                    Require(first || bits != headway, "Noncanonical traffic headway.");
                }
                else Require(!first && count != 4, "Missing or noncanonical traffic headway.");
                definition.HeadwayMinutes = BitConverter.Int64BitsToDouble(bits);
                if (count == 8)
                    definition.Signal = new TrafficSignalSchedule
                    {
                        CycleMinutes = WireBits(wire, f[8], f[9]), GreenStartMinutes = WireBits(wire, f[10], f[11]),
                        GreenDurationMinutes = WireBits(wire, f[12], f[13]), PhaseOffsetMinutes = WireBits(wire, f[14], f[15]),
                    };
                result.Add(definition);
                headway = bits; previousId = definition.Id;
                if (end == wire.Length) return result;
                position = end + 1;
            }
        }

        internal static List<TrafficLinkState> ReadTrafficLinks(string wire, ref int records)
        {
            var result = new List<TrafficLinkState>();
            if (!TrafficHeader(wire)) return result;
            var f = new int[14];
            int position = 2, previousId = -1;
            long discharge = 0;
            while (true)
            {
                int end = ItemEnd(wire, position);
                int count = TrafficFields(wire, position, end, f, 7);
                Require(count == 0 || f[2 * count - 1] > f[2 * count - 2], "Noncanonical traffic link item.");
                for (int i = 2 * count; i < f.Length; i += 2) f[i] = f[i + 1] = 0;
                Require(result.Count < MaxTrafficLinks, "Oversized collection.");
                Charge(ref records);
                bool first = result.Count == 0;
                var link = new TrafficLinkState { LinkId = WireId(wire, f[0], f[1], previousId) };
                if (f[3] > f[2]) { Require(f[3] - f[2] == 1 && wire[f[2]] == '1', "Invalid traffic link closure."); link.Closed = true; }
                long bits = discharge;
                if (f[5] > f[4])
                {
                    bits = BitConverter.DoubleToInt64Bits(WireBits(wire, f[4], f[5]));
                    Require(first || bits != discharge, "Noncanonical traffic discharge.");
                }
                else Require(!first, "Missing first traffic discharge.");
                link.NextDischargeAt = BitConverter.Int64BitsToDouble(bits);
                link.MergeCursor = f[7] > f[6] ? WireInt(wire, f[6], f[7]) : -1;
                ReadIncoming(wire, f[8], f[9], link.IncomingLinks, ref records);
                ReadIds(wire, f[10], f[11], link.Vehicles, MaxCitizens, ref records);
                ReadIds(wire, f[12], f[13], link.WaitingSources, MaxCitizens, ref records);
                result.Add(link);
                discharge = bits; previousId = link.LinkId;
                if (end == wire.Length) return result;
                position = end + 1;
            }
        }

        private static int TrafficFields(string wire, int start, int end, int[] bounds, int maximum)
        {
            if (start == end) return 0;
            int count = 0, position = start;
            while (true)
            {
                Require(count < maximum, "Surplus traffic wire field.");
                int comma = wire.IndexOf(TrafficField, position, end - position);
                int stop = comma < 0 ? end : comma;
                bounds[2 * count] = position; bounds[2 * count + 1] = stop; count++;
                if (comma < 0) return count;
                position = comma + 1;
            }
        }

        private static int WireId(string wire, int start, int end, int previous)
        {
            if (end == start) { Require(previous < int.MaxValue, "Traffic link ID out of range."); return previous + 1; }
            int id = WireInt(wire, start, end);
            Require(id != previous + 1, "Noncanonical traffic link ID.");
            return id;
        }

        private static StringBuilder TrafficBuffer()
        {
            var text = trafficBuffer ??= new StringBuilder(1024);
            text.Clear();
            text.Append('1');
            return text;
        }

        private static string TrafficText(StringBuilder text)
        {
            string wire = text.ToString();
            text.Clear();
            if (text.Capacity > 1 << 20) text.Capacity = 1024;
            return wire;
        }

        private static bool TrafficHeader(string wire)
        {
            Require(wire.Length >= 1 && wire[0] == '1', "Invalid traffic wire version.");
            if (wire.Length == 1) return false;
            Require(wire[1] == TrafficItem, "Invalid traffic wire header.");
            return true;
        }

        private static int ItemEnd(string wire, int position)
        {
            int end = wire.IndexOf(TrafficItem, position);
            return end < 0 ? wire.Length : end;
        }

        private static void ReadIncoming(string wire, int start, int end, List<int> target, ref int records)
        {
            if (start == end) return;
            int position = start;
            while (true)
            {
                int stop = wire.IndexOf(TrafficElement, position, end - position);
                if (stop < 0) stop = end;
                Require(target.Count < MaxTrafficLinks, "Oversized collection.");
                Require(++records <= MaxEmbeddedRecords, "Too many opening, route and visit records.");
                target.Add(WireInt(wire, position, stop));
                if (stop == end) return;
                position = stop + 1;
            }
        }

        private static void ReadIds(string wire, int start, int end, List<string> target, int limit, ref int records)
        {
            if (start == end) return;
            int position = start;
            while (true)
            {
                int stop = wire.IndexOf(TrafficElement, position, end - position);
                if (stop < 0) stop = end;
                Require(target.Count < limit, "Oversized collection.");
                Require(++records <= MaxEmbeddedRecords, "Too many opening, route and visit records.");
                target.Add(WireVehicle(wire, position, stop));
                if (stop == end) return;
                position = stop + 1;
            }
        }

        private static void AppendWireInt(StringBuilder text, int value)
        {
            Require(value >= 0, "Negative traffic wire integer.");
            if (value >= 10) AppendWireInt(text, value / 10);
            text.Append((char)('0' + value % 10));
        }

        private static int WireInt(string wire, int start, int end)
        {
            Require(end > start && end - start <= 10 && (wire[start] != '0' || end - start == 1), "Noncanonical traffic wire integer.");
            long value = 0;
            for (int i = start; i < end; i++)
            {
                char c = wire[i];
                Require(c >= '0' && c <= '9', "Invalid traffic wire integer.");
                value = value * 10 + (c - '0');
            }
            Require(value <= int.MaxValue, "Traffic wire integer out of range.");
            return (int)value;
        }

        private static void AppendBits(StringBuilder text, double value)
        {
            ulong bits = unchecked((ulong)BitConverter.DoubleToInt64Bits(value));
            for (int shift = 60; shift >= 0; shift -= 4)
            {
                int digit = (int)(bits >> shift) & 15;
                text.Append((char)(digit < 10 ? '0' + digit : 'A' + digit - 10));
            }
        }

        private static double WireBits(string wire, int start, int end)
        {
            Require(end - start == 16, "Invalid traffic binary64 length.");
            ulong bits = 0;
            for (int i = start; i < end; i++)
            {
                char c = wire[i];
                int digit = c >= '0' && c <= '9' ? c - '0' : c >= 'A' && c <= 'F' ? c - 'A' + 10 : -1;
                Require(digit >= 0, "Invalid traffic binary64 alphabet.");
                bits = bits << 4 | (uint)digit;
            }
            return BitConverter.Int64BitsToDouble(unchecked((long)bits));
        }

        private static bool SafeIdChar(char c) => c >= 'a' && c <= 'z' || c >= 'A' && c <= 'Z' || c >= '0' && c <= '9' || c == ':' || c == '_' || c == '.' || c == '-';

        private static void AppendIds(StringBuilder text, List<string> ids)
        {
            for (int k = 0; k < ids.Count; k++)
            {
                string id = ids[k];
                Require(!string.IsNullOrEmpty(id) && id.Length <= MaxTrafficIdLength, "Invalid traffic vehicle reference.");
                if (k > 0) text.Append(TrafficElement);
                bool safe = true;
                foreach (char c in id) if (!SafeIdChar(c)) { safe = false; break; }
                if (safe) { text.Append(id); continue; }
                foreach (byte value in Encoding.UTF8.GetBytes(id))
                {
                    char c = (char)value;
                    if (value < 128 && SafeIdChar(c)) { text.Append(c); continue; }
                    text.Append('%').Append("0123456789ABCDEF"[value >> 4]).Append("0123456789ABCDEF"[value & 15]);
                }
            }
        }

        private static string WireVehicle(string wire, int start, int end)
        {
            Require(end > start, "Empty traffic vehicle reference.");
            bool escaped = false;
            for (int i = start; i < end; i++)
            {
                char c = wire[i];
                if (c == '%') escaped = true;
                else Require(SafeIdChar(c), "Invalid traffic vehicle reference alphabet.");
            }
            if (!escaped)
            {
                Require(end - start <= MaxTrafficIdLength, "Oversized traffic vehicle reference.");
                return wire.Substring(start, end - start);
            }
            var bytes = new List<byte>(end - start);
            for (int i = start; i < end; i++)
            {
                char c = wire[i];
                if (c != '%') { bytes.Add((byte)c); continue; }
                Require(i + 2 < end, "Truncated traffic vehicle escape.");
                bytes.Add((byte)(WireHex(wire[i + 1]) * 16 + WireHex(wire[i + 2])));
                i += 2;
            }
            string id;
            try { id = new UTF8Encoding(false, true).GetString(bytes.ToArray()); }
            catch (ArgumentException) { throw new SaveValidationException("Invalid traffic vehicle reference encoding."); }
            Require(id.Length <= MaxTrafficIdLength, "Oversized traffic vehicle reference.");
            var canonical = new StringBuilder(end - start);
            AppendIds(canonical, new List<string> { id });
            Require(string.CompareOrdinal(canonical.ToString(), 0, wire, start, end - start) == 0 && canonical.Length == end - start,
                "Noncanonical traffic vehicle reference.");
            return id;
        }

        private static int WireHex(char c)
        {
            int digit = c >= '0' && c <= '9' ? c - '0' : c >= 'A' && c <= 'F' ? c - 'A' + 10 : -1;
            Require(digit >= 0, "Invalid traffic vehicle escape.");
            return digit;
        }

        private static bool CheckTrafficWire(JsonReader reader, Type type, TokenText text, ref int records)
        {
            bool definitions = type == typeof(List<TrafficLinkDefinition>);
            if (!definitions && type != typeof(List<TrafficLinkState>) || reader.TokenType != JsonToken.String) return false;
            string wire = (string)reader.Value!;
            Require(text.LiteralAsciiString((IJsonLineInfo)reader, wire), "Noncanonical traffic wire encoding.");
            if (definitions) ReadTrafficDefinitions(wire, ref records);
            else ReadTrafficLinks(wire, ref records);
            return true;
        }

        private sealed class TrafficConverter : JsonConverter
        {
            public override bool CanConvert(Type objectType) => objectType == typeof(List<TrafficLinkDefinition>) || objectType == typeof(List<TrafficLinkState>);

            public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
            {
                if (value is List<TrafficLinkDefinition> definitions) writer.WriteValue(TrafficDefinitionsWire(definitions));
                else if (value is List<TrafficLinkState> links) writer.WriteValue(TrafficLinksWire(links));
                else writer.WriteNull();
            }

            public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
            {
                int records = 0;
                bool definitions = objectType == typeof(List<TrafficLinkDefinition>);
                if (reader.TokenType == JsonToken.String)
                    return definitions ? ReadTrafficDefinitions((string)reader.Value!, ref records) : (object)ReadTrafficLinks((string)reader.Value!, ref records);
                if (reader.TokenType == JsonToken.Null) return null;
                Require(reader.TokenType == JsonToken.StartArray, "Invalid traffic collection.");
                return definitions ? Legacy<TrafficLinkDefinition>(reader, serializer) : (object)Legacy<TrafficLinkState>(reader, serializer);
            }

            private static List<T> Legacy<T>(JsonReader reader, JsonSerializer serializer)
            {
                var result = new List<T>();
                while (reader.Read() && reader.TokenType != JsonToken.EndArray) result.Add(serializer.Deserialize<T>(reader)!);
                Require(reader.TokenType == JsonToken.EndArray, "Incomplete traffic collection.");
                return result;
            }
        }
    }
}
