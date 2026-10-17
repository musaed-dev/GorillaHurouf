using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace GorillaHurouf
{
    /// <summary>
    /// Strings by key and language, for a mod that speaks Arabic and English. Make one with <c>Arabic.Translator("MyMod")</c>.
    /// <c>T</c> returns the text as written (not yet fixed for Arabic): pass it to <c>SetArabic</c> or <c>Notify</c>, which fix it.
    /// No Unity in this file, so it is tested outside the game.
    /// </summary>
    public sealed class Translator
    {
        readonly Func<string> language;
        readonly Action<string> warn;
        readonly Dictionary<string, Dictionary<string, string>> entries = new Dictionary<string, Dictionary<string, string>>();
        readonly HashSet<string> warned = new HashSet<string>();

        /// <summary>The name given to <c>Arabic.Translator</c>; it starts every message this object logs.</summary>
        public string Name { get; private set; }

        internal Translator(string name, Func<string> language, Action<string> warn)
        {
            Name = name;
            this.language = language;
            this.warn = warn;
        }

        /// <summary>Adds strings from JSON like <c>{ "welcome": { "en": "Welcome", "ar": "هلا" } }</c>. A key that is already known is replaced.
        /// Returns false (and logs why) if the text is not that shape; nothing is added then.</summary>
        public bool LoadJson(string json)
        {
            object root;
            try { root = Json.Parse(json); }
            catch (FormatException e) { warn(Name + ": the translations are not valid JSON: " + e.Message); return false; }
            var top = root as Dictionary<string, object>;
            if (top == null) { warn(Name + ": the translations must be a JSON object of keys"); return false; }

            var found = new Dictionary<string, Dictionary<string, string>>();
            foreach (KeyValuePair<string, object> kv in top)
            {
                var byLanguage = kv.Value as Dictionary<string, object>;
                if (byLanguage == null) { warn(Name + ": \"" + kv.Key + "\" must be an object such as { \"en\": \"...\", \"ar\": \"...\" }"); continue; }
                var texts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (KeyValuePair<string, object> l in byLanguage)
                {
                    string s = l.Value as string;
                    if (s != null) texts[l.Key] = s;
                }
                found[kv.Key] = texts;
            }
            foreach (KeyValuePair<string, Dictionary<string, string>> kv in found) entries[kv.Key] = kv.Value;
            return true;
        }

        /// <summary>Same as <see cref="LoadJson"/>, reading a UTF-8 file. Returns false (and logs why) if it cannot be read.</summary>
        public bool LoadFile(string path)
        {
            string json;
            try { json = File.ReadAllText(path, Encoding.UTF8); }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException || e is NotSupportedException)
            {
                warn(Name + ": could not read " + path + ": " + e.Message);
                return false;
            }
            return LoadJson(json);
        }

        /// <summary>The text for <paramref name="key"/> in the current language (<c>Arabic.Language</c>), else in English, else the key itself.
        /// With <paramref name="args"/> the text is a <c>string.Format</c> pattern: <c>T("hello", name)</c> for "Hello {0}". A missing key is logged once.</summary>
        public string T(string key, params object[] args)
        {
            if (key == null) return "";
            string text = null;
            Dictionary<string, string> byLanguage;
            if (entries.TryGetValue(key, out byLanguage))
            {
                string want = language();
                if (want == null || !byLanguage.TryGetValue(want, out text)) byLanguage.TryGetValue("en", out text);
            }
            if (text == null)
            {
                if (warned.Add(key)) warn(Name + ": no text for \"" + key + "\"");
                return key;
            }
            if (args == null || args.Length == 0) return text;
            try { return string.Format(text, args); }
            catch (FormatException) { return text; }   // a bad {} pattern in a translation file must not break the caller
        }
    }

    // Just enough JSON for translation files: objects become Dictionary<string, object>, strings stay strings,
    // arrays become List<object>, numbers and true/false/null are checked and dropped (null). Mono has no System.Web.Extensions.
    internal static class Json
    {
        const int MaxDepth = 64;

        public static object Parse(string s)
        {
            if (string.IsNullOrEmpty(s)) throw new FormatException("the text is empty");
            int i = 0;
            object v = Value(s, ref i, 0);
            Skip(s, ref i);
            if (i != s.Length) throw Err(s, i, "unexpected text after the end");
            return v;
        }

        static object Value(string s, ref int i, int depth)
        {
            if (depth > MaxDepth) throw Err(s, i, "nested too deeply");
            Skip(s, ref i);
            if (i >= s.Length) throw Err(s, i, "the text ends too early");
            char c = s[i];
            if (c == '{')
            {
                var d = new Dictionary<string, object>();
                i++;
                Skip(s, ref i);
                if (Peek(s, i) == '}') { i++; return d; }
                while (true)
                {
                    Skip(s, ref i);
                    if (Peek(s, i) != '"') throw Err(s, i, "expected a quoted key");
                    string key = Str(s, ref i);
                    Skip(s, ref i);
                    if (Peek(s, i) != ':') throw Err(s, i, "expected ':'");
                    i++;
                    d[key] = Value(s, ref i, depth + 1);
                    Skip(s, ref i);
                    char n = Peek(s, i);
                    i++;
                    if (n == '}') return d;
                    if (n != ',') throw Err(s, i - 1, "expected ',' or '}'");
                }
            }
            if (c == '[')
            {
                var a = new List<object>();
                i++;
                Skip(s, ref i);
                if (Peek(s, i) == ']') { i++; return a; }
                while (true)
                {
                    a.Add(Value(s, ref i, depth + 1));
                    Skip(s, ref i);
                    char n = Peek(s, i);
                    i++;
                    if (n == ']') return a;
                    if (n != ',') throw Err(s, i - 1, "expected ',' or ']'");
                }
            }
            if (c == '"') return Str(s, ref i);

            int start = i;
            while (i < s.Length && ",}] \t\r\n".IndexOf(s[i]) < 0) i++;
            string token = s.Substring(start, i - start);
            double ignored;
            if (token != "true" && token != "false" && token != "null" && !double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out ignored))
                throw Err(s, start, "unexpected '" + token + "'");
            return null;
        }

        static string Str(string s, ref int i)
        {
            var sb = new StringBuilder();
            i++;   // the opening quote
            while (i < s.Length)
            {
                char c = s[i++];
                if (c == '"') return sb.ToString();
                if (c != '\\') { sb.Append(c); continue; }
                if (i >= s.Length) break;
                char e = s[i++];
                switch (e)
                {
                    case '"': sb.Append('"'); break;
                    case '\\': sb.Append('\\'); break;
                    case '/': sb.Append('/'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'u':
                        int code;
                        if (i + 4 > s.Length || !int.TryParse(s.Substring(i, 4), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out code))
                            throw Err(s, i - 2, "bad \\u escape");
                        sb.Append((char)code);
                        i += 4;
                        break;
                    default: throw Err(s, i - 2, "bad escape '\\" + e + "'");
                }
            }
            throw Err(s, s.Length, "a string is not closed");
        }

        static char Peek(string s, int i) { return i < s.Length ? s[i] : '\0'; }

        static void Skip(string s, ref int i)
        {
            while (i < s.Length && (s[i] == ' ' || s[i] == '\t' || s[i] == '\r' || s[i] == '\n' || s[i] == '\uFEFF')) i++;
        }

        static FormatException Err(string s, int at, string what)
        {
            int line = 1;
            for (int k = 0; k < at && k < s.Length; k++) if (s[k] == '\n') line++;
            return new FormatException(what + " (line " + line + ")");
        }
    }
}
