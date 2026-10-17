using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace GorillaHurouf
{
    /// <summary>Ready-made colours for <c>Arabic.Notify</c>.</summary>
    public enum NotifyKind { Info, Success, Warning, Error }

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

        const int MaxEntries = 6;
        const float FadeIn = 0.15f, FadeOut = 0.6f;
        const float Width = 80f;     // in text units: how wide a line gets before it is wrapped
        const float Scale = 0.012f;  // metres per text unit
        const float Gap = 0.012f;    // metres between notifications
        // Layer 19 "FirstPersonOnly": the main camera draws it and the spectator ("Shoulder") camera does not (read from their culling masks).
        const int SpectatorHiddenLayer = 19;
        static readonly Vector3 Offset = new Vector3(0.55f, 0.40f, 1.4f);   // right edge, top edge and distance from the camera

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

        void Update()
        {
            float now = Time.unscaledTime;
            if (pending.Count > 0 && Camera.main != null)
            {
                if (root == null) root = new GameObject("GorillaHurouf_Notifications");
                foreach (Request r in pending) Add(r, now);
                pending.Clear();
            }
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

        void LateUpdate()
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
            TMP_FontAsset font = Arabic.GameFont ?? Arabic.FontAsset;
            if (font != null) tmp.font = font;
            tmp.fontSize = 36;
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
            string wrapped = Arabic.Wrap(e.Tmp, text, Width, new ShapeOptions { Direction = arabic ? TextDirection.Rtl : TextDirection.Auto });
            e.Tmp.text = wrapped;
            e.Tmp.ForceMeshUpdate();
            e.Height = e.Tmp.GetRenderedValues(false).y * Scale;
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
