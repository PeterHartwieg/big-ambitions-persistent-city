using System;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace PersistentCity.Tools.CompactFormat
{
    public abstract class Candidate
    {
        public static readonly Candidate[] All = { new Writer25(), new Columnar("B", ColumnarOptions.B), new Columnar("C", ColumnarOptions.C), new Deflated() };

        public abstract string Id { get; }
        public abstract string Description { get; }
        public abstract string Encode(object? tree, ColumnarStats? stats = null);
        public abstract object? Decode(string payload);

        public static Candidate Get(string id) => Array.Find(All, c => c.Id == id) ?? throw new ArgumentException("Unknown candidate " + id);

        private sealed class Writer25 : Candidate
        {
            public override string Id => "A";
            public override string Description => "writer-25 JSON as written today";
            public override string Encode(object? tree, ColumnarStats? stats = null) => WireTree.Write(tree, 1 << 16);
            public override object? Decode(string payload) => WireTree.Parse(payload);
        }

        private sealed class Columnar : Candidate
        {
            private readonly ColumnarOptions options;
            public Columnar(string id, ColumnarOptions options) { Id = id; this.options = options; }
            public override string Id { get; }
            public override string Description => options.RouteTable
                ? "columnar compact JSON plus shared route table and topology-range road legs"
                : "columnar compact JSON: per-field columns, interned ordinal string tables, exact number text";
            public override string Encode(object? tree, ColumnarStats? stats = null) => ColumnarCodec.Encode(tree, options, stats, 1 << 16);
            public override object? Decode(string payload) => ColumnarCodec.Decode(payload, options);
        }

        private sealed class Deflated : Candidate
        {
            public const string Prefix = "pcd1:";
            public const CompressionLevel Level = CompressionLevel.Optimal;
            public override string Id => "D";
            public override string Description => "C plus deflate (Optimal) and base64";
            public override string Encode(object? tree, ColumnarStats? stats = null)
            {
                byte[] utf8 = Encoding.UTF8.GetBytes(ColumnarCodec.Encode(tree, ColumnarOptions.C, stats, 1 << 16));
                using var buffer = new MemoryStream(utf8.Length / 4 + 64);
                using (var deflate = new DeflateStream(buffer, Level, leaveOpen: true)) deflate.Write(utf8, 0, utf8.Length);
                return Prefix + Convert.ToBase64String(buffer.GetBuffer(), 0, (int)buffer.Length);
            }
            public override object? Decode(string payload)
            {
                if (!payload.StartsWith(Prefix, StringComparison.Ordinal)) throw new FormatException("Not a D payload.");
                byte[] packed = Convert.FromBase64String(payload.Substring(Prefix.Length));
                using var input = new DeflateStream(new MemoryStream(packed), CompressionMode.Decompress);
                using var output = new MemoryStream(packed.Length * 4);
                input.CopyTo(output);
                var text = new UTF8Encoding(false, true).GetString(output.GetBuffer(), 0, (int)output.Length);
                return ColumnarCodec.Decode(text, ColumnarOptions.C);
            }
        }
    }
}
