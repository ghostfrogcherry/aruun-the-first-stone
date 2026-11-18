using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Aruun.Core.Combat;

namespace Aruun.Core.Net
{
    /// <summary>
    /// The co-op wire format: each message is a frame of UTF-8 text, a 4-byte big-endian length
    /// then the text. The first line names the kind of message; the rest is its body. Text keeps
    /// the protocol easy to read in a log and to test; a demo with 2-4 players sends little.
    /// </summary>
    public static class Frames
    {
        public const int MaxFrame = 8 * 1024 * 1024;

        public static void Write(Stream s, string kind, string body)
        {
            byte[] data = Encoding.UTF8.GetBytes(body.Length > 0 ? kind + "\n" + body : kind);
            var head = new byte[4];
            head[0] = (byte)(data.Length >> 24); head[1] = (byte)(data.Length >> 16);
            head[2] = (byte)(data.Length >> 8); head[3] = (byte)data.Length;
            s.Write(head, 0, 4);
            s.Write(data, 0, data.Length);
        }

        /// <summary>Reads one frame; null when the stream has ended.</summary>
        public static Frame Read(Stream s)
        {
            var head = new byte[4];
            if (!Fill(s, head, 4)) return null;
            int len = head[0] << 24 | head[1] << 16 | head[2] << 8 | head[3];
            if (len < 0 || len > MaxFrame) throw new IOException("frame too large: " + len);
            var data = new byte[len];
            if (!Fill(s, data, len)) return null;
            string text = Encoding.UTF8.GetString(data);
            int nl = text.IndexOf('\n');
            return nl < 0 ? new Frame(text, "") : new Frame(text.Substring(0, nl), text.Substring(nl + 1));
        }

        static bool Fill(Stream s, byte[] buf, int n)
        {
            int got = 0;
            while (got < n)
            {
                int r = s.Read(buf, got, n - got);
                if (r <= 0) return false;
                got += r;
            }
            return true;
        }
    }

    public sealed class Frame
    {
        public readonly string Kind, Body;
        public Frame(string kind, string body) { Kind = kind; Body = body; }
        public string[] Lines { get { return Body.Length == 0 ? new string[0] : Body.Split('\n'); } }
    }

    /// <summary>Numbers and fields in frames: invariant culture, tab-separated.</summary>
    public static class Wire
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static string N(double v) { return v.ToString("0.###", Inv); }
        public static string V(Vec3 v) { return N(v.X) + "\t" + N(v.Y) + "\t" + N(v.Z); }
        public static double D(string s) { return double.Parse(s, NumberStyles.Float, Inv); }
        public static int I(string s) { return int.Parse(s, NumberStyles.Integer, Inv); }
        public static Vec3 V(string[] f, int at) { return new Vec3(D(f[at]), D(f[at + 1]), D(f[at + 2])); }

        public static string Join(params object[] fields)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < fields.Length; i++)
            {
                if (i > 0) sb.Append('\t');
                object f = fields[i];
                if (f is double) sb.Append(N((double)f));
                else if (f is Vec3) sb.Append(V((Vec3)f));
                else if (f is bool) sb.Append((bool)f ? "1" : "0");
                else if (f is int) sb.Append(((int)f).ToString(Inv));
                else sb.Append(Clean(Convert.ToString(f, Inv)));
            }
            return sb.ToString();
        }

        /// <summary>Free text in a field: no tabs or newlines.</summary>
        public static string Clean(string s) { return (s ?? "").Replace('\t', ' ').Replace('\n', ' ').Replace('\r', ' '); }

        /// <summary>Several lines of text as one field ("\n" escaped).</summary>
        public static string Pack(string text) { return (text ?? "").Replace("\\", "\\\\").Replace("\n", "\\n").Replace("\t", "\\t"); }

        public static string Unpack(string s)
        {
            var sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '\\' && i + 1 < s.Length)
                {
                    char d = s[++i];
                    sb.Append(d == 'n' ? '\n' : d == 't' ? '\t' : d);
                }
                else sb.Append(c);
            }
            return sb.ToString();
        }

        public static string[] Split(string line) { return line.Split('\t'); }

        public static string Lines(IEnumerable<string> lines) { return string.Join("\n", lines); }
    }
}
