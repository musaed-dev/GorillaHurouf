using System;
using System.Collections.Generic;
using System.Text;

namespace GorillaHurouf
{
    /// <summary>Which way a line runs. Auto follows the first strong letter, like a browser.</summary>
    public enum TextDirection { Auto, Rtl, Ltr }

    /// <summary>What to do with the digits 0-9 in text that contains Arabic.</summary>
    public enum DigitStyle { Keep, ArabicIndic, Western }

    public sealed class ShapeOptions
    {
        public TextDirection Direction = TextDirection.Auto;
        public DigitStyle Digits = DigitStyle.Keep;
        /// <summary>false removes the small vowel marks (fatha, kasra, shadda...).</summary>
        public bool KeepDiacritics = true;
        /// <summary>true keeps TextMeshPro tags such as &lt;color=red&gt; in place around the right letters.</summary>
        public bool RichText = true;
    }

    /// <summary>
    /// Turns logical Arabic text into text a left-to-right renderer draws correctly: letters get their
    /// joined forms (Unicode Arabic Presentation Forms-B), lam-alef becomes one glyph, and each line is
    /// put in display order. No Unity or game code, so it can be tested outside the game.
    /// Text with no Arabic letters comes back untouched, and so does text that was already shaped.
    /// </summary>
    public static class ArabicShaper
    {
        enum Cls : byte { L, R, AL, EN, AN, ES, ET, CS, WS, ON }

        sealed class Tag { public string Open, Close, Name; }

        sealed class Unit
        {
            public string Text, Marks = "";
            public Cls Cls;
            public int Join;               // 0 none, 1 joins previous only, 2 joins both, 3 joiner (ZWJ)
            public bool Atomic, Hidden;
            public char B, Iso, Fin, Ini, Med;
            public int Level;
            public Tag[] Tags;
        }

        // base letter, isolated, final, initial, medial (0 = the form does not exist)
        static readonly int[] LetterData =
        {
            0x0621, 0xFE80, 0,      0,      0,
            0x0622, 0xFE81, 0xFE82, 0,      0,
            0x0623, 0xFE83, 0xFE84, 0,      0,
            0x0624, 0xFE85, 0xFE86, 0,      0,
            0x0625, 0xFE87, 0xFE88, 0,      0,
            0x0626, 0xFE89, 0xFE8A, 0xFE8B, 0xFE8C,
            0x0627, 0xFE8D, 0xFE8E, 0,      0,
            0x0628, 0xFE8F, 0xFE90, 0xFE91, 0xFE92,
            0x0629, 0xFE93, 0xFE94, 0,      0,
            0x062A, 0xFE95, 0xFE96, 0xFE97, 0xFE98,
            0x062B, 0xFE99, 0xFE9A, 0xFE9B, 0xFE9C,
            0x062C, 0xFE9D, 0xFE9E, 0xFE9F, 0xFEA0,
            0x062D, 0xFEA1, 0xFEA2, 0xFEA3, 0xFEA4,
            0x062E, 0xFEA5, 0xFEA6, 0xFEA7, 0xFEA8,
            0x062F, 0xFEA9, 0xFEAA, 0,      0,
            0x0630, 0xFEAB, 0xFEAC, 0,      0,
            0x0631, 0xFEAD, 0xFEAE, 0,      0,
            0x0632, 0xFEAF, 0xFEB0, 0,      0,
            0x0633, 0xFEB1, 0xFEB2, 0xFEB3, 0xFEB4,
            0x0634, 0xFEB5, 0xFEB6, 0xFEB7, 0xFEB8,
            0x0635, 0xFEB9, 0xFEBA, 0xFEBB, 0xFEBC,
            0x0636, 0xFEBD, 0xFEBE, 0xFEBF, 0xFEC0,
            0x0637, 0xFEC1, 0xFEC2, 0xFEC3, 0xFEC4,
            0x0638, 0xFEC5, 0xFEC6, 0xFEC7, 0xFEC8,
            0x0639, 0xFEC9, 0xFECA, 0xFECB, 0xFECC,
            0x063A, 0xFECD, 0xFECE, 0xFECF, 0xFED0,
            0x0640, 0x0640, 0x0640, 0x0640, 0x0640,   // tatweel
            0x0641, 0xFED1, 0xFED2, 0xFED3, 0xFED4,
            0x0642, 0xFED5, 0xFED6, 0xFED7, 0xFED8,
            0x0643, 0xFED9, 0xFEDA, 0xFEDB, 0xFEDC,
            0x0644, 0xFEDD, 0xFEDE, 0xFEDF, 0xFEE0,
            0x0645, 0xFEE1, 0xFEE2, 0xFEE3, 0xFEE4,
            0x0646, 0xFEE5, 0xFEE6, 0xFEE7, 0xFEE8,
            0x0647, 0xFEE9, 0xFEEA, 0xFEEB, 0xFEEC,
            0x0648, 0xFEED, 0xFEEE, 0,      0,
            0x0649, 0xFEEF, 0xFEF0, 0,      0,
            0x064A, 0xFEF1, 0xFEF2, 0xFEF3, 0xFEF4,
        };

        // lam + alef ligatures: alef variant, isolated, final
        static readonly int[] LigatureData =
        {
            0x0622, 0xFEF5, 0xFEF6,
            0x0623, 0xFEF7, 0xFEF8,
            0x0625, 0xFEF9, 0xFEFA,
            0x0627, 0xFEFB, 0xFEFC,
        };

        static readonly Dictionary<char, char[]> Forms = new Dictionary<char, char[]>();
        static readonly Dictionary<char, char[]> Ligatures = new Dictionary<char, char[]>();
        static readonly Dictionary<char, char> Mirrors = new Dictionary<char, char>
        {
            { '(', ')' }, { ')', '(' }, { '[', ']' }, { ']', '[' }, { '{', '}' }, { '}', '{' },
            { '<', '>' }, { '>', '<' }, { '«', '»' }, { '»', '«' },
            { '‹', '›' }, { '›', '‹' },
        };

        // TextMeshPro tags that have a closing tag (read from TMP's own MarkupTag list: each has a SLASH_ entry).
        static readonly HashSet<string> PairedTags = new HashSet<string>
        {
            "a", "action", "align", "allcaps", "b", "cspace", "color", "font", "font-weight", "frac", "gradient", "indent", "i",
            "liga", "line-height", "line-indent", "link", "lowercase", "margin", "mark", "material", "mspace", "nobr", "noparse",
            "rotate", "scale", "size", "smallcaps", "strikethrough", "s", "style", "sub", "sup", "table", "td", "th", "tr",
            "underline", "u", "uppercase", "voffset", "width",
        };
        // Tags with no closing tag. They stay where they are. <alpha> is handled apart: it stays on until changed,
        // so it is treated as wrapping, and "closed" by setting it back to <alpha=#FF>.
        static readonly HashSet<string> AtomicTags = new HashSet<string>
        {
            "sprite", "space", "pos", "nbsp", "zwj", "zwsp", "shy", "cr", "page", "margin-left", "margin-right",
        };

        static ArabicShaper()
        {
            for (int i = 0; i < LetterData.Length; i += 5)
                Forms[(char)LetterData[i]] = new[] { (char)LetterData[i + 1], (char)LetterData[i + 2], (char)LetterData[i + 3], (char)LetterData[i + 4] };
            for (int i = 0; i < LigatureData.Length; i += 3)
                Ligatures[(char)LigatureData[i]] = new[] { (char)LigatureData[i + 1], (char)LigatureData[i + 2] };
        }

        // For the self-check: the table, so every form can be compared with Unicode's own decomposition.
        internal static IEnumerable<KeyValuePair<char, char[]>> FormTable { get { return Forms; } }
        internal static IEnumerable<KeyValuePair<char, char[]>> LigatureTable { get { return Ligatures; } }

        /// <summary>True if the text has any Arabic-script letters, shaped or not.</summary>
        public static bool ContainsArabic(string text)
        {
            if (text == null) return false;
            for (int i = 0; i < text.Length; i++)
                if (IsArabicLetter(text[i]) || IsPresentationForm(text[i])) return true;
            return false;
        }

        /// <summary>True if the text has Arabic letters that still need shaping (already shaped text is false).</summary>
        public static bool NeedsShaping(string text)
        {
            if (text == null) return false;
            for (int i = 0; i < text.Length; i++)
                if (IsArabicLetter(text[i])) return true;
            return false;
        }

        /// <summary>Shapes and reorders text for a left-to-right renderer. Safe to call twice on the same text.</summary>
        public static string Shape(string text, ShapeOptions options = null)
        {
            if (!NeedsShaping(text)) return text;
            ShapeOptions o = options ?? new ShapeOptions();
            if (o.RichText) text = text.Replace("<br>", "\n").Replace("<BR>", "\n");

            var sb = new StringBuilder(text.Length + 16);
            var stack = new List<Tag>();   // tags still open, carried from line to line
            int start = 0;
            for (int i = 0; i <= text.Length; i++)
            {
                if (i < text.Length && text[i] != '\n') continue;
                int len = i - start;
                bool cr = len > 0 && text[start + len - 1] == '\r';
                if (cr) len--;
                ShapeLine(text.Substring(start, len), o, stack, sb, i == text.Length);
                if (cr) sb.Append('\r');
                if (i < text.Length) sb.Append('\n');
                start = i + 1;
            }
            return sb.ToString();
        }

        /// <summary>
        /// Breaks text into lines no wider than <paramref name="maxWidth"/> and shapes them. A candidate line is measured after shaping,
        /// so joined letters and mixed English count the way they will be drawn; tags carry over from line to line.
        /// A word wider than the limit stays whole on its own line. <paramref name="measure"/> returns the width of an already shaped line.
        /// </summary>
        public static string Wrap(string text, Func<string, float> measure, float maxWidth, ShapeOptions options = null)
        {
            if (string.IsNullOrEmpty(text)) return text;
            ShapeOptions o = options ?? new ShapeOptions();
            if (o.RichText) text = text.Replace("<br>", "\n").Replace("<BR>", "\n");

            var lines = new List<string>();
            foreach (string paragraph in text.Replace("\r\n", "\n").Split('\n'))
            {
                string line = "";
                foreach (string word in paragraph.Split(' '))
                {
                    string candidate = line.Length == 0 ? word : line + " " + word;
                    if (line.Length > 0 && measure(Shape(candidate, o)) > maxWidth) { lines.Add(line); line = word; }
                    else line = candidate;
                }
                lines.Add(line);
            }
            return Shape(string.Join("\n", lines.ToArray()), o);
        }

        static void ShapeLine(string line, ShapeOptions o, List<Tag> stack, StringBuilder sb, bool last)
        {
            var startState = new List<Tag>(stack);
            if (!NeedsShaping(line))
            {
                if (o.RichText && line.IndexOf('<') >= 0) Parse(line, o, stack);   // keep the open-tag stack right
                sb.Append(line);
                return;
            }

            List<Unit> units = Parse(line, o, stack);
            MergeLigatures(units);
            Join(units);

            int baseLevel = o.Direction == TextDirection.Rtl ? 1 : 0;
            if (o.Direction == TextDirection.Auto)
                foreach (Unit u in units)
                {
                    if (u.Cls == Cls.L) break;
                    if (u.Cls == Cls.R || u.Cls == Cls.AL) { baseLevel = 1; break; }
                }

            Resolve(units, baseLevel);
            Reorder(units);
            foreach (Unit u in units)
            {
                char m;
                if ((u.Level & 1) == 1 && !u.Atomic && u.Text.Length == 1 && Mirrors.TryGetValue(u.Text[0], out m)) u.Text = m.ToString();
            }
            Emit(sb, units, startState, stack.ToArray(), last);
        }

        // ---- reading the line into units (one unit = a letter plus its marks, or one tag) ----

        static List<Unit> Parse(string s, ShapeOptions o, List<Tag> stack)
        {
            var units = new List<Unit>(s.Length);
            Tag[] snap = null;
            for (int i = 0; i < s.Length;)
            {
                char c = s[i];
                if (c == '<' && o.RichText)
                {
                    int end; bool closing; string name;
                    if (TryTag(s, i, out end, out closing, out name))
                    {
                        string raw = s.Substring(i, end - i + 1);
                        if (!closing && AtomicTags.Contains(name))
                        {
                            units.Add(new Unit { Text = raw, Cls = Cls.ON, Atomic = true, Tags = snap ?? (snap = stack.ToArray()) });
                            i = end + 1;
                            continue;
                        }
                        bool alpha = name == "alpha";
                        if (PairedTags.Contains(name) || (alpha && !closing))   // </alpha> is not a TMP tag: left as plain text
                        {
                            if (!closing) stack.Add(new Tag { Open = raw, Close = alpha ? "<alpha=#FF>" : "</" + name + ">", Name = name });
                            else
                            {
                                int k = stack.FindLastIndex(t => t.Name == name);
                                if (k >= 0) stack.RemoveAt(k);   // a closer with nothing to close is dropped
                            }
                            snap = null;
                            i = end + 1;
                            continue;
                        }
                    }
                }

                if (o.Digits == DigitStyle.ArabicIndic && c >= '0' && c <= '9') c = (char)(0x0660 + (c - '0'));
                else if (o.Digits == DigitStyle.Western)
                {
                    if (c >= '٠' && c <= '٩') c = (char)('0' + (c - 0x0660));
                    else if (c >= '۰' && c <= '۹') c = (char)('0' + (c - 0x06F0));
                }

                if (char.IsHighSurrogate(c) && i + 1 < s.Length && char.IsLowSurrogate(s[i + 1]))
                {
                    units.Add(new Unit { Text = s.Substring(i, 2), Cls = Cls.ON, Tags = snap ?? (snap = stack.ToArray()) });
                    i += 2;
                    continue;
                }
                if (IsMark(c))
                {
                    if (!o.KeepDiacritics && IsArabicMark(c)) { i++; continue; }
                    Unit last = units.Count > 0 ? units[units.Count - 1] : null;
                    if (last != null && !last.Atomic && !last.Hidden) { last.Marks += c; i++; continue; }
                }
                if (c == '‌' || c == '‍')
                {
                    units.Add(new Unit { Text = "", Hidden = true, Cls = Cls.ON, Join = c == '‍' ? 3 : 0, Tags = snap ?? (snap = stack.ToArray()) });
                    i++;
                    continue;
                }
                if (c == '‎' || c == '‏')
                {
                    units.Add(new Unit { Text = "", Hidden = true, Cls = c == '‎' ? Cls.L : Cls.R, Tags = snap ?? (snap = stack.ToArray()) });
                    i++;
                    continue;
                }
                if ((c >= '‪' && c <= '‮') || (c >= '⁦' && c <= '⁩')) { i++; continue; }   // embedding controls: not supported, dropped

                var u = new Unit { Text = c.ToString(), B = c, Cls = Classify(c), Tags = snap ?? (snap = stack.ToArray()) };
                char[] f;
                if (Forms.TryGetValue(c, out f))
                {
                    u.Iso = f[0]; u.Fin = f[1]; u.Ini = f[2]; u.Med = f[3];
                    u.Join = f[1] == 0 ? 0 : (f[2] == 0 ? 1 : 2);
                }
                units.Add(u);
                i++;
            }
            return units;
        }

        // s[i] is '<'. A tag is a known TextMeshPro name (or <#rrggbb>) up to the next '>'.
        static bool TryTag(string s, int i, out int end, out bool closing, out string name)
        {
            end = i; closing = false; name = null;
            int j = i + 1;
            if (j < s.Length && s[j] == '/') { closing = true; j++; }
            int nameStart = j;
            while (j < s.Length && s[j] != '>' && s[j] != '<' && j - i < 128) j++;
            if (j >= s.Length || s[j] != '>' || j == nameStart) return false;
            char first = s[nameStart];
            if (!((first >= 'a' && first <= 'z') || (first >= 'A' && first <= 'Z') || first == '#')) return false;
            if (first == '#') name = "color";
            else
            {
                int k = nameStart;
                while (k < j && s[k] != '=' && s[k] != ' ') k++;
                name = s.Substring(nameStart, k - nameStart).ToLowerInvariant();
            }
            end = j;
            return PairedTags.Contains(name) || AtomicTags.Contains(name) || name == "alpha";
        }

        // ---- shaping ----

        static void MergeLigatures(List<Unit> u)
        {
            for (int i = 0; i + 1 < u.Count; i++)
            {
                Unit a = u[i], b = u[i + 1];
                char[] lig;
                if (a.B != 'ل' || a.Atomic || b.Atomic || a.Hidden || b.Hidden || !ReferenceEquals(a.Tags, b.Tags)) continue;
                if (!Ligatures.TryGetValue(b.B, out lig)) continue;
                a.Marks += b.Marks;
                a.Iso = lig[0]; a.Fin = lig[1]; a.Ini = '\0'; a.Med = '\0';
                a.Join = 1;
                a.B = '\0';
                u.RemoveAt(i + 1);
            }
        }

        static void Join(List<Unit> u)
        {
            for (int i = 0; i < u.Count; i++)
            {
                Unit c = u[i];
                if (c.Iso == '\0') continue;
                bool prev = c.Join >= 1 && i > 0 && (u[i - 1].Join == 2 || u[i - 1].Join == 3);
                bool next = c.Join == 2 && i + 1 < u.Count && u[i + 1].Join >= 1;
                c.Text = (prev ? (next ? c.Med : c.Fin) : (next ? c.Ini : c.Iso)).ToString();
                c.Cls = Cls.AL;
            }
        }

        // ---- display order (a trimmed version of the Unicode bidirectional algorithm) ----

        static void Resolve(List<Unit> u, int baseLevel)
        {
            int n = u.Count;
            var t = new Cls[n];
            for (int i = 0; i < n; i++) t[i] = u[i].Cls;
            Cls sos = baseLevel == 0 ? Cls.L : Cls.R;

            Cls last = sos;                                              // W2: a digit after an Arabic letter is an Arabic digit
            for (int i = 0; i < n; i++)
            {
                if (t[i] == Cls.L || t[i] == Cls.R || t[i] == Cls.AL) last = t[i];
                else if (t[i] == Cls.EN && last == Cls.AL) t[i] = Cls.AN;
            }
            for (int i = 0; i < n; i++) if (t[i] == Cls.AL) t[i] = Cls.R;   // W3

            for (int i = 1; i < n - 1; i++)                              // W4: 1,000 and 12.5 and 10:30 stay one number
            {
                if (t[i] == Cls.ES && t[i - 1] == Cls.EN && t[i + 1] == Cls.EN) t[i] = Cls.EN;
                else if (t[i] == Cls.CS && (t[i - 1] == Cls.EN || t[i - 1] == Cls.AN) && t[i + 1] == t[i - 1]) t[i] = t[i - 1];
            }
            for (int i = 0; i < n; i++)                                  // W5: 50% stays together
            {
                if (t[i] != Cls.ET) continue;
                int j = i;
                while (j < n && t[j] == Cls.ET) j++;
                if ((i > 0 && t[i - 1] == Cls.EN) || (j < n && t[j] == Cls.EN))
                    for (int k = i; k < j; k++) t[k] = Cls.EN;
                i = j - 1;
            }
            for (int i = 0; i < n; i++)                                  // W6
                if (t[i] == Cls.ES || t[i] == Cls.ET || t[i] == Cls.CS) t[i] = Cls.ON;
            last = sos;                                                  // W7: a digit after Latin text is Latin
            for (int i = 0; i < n; i++)
            {
                if (t[i] == Cls.L || t[i] == Cls.R) last = t[i];
                else if (t[i] == Cls.EN && last == Cls.L) t[i] = Cls.L;
            }

            Cls embed = baseLevel == 0 ? Cls.L : Cls.R;                  // N1/N2: spaces and punctuation take the side they sit in
            for (int i = 0; i < n; i++)
            {
                if (t[i] != Cls.WS && t[i] != Cls.ON) continue;
                int j = i;
                while (j < n && (t[j] == Cls.WS || t[j] == Cls.ON)) j++;
                Cls before = i == 0 ? sos : Side(t[i - 1]);
                Cls after = j == n ? sos : Side(t[j]);
                Cls r = before == after ? before : embed;
                for (int k = i; k < j; k++) t[k] = r;
                i = j - 1;
            }

            for (int i = 0; i < n; i++)                                  // I1/I2: levels
            {
                bool rtl = baseLevel == 1;
                if (t[i] == Cls.L) u[i].Level = rtl ? 2 : 0;
                else if (t[i] == Cls.R) u[i].Level = 1;
                else u[i].Level = 2;
            }
            for (int i = n - 1; i >= 0 && (u[i].Cls == Cls.WS || u[i].Hidden); i--) u[i].Level = baseLevel;   // L1: trailing spaces
        }

        static Cls Side(Cls c) { return c == Cls.L ? Cls.L : Cls.R; }   // digits count as right-to-left next to a space

        static void Reorder(List<Unit> u)
        {
            int max = 0, minOdd = int.MaxValue;
            foreach (Unit x in u)
            {
                if (x.Level > max) max = x.Level;
                if ((x.Level & 1) == 1 && x.Level < minOdd) minOdd = x.Level;
            }
            for (int lvl = max; lvl >= minOdd; lvl--)
                for (int i = 0; i < u.Count;)
                {
                    if (u[i].Level < lvl) { i++; continue; }
                    int j = i;
                    while (j < u.Count && u[j].Level >= lvl) j++;
                    u.Reverse(i, j - i);
                    i = j;
                }
        }

        // ---- writing the result, with tags moved to follow their letters ----

        // At the end of a line the tags still open in the text must be open in the output too, because the next line carries on
        // from them. After the last line nothing follows, so tags are only closed there, never opened again.
        static void Emit(StringBuilder sb, List<Unit> units, List<Tag> open, Tag[] endState, bool last)
        {
            foreach (Unit x in units)
            {
                if (x.Hidden) continue;
                Transition(sb, open, x.Tags, true);
                sb.Append(x.Text).Append(x.Marks);
            }
            Transition(sb, open, endState, !last);
        }

        static void Transition(StringBuilder sb, List<Tag> open, Tag[] target, bool openNew)
        {
            int common = 0;
            while (common < open.Count && common < target.Length && ReferenceEquals(open[common], target[common])) common++;
            for (int k = open.Count - 1; k >= common; k--) sb.Append(open[k].Close);
            open.RemoveRange(common, open.Count - common);
            if (!openNew) return;
            for (int k = common; k < target.Length; k++) { sb.Append(target[k].Open); open.Add(target[k]); }
        }

        // ---- character classes ----

        static bool IsArabicLetter(char c)
        {
            return (c >= 'ؠ' && c <= 'ي') || (c >= 'ٮ' && c <= 'ە') || c == 'ۮ' || c == 'ۯ'
                || (c >= 'ۺ' && c <= 'ۿ') || (c >= 'ݐ' && c <= 'ݿ') || (c >= 'ࢠ' && c <= 'ࣉ');
        }

        static bool IsPresentationForm(char c) { return (c >= 'ﭐ' && c <= '﷿') || (c >= 'ﹰ' && c <= '﻿'); }

        static bool IsArabicMark(char c)
        {
            return (c >= 'ً' && c <= 'ٟ') || c == 'ٰ' || (c >= 'ۖ' && c <= 'ۜ')
                || (c >= '۟' && c <= 'ۤ') || c == 'ۧ' || c == 'ۨ' || (c >= '۪' && c <= 'ۭ');
        }

        static bool IsMark(char c) { return IsArabicMark(c) || (c >= '̀' && c <= 'ͯ'); }

        static Cls Classify(char c)
        {
            if (c >= '0' && c <= '9') return Cls.EN;
            if (c == '+' || c == '-') return Cls.ES;
            if (c == '#' || c == '$' || c == '%' || (c >= '¢' && c <= '¥') || c == '°' || c == '‰' || c == '٪') return Cls.ET;
            if (c == ',' || c == '.' || c == '/' || c == ':' || c == ' ' || c == '،' || c == '٫' || c == '٬') return Cls.CS;
            if (c == ' ' || c == '\t' || c == '\r') return Cls.WS;
            if (c >= '٠' && c <= '٩') return Cls.AN;
            if (c >= '۰' && c <= '۹') return Cls.EN;
            if (IsArabicLetter(c) || IsPresentationForm(c) || c == '؛' || c == '؟' || c == '٭') return Cls.AL;
            if (c >= '֐' && c <= '׿') return Cls.R;
            if (char.IsLetter(c)) return Cls.L;
            if (char.IsDigit(c)) return Cls.EN;
            return Cls.ON;
        }
    }
}
