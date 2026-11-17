using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Aruun.Core.Content
{
    /// <summary>
    /// A small JSON reader with no dependencies, so the rules library works the same inside
    /// Unity and in plain .NET. Objects become Dictionary&lt;string, object&gt;, arrays become
    /// List&lt;object&gt;, numbers become double, plus string, bool and null.
    /// </summary>
    public static class MiniJson
    {
        public static object Parse(string text)
        {
            int i = 0;
            object value = ReadValue(text, ref i);
            SkipSpace(text, ref i);
            if (i != text.Length) throw Error(text, i, "unexpected text after the end");
            return value;
        }

        public static Dictionary<string, object> ParseObject(string text)
        {
            var d = Parse(text) as Dictionary<string, object>;
            if (d == null) throw new FormatException("JSON: expected an object at the top level");
            return d;
        }

        // Helpers for reading values out of parsed objects.
        public static string Str(Dictionary<string, object> o, string key, string fallback = "")
        {
            object v; return o.TryGetValue(key, out v) && v is string ? (string)v : fallback;
        }

        public static int Int(Dictionary<string, object> o, string key, int fallback = 0)
        {
            object v; return o.TryGetValue(key, out v) && v is double ? (int)(double)v : fallback;
        }

        public static double Num(Dictionary<string, object> o, string key, double fallback = 0)
        {
            object v; return o.TryGetValue(key, out v) && v is double ? (double)v : fallback;
        }

        public static bool Bool(Dictionary<string, object> o, string key, bool fallback = false)
        {
            object v; return o.TryGetValue(key, out v) && v is bool ? (bool)v : fallback;
        }

        public static List<object> List(Dictionary<string, object> o, string key)
        {
            object v; return o.TryGetValue(key, out v) && v is List<object> ? (List<object>)v : new List<object>();
        }

        /// <summary>A sub-object, or an empty one when it is missing.</summary>
        public static Dictionary<string, object> Obj(Dictionary<string, object> o, string key)
        {
            object v; return o != null && o.TryGetValue(key, out v) && v is Dictionary<string, object> ? (Dictionary<string, object>)v : new Dictionary<string, object>();
        }

        static object ReadValue(string s, ref int i)
        {
            SkipSpace(s, ref i);
            if (i >= s.Length) throw Error(s, i, "unexpected end");
            char c = s[i];
            if (c == '{') return ReadObject(s, ref i);
            if (c == '[') return ReadArray(s, ref i);
            if (c == '"') return ReadString(s, ref i);
            if (Match(s, ref i, "true")) return true;
            if (Match(s, ref i, "false")) return false;
            if (Match(s, ref i, "null")) return null;
            return ReadNumber(s, ref i);
        }

        static Dictionary<string, object> ReadObject(string s, ref int i)
        {
            var result = new Dictionary<string, object>();
            i++;
            SkipSpace(s, ref i);
            if (i < s.Length && s[i] == '}') { i++; return result; }
            while (true)
            {
                SkipSpace(s, ref i);
                if (i >= s.Length || s[i] != '"') throw Error(s, i, "expected a key");
                string key = ReadString(s, ref i);
                SkipSpace(s, ref i);
                if (i >= s.Length || s[i] != ':') throw Error(s, i, "expected ':'");
                i++;
                result[key] = ReadValue(s, ref i);
                SkipSpace(s, ref i);
                if (i < s.Length && s[i] == ',') { i++; continue; }
                if (i < s.Length && s[i] == '}') { i++; return result; }
                throw Error(s, i, "expected ',' or '}'");
            }
        }

        static List<object> ReadArray(string s, ref int i)
        {
            var result = new List<object>();
            i++;
            SkipSpace(s, ref i);
            if (i < s.Length && s[i] == ']') { i++; return result; }
            while (true)
            {
                result.Add(ReadValue(s, ref i));
                SkipSpace(s, ref i);
                if (i < s.Length && s[i] == ',') { i++; continue; }
                if (i < s.Length && s[i] == ']') { i++; return result; }
                throw Error(s, i, "expected ',' or ']'");
            }
        }

        static string ReadString(string s, ref int i)
        {
            var sb = new StringBuilder();
            i++;
            while (i < s.Length)
            {
                char c = s[i++];
                if (c == '"') return sb.ToString();
                if (c != '\\') { sb.Append(c); continue; }
                if (i >= s.Length) break;
                char e = s[i++];
                switch (e)
                {
                    case 'n': sb.Append('\n'); break;
                    case 't': sb.Append('\t'); break;
                    case 'r': sb.Append('\r'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'u':
                        if (i + 4 > s.Length) throw Error(s, i, "bad \\u escape");
                        sb.Append((char)int.Parse(s.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                        i += 4;
                        break;
                    default: sb.Append(e); break;
                }
            }
            throw Error(s, i, "string never closed");
        }

        static double ReadNumber(string s, ref int i)
        {
            int start = i;
            while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
            double v;
            if (start == i || !double.TryParse(s.Substring(start, i - start), NumberStyles.Float, CultureInfo.InvariantCulture, out v))
                throw Error(s, start, "expected a value");
            return v;
        }

        static bool Match(string s, ref int i, string word)
        {
            if (string.CompareOrdinal(s, i, word, 0, word.Length) != 0) return false;
            i += word.Length;
            return true;
        }

        static void SkipSpace(string s, ref int i)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
        }

        static FormatException Error(string s, int i, string what)
        {
            int line = 1;
            for (int k = 0; k < i && k < s.Length; k++) if (s[k] == '\n') line++;
            return new FormatException("JSON line " + line + ": " + what);
        }
    }
}
