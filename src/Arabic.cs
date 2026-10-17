using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

namespace GorillaHurouf
{
    /// <summary>
    /// What other mods call. Use it from the main thread after the game has started.
    /// Quick start: <c>myTmpText.SetArabic("هلا");</c> or <c>myLegacyText.SetArabic("هلا");</c>
    /// </summary>
    public static class Arabic
    {
        internal static readonly ManualLogSource Log = BepInEx.Logging.Logger.CreateLogSource(PluginInfo.NAME);
        const string FontResource = "GorillaHurouf.NotoSansArabic-Regular.ttf";   // the csproj's LogicalName for the embedded font

        static TMP_FontAsset fontAsset;
        static bool fontTried;
        static readonly List<TMP_FontAsset> patched = new List<TMP_FontAsset>();

        /// <summary>Text a left-to-right renderer draws correctly: joined letters, display order, mixed English and digits kept right.
        /// Text without Arabic letters comes back unchanged, and fixed text is left alone if fixed again.</summary>
        public static string Fix(string text, ShapeOptions options = null) { return ArabicShaper.Shape(text, options ?? defaultOptions); }

        static ShapeOptions defaultOptions = new ShapeOptions();

        /// <summary>The shaping options used whenever a call is given none: <see cref="Fix"/>, <see cref="Wrap"/>, <see cref="SetArabic(TMP_Text, string, ShapeOptions)"/>,
        /// auto-fix, notifications and the message-of-the-day board. Change a field (<c>Arabic.DefaultOptions.Digits = DigitStyle.ArabicIndic;</c>) or replace the object;
        /// null puts the built-in defaults back. Text already drawn is not shaped again until it changes.</summary>
        public static ShapeOptions DefaultOptions
        {
            get { return defaultOptions; }
            set { defaultOptions = value ?? new ShapeOptions(); }
        }

        // The default options with one direction, for the places that must force it (notifications are always right to left for Arabic).
        internal static ShapeOptions DefaultWith(TextDirection direction)
        {
            ShapeOptions d = defaultOptions;
            return new ShapeOptions { Direction = direction, Digits = d.Digits, KeepDiacritics = d.KeepDiacritics, RichText = d.RichText };
        }

        /// <summary>True if the text has any Arabic letters (fixed or not).</summary>
        public static bool IsArabic(string text) { return ArabicShaper.ContainsArabic(text); }

        /// <summary>The Arabic font as a TextMeshPro font asset, made on first use. Null if it could not be made (the reason is in the log).
        /// It has Arabic only: no Latin letters and no brackets. Use <see cref="EnableFor"/> to put it behind a text's own font instead.</summary>
        public static TMP_FontAsset FontAsset
        {
            get
            {
                if (fontAsset != null || fontTried) return fontAsset;
                fontTried = true;
                try
                {
                    string path = ExtractFont();
                    fontAsset = TMP_FontAsset.CreateFontAsset(path, 0, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024);
                    if (fontAsset == null) { Log.LogError("The Arabic font asset could not be made from " + path); return null; }
                    fontAsset.name = "GorillaHurouf Arabic";
                    // Noto's line height is 2.1 times its size, the game's fonts 1.15: a line with any Arabic in it would stand twice as far apart.
                    FaceInfo face = fontAsset.faceInfo;
                    face.lineHeight = face.pointSize * 1.45f;
                    face.ascentLine = face.pointSize * 1.0f;
                    face.descentLine = -face.pointSize * 0.4f;
                    fontAsset.faceInfo = face;
                    fontAsset.hideFlags = HideFlags.DontUnloadUnusedAsset;
                    if (fontAsset.material != null) fontAsset.material.hideFlags = HideFlags.DontUnloadUnusedAsset;
                    if (fontAsset.atlasTextures != null)
                        foreach (Texture2D t in fontAsset.atlasTextures) if (t != null) t.hideFlags = HideFlags.DontUnloadUnusedAsset;
                    Log.LogDebug("Arabic font ready (" + fontAsset.atlasPopulationMode + ")");
                }
                catch (Exception e) { Log.LogError("The Arabic font failed: " + e); fontAsset = null; }
                return fontAsset;
            }
        }

        static TMP_FontAsset gameFont;
        static bool gameFontTried;

        /// <summary>The game's own default text font (one with Latin letters and brackets), or null if none was found.</summary>
        internal static TMP_FontAsset GameFont
        {
            get
            {
                if (gameFont != null || gameFontTried) return gameFont;
                gameFontTried = true;
                try
                {
                    TMP_FontAsset d = TMP_Settings.defaultFontAsset;
                    if (d != null && d != fontAsset && d.HasCharacter('A') && d.HasCharacter('(')) gameFont = d;
                }
                catch (Exception) { }   // TMP_Settings can be missing this early
                if (gameFont == null)
                    foreach (TMP_FontAsset f in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
                        if (f != null && f != fontAsset && f.HasCharacter('A') && f.HasCharacter('(')) { gameFont = f; break; }
                Log.LogDebug("Game font for notifications: " + (gameFont != null ? gameFont.name : "none found, using the Arabic font only"));
                return gameFont;
            }
        }

        static string languageOverride;

        /// <summary>The language <see cref="Translator"/> objects use: "ar" or "en". By default "ar" when the game's system language is Arabic, otherwise "en".
        /// Set it to force one (any code your JSON uses); set null to follow the system again.</summary>
        public static string Language
        {
            get { return languageOverride ?? (Application.systemLanguage == SystemLanguage.Arabic ? "ar" : "en"); }
            set { languageOverride = value; }
        }

        /// <summary>A new, empty translations object for your mod. Load strings with <c>LoadJson</c> or <c>LoadFile</c>, read them with <c>T("key")</c>.
        /// <c>T</c> gives logical text: show it with <c>SetArabic</c> or <c>Notify</c>.</summary>
        public static Translator Translator(string name)
        {
            return new Translator(name ?? "Translator", () => Language, Log.LogWarning);
        }

        /// <summary>Today's (or the given) date in the Hijri Umm al-Qura calendar, in Arabic: "25 جمادى الأولى 1447 هـ". Logical text: show it with
        /// <c>SetArabic</c> or <c>Notify</c>. Null if the date is outside the calendar's range (1900 to 2077).</summary>
        public static string Hijri(DateTime? date = null, bool arabicDigits = false)
        {
            return HijriDate.Format(date ?? DateTime.Now, arabicDigits);
        }

        /// <summary>true: notifications are hidden from the game's spectator ("Shoulder") camera, so a stream or a recording of that view
        /// does not show them. Default false. Note that without a headset (Desktop Mode) the game window itself shows that camera.</summary>
        public static bool HideNotificationsFromSpectators { get; set; }

        /// <summary>Shows a notification in front of the player's view. The text is wrapped and fixed for Arabic; repeats are counted (×2).
        /// It queues, so it is safe to call before the game has finished loading.</summary>
        /// <param name="source">Optional name shown in front, such as your mod's name: "[Source] text".</param>
        public static void Notify(string text, NotifyKind kind = NotifyKind.Info, float seconds = -1f, string source = null)
        {
            Notify(text, NotificationSettings.ColorOf(kind), seconds, source);
        }

        /// <summary>Same with your own colour. Seconds below 0 (the default) mean <see cref="NotificationSettings.DefaultSeconds"/>.</summary>
        public static void Notify(string text, Color color, float seconds = -1f, string source = null)
        {
            if (string.IsNullOrEmpty(text)) return;
            NotificationPanel.Enqueue(text, source, color, Mathf.Max(0.5f, seconds < 0f ? NotificationSettings.DefaultSeconds : seconds));
        }

        /// <summary>Breaks the text into lines that fit <paramref name="maxWidth"/> (in the text's own units) and fixes them for Arabic.
        /// Use it instead of <see cref="Fix"/> when a long text must wrap: a fixed line is in display order, so TextMeshPro's own wrapping
        /// would cut the wrong end of it. Turn off the text's own wrapping and set the result as its text.</summary>
        public static string Wrap(TMP_Text text, string value, float maxWidth, ShapeOptions options = null)
        {
            if (text == null) return value;
            return ArabicShaper.Wrap(value, s => text.GetPreferredValues(s).x, maxWidth, options ?? defaultOptions);
        }

        // The pieces auto-fix and the message-of-the-day board share. Wrapping is on the logical text and only when the text wraps itself at a fixed size
        // (a shaped line is in display order, so TextMeshPro's own wrapping would cut it at the wrong end); 0 means no wrapping.
        internal static float WrapWidth(TMP_Text text)
        {
            TextWrappingModes mode = text.textWrappingMode;
            if ((mode != TextWrappingModes.Normal && mode != TextWrappingModes.PreserveWhitespace) || text.enableAutoSizing) return 0f;
            Vector4 margin = text.margin;
            return Mathf.Max(0f, text.rectTransform.rect.width - margin.x - margin.z);
        }

        // A tag in front draws a left-aligned text from the right. It needs rich text; the text's own alignment is not touched.
        internal static bool WantsRightAlign(TMP_Text text, bool enabled)
        {
            return enabled && text.richText && text.horizontalAlignment == HorizontalAlignmentOptions.Left;
        }

        internal static string Render(TMP_Text text, string logical, float wrapWidth, bool rightAlign)
        {
            string fixedText = wrapWidth > 0f ? Wrap(text, logical, wrapWidth) : Fix(logical);
            return rightAlign ? "<align=right>" + fixedText : fixedText;
        }

        /// <summary>Lets a TextMeshPro text draw Arabic: puts the Arabic font behind the text's own font (added once; the text keeps its look)
        /// and turns kerning off for that text, because the font's kerning pairs are written for logical order and pull joined letters apart.</summary>
        public static void EnableFor(TMP_Text text) { EnableFor(text, false); }

        // building: the text is being built right now (auto-fix). Assigning fontFeatures marks it dirty and asks for another rebuild inside the
        // rebuild, so the list is edited in place; the build that is running reads it afterwards.
        internal static void EnableFor(TMP_Text text, bool building)
        {
            if (text == null) return;
            if (text.fontFeatures.Contains(OTL_FeatureTag.kern))
            {
                if (building) text.fontFeatures.Remove(OTL_FeatureTag.kern);
                else
                {
                    var features = new List<OTL_FeatureTag>(text.fontFeatures);
                    features.Remove(OTL_FeatureTag.kern);
                    text.fontFeatures = features;
                }
            }
            TMP_FontAsset arabic = FontAsset;
            if (arabic == null || text.font == null || text.font == arabic) return;
            TMP_FontAsset own = text.font;
            if (own.fallbackFontAssetTable == null) own.fallbackFontAssetTable = new List<TMP_FontAsset>();
            if (own.fallbackFontAssetTable.Contains(arabic)) return;
            own.fallbackFontAssetTable.Add(arabic);
            patched.Add(own);
        }

        /// <summary>Turns on auto-fix: from now on every TextMeshPro and legacy <c>Text</c> in the game that is given Arabic is drawn fixed, and
        /// <c>.text</c> still returns what its owner set (nothing networked changes, only what is drawn). English text is left alone and costs one scan.
        /// Texts that wrap are wrapped on the Arabic text before it is fixed. Call it once from your mod; calling it again does nothing.
        /// Legacy <c>Text</c> is fixed per line (it is not wrapped), and a text that already went through <see cref="Fix"/> is left as it is.</summary>
        public static void EnableAutoFix() { AutoFix.Enable(); }

        /// <summary>Turns auto-fix off again. Texts already drawn stay as they are until they change.</summary>
        public static void DisableAutoFix() { AutoFix.Disable(); }

        /// <summary>True while auto-fix is on.</summary>
        public static bool AutoFixEnabled { get { return AutoFix.Enabled; } }

        /// <summary>Shows your own text on the game's message-of-the-day board (the one in the tree room; its hidden copies too), in the given colour.
        /// Arabic is fixed, wrapped to the board and drawn from the right. Pass no colour to keep the game's. This is local: nothing is sent, so only the
        /// player running your mod sees it. The game rewrites the board when its data changes or the language does; the library puts your text back at once.
        /// It can be called before the board exists (the library keeps looking). <see cref="ClearMessageOfTheDay"/> gives the game's own text back.</summary>
        public static void SetMessageOfTheDay(string text, Color? color = null) { MessageOfTheDay.Set(text, color); }

        /// <summary>Puts the game's own message of the day (and its colour) back on the board.</summary>
        public static void ClearMessageOfTheDay() { MessageOfTheDay.Set(null, null); }

        /// <summary>Changes the heading above the message-of-the-day board ("MESSAGE OF THE DAY") to your own text, fixed for Arabic, in the given colour (none keeps the game's).
        /// Local, like <see cref="SetMessageOfTheDay"/>; the library puts it back when the game rewrites the heading (a language change). Pass null or "" to give the game's heading back.
        /// It works on its own: you can change the title and leave the message alone.</summary>
        public static void SetMessageOfTheDayTitle(string title, Color? color = null) { MessageOfTheDay.SetTitle(title, color); }

        /// <summary>Tells auto-fix to leave this text alone (or, with <c>false</c>, to fix it again). The text is shown as its owner set it, even if it has Arabic.
        /// A text that is already drawn changes the next time it is rebuilt. <see cref="SetArabic(TMP_Text, string, ShapeOptions)"/> still works on it.</summary>
        public static void SkipAutoFix(TMP_Text text, bool skip = true) { AutoFix.Skip(text, skip); }

        /// <summary>Same for Unity's older <c>Text</c>.</summary>
        public static void SkipAutoFix(Text text, bool skip = true) { AutoFix.Skip(text, skip); }

        /// <summary>Default false. true: a TextMeshPro text that auto-fix fixes and that is left-aligned (the default) is drawn right-aligned, as Arabic is read.
        /// Texts aligned to the centre or the right are left as they are, and so are texts with rich text turned off. The text's own alignment setting is not changed.
        /// Legacy <c>Text</c> is not affected.</summary>
        public static bool AutoFixRightAlign
        {
            get { return AutoFix.RightAlign; }
            set { AutoFix.RightAlign = value; }
        }

        /// <summary>Sets the text, fixed for Arabic, and makes sure the text's font can draw it.</summary>
        public static void SetArabic(this TMP_Text text, string value, ShapeOptions options = null)
        {
            if (text == null) return;
            if (IsArabic(value)) EnableFor(text);   // before the text is set, so the new text is built with the fallback
            text.text = Fix(value, options);
        }

        /// <summary>Same for Unity's older <c>Text</c>: sets the fixed text. It does not change the font: a dynamic font such as the built-in one
        /// draws Arabic through the system's fallback fonts (seen in game); a font with no Arabic and no fallback would still show boxes.</summary>
        public static void SetArabic(this Text text, string value, ShapeOptions options = null)
        {
            if (text != null) text.text = Fix(value, options);
        }

        // Plugin.OnDestroy: undo everything this library added to the game's fonts, so a reload starts clean.
        internal static void Cleanup()
        {
            AutoFix.Disable();   // before the font goes: a patched text must not look for a font asset that is destroyed
            foreach (TMP_FontAsset f in patched)
                if (f != null && f.fallbackFontAssetTable != null) f.fallbackFontAssetTable.Remove(fontAsset);
            patched.Clear();
            if (fontAsset != null)
            {
                if (fontAsset.atlasTextures != null) foreach (Texture2D t in fontAsset.atlasTextures) Destroy(t);
                Destroy(fontAsset.material);
                Destroy(fontAsset);
            }
            fontAsset = null;
            gameFont = null;
        }

        static void Destroy(UnityEngine.Object o) { if (o != null) UnityEngine.Object.Destroy(o); }

        // TextMeshPro loads a font from a file path, so the embedded font is written to BepInEx's cache folder once.
        // The licences of the library (MIT) and of the font (OFL 1.1) are written next to it, so a release can be the DLL alone.
        static string ExtractFont()
        {
            string dir = Path.Combine(Paths.CachePath, PluginInfo.NAME);
            string path = Path.Combine(dir, "NotoSansArabic-Regular.ttf");
            if (!WriteResource(FontResource, path)) throw new FileNotFoundException("The font is not embedded: " + FontResource);
            try
            {
                if (!WriteResource("GorillaHurouf.LICENSE.txt", Path.Combine(dir, "LICENSE.txt")) || !WriteResource("GorillaHurouf.OFL.txt", Path.Combine(dir, "OFL.txt")))
                    Log.LogWarning("The licence texts are not embedded in this build.");
            }
            catch (Exception e) { Log.LogWarning("Could not write the licence files next to the font: " + e.Message); }
            return path;
        }

        // Writes an embedded resource to a file unless the file is already there with the same size. False if there is no such resource.
        static bool WriteResource(string resource, string path)
        {
            using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource))
            {
                if (s == null) return false;
                if (!File.Exists(path) || new FileInfo(path).Length != s.Length)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    using (FileStream f = File.Create(path)) s.CopyTo(f);
                }
                return true;
            }
        }
    }
}
