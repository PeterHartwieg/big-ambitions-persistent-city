using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace PersistentCity.Core
{
    internal sealed class TextDigest
    {
        [ThreadStatic] private static TextDigest? current;
        private readonly SHA256 sha = SHA256.Create();
        private readonly byte[] hash = new byte[32];
        private char[] chars = new char[256];
        private byte[] bytes = new byte[1024];
        private int length;

        internal static TextDigest Begin()
        {
            var digest = current ??= new TextDigest();
            digest.length = 0;
            return digest;
        }

        internal TextDigest Append(string? text)
        {
            if (string.IsNullOrEmpty(text)) return this;
            Reserve(text!.Length);
            text.CopyTo(0, chars, length, text.Length);
            length += text.Length;
            return this;
        }

        internal TextDigest Append(char value)
        {
            Reserve(1);
            chars[length++] = value;
            return this;
        }

        internal TextDigest Append(int value)
        {
            int written;
            while (!value.TryFormat(chars.AsSpan(length), out written, default, CultureInfo.InvariantCulture)) Reserve(chars.Length);
            length += written;
            return this;
        }

        internal TextDigest AppendRoundTrip(double value)
        {
            int written;
            while (!value.TryFormat(chars.AsSpan(length), out written, "R", CultureInfo.InvariantCulture)) Reserve(chars.Length);
            length += written;
            return this;
        }

        internal string Base64() => Convert.ToBase64String(Hash());

        internal byte[] Hash()
        {
            int needed = Encoding.UTF8.GetMaxByteCount(length);
            if (bytes.Length < needed) bytes = new byte[Math.Max(needed, bytes.Length * 2)];
            int count = Encoding.UTF8.GetBytes(chars, 0, length, bytes, 0);
            if (!sha.TryComputeHash(new ReadOnlySpan<byte>(bytes, 0, count), hash, out int written) || written != hash.Length)
                throw new InvalidOperationException("SHA-256 digest failed.");
            return hash;
        }

        private void Reserve(int extra)
        {
            if (length + extra <= chars.Length) return;
            Array.Resize(ref chars, Math.Max(length + extra, chars.Length * 2));
        }
    }
}
