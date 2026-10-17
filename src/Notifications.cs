using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace GorillaHurouf
{
    /// <summary>Ready-made colours for <c>Arabic.Notify</c>.</summary>
    public enum NotifyKind { Info, Success, Warning, Error }

    /// <summary>How notifications look and where they sit. Set these once from your mod (in <c>Awake</c>, say).
    /// Position, Gap, MaxEntries, the fades and the colours of new notifications change at once; Scale, Width, FontSize and Font apply to notifications made after the change.</summary>
    public static class NotificationSettings
    {
        /// <summary>Where the stack sits, in metres from the camera: x = right edge, y = top edge, z = distance. Default (0.55, 0.40, 1.4). Negative x moves it left of the view's centre.</summary>
        public static Vector3 Position { get; set; } = new Vector3(0.55f, 0.40f, 1.4f);
        /// <summary>Metres per text unit, so the size of the letters. Default 0.012.</summary>
        public static float Scale { get; set; } = 0.012f;
        /// <summary>How wide a line gets before it wraps, in text units. Default 80.</summary>
        public static float Width { get; set; } = 80f;
        /// <summary>Font size in text units. Default 36.</summary>
        public static float FontSize { get; set; } = 36f;
        /// <summary>Metres between two notifications. Default 0.012.</summary>
        public static float Gap { get; set; } = 0.012f;
        /// <summary>How many show at once; the oldest go first. Default 6.</summary>
        public static int MaxEntries { get; set; } = 6;
        /// <summary>Seconds to fade in and out. Defaults 0.15 and 0.6.</summary>
        public static float FadeIn { get; set; } = 0.15f;
        public static float FadeOut { get; set; } = 0.6f;
        /// <summary>How long a notification stays when <c>Notify</c> is called without seconds. Default 4.</summary>
        public static float DefaultSeconds { get; set; } = 4f;
        /// <summary>Your own font for notifications. Null (default) uses the game's font, with the Arabic font behind it.</summary>
        public static TMP_FontAsset Font { get; set; }
        /// <summary>The colours of the four <see cref="NotifyKind"/> values.</summary>
        public static Color InfoColor { get; set; } = Color.white;
        public static Color SuccessColor { get; set; } = new Color(0.45f, 0.90f, 0.50f);
        public static Color WarningColor { get; set; } = new Color(1.00f, 0.80f, 0.30f);
        public static Color ErrorColor { get; set; } = new Color(1.00f, 0.40f, 0.40f);

        internal static Color ColorOf(NotifyKind kind)
        {
            switch (kind)
            {
                case NotifyKind.Success: return SuccessColor;
                case NotifyKind.Warning: return WarningColor;
                case NotifyKind.Error: return ErrorColor;
                default: return InfoColor;
            }
        }
    }

    // Draws the notifications in front of the player's view. Arabic.Notify only queues a request; this component (one, on the plugin) shows it.
    class NotificationPanel : MonoBehaviour
    {
        sealed class Request { public string Text, Source; public Color Color; public float Seconds; }

        sealed class Entry
        {
            public TextMeshPro Tmp;
            public string Text, Source;
            public Color Color;
            public int Count = 1;
            public float Born, Expires, Height;
        }

        // the settings, kept safe to use (a zero width or fade would break the layout)
        static int MaxEntries { get { return Mathf.Max(1, NotificationSettings.MaxEntries); } }
        static float FadeIn { get { return Mathf.Max(0.01f, NotificationSettings.FadeIn); } }
        static float FadeOut { get { return Mathf.Max(0.01f, NotificationSettings.FadeOut); } }
        static float Width { get { return Mathf.Max(1f, NotificationSettings.Width); } }
        static float Scale { get { return Mathf.Max(0.0001f, NotificationSettings.Scale); } }
        static float Gap { get { return NotificationSettings.Gap; } }
        static Vector3 Offset { get { return NotificationSettings.Position; } }
        // Layer 19 "FirstPersonOnly": the main camera draws it and the spectator ("Shoulder") camera does not (read from their culling masks).
        const int SpectatorHiddenLayer = 19;

        static readonly List<Request> pending = new List<Request>();

        readonly List<Entry> entries = new List<Entry>();
        GameObject root;
        int layer;

        /// <summary>How many notifications are on screen now.</summary>
        internal static int LiveCount { get; private set; }

        internal static void Enqueue(string text, string source, Color color, float seconds)
        {
            if (pending.Count >= 50) pending.RemoveAt(0);   // nothing draws before the camera exists: keep the latest
            pending.Add(new Request { Text = text, Source = source, Color = color, Seconds = seconds });
        }

        /// <summary>True once a step threw: the error was logged (once) and the panel carries on with the next frame.</summary>
        internal static bool Failed { get; private set; }

        void Update() { try { Tick(); } catch (Exception e) { Fail("Update", e); } }
        void LateUpdate() { try { Follow(); } catch (Exception e) { Fail("LateUpdate", e); } }

        // Unity's own exception log does not reach LogOutput.log here, so a failure is logged once through ours instead of repeating silently every frame.
        static void Fail(string where, Exception e)
        {
            if (Failed) return;
            Failed = true;
            Arabic.Log.LogError("Notifications failed in " + where + " (further errors are not logged): " + e);
        }

        void Tick()
        {
            float now = Time.unscaledTime;
            if (pending.Count > 0 && Camera.main != null)
            {
                if (root == null)
                {
                    root = new GameObject("GorillaHurouf_Notifications");
                    DontDestroyOnLoad(root);   // the game loads and unloads scenes while it starts: a root left in one of them is destroyed with it
                }
                foreach (Request r in pending) Add(r, now);
                pending.Clear();
            }
            entries.RemoveAll(e => e.Tmp == null);   // texts something else destroyed: drop them, or every frame throws on them
            for (int i = entries.Count - 1; i >= 0; i--)
                if (now >= entries[i].Expires) { Destroy(entries[i].Tmp.gameObject); entries.RemoveAt(i); }
            LiveCount = entries.Count;
            int wanted = Arabic.HideNotificationsFromSpectators ? SpectatorHiddenLayer : 0;
            if (wanted != layer) { layer = wanted; foreach (Entry e in entries) SetLayer(e.Tmp); }
            Layout(now);
        }

        // The text's own object and the child meshes TextMeshPro makes for fallback fonts.
        void SetLayer(TMP_Text text)
        {
            foreach (Transform t in text.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
        }

        void Follow()
        {
            Camera main = Camera.main;
            if (root == null || main == null) return;
            root.transform.position = main.transform.TransformPoint(Offset);
            root.transform.rotation = main.transform.rotation;
        }

        void OnDestroy()
        {
            if (root != null) Destroy(root);
            entries.Clear();
            LiveCount = 0;
        }

        void Add(Request r, float now)
        {
            Entry e = entries.Find(x => x.Text == r.Text && x.Source == r.Source && x.Color == r.Color);
            if (e != null)   // the same notification again: count it instead of stacking a copy
            {
                e.Count++;
                e.Expires = now + r.Seconds;
                Fill(e);
                return;
            }

            var go = new GameObject("Notification");
            go.transform.SetParent(root.transform, false);
            go.transform.localScale = Vector3.one * Scale;
            var tmp = go.AddComponent<TextMeshPro>();
            TMP_FontAsset font = NotificationSettings.Font ?? Arabic.GameFont ?? Arabic.FontAsset;
            if (font != null) tmp.font = font;
            tmp.fontSize = NotificationSettings.FontSize;
            tmp.alignment = TextAlignmentOptions.TopRight;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;   // lines are wrapped before shaping, not by TextMeshPro
            tmp.color = r.Color;
            tmp.rectTransform.pivot = new Vector2(1f, 1f);
            tmp.rectTransform.sizeDelta = new Vector2(Width, 10f);

            e = new Entry { Tmp = tmp, Text = r.Text, Source = r.Source, Color = r.Color, Born = now, Expires = now + r.Seconds };
            Fill(e);
            entries.Insert(0, e);   // newest on top
            while (entries.Count > MaxEntries)
            {
                Destroy(entries[entries.Count - 1].Tmp.gameObject);
                entries.RemoveAt(entries.Count - 1);
            }
        }

        void Fill(Entry e)
        {
            string text = (string.IsNullOrEmpty(e.Source) ? "" : "[" + e.Source + "] ") + e.Text + (e.Count > 1 ? " ×" + e.Count : "");
            bool arabic = Arabic.IsArabic(e.Text);
            if (arabic) Arabic.EnableFor(e.Tmp);
            // a "[Source]" in front must not turn an Arabic line left to right, so Arabic text is always laid out right to left
            string wrapped = Arabic.Wrap(e.Tmp, text, Width, Arabic.DefaultWith(arabic ? TextDirection.Rtl : TextDirection.Auto));
            e.Tmp.text = wrapped;
            e.Tmp.ForceMeshUpdate();
            e.Height = e.Tmp.GetRenderedValues(false).y * e.Tmp.transform.localScale.x;
            SetLayer(e.Tmp);   // after the mesh exists, so the fallback child meshes get the layer too
#if HUROUF_DEMO
            Arabic.Log.LogInfo("notify: " + e.Text.Length + " chars x" + e.Count + " -> lines=" + wrapped.Split('\n').Length + " height=" + e.Height.ToString("0.000") + "m");
#endif
        }

        void Layout(float now)
        {
            float y = 0f;
            foreach (Entry e in entries)
            {
                e.Tmp.transform.localPosition = new Vector3(0f, -y, 0f);
                float a = Mathf.Clamp01((now - e.Born) / FadeIn) * Mathf.Clamp01((e.Expires - now) / FadeOut);
                if (Mathf.Abs(e.Tmp.alpha - a) > 0.003f) e.Tmp.alpha = a;
                y += e.Height + Gap;
            }
        }
    }
}
