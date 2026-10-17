using System;
using System.Text;
using System.Text.RegularExpressions;
using GorillaHurouf;

// Run with tests\run.ps1. Arabic is written as \u escapes so the file is safe in any editor or encoding.
static class ShaperChecks
{
    static int failed;

    static void Check(string name, string actual, string expected)
    {
        bool ok = actual == expected;
        if (!ok) failed++;
        Console.WriteLine((ok ? "PASS  " : "FAIL  ") + name);
        if (!ok) Console.WriteLine("      expected " + Dump(expected) + "\n      actual   " + Dump(actual));
    }

    static string Dump(string s)
    {
        if (s == null) return "null";
        var sb = new StringBuilder();
        foreach (char c in s) sb.Append(c < 128 && c >= 32 ? c.ToString() : "\\u" + ((int)c).ToString("X4"));
        return sb.ToString();
    }

    static int Main()
    {
        const string Marhaba = "مرحبا";             // logical order
        const string V = "ﺎﺒﺣﺮﻣ";                    // the same word, joined and in display order

        Check("one word joins and reverses", ArabicShaper.Shape(Marhaba), V);
        Check("lam-alef is one glyph", ArabicShaper.Shape("لا"), "ﻻ");
        Check("lam-alef joined to the letter before it", ArabicShaper.Shape("بلا"), "ﻼﺑ");
        Check("Latin first keeps the line left to right", ArabicShaper.Shape("ABC " + Marhaba), "ABC " + V);
        Check("Latin inside an Arabic line", ArabicShaper.Shape(Marhaba + " Hex"), "Hex " + V);
        Check("number after Arabic keeps its digit order", ArabicShaper.Shape("رقم 123"), "123 ﻢﻗﺭ");
        Check("brackets swap sides", ArabicShaper.Shape("(" + Marhaba + ")"), "(" + V + ")");
        Check("tags follow their letters", ArabicShaper.Shape("<color=red>" + Marhaba + "</color>"), "<color=red>" + V + "</color>");
        Check("a tag open across lines stays open", ArabicShaper.Shape("<color=red>" + Marhaba + "\n" + Marhaba + "</color>"), "<color=red>" + V + "\n" + V + "</color>");
        // wrapping, with a stand-in ruler: a line is as wide as its visible characters
        Func<string, float> ruler = s => Regex.Replace(s, "<[^>]*>", "").Length;
        const string Ghoryla = "غوريلا";
        string[] wrapped = ArabicShaper.Wrap(Marhaba + " بك في " + Ghoryla, ruler, 11).Split('\n');
        Check("wrap breaks between words", wrapped.Length.ToString(), "2");
        Check("wrap shapes each line", string.Join("\n", wrapped), ArabicShaper.Shape(Marhaba + " بك في\n" + Ghoryla));
        Check("a word wider than the limit stays whole", ArabicShaper.Wrap("abcdefghij kl", ruler, 3), "abcdefghij\nkl");
        Check("wrap carries a tag across the break", ArabicShaper.Wrap("<color=red>" + Marhaba + " " + Marhaba + "</color>", ruler, 6), "<color=red>" + V + "\n" + V + "</color>");
        Check("wrap keeps a text that fits on one line", ArabicShaper.Wrap(Marhaba, ruler, 50), V);
        Check("alpha stays on (it has no closing tag)", ArabicShaper.Shape("<alpha=#80>" + Marhaba), "<alpha=#80>" + V);
        Check("alpha is reset where the order moves out of it", ArabicShaper.Shape(Marhaba + " <alpha=#80>Hex"), "<alpha=#80>Hex<alpha=#FF> " + V);
        // fixed text must be left alone, even with a tatweel in it (the Hijri "هـ" has one): shaping it again used to flip the line back
        string hijriFixed = ArabicShaper.Shape(HijriDate.Format(new DateTime(2025, 3, 1), true));
        Check("a fixed Hijri date (tatweel in هـ) is left alone", ArabicShaper.Shape(hijriFixed), hijriFixed);
        string tatweelFixed = ArabicShaper.Shape("كـــتاب");
        Check("a fixed word with a tatweel is left alone", ArabicShaper.Shape(tatweelFixed), tatweelFixed);
        Check("a tatweel alone needs no shaping", ArabicShaper.NeedsShaping("ـــ") ? "yes" : "no", "no");
        Check("control: unfixed Arabic still needs shaping", ArabicShaper.NeedsShaping("كتاب") ? "yes" : "no", "yes");
        Check("control: the tatweel word really is shaped the first time", tatweelFixed != "كـــتاب" ? "yes" : "no", "yes");
        Check("a stray </alpha> is plain text, not a tag", ArabicShaper.Shape("x" + Marhaba + "</alpha>").Contains("</alpha>") ? "kept" : "dropped", "kept");
        Check("a diacritic stays after its letter", ArabicShaper.Shape("بَ"), "ﺏَ");
        Check("diacritics can be removed", ArabicShaper.Shape("بَ", new ShapeOptions { KeepDiacritics = false }), "ﺏ");
        Check("digits can become Arabic-Indic", ArabicShaper.Shape("رقم 5", new ShapeOptions { Digits = DigitStyle.ArabicIndic }), "٥ ﻢﻗﺭ");
        Check("text with no Arabic is untouched", ArabicShaper.Shape("Hello <b>World</b> 123"), "Hello <b>World</b> 123");
        Check("null stays null", ArabicShaper.Shape(null), null);

        foreach (string s in new[] { Marhaba, "ABC " + Marhaba, Marhaba + " Hex 12", "(" + Marhaba + ")\n" + Marhaba, "<b>" + Marhaba + "</b>", "لا بَ" })
        {
            string once = ArabicShaper.Shape(s);
            Check("shaping twice changes nothing: " + Dump(s), ArabicShaper.Shape(once), once);
        }

        // Every form in the table must decompose (Unicode compatibility mapping) back to its own base letter.
        int bad = 0;
        foreach (var e in ArabicShaper.FormTable)
            foreach (char f in e.Value)
                if (f != 0 && f.ToString().Normalize(NormalizationForm.FormKC) != e.Key.ToString()) { bad++; Console.WriteLine("      table: U+" + ((int)f).ToString("X4") + " is not a form of U+" + ((int)e.Key).ToString("X4")); }
        foreach (var e in ArabicShaper.LigatureTable)
            foreach (char f in e.Value)
                if (f.ToString().Normalize(NormalizationForm.FormKC) != "ل" + e.Key) { bad++; Console.WriteLine("      table: U+" + ((int)f).ToString("X4") + " is not lam + U+" + ((int)e.Key).ToString("X4")); }
        Check("every table entry matches Unicode", bad.ToString(), "0");

        // Translator: JSON reader, language fallback, formatting
        string lang = "ar";
        var warnings = new System.Collections.Generic.List<string>();
        var tr = new Translator("Test", () => lang, warnings.Add);
        const string Sample = "{ \"welcome\": { \"en\": \"Welcome\", \"ar\": \"\\u0623\\u0647\\u0644\\u0627\" }, \"hi\": { \"en\": \"Hi {0}, \\\"x\\\"\\n\" }, \"n\": 5, \"list\": [1, {\"a\": null}, true] }";
        Check("json: valid text loads", tr.LoadJson(Sample).ToString(), "True");
        Check("json: \"n\" and \"list\" (not objects) are reported, the rest still loads", warnings.Count + " " + tr.T("welcome"), "2 \u0623\u0647\u0644\u0627");
        lang = "en"; Check("translator: requested language", tr.T("welcome"), "Welcome");
        lang = "fr"; Check("translator: falls back to English", tr.T("welcome"), "Welcome");
        lang = "AR"; Check("translator: language code ignores case", tr.T("welcome"), "\u0623\u0647\u0644\u0627");
        lang = "ar"; Check("translator: no Arabic text falls back to English, with format and escapes", tr.T("hi", "Hex"), "Hi Hex, \"x\"\n");
        Check("translator: missing key returns the key", tr.T("nope"), "nope");
        tr.T("nope");
        Check("translator: a missing key is reported once", warnings.Count.ToString(), "3");
        var trBad = new Translator("Bad", () => "en", warnings.Add);
        trBad.LoadJson("{ \"k\": { \"en\": \"{0} {1}\" } }");
        Check("translator: too few arguments does not throw", trBad.T("k", "a"), "{0} {1}");
        int before = warnings.Count;
        foreach (string broken in new[] { "", "[1]", "{ \"a\": ", "{ \"a\": { \"en\": \"x\" }", "{ \"a\" 1 }", "{ \"a\": tru }", "{ \"a\": \"\\q\" }", "{} x" })
            Check("json: rejects " + Dump(broken), trBad.LoadJson(broken).ToString(), "False");
        Check("json: every rejection was reported", (warnings.Count - before).ToString(), "8");
        Check("json: a rejected file changes nothing", trBad.T("k", "a", "b"), "a b");
        Check("json: deep nesting is rejected, not a crash", trBad.LoadJson(new string('[', 200)).ToString(), "False");

        // Hijri: dates whose Umm al-Qura value is known (1 Muharram 1446 = 2024-07-07, 1 Ramadan 1446 = 2025-03-01, 1 Shawwal 1446 = 2025-03-30)
        Check("hijri: 1 Muharram 1446", HijriDate.Format(new DateTime(2024, 7, 7), false), "1 \u0645\u062D\u0631\u0645 1446 \u0647\u0640");
        Check("hijri: 1 Ramadan 1446", HijriDate.Format(new DateTime(2025, 3, 1), false), "1 \u0631\u0645\u0636\u0627\u0646 1446 \u0647\u0640");
        Check("hijri: the day before is in Sha'ban", HijriDate.Format(new DateTime(2025, 2, 28), false).Split(' ')[1], "\u0634\u0639\u0628\u0627\u0646");
        Check("hijri: 1 Shawwal 1446 with Arabic-Indic digits", HijriDate.Format(new DateTime(2025, 3, 30), true), "\u0661 \u0634\u0648\u0627\u0644 \u0661\u0664\u0664\u0666 \u0647\u0640");
        Check("hijri: before the calendar's range is null", HijriDate.Format(new DateTime(1800, 1, 1), false), null);
        Check("hijri: after the calendar's range is null", HijriDate.Format(new DateTime(2100, 1, 1), false), null);

        Console.WriteLine(failed == 0 ? "\nall passed" : "\n" + failed + " FAILED");
        return failed == 0 ? 0 : 1;
    }
}
