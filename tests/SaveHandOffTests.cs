using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;

namespace PersistentCity.Tests;

public sealed class SaveHandOffTests
{
    private const int Wait = 30000;
    private static CityState City(string key = "source") => TestCity.Minimal(key, "0.40.4", 0, "resident");

    private sealed class Save
    {
        public readonly object Owner = new object();
        public readonly Dictionary<string, string> Data;
        public readonly SaveSession Session;
        public readonly SaveEpochBinding Binding;
        public readonly SaveHandOffWriter Writer = new SaveHandOffWriter("test save writer");
        public readonly ManualResetEventSlim Gate = new ManualResetEventSlim(true);
        public readonly List<string> Stops = new List<string>();

        public Save(CityState city, Dictionary<string, string>? data = null)
        {
            Data = data ?? new Dictionary<string, string>(StringComparer.Ordinal) { ["other:sentinel"] = "keep" };
            Session = SaveSession.Open(Data, city.PopulationKey);
            Binding = new SaveEpochBinding(Owner, Data, 0);
            Writer.BeforeWrite = () => Gate.Wait();
        }

        public (SaveEpochResult Result, PendingSave? Pending) Begin(CityState city, SaveCostRecorder? writerCost = null)
        {
            PendingSave? queued = null;
            var result = SaveEpochCallback.Run(Binding, Owner, Data, false, ticket =>
            {
                var begun = Session.BeginWrite(Data, city, Writer, null, writerCost, out var pending);
                if (!begun.Success) return SaveEpochCapture.Refused("Population save refused: " + begun.Error);
                queued = pending;
                return SaveEpochCapture.Queued(pending!);
            }, Stops.Add, _ => { });
            return (result, queued);
        }

        public string? Settle(PendingSave pending)
        {
            Assert.True(pending.Wait(Wait));
            var stored = Session.Settle(pending);
            string? refusal = stored.Success ? null : stored.Error;
            if (!Binding.Settle(pending, refusal) && refusal == null) refusal = Binding.LastRefusal;
            pending.Release();
            return refusal;
        }
    }

    [Fact]
    public void BackgroundPayloadIsByteIdenticalToTheSynchronousWriterAndReloads()
    {
        var city = RepresentativeFixture.Build(120).State;
        var save = new Save(city);
        SaveCodec.Write(city);
        string expected = SaveCodec.Write(city);
        var (result, pending) = save.Begin(city);
        Assert.Equal(SaveEpochResult.Queued, result);
        Assert.Same(pending!.PayloadToken, save.Data[SaveStore.StateKey]);
        Assert.Same(pending.MarkerToken, save.Data[SaveEpochStore.Key]);
        Assert.Null(save.Settle(pending));
        Assert.Equal(expected, save.Data[SaveStore.StateKey]);
        Assert.Equal(SaveEpochStore.Success(pending.Sequence, CityState.CurrentSchema, city.Minute, city.PopulationKey, save.Data), save.Data[SaveEpochStore.Key]);
        Assert.Null(SaveEpochStore.CheckForStart(save.Data, out _));
        var read = SaveStore.ReadFrom(save.Data, city.PopulationKey);
        Assert.Equal(SaveReadStatus.Loaded, read.Status);
        Assert.Equal(expected, SaveCodec.Write(read.State!));
        Assert.Equal("keep", save.Data["other:sentinel"]);
        Assert.False(save.Data.ContainsKey(AccountingStopStore.Key));
        Assert.Equal(1, save.Binding.Published);
        Assert.Empty(save.Stops);
    }

    [Fact]
    public void LaterLiveChangesNeverReachTheQueuedSnapshot()
    {
        var city = RepresentativeFixture.Build(120).State;
        var save = new Save(city);
        SaveCodec.Write(city);
        string expected = SaveCodec.Write(city);
        save.Gate.Reset();
        var (_, pending) = save.Begin(city);
        city.Minute += 90;
        city.Citizens[0].CompletedJourneys += 7;
        city.Citizens[0].Identity.Name = "Changed While Writing";
        city.Citizens.RemoveAt(city.Citizens.Count - 1);
        city.Places[0].Capacity += 3;
        Assert.False(pending!.Resolved);
        save.Gate.Set();
        Assert.Null(save.Settle(pending));
        Assert.Equal(expected, save.Data[SaveStore.StateKey]);
        Assert.NotEqual(Environment.CurrentManagedThreadId, save.Writer.ThreadId);
    }

    [Fact]
    public void BackgroundWorkHasNoUnityOrGameAssembly()
    {
        var names = typeof(PendingSave).Assembly.GetReferencedAssemblies().Select(a => a.Name ?? "").ToList();
        Assert.DoesNotContain(names, n => n.StartsWith("UnityEngine", StringComparison.Ordinal) || n.StartsWith("BigAmbitions", StringComparison.Ordinal)
            || n.StartsWith("Assembly-CSharp", StringComparison.Ordinal) || n.StartsWith("OdinSerializer", StringComparison.Ordinal) || n == "0Harmony");
        Assert.DoesNotContain(typeof(PersistentCity.Core.CityState).Assembly.GetReferencedAssemblies(), a => (a.Name ?? "").StartsWith("UnityEngine", StringComparison.Ordinal));
    }

    [Fact]
    public void EncodeFailureSurfacesTheSameRefusalAndKeepsThePreviousPayload()
    {
        var city = City();
        var save = new Save(city, new Dictionary<string, string> { [SaveStore.StateKey] = SaveCodec.Write(City()) });
        string previous = save.Data[SaveStore.StateKey];
        var sync = City();
        var syncSession = SaveSession.Open(new Dictionary<string, string>(), sync.PopulationKey);
        sync.Citizens[0].HomeId = "missing";
        string syncError = syncSession.WriteTo(new Dictionary<string, string>(), sync).Error;
        city.Citizens[0].HomeId = "missing";
        var (result, pending) = save.Begin(city);
        Assert.Equal(SaveEpochResult.Queued, result);
        Assert.True(pending!.Wait(Wait));
        Assert.False(pending.Succeeded);
        Assert.Equal("Population save refused: " + syncError, pending.Error);
        Assert.Equal(pending.Error, save.Settle(pending));
        Assert.Same(previous, save.Data[SaveStore.StateKey]);
        Assert.Equal(SaveEpochStore.Failed(pending.Sequence), save.Data[SaveEpochStore.Key]);
        Assert.Equal(AccountingStopStore.SaveEpochFailed, save.Data[AccountingStopStore.Key]);
        Assert.NotNull(SaveEpochStore.CheckForStart(save.Data, out _));
        Assert.Equal(1, save.Binding.Refused);
    }

    [Fact]
    public void ASecondSaveQueuesBehindTheFirstAndTheLatestWins()
    {
        var city = City();
        var save = new Save(city);
        save.Gate.Reset();
        var (_, first) = save.Begin(city);
        var refused = save.Session.BeginWrite(save.Data, city, save.Writer, null, null, out var none);
        Assert.False(refused.Success);
        Assert.Null(none);
        Assert.Same(first!.PayloadToken, save.Data[SaveStore.StateKey]);
        save.Gate.Set();
        Assert.Null(save.Settle(first));
        string firstPayload = save.Data[SaveStore.StateKey];
        city.Citizens[0].CompletedJourneys = 41;
        var (_, second) = save.Begin(city);
        Assert.Null(save.Settle(second!));
        Assert.NotEqual(firstPayload, save.Data[SaveStore.StateKey]);
        Assert.Equal(41, SaveStore.ReadFrom(save.Data, city.PopulationKey).State!.Citizens[0].CompletedJourneys);
        Assert.Equal(second!.Sequence, first.Sequence + 1);

        SaveHandOff.Expect(first, "temp");
        SaveHandOff.Expect(second, "temp");
        Assert.Null(SaveHandOff.Claim("other"));
        Assert.Same(second, SaveHandOff.Claim("temp"));
        Assert.Null(SaveHandOff.Claim("temp"));

        var firstFile = Odin(("other", "x"), (SaveStore.StateKey, first.PayloadToken), (SaveEpochStore.Key, first.MarkerToken));
        var spliced = new MemoryStream();
        Assert.Equal(SaveSpliceOutcome.Absent, SaveSplice.Copy(new MemoryStream(firstFile), spliced,
            new[] { second.PayloadToken, second.MarkerToken }, new[] { second.Payload, second.Marker }));
        Assert.Equal(firstFile, spliced.ToArray());
    }

    [Fact]
    public void CompressionWaitsForAnInFlightWriteAndProducesACompleteSave()
    {
        var city = RepresentativeFixture.Build(120).State;
        var save = new Save(city);
        save.Gate.Reset();
        var (_, pending) = save.Begin(city);
        string temp = TempFile(Odin(("before", "b"), (SaveStore.StateKey, pending!.PayloadToken), ("middle", "m"),
            (SaveEpochStore.Key, pending.MarkerToken), ("after", "a")));
        string output = temp + ".out";
        try
        {
            bool written = false;
            var compressing = new Thread(() => written = SaveHandOff.Compress(temp, output, pending)) { Name = "test compress" };
            compressing.Start();
            Assert.False(compressing.Join(200));
            save.Gate.Set();
            Assert.True(compressing.Join(Wait));
            Assert.True(written);
            Assert.Equal(SaveSpliceOutcome.Spliced, pending.Splice);
            Assert.True(pending.SpliceDone);
            var read = ReadOdin(Gunzip(output));
            Assert.Equal(pending.Payload, read[SaveStore.StateKey]);
            Assert.Equal(pending.Marker, read[SaveEpochStore.Key]);
            Assert.Equal(new[] { "before", SaveStore.StateKey, "middle", SaveEpochStore.Key, "after" }, read.Keys.ToArray());
            Assert.Null(SaveEpochStore.CheckForStart(read, out _));
            Assert.Equal(SaveReadStatus.Loaded, SaveStore.ReadFrom(read, city.PopulationKey).Status);
            Assert.Null(save.Settle(pending));
            Assert.Equal(read[SaveStore.StateKey], save.Data[SaveStore.StateKey]);
            Assert.Equal(read[SaveEpochStore.Key], save.Data[SaveEpochStore.Key]);
        }
        finally { File.Delete(temp); File.Delete(output); }
    }

    [Fact]
    public void AStuckWriterIsAbortedOnceAndTheFileAndLiveStateAgreeOnTheRefusal()
    {
        var city = City();
        var save = new Save(city, new Dictionary<string, string> { [SaveStore.StateKey] = SaveCodec.Write(City()) });
        string previous = save.Data[SaveStore.StateKey];
        save.Gate.Reset();
        var (_, pending) = save.Begin(city);
        string temp = TempFile(Odin((SaveStore.StateKey, pending!.PayloadToken), (SaveEpochStore.Key, pending.MarkerToken)));
        string output = temp + ".out";
        try
        {
            Assert.True(SaveHandOff.Compress(temp, output, pending, 150));
            Assert.True(pending.Aborted);
            save.Gate.Set();
            Thread.Sleep(100);
            Assert.False(pending.Succeeded);
            var file = ReadOdin(Gunzip(output));
            Assert.Equal(previous, file[SaveStore.StateKey]);
            Assert.Equal(SaveEpochStore.Failed(pending.Sequence), file[SaveEpochStore.Key]);
            Assert.NotNull(SaveEpochStore.CheckForStart(file, out _));
            Assert.NotNull(save.Settle(pending));
            Assert.Same(previous, save.Data[SaveStore.StateKey]);
            Assert.Equal(file[SaveEpochStore.Key], save.Data[SaveEpochStore.Key]);
        }
        finally { File.Delete(temp); File.Delete(output); }
    }

    [Fact]
    public void ShutdownDuringAnInFlightWriteNeverLeavesATokenInTheSave()
    {
        var city = City();
        var save = new Save(city);
        save.Gate.Reset();
        var (_, pending) = save.Begin(city);
        Assert.False(pending!.WaitOrAbort(100, "quit"));
        pending.Release();
        save.Gate.Set();
        Assert.False(save.Data.ContainsKey(SaveStore.StateKey));
        Assert.Equal(SaveEpochStore.Failed(pending.Sequence), save.Data[SaveEpochStore.Key]);
        Assert.Equal(AccountingStopStore.SaveEpochFailed, save.Data[AccountingStopStore.Key]);
        Assert.DoesNotContain(save.Data.Values, v => v.StartsWith(PendingSave.TokenPrefix, StringComparison.Ordinal));

        var flushed = new Save(city);
        var (_, done) = flushed.Begin(city);
        Assert.True(done!.WaitOrAbort(Wait, "quit"));
        done.Release();
        Assert.Equal(SaveReadStatus.Loaded, SaveStore.ReadFrom(flushed.Data, city.PopulationKey).Status);
        Assert.Null(SaveEpochStore.CheckForStart(flushed.Data, out _));
    }

    [Fact]
    public void APublicationRefusalAbandonsTheTokenBeforeTheNativeSaveSeesIt()
    {
        var city = City();
        var save = new Save(city, new Dictionary<string, string> { [SaveStore.StateKey] = SaveCodec.Write(City()) });
        string previous = save.Data[SaveStore.StateKey];
        PendingSave? queued = null;
        var result = SaveEpochCallback.Run(save.Binding, save.Owner, save.Data, false, ticket =>
        {
            save.Session.BeginWrite(save.Data, city, save.Writer, null, null, out queued);
            save.Binding.Detach(null);
            return SaveEpochCapture.Queued(queued!);
        }, save.Stops.Add, _ => { });
        Assert.Equal(SaveEpochResult.Refused, result);
        Assert.Same(previous, save.Data[SaveStore.StateKey]);
        Assert.True(queued!.Aborted);
        Assert.DoesNotContain(save.Data.Values, v => v.StartsWith(PendingSave.TokenPrefix, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(23)]
    public void OdinSpliceFindsWideAndNarrowTokensAcrossChunkBoundaries(int seed)
    {
        var random = new Random(seed);
        string payload = "{\"n\":\"\u00e9\u0416\ud83d\ude00\"," + new string('q', 9000 + random.Next(4000)) + "}";
        string marker = "v1\nsequence=3";
        const string a = PendingSave.TokenPrefix + "state-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        const string b = PendingSave.TokenPrefix + "epoch-bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
        var input = new MemoryStream();
        var expected = new MemoryStream();
        void Both(byte[] bytes) { input.Write(bytes); expected.Write(bytes); }
        Both(Filler(random, 4000 + random.Next(9000)));
        input.Write(OdinString(a, true)); expected.Write(OdinString(payload, true));
        Both(Filler(random, random.Next(9000)));
        input.Write(OdinString(b, false)); expected.Write(OdinString(marker, true));
        Both(Filler(random, random.Next(9000)));
        var output = new MemoryStream();
        Assert.Equal(SaveSpliceOutcome.Spliced, SaveSplice.Copy(new MemoryStream(input.ToArray()), output, new[] { a, b }, new[] { payload, marker }, 1));
        Assert.Equal(expected.ToArray(), output.ToArray());
    }

    [Fact]
    public void JsonSpliceEscapesValuesSoTheNativeReaderRoundTripsThem()
    {
        const string a = PendingSave.TokenPrefix + "state-0123456789abcdef0123456789abcdef";
        const string b = PendingSave.TokenPrefix + "epoch-0123456789abcdef0123456789abcdef";
        string payload = "{\"q\":\"\\\"\",\"s\":\"\\\\\",\"t\":\"\t\r\n\b\f\u0001\u2028\",\"n\":\"\u00e9\u0416\ud83d\ude00\"," + new string('z', 200000) + "}";
        string marker = "v1\nsequence=9\nschema=1";
        var data = new Dictionary<string, string> { ["x"] = "y", [SaveStore.StateKey] = a, [SaveEpochStore.Key] = b };
        var json = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new { day = 3, modData = data }));
        var output = new MemoryStream();
        Assert.Equal(SaveSpliceOutcome.Spliced, SaveSplice.Copy(new MemoryStream(json), output, new[] { a, b }, new[] { payload, marker }));
        var read = JsonConvert.DeserializeAnonymousType(Encoding.UTF8.GetString(output.ToArray()), new { day = 0, modData = new Dictionary<string, string>() })!;
        Assert.Equal(3, read.day);
        Assert.Equal(payload, read.modData[SaveStore.StateKey]);
        Assert.Equal(marker, read.modData[SaveEpochStore.Key]);
        Assert.Equal("y", read.modData["x"]);
    }

    [Fact]
    public void AbsentPartialAndDuplicateTokensAreReportedNotGuessed()
    {
        const string a = PendingSave.TokenPrefix + "state-11111111111111111111111111111111";
        const string b = PendingSave.TokenPrefix + "epoch-11111111111111111111111111111111";
        var plain = Odin(("k", "v"));
        var copy = new MemoryStream();
        Assert.Equal(SaveSpliceOutcome.Absent, SaveSplice.Copy(new MemoryStream(plain), copy, new[] { a, b }, new[] { "p", "m" }));
        Assert.Equal(plain, copy.ToArray());
        Assert.Equal(SaveSpliceOutcome.Partial, SaveSplice.Copy(new MemoryStream(Odin((SaveStore.StateKey, a))), new MemoryStream(), new[] { a, b }, new[] { "p", "m" }));
        Assert.Throws<InvalidDataException>(() => SaveSplice.Copy(new MemoryStream(Odin(("one", a), ("two", a))), new MemoryStream(), new[] { a, b }, new[] { "p", "m" }));
    }

    [Fact]
    public void LengthMarkerMatchesTheDictionaryMarker()
    {
        var data = new Dictionary<string, string> { [SaveStore.StateKey] = new string('p', 12345) };
        Assert.Equal(SaveEpochStore.Success(4, 1, 1440.25, "population", data), SaveEpochStore.Success(4, 1, 1440.25, "population", 12345));
    }

    [Fact]
    public void WriterTimingFillsOnlyTheWriterRecorder()
    {
        var city = RepresentativeFixture.Build(60).State;
        var save = new Save(city);
        var writerCost = new SaveCostRecorder();
        var (_, pending) = save.Begin(city, writerCost);
        Assert.Null(save.Settle(pending!));
        Assert.True(writerCost.Success);
        Assert.True(writerCost.Milliseconds(SaveCostPhase.Encode) > 0);
        Assert.True(writerCost.Covered(SaveCostPhase.Validate));
        Assert.Equal(save.Data[SaveStore.StateKey].Length, writerCost.PayloadCharacters);
        Assert.True(writerCost.StreamingRecords > 0 && writerCost.SemanticRecords > 0);
        Assert.Same(writerCost, pending!.Cost);
    }

    internal static byte[] OdinString(string value, bool wide)
    {
        var bytes = new byte[5 + value.Length * (wide ? 2 : 1)];
        bytes[0] = wide ? (byte)1 : (byte)0;
        BitConverter.GetBytes(value.Length).CopyTo(bytes, 1);
        for (int i = 0; i < value.Length; i++)
            if (wide) BitConverter.GetBytes(value[i]).CopyTo(bytes, 5 + 2 * i);
            else bytes[5 + i] = (byte)value[i];
        return bytes;
    }

    internal static byte[] Odin(params (string Key, string Value)[] entries)
    {
        var stream = new MemoryStream();
        stream.WriteByte(0x2f);
        stream.Write(BitConverter.GetBytes(entries.Length));
        foreach (var (key, value) in entries)
        {
            stream.WriteByte(0x28); stream.Write(OdinString(key, true));
            stream.WriteByte(0x28); stream.Write(OdinString(value, true));
        }
        stream.WriteByte(0x05);
        return stream.ToArray();
    }

    internal static Dictionary<string, string> ReadOdin(byte[] bytes)
    {
        var reader = new BinaryReader(new MemoryStream(bytes));
        Assert.Equal(0x2f, reader.ReadByte());
        int count = reader.ReadInt32();
        var result = new Dictionary<string, string>();
        string Next()
        {
            Assert.Equal(0x28, reader.ReadByte());
            byte wide = reader.ReadByte();
            int length = reader.ReadInt32();
            if (wide == 0) return new string(reader.ReadBytes(length).Select(c => (char)c).ToArray());
            var chars = new char[length];
            for (int i = 0; i < length; i++) chars[i] = (char)reader.ReadUInt16();
            return new string(chars);
        }
        for (int i = 0; i < count; i++) { string key = Next(); result.Add(key, Next()); }
        Assert.Equal(0x05, reader.ReadByte());
        Assert.Equal(bytes.Length, reader.BaseStream.Position);
        return result;
    }

    private static byte[] Filler(Random random, int count)
    {
        var bytes = new byte[count];
        random.NextBytes(bytes);
        return bytes;
    }

    private static string TempFile(byte[] bytes)
    {
        string path = Path.Combine(Path.GetTempPath(), "pc-handoff-" + Guid.NewGuid().ToString("N"));
        File.WriteAllBytes(path, bytes);
        return path;
    }

    private static byte[] Gunzip(string path)
    {
        using var input = new GZipStream(File.OpenRead(path), CompressionMode.Decompress);
        using var output = new MemoryStream();
        input.CopyTo(output);
        return output.ToArray();
    }
}
