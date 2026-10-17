#if HUROUF_DEMO
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GorillaHurouf
{
    // Development only (Debug builds define HUROUF_DEMO): shows each feature in front of the camera
    // and logs what was drawn, so a test can read the result without a headset.
    class Demo : MonoBehaviour
    {
        const string Welcome = "هلا والله في غوريلا تاغ";
        const string Mixed = "اللاعب Hex دخل (الروم) ABCD 123";
        const string Marks = "مُحَمَّد";
        const string Saved = "انحفظت الإعدادات";
        const string LongWarning = "انتبه: النت عندك ضعيف الحين، جرب مرة ثانية بعد شوي";
        const string Joined = "دخل اللاعب Hex الروم";
        const string Short = "حفظ";
        const string Brief = "إشعار قصير بس";

        GameObject root;
        Transform cam;

        void OnDestroy() { if (root != null) Destroy(root); }

        void LateUpdate()
        {
            if (root == null || cam == null) return;
            root.transform.position = cam.TransformPoint(0f, 0f, 1.6f);
            root.transform.rotation = cam.rotation;
        }

        IEnumerator Start()
        {
            while (Camera.main == null) yield return null;
            yield return new WaitForSeconds(2f);
            cam = Camera.main.transform;
            root = new GameObject("GorillaHurouf_Demo");

            TMP_FontAsset game = Arabic.GameFont;
            Arabic.Log.LogInfo("demo: game font = " + (game != null ? game.name : "none") + ", arabic font = " + (Arabic.FontAsset != null));

            TextMeshPro a = Tmp("welcome-game-font", game, 0.50f); a.SetArabic(Welcome); Report(a);
            TextMeshPro b = Tmp("mixed-game-font", game, 0.30f); b.SetArabic(Mixed); Report(b);
            TextMeshPro c = Tmp("marks-arabic-font", Arabic.FontAsset, 0.10f); c.SetArabic(Marks); Report(c);
            TextMeshPro d = Tmp("control-unshaped", game, -0.10f); Arabic.EnableFor(d); d.text = Welcome; Report(d);

            // step 4: translator (logical text in, SetArabic shapes it) and the Hijri date, read back from the Mono the game runs
            Translator tr = Arabic.Translator("Demo");
            bool loaded = tr.LoadJson("{ \"hello\": { \"en\": \"Hello {0}\", \"ar\": \"هلا يا {0}\" }, \"only-en\": { \"en\": \"English only\" } }");
            Arabic.Language = "ar";
            string helloAr = tr.T("hello", "Hex");
            string onlyEnInAr = tr.T("only-en");
            Arabic.Language = "en";
            string helloEn = tr.T("hello", "Hex");
            Arabic.Language = null;
            Arabic.Log.LogInfo("demo: translator loaded=" + loaded + " ar='" + helloAr + "' en='" + helloEn + "' fallback='" + onlyEnInAr + "' missing='" + tr.T("nope") + "' systemLanguage=" + Arabic.Language);
            TextMeshPro e = Tmp("translated-ar", game, -0.20f); e.SetArabic(helloAr); Report(e);
            string h = Arabic.Hijri(new System.DateTime(2025, 3, 1));
            Arabic.Log.LogInfo("demo: hijri 2025-03-01='" + h + "' (expect 1 رمضان 1446 هـ: " + (h == "1 رمضان 1446 هـ") + ") indic='" + Arabic.Hijri(new System.DateTime(2025, 3, 1), true) + "' out of range=" + (Arabic.Hijri(new System.DateTime(2100, 1, 1)) == null) + " today='" + Arabic.Hijri() + "'");
            TextMeshPro f = Tmp("hijri-today", game, -0.30f); f.SetArabic(Arabic.Hijri(null, true)); Report(f);

            // notifications: Arabic, a long one that must wrap, mixed with English, plain English, a repeat, a short-lived one
            Arabic.Notify(Saved, NotifyKind.Success, 60f, "Hurouf");
            Arabic.Notify(LongWarning, NotifyKind.Warning, 60f);
            Arabic.Notify(Joined, NotifyKind.Info, 60f, "Room");
            Arabic.Notify("Plain English notification that is long enough to need wrapping onto a second line", NotifyKind.Error, 60f);
            Arabic.Notify(Short, Color.cyan, 60f);
            Arabic.Notify(Short, Color.cyan, 60f);
            Arabic.Notify(Short, Color.cyan, 60f);
            Arabic.Notify(Brief, NotifyKind.Info, 4f);
            yield return new WaitForSeconds(1f);
            Arabic.Log.LogInfo("demo: notifications live=" + NotificationPanel.LiveCount + " (expect 6)");
            LogLayers();
            Arabic.HideNotificationsFromSpectators = true;    // the switch must move every mesh, and back
            yield return new WaitForSeconds(0.5f);
            LogLayers();
            Arabic.HideNotificationsFromSpectators = false;
            yield return new WaitForSeconds(0.5f);
            LogLayers();
            yield return new WaitForSeconds(5f);
            Arabic.Log.LogInfo("demo: notifications live after the 4 s one expired=" + NotificationPanel.LiveCount + " (expect 5)");
            yield return CustomizeChecks();
            yield return DeadRootChecks();
            AutoFixChecks(game);
            AutoFixOptionChecks(game);
            List<TMP_Text> motdBoxes = MotdDiscovery();
            Arabic.Log.LogInfo("demo: autofix rows up");
            yield return new WaitForSeconds(6f);   // time for a screenshot, with auto-fix still on
            AutoFixOffChecks();
            yield return MotdChecks(motdBoxes);
            root.SetActive(false);
            yield return GameTextChecks();
            Arabic.Log.LogInfo("demo: done");
        }

        // Auto-fix on, the game's own texts rebuilt through the hook: they must draw exactly what they draw with it off, and nothing of theirs gets shaped.
        IEnumerator GameTextChecks()
        {
            var tmps = new List<TMP_Text>();
            foreach (TMP_Text t in Resources.FindObjectsOfTypeAll<TMP_Text>())
                if (t != null && t.gameObject.scene.IsValid() && t.isActiveAndEnabled && tmps.Count < 300) tmps.Add(t);
            var legacy = new List<Text>();
            foreach (Text t in Resources.FindObjectsOfTypeAll<Text>())
                if (t != null && t.gameObject.scene.IsValid() && t.isActiveAndEnabled && legacy.Count < 300) legacy.Add(t);

            Arabic.EnableAutoFix();
            int calls = AutoFix.Calls, shaped = AutoFix.Shaped;
            yield return new WaitForSeconds(5f);   // the game's own updates, with the hook on
            int natural = AutoFix.Calls - calls;
            calls = AutoFix.Calls;
            var on = new string[tmps.Count];
            var onWidth = new float[legacy.Count];
            for (int i = 0; i < tmps.Count; i++) if (tmps[i] != null) { tmps[i].ForceMeshUpdate(); on[i] = tmps[i].GetParsedText(); }
            for (int i = 0; i < legacy.Count; i++) if (legacy[i] != null) onWidth[i] = legacy[i].preferredWidth;
            int forced = AutoFix.Calls - calls;
            int shapedWhileOn = AutoFix.Shaped - shaped;

            Arabic.DisableAutoFix();
            int tmpChanged = 0, legacyChanged = 0;
            for (int i = 0; i < tmps.Count; i++) if (tmps[i] != null) { tmps[i].ForceMeshUpdate(); if (tmps[i].GetParsedText() != on[i]) tmpChanged++; }
            for (int i = 0; i < legacy.Count; i++) if (legacy[i] != null && !Mathf.Approximately(legacy[i].preferredWidth, onWidth[i])) legacyChanged++;
            Arabic.Log.LogInfo("demo: autofix on the game's own texts: " + tmps.Count + " TextMeshPro and " + legacy.Count + " legacy Text | hook ran " + natural + " times on its own in 5 s and " + forced
                + " times when rebuilt (expect more than 0) | shaped " + shapedWhileOn + " (expect 0) | drawn differently with it off: TMP " + tmpChanged + ", legacy " + legacyChanged + " (expect 0)");
        }

        // A scene unload destroys the notification root and its texts while entries are live: the panel must drop them, keep working, and raise no error
        // (it used to throw every frame, and at expiry, until the game closed). The control: the entries really were live before the root was destroyed.
        IEnumerator DeadRootChecks()
        {
            Arabic.Notify("Dead root check", NotifyKind.Info, 2f);
            yield return new WaitForSeconds(0.5f);
            int before = NotificationPanel.LiveCount;
            GameObject panel = GameObject.Find("GorillaHurouf_Notifications");
            if (panel != null) Destroy(panel);
            yield return new WaitForSeconds(3f);   // past the 2 s expiry of the notification that was in it
            Arabic.Notify("After the dead root", NotifyKind.Info, 1f);
            yield return new WaitForSeconds(0.5f);
            Arabic.Log.LogInfo("demo: dead notification root: live before " + before + " (control: expect more than 0) | live after " + NotificationPanel.LiveCount + " (expect 1, the new one)"
                + " | new root made " + (GameObject.Find("GorillaHurouf_Notifications") != null) + " | error flagged " + NotificationPanel.Failed + " (expect False)");
        }

        // The settings: a changed value must show, and the old value comes back afterwards. Controls: digits are Western until set, and the default colour is not magenta.
        IEnumerator CustomizeChecks()
        {
            ShapeOptions d = Arabic.DefaultOptions;
            string before = Arabic.Fix("هلا 123");
            d.Digits = DigitStyle.ArabicIndic;
            string indic = Arabic.Fix("هلا 123");
            d.Digits = DigitStyle.Keep;
            Arabic.Log.LogInfo("demo: default options: digits Western by default " + before.Contains("123") + " | Arabic-Indic once set " + (indic.Contains("١") && !indic.Contains("1"))
                + " | back to Western " + Arabic.Fix("هلا 123").Contains("123") + " | default warning colour is not magenta " + (NotificationSettings.WarningColor != Color.magenta));

            Vector3 position = NotificationSettings.Position; Color warning = NotificationSettings.WarningColor; float scale = NotificationSettings.Scale; int max = NotificationSettings.MaxEntries;
            NotificationSettings.Position = new Vector3(-0.3f, 0.1f, 1.0f);
            NotificationSettings.WarningColor = Color.magenta;
            NotificationSettings.Scale = 0.02f;
            NotificationSettings.MaxEntries = 2;
            Arabic.Notify("Customize check", NotifyKind.Warning, 3f);
            yield return new WaitForSeconds(0.5f);
            GameObject panel = GameObject.Find("GorillaHurouf_Notifications");
            TextMeshPro newest = panel != null ? panel.GetComponentsInChildren<TextMeshPro>().FirstOrDefault(t => t.text.Contains("Customize")) : null;
            bool at = panel != null && (panel.transform.position - Camera.main.transform.TransformPoint(NotificationSettings.Position)).sqrMagnitude < 0.0001f;
            bool magenta = newest != null && newest.color.r > 0.99f && newest.color.g < 0.01f && newest.color.b > 0.99f;
            Arabic.Log.LogInfo("demo: notification settings: position " + at + " | colour " + magenta + " | scale " + (newest != null && Mathf.Approximately(newest.transform.localScale.x, 0.02f))
                + " | live " + NotificationPanel.LiveCount + " (expect 2)");
            NotificationSettings.Position = position; NotificationSettings.WarningColor = warning; NotificationSettings.Scale = scale; NotificationSettings.MaxEntries = max;
        }

        // step 5: texts set the plain way (.text = ...), with and without auto-fix. Every line says "ok" or "WRONG"; a control must come out as "differs".
        void AutoFixChecks(TMP_FontAsset game)
        {
            string expected = Arabic.Fix(Welcome);
            string expectedTagged = Arabic.Fix("<color=red>" + Mixed + "</color>");

            // the earlier rows and the notifications would cover these, and text under the floor is cut off, so these rows sit high
            foreach (Transform t in root.transform) t.gameObject.SetActive(false);
            GameObject panel = GameObject.Find("GorillaHurouf_Notifications");
            if (panel != null) panel.SetActive(false);

            // controls before it is on: the same plain set draws the raw letters
            TextMeshPro off = Tmp("autofix-off", game, 0.80f); off.text = Welcome;
            string offDrawn = Drawn(off);
            Text legacyOff = Legacy("autofix-legacy-off", 0.12f); legacyOff.text = Welcome;
            float legacyOffWidth = legacyOff.preferredWidth;
            Canvas.ForceUpdateCanvases();   // builds the legacy meshes now, so the one with auto-fix off is really built with it off
            float legacyOffSig = Sig(legacyOff);
            autoExpected = expected; autoLegacyRaw = legacyOffWidth; autoGame = game;
            Arabic.Log.LogInfo("demo: autofix control off: tmp raw differs from fixed=" + (offDrawn != expected) + " | autofix enabled=" + Arabic.AutoFixEnabled);

            Arabic.EnableAutoFix();
            Arabic.EnableAutoFix();   // twice on purpose: must not patch twice
            Arabic.Log.LogInfo("demo: autofix enabled=" + Arabic.AutoFixEnabled);

            TextMeshPro on = Tmp("autofix-on", game, 0.64f); on.text = Welcome;
            string onDrawn = Drawn(on);
            Arabic.Log.LogInfo("demo: autofix tmp drawn==Fix " + (onDrawn == expected ? "ok" : "WRONG") + " | .text kept " + (on.text == Welcome ? "ok" : "WRONG"));
            Report(on);

            TextMeshPro tagged = Tmp("autofix-tags", game, 0.54f); tagged.text = "<color=red>" + Mixed + "</color>";
            Arabic.Log.LogInfo("demo: autofix tags+mixed drawn==Fix " + (Drawn(tagged) == Arabic.Fix(Mixed) ? "ok" : "WRONG (" + Drawn(tagged).Length + " vs " + Arabic.Fix(Mixed).Length + ")") + " | expectedTagged length " + expectedTagged.Length);

            int shapedBefore = AutoFix.Shaped;
            TextMeshPro english = Tmp("autofix-english", game, 0.44f); english.text = "Hello <b>World</b> 123";
            Arabic.Log.LogInfo("demo: autofix english untouched " + (Drawn(english) == "Hello World 123" ? "ok" : "WRONG: " + Drawn(english)) + " | shaped count moved by " + (AutoFix.Shaped - shapedBefore) + " (expect 0)");

            // the same text drawn again (a colour change) must not shape again
            shapedBefore = AutoFix.Shaped;
            on.color = Color.yellow; Drawn(on);
            Arabic.Log.LogInfo("demo: autofix redraw shaped " + (AutoFix.Shaped - shapedBefore) + " more (expect 0)");
            on.color = Color.white; Drawn(on);   // back to white, so a screenshot shows it next to the raw control in the same colour

            // wrapping: a long text in a narrow box wraps on the Arabic text; TMP must not wrap the shaped lines again
            TextMeshPro wrap = Tmp("autofix-wrap", game, 0.34f);
            wrap.textWrappingMode = TextWrappingModes.Normal;
            wrap.alignment = TextAlignmentOptions.TopRight;
            wrap.rectTransform.sizeDelta = new Vector2(40f, 10f);
            wrap.text = LongWarning;
            string wrapDrawn = Drawn(wrap);
            string wrapExpected = Arabic.Wrap(wrap, LongWarning, 40f);
            Arabic.Log.LogInfo("demo: autofix wrap lines=" + wrap.textInfo.lineCount + " expected=" + wrapExpected.Split('\n').Length + " " + (wrap.textInfo.lineCount == wrapExpected.Split('\n').Length ? "ok" : "WRONG") + " | text equal " + (wrapDrawn == wrapExpected));

            // legacy Text: the same plain set; its width must equal a Text that was fixed by hand, and .text stays as set
            Text legacyOn = Legacy("autofix-legacy-on", 0.02f); legacyOn.text = Welcome;
            Text legacyHand = Legacy("autofix-legacy-hand", -0.08f); legacyHand.text = expected;
            Canvas.ForceUpdateCanvases();   // the meshes are built here, while auto-fix is on: compare the real vertices, not only the width
            float sigOn = Sig(legacyOn), sigHand = Sig(legacyHand);
            Arabic.Log.LogInfo("demo: autofix legacy width auto=" + legacyOn.preferredWidth + " by hand=" + legacyHand.preferredWidth + " raw(control)=" + legacyOffWidth
                + " | " + (Mathf.Approximately(legacyOn.preferredWidth, legacyHand.preferredWidth) ? "ok" : "WRONG") + " | .text kept " + (legacyOn.text == Welcome ? "ok" : "WRONG"));
            Arabic.Log.LogInfo("demo: autofix legacy mesh auto=" + sigOn + " by hand=" + sigHand + " raw(control)=" + legacyOffSig + " | " + (Mathf.Approximately(sigOn, sigHand) && !Mathf.Approximately(sigOn, legacyOffSig) ? "ok" : "WRONG"));
        }

        // off again: unpatched, a plain set draws raw letters once more
        string autoExpected;
        float autoLegacyRaw;
        TMP_FontAsset autoGame;

        void AutoFixOffChecks()
        {
            Arabic.DisableAutoFix();
            TextMeshPro after = Tmp("autofix-after", autoGame, 0.72f); after.text = Welcome;
            Text legacyAfter = Legacy("autofix-legacy-after", -0.18f); legacyAfter.text = Welcome;
            Arabic.Log.LogInfo("demo: autofix after disable: enabled=" + Arabic.AutoFixEnabled + " tmp raw differs from fixed=" + (Drawn(after) != autoExpected)
                + " legacy width back to raw=" + Mathf.Approximately(legacyAfter.preferredWidth, autoLegacyRaw) + " | patches left by us: " + HarmonyLib.Harmony.GetAllPatchedMethods().Count(m => HarmonyLib.Harmony.GetPatchInfo(m).Owners.Contains(PluginInfo.GUID + ".autofix")) + " (expect 0)");
        }

        // A number that changes if any vertex of the legacy text's mesh moves: the count plus a weighted sum of the positions.
        static float Sig(Text t)
        {
            float s = 0f;
            foreach (UIVertex v in t.cachedTextGenerator.verts) s += v.position.x * 1.37f + v.position.y * 0.61f;
            return s + t.cachedTextGenerator.verts.Count;
        }

        // Skipping one text, and right-aligning Arabic. Distances are in text units: "gap" is the space between the drawn text and the box's right edge.
        void AutoFixOptionChecks(TMP_FontAsset game)
        {
            string expected = Arabic.Fix(Welcome);
            Arabic.Log.LogInfo("demo: autofix right-align default=" + Arabic.AutoFixRightAlign + " (expect False)");

            TextMeshPro leftOff = Left("ra-off", game, 0.80f); leftOff.text = Welcome; Drawn(leftOff);
            Arabic.AutoFixRightAlign = true;
            TextMeshPro leftOn = Left("ra-on", game, 0.70f); leftOn.text = Welcome; string leftOnDrawn = Drawn(leftOn);
            TextMeshPro centre = Left("ra-centre", game, 0.60f); centre.alignment = TextAlignmentOptions.Center; centre.text = Welcome; Drawn(centre);
            TextMeshPro english = Left("ra-english", game, 0.50f); english.text = "Hello World"; Drawn(english);
            TextMeshPro plain = Left("ra-richtext-off", game, 0.40f); plain.richText = false; plain.text = Welcome; string plainDrawn = Drawn(plain);
            Arabic.Log.LogInfo("demo: autofix right-align gap: off=" + Gap(leftOff).ToString("0.0") + " (control: large) on=" + Gap(leftOn).ToString("0.0") + " (expect ~0)"
                + " | centre offset=" + (centre.textBounds.center.x - centre.rectTransform.rect.center.x).ToString("0.0") + " (expect ~0)"
                + " | english left gap=" + (english.textBounds.min.x - english.rectTransform.rect.xMin).ToString("0.0") + " (expect ~0)"
                + " | rich text off: gap=" + Gap(plain).ToString("0.0") + " (expect large), no literal tag " + (plainDrawn == expected ? "ok" : "WRONG: " + plainDrawn)
                + " | drawn==Fix " + (leftOnDrawn == expected ? "ok" : "WRONG") + " | .text kept " + (leftOn.text == Welcome ? "ok" : "WRONG"));
            Arabic.AutoFixRightAlign = false;

            // skipping: left raw while skipped, fixed again after skip is taken back
            TextMeshPro skipped = Left("skipped", game, 0.30f);
            Arabic.SkipAutoFix(skipped);
            skipped.text = Welcome;
            int shapedBefore = AutoFix.Shaped;
            string skippedDrawn = Drawn(skipped);
            Arabic.Log.LogInfo("demo: autofix skip: drawn raw (differs from fixed) " + (skippedDrawn != expected) + " | shaped moved by " + (AutoFix.Shaped - shapedBefore) + " (expect 0)");
            Arabic.SkipAutoFix(skipped, false);
            Arabic.Log.LogInfo("demo: autofix skip taken back: drawn==Fix " + (Drawn(skipped) == expected ? "ok" : "WRONG"));

            Text legacySkipped = Legacy("skip-legacy", 0.20f); legacySkipped.transform.parent.localPosition += Vector3.right * 0.85f;
            Text legacyFixed = Legacy("skip-legacy-hand", 0.10f); legacyFixed.transform.parent.localPosition += Vector3.right * 0.85f;
            Arabic.SkipAutoFix(legacySkipped);
            legacySkipped.text = Welcome; legacyFixed.text = expected;
            Canvas.ForceUpdateCanvases();
            Arabic.Log.LogInfo("demo: autofix skip legacy: mesh " + (Mathf.Approximately(Sig(legacySkipped), Sig(legacyFixed)) ? "WRONG (it was fixed)" : "left raw, ok"));
        }

        static float Gap(TMP_Text t) { return t.rectTransform.rect.xMax - t.textBounds.max.x; }

        TextMeshPro Left(string label, TMP_FontAsset font, float y)
        {
            TextMeshPro t = Tmp(label, font, y);
            t.alignment = TextAlignmentOptions.Left;
            t.transform.localPosition += Vector3.right * 0.85f;   // a second column, clear of the earlier rows
            return t;
        }

        // Every message-of-the-day style board in the game: what the game calls it, where it is and how its text is set up. Read by reflection:
        // the library does not reference the game's own assembly.
        static List<TMP_Text> MotdDiscovery()
        {
            var motd = new List<TMP_Text>();
            int found = 0;
            foreach (MonoBehaviour mb in Resources.FindObjectsOfTypeAll<MonoBehaviour>())
            {
                if (mb == null || mb.GetType().Name != "PlayFabTitleDataTextDisplay") continue;
                found++;
                object key = mb.GetType().GetProperty("playFabKeyValue")?.GetValue(mb, null);
                var box = mb.GetType().GetField("textBox", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(mb) as TMP_Text;
                string path = mb.name;
                for (Transform p = mb.transform.parent; p != null; p = p.parent) path = p.name + "/" + path;
                string text = box != null ? box.text : "(no text box)";
                if (box != null && "MOTD".Equals(key)) motd.Add(box);
                Arabic.Log.LogInfo("demo: board key='" + key + "' active=" + mb.gameObject.activeInHierarchy + " scene=" + mb.gameObject.scene.IsValid() + " path=" + path
                    + (box == null ? "" : " world=" + box.transform.position.ToString("F2") + " fwd=" + box.transform.forward.ToString("F3"))
                    + (box == null ? "" : " | box=" + box.GetType().Name + " " + box.gameObject.name + " rect=" + box.rectTransform.rect.size + " wrap=" + box.textWrappingMode + " align=" + box.horizontalAlignment
                        + " rich=" + box.richText + " size=" + box.fontSize + " autosize=" + box.enableAutoSizing + " color=" + box.color + " font=" + (box.font != null ? box.font.name : "none")
                        + " | text(" + text.Length + ")='" + (text.Length > 70 ? text.Substring(0, 70) : text).Replace("\n", "\\n") + "'"));
            }
            // the heading above each board: every text in the scene that says "message of the day", where it is and what is on it
            foreach (TMP_Text t in Resources.FindObjectsOfTypeAll<TMP_Text>())
            {
                if (t == null || !t.gameObject.scene.IsValid() || t.text.ToUpperInvariant().IndexOf("MESSAGE OF THE DAY") < 0) continue;
                string p = t.name;
                for (Transform q = t.transform.parent; q != null; q = q.parent) p = q.name + "/" + p;
                Arabic.Log.LogInfo("demo: heading '" + p + "' = '" + t.text.Replace("\n", "\\n") + "' active=" + t.gameObject.activeInHierarchy + " world=" + t.transform.position.ToString("F2")
                    + " color=" + t.color + " font=" + (t.font != null ? t.font.name : "none") + " | components: " + string.Join(",", t.GetComponents<Component>().Select(c => c == null ? "null" : c.GetType().Name).ToArray()));
            }
            Arabic.Log.LogInfo("demo: boards found=" + found + ", with the MOTD key " + motd.Count);
            return motd;
        }

        const string MotdTitle = "رسالة اليوم";
        const string MotdText ="تنبيه: فيه صيانة بكرة بعد صلاة العصر، يعطيكم العافية على تفهمكم";

        // The message-of-the-day board: our text and colour on it, put back when the game rewrites it, and the game's own text back after Clear.
        IEnumerator MotdChecks(List<TMP_Text> found)
        {
            root.SetActive(false);   // the demo's own rows would cover the board in a screenshot
            var gameText = new Dictionary<TMP_Text, string>();
            var gameColor = new Dictionary<TMP_Text, Color>();
            foreach (TMP_Text b in found) { gameText[b] = b.text; gameColor[b] = b.color; }

            Arabic.SetMessageOfTheDay(MotdText, Color.green);
            yield return new WaitForSeconds(1f);
            List<TMP_Text> managed = MessageOfTheDay.Boxes;
            Arabic.Log.LogInfo("demo: motd boards managed=" + managed.Count + " (discovery found " + found.Count + ") " + MotdState(managed, Color.green));

            // what the game does on a language change or new data: writes its own text again, and the "new" colour
            foreach (TMP_Text b in managed) { b.text = gameText.ContainsKey(b) ? gameText[b] : "GAME TEXT"; b.color = Color.magenta; }
            yield return new WaitForSeconds(0.3f);
            Arabic.Log.LogInfo("demo: motd after the game rewrote it: " + MotdState(managed, Color.green));
            // the heading above the board: ours, put back after the game's LocalizationTextSyncer rewrites it, the game's own back after a clear
            Arabic.SetMessageOfTheDayTitle(MotdTitle, Color.cyan);
            yield return new WaitForSeconds(1f);
            List<TMP_Text> heads = MessageOfTheDay.Headings;
            string wantTitle = Arabic.Fix(MotdTitle);
            Arabic.Log.LogInfo("demo: motd title on " + heads.Count(h => h.text == wantTitle) + " of " + heads.Count + " headings (expect 3 of 3), cyan on " + heads.Count(h => h.color.r < 0.01f && h.color.g > 0.99f) + " (expect 3)");
            foreach (TMP_Text h in heads) h.text = "MESSAGE OF THE DAY";
            yield return new WaitForSeconds(0.3f);
            Arabic.Log.LogInfo("demo: motd title after the game rewrote it: ours on " + heads.Count(h => h.text == wantTitle) + " of " + heads.Count + " (expect 3 of 3)");
            Arabic.Log.LogInfo("demo: motd up");
            yield return new WaitForSeconds(25f);   // time to stand in front of the board for a screenshot

            Arabic.ClearMessageOfTheDay();
            Arabic.SetMessageOfTheDayTitle(null);
            yield return new WaitForSeconds(0.5f);
            Arabic.Log.LogInfo("demo: motd title cleared: the game's heading is back on " + heads.Count(h => h != null && h.text == "MESSAGE OF THE DAY" && h.color.r > 0.99f && h.color.g > 0.99f && h.color.b > 0.99f) + " of " + heads.Count + " (expect 3 of 3)");
            int back = 0;
            foreach (TMP_Text b in managed) if (b != null && gameText.ContainsKey(b) && b.text == gameText[b] && b.color == gameColor[b]) back++;
            Arabic.Log.LogInfo("demo: motd cleared: the game's own text and colour are back on " + back + " of " + managed.Count + " boards (expect all)");
        }

        static string MotdState(List<TMP_Text> boards, Color wantColor)
        {
            int textOk = 0, colourOk = 0;
            foreach (TMP_Text b in boards)
            {
                if (b.text == Arabic.Render(b, MotdText, Arabic.WrapWidth(b), Arabic.WantsRightAlign(b, true))) textOk++;
                if (b.color == wantColor) colourOk++;
            }
            return "| our text on " + textOk + " of " + boards.Count + ", our colour on " + colourOk + " of " + boards.Count + " (expect all)";
        }

        static string Drawn(TMP_Text t) { t.ForceMeshUpdate(); return t.GetParsedText(); }

        // A legacy Text on a small world-space canvas, with the built-in font.
        Text Legacy(string label, float y)
        {
            var canvasGo = new GameObject(label);
            canvasGo.transform.SetParent(root.transform, false);
            canvasGo.transform.localPosition = new Vector3(-0.40f, y, 0f);
            canvasGo.transform.localScale = Vector3.one * 0.002f;   // 24 px letters then stand about 5 cm tall, like the TextMeshPro rows
            canvasGo.AddComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            ((RectTransform)canvasGo.transform).sizeDelta = new Vector2(400f, 40f);
            var go = new GameObject("Text");
            go.transform.SetParent(canvasGo.transform, false);
            Text t = go.AddComponent<Text>();
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = 24;
            t.alignment = TextAnchor.UpperCenter;
            t.rectTransform.sizeDelta = new Vector2(400f, 40f);   // the default 100 x 100 wraps a line in two, and wrapped shaped text reads wrong
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            return t;
        }

        TextMeshPro Tmp(string label, TMP_FontAsset font, float y)
        {
            var go = new GameObject(label);
            go.transform.SetParent(root.transform, false);
            go.transform.localPosition = new Vector3(-0.40f, y, 0f);
            go.transform.localScale = Vector3.one * 0.01f;
            var tmp = go.AddComponent<TextMeshPro>();
            if (font != null) tmp.font = font;
            tmp.fontSize = 36;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.rectTransform.sizeDelta = new Vector2(80f, 10f);
            return tmp;
        }

        // The notification meshes (including the fallback-font child meshes) must be on a layer the main camera draws and the spectator camera does not.
        static void LogLayers()
        {
            GameObject panel = GameObject.Find("GorillaHurouf_Notifications");
            var layers = new SortedDictionary<int, int>();
            if (panel != null)
                foreach (Transform t in panel.GetComponentsInChildren<Transform>(true))
                    if (t != panel.transform) { int k; layers.TryGetValue(t.gameObject.layer, out k); layers[t.gameObject.layer] = k + 1; }
            var parts = new List<string>();
            foreach (var kv in layers) parts.Add("layer " + kv.Key + " x" + kv.Value);
            Arabic.Log.LogInfo("demo: notification objects: " + string.Join(", ", parts.ToArray()) + " (spectators " + (Arabic.HideNotificationsFromSpectators ? "do not see them" : "see them") + ")");
            foreach (Camera cm in Resources.FindObjectsOfTypeAll<Camera>())
                if (cm.gameObject.activeInHierarchy && (cm == Camera.main || cm.name == "Shoulder Camera"))
                    foreach (int layer in layers.Keys)
                        Arabic.Log.LogInfo("demo: camera '" + cm.name + "' draws layer " + layer + " = " + ((cm.cullingMask >> layer & 1) == 1));
        }

#if HUROUF_VR
        // For wearing a headset (build with HUROUF_VR): waits until the player has loaded, then puts every feature in the world
        // 2 m ahead (not stuck to the head), turns auto-fix on and leaves it on, keeps the message of the day, and sends a notification every 25 s.
        internal class VrDemo : MonoBehaviour
        {
            const string PlayerPath = "Player Objects/Player VR Controller/GorillaPlayer";
            GameObject root;
            TMP_FontAsset game;

            void OnDestroy() { if (root != null) Destroy(root); }

            IEnumerator Start()
            {
                // the player rig (found by its scene path: the library does not reference the game's assembly) exists once the world has loaded
                while (Camera.main == null || GameObject.Find(PlayerPath) == null) yield return new WaitForSeconds(0.5f);
                Arabic.Log.LogInfo("vr: player loaded at " + Time.realtimeSinceStartup.ToString("0") + " s, starting in 3 s");
                yield return new WaitForSeconds(3f);
                Transform cam = Camera.main.transform;
                game = Arabic.GameFont;
                Arabic.EnableAutoFix();

                Vector3 fwd = Vector3.ProjectOnPlane(cam.forward, Vector3.up);
                fwd = fwd.sqrMagnitude < 0.01f ? Vector3.forward : fwd.normalized;
                root = new GameObject("GorillaHurouf_VrDemo");
                // left and a little low: the notifications sit up and to the right of the head
                root.transform.position = cam.position + fwd * 2f - Vector3.Cross(Vector3.up, fwd) * 0.5f + Vector3.down * 0.1f;
                root.transform.rotation = Quaternion.LookRotation(fwd);

                Row("SetArabic", 0.45f).SetArabic(Welcome);
                Row("mixed, brackets, digits", 0.31f).SetArabic(Mixed);
                Row("auto-fix: plain .text =", 0.17f).text = Welcome;
                Row("auto-fix: with tags", 0.03f).text = "<color=red>" + Mixed + "</color>";
                Translator tr = Arabic.Translator("VrDemo");
                tr.LoadJson("{ \"hello\": { \"en\": \"Hello {0}\", \"ar\": \"هلا يا {0}\" } }");
                Arabic.Language = "ar";
                Row("translator (ar)", -0.11f).SetArabic(tr.T("hello", "Hex"));
                Arabic.Language = null;
                TextMeshPro hijriRow = Row("Hijri today", -0.25f);
                string hijri = Arabic.Hijri(null, true);
                hijriRow.SetArabic(hijri);
                hijriRow.ForceMeshUpdate();
                Arabic.Log.LogInfo("vr: hijri row drawn exactly once-fixed (auto-fix on) " + (hijriRow.GetParsedText() == Arabic.Fix(hijri)) + " | '" + hijri + "'");
                TextMeshPro control = Row("control: raw, skipped (must look broken)", -0.39f);
                Arabic.EnableFor(control); Arabic.SkipAutoFix(control); control.text = Welcome;
                LegacyRow("auto-fix: legacy Text", -0.53f).text = Welcome;
                TextMeshPro wrap = Row("auto-fix: wrapped, narrow box", -0.67f);
                wrap.textWrappingMode = TextWrappingModes.Normal;
                wrap.alignment = TextAlignmentOptions.TopRight;
                wrap.rectTransform.sizeDelta = new Vector2(40f, 10f);
                wrap.text = LongWarning;

                Arabic.Notify(Saved, NotifyKind.Success, 120f, "Hurouf");
                Arabic.Notify(LongWarning, NotifyKind.Warning, 120f);
                Arabic.Notify(Joined, NotifyKind.Info, 120f, "Room");
                Arabic.Notify("Plain English notification that is long enough to need wrapping onto a second line", NotifyKind.Error, 120f);
                Arabic.Notify(Short, Color.cyan, 120f);
                Arabic.Notify(Short, Color.cyan, 120f);
                Arabic.SetMessageOfTheDay(MotdText, Color.green);
                Arabic.SetMessageOfTheDayTitle(MotdTitle, Color.green);
                Arabic.Log.LogInfo("vr: up (rows 2 m ahead, auto-fix on, message of the day set, notifications)");

                string[] more = { Saved, Joined, Brief };
                for (int i = 0; ; i++)
                {
                    yield return new WaitForSeconds(25f);
                    Arabic.Notify(more[i % more.Length], NotifyKind.Info, 8f, "Hurouf");
                }
            }

            TextMeshPro Row(string label, float y)
            {
                TextMeshPro l = Make("label", -1.0f, y, 28, TextAlignmentOptions.Left, 90f);
                l.color = new Color(0.7f, 0.7f, 0.7f);
                l.text = label;
                return Make("row", 0.25f, y, 36, TextAlignmentOptions.Center, 80f);
            }

            TextMeshPro Make(string name, float x, float y, float size, TextAlignmentOptions align, float width)
            {
                var go = new GameObject(name);
                go.transform.SetParent(root.transform, false);
                go.transform.localPosition = new Vector3(x, y, 0f);
                go.transform.localScale = Vector3.one * 0.01f;
                var tmp = go.AddComponent<TextMeshPro>();
                if (game != null) tmp.font = game;
                tmp.fontSize = size;
                tmp.alignment = align;
                tmp.textWrappingMode = TextWrappingModes.NoWrap;
                tmp.rectTransform.sizeDelta = new Vector2(width, 10f);
                return tmp;
            }

            Text LegacyRow(string label, float y)
            {
                TextMeshPro l = Make("label", -1.0f, y, 28, TextAlignmentOptions.Left, 90f);
                l.color = new Color(0.7f, 0.7f, 0.7f);
                l.text = label;
                var canvasGo = new GameObject("legacy");
                canvasGo.transform.SetParent(root.transform, false);
                canvasGo.transform.localPosition = new Vector3(0.25f, y, 0f);
                canvasGo.transform.localScale = Vector3.one * 0.002f;
                canvasGo.AddComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                ((RectTransform)canvasGo.transform).sizeDelta = new Vector2(400f, 40f);
                var go = new GameObject("Text");
                go.transform.SetParent(canvasGo.transform, false);
                Text t = go.AddComponent<Text>();
                t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                t.fontSize = 24;
                t.alignment = TextAnchor.MiddleCenter;
                t.rectTransform.sizeDelta = new Vector2(400f, 40f);
                t.horizontalOverflow = HorizontalWrapMode.Overflow;
                return t;
            }
        }
#endif

        // Which font asset drew the visible characters of this text.
        static void Report(TextMeshPro tmp)
        {
            tmp.ForceMeshUpdate();
            var used = new Dictionary<string, int>();
            for (int i = 0; i < tmp.textInfo.characterCount; i++)
            {
                TMP_CharacterInfo ci = tmp.textInfo.characterInfo[i];
                if (!ci.isVisible) continue;
                string n = ci.fontAsset != null ? ci.fontAsset.name : "none";
                int k; used.TryGetValue(n, out k); used[n] = k + 1;
            }
            var parts = new List<string>();
            foreach (var kv in used) parts.Add(kv.Key + "=" + kv.Value);
            Arabic.Log.LogInfo("demo: " + tmp.gameObject.name + " visible " + string.Join(", ", parts.ToArray()) + " | width=" + tmp.preferredWidth);
        }
    }
}
#endif
