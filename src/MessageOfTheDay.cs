using System;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;

namespace GorillaHurouf
{
    // Puts a mod's own text (Arabic or not) on the game's message-of-the-day board(s), in a colour. Local only: nothing is sent, other players see their own board.
    // A board is the game's PlayFabTitleDataTextDisplay with the title-data key "MOTD". It writes its text and colour from the server's data at start, when the
    // language changes and when new data arrives, so this component looks at the boards every frame and puts the custom text back (and puts the game's text back
    // when the custom one is cleared). The game's type is read by name and reflection: the library does not reference the game's own assembly, so a game update
    // cannot break the build; at worst this feature finds no board (and says so once in the log).
    class MessageOfTheDay : MonoBehaviour
    {
        const string BoardType = "PlayFabTitleDataTextDisplay";
        const string BoardKey = "MOTD";
        const float RescanSeconds = 10f;      // boards can appear later (a scene loads); finding them walks every component, so not every frame
        const float WarnAfterSeconds = 60f;

        sealed class Board
        {
            public MonoBehaviour Source;
            public TMP_Text Box;
            public string GameText, Applied;
            public Color GameColor;
            public bool ColorTaken;
            public int Version = -1;
            public float Width;
        }

        static string text;             // the custom text; null = leave the game's own
        static Color? color;
        static int version;             // bumped by every Set, so each board rebuilds its text
        static PropertyInfo keyProperty;
        static FieldInfo boxField;
        static MessageOfTheDay instance;

        readonly List<Board> boards = new List<Board>();
        float nextScan, setTime;
        bool warned;

        internal static void Set(string value, Color? tint)
        {
            text = string.IsNullOrEmpty(value) ? null : value;
            color = text == null ? null : tint;
            version++;
            if (instance != null) instance.setTime = Time.unscaledTime;
            if (instance != null) instance.nextScan = 0f;
        }

        /// <summary>The boards being managed now (the demo reads them to check what was drawn).</summary>
        internal static List<TMP_Text> Boxes
        {
            get
            {
                var list = new List<TMP_Text>();
                if (instance != null) foreach (Board b in instance.boards) if (b.Box != null) list.Add(b.Box);
                return list;
            }
        }

        void Awake() { instance = this; }

        void OnDestroy()
        {
            foreach (Board b in boards) Restore(b);   // a reload must leave the game's own text on the board
            boards.Clear();
            if (instance == this) instance = null;
        }

        void LateUpdate()
        {
            boards.RemoveAll(b => b.Box == null);
            if (text == null)
            {
                foreach (Board b in boards) Restore(b);
                boards.Clear();
                return;
            }

            float now = Time.unscaledTime;
            if (now >= nextScan)
            {
                nextScan = now + RescanSeconds;
                Scan();
                if (boards.Count == 0 && !warned && now - setTime > WarnAfterSeconds)
                {
                    warned = true;
                    Arabic.Log.LogWarning("No message-of-the-day board was found (looked for a " + BoardType + " with the key " + BoardKey + "); the game may have changed.");
                }
            }
            foreach (Board b in boards) Apply(b);
        }

        void Scan()
        {
            foreach (MonoBehaviour mb in Resources.FindObjectsOfTypeAll<MonoBehaviour>())
            {
                if (mb == null || mb.GetType().Name != BoardType || !mb.gameObject.scene.IsValid() || boards.Exists(b => b.Source == mb)) continue;
                if (keyProperty == null) keyProperty = mb.GetType().GetProperty("playFabKeyValue");
                if (boxField == null) boxField = mb.GetType().GetField("textBox", BindingFlags.NonPublic | BindingFlags.Instance);
                if (keyProperty == null || boxField == null) continue;
                string key;
                TMP_Text box;
                try { key = keyProperty.GetValue(mb, null) as string; box = boxField.GetValue(mb) as TMP_Text; }
                catch (Exception e) { Arabic.Log.LogWarning("Could not read a message-of-the-day board: " + e.Message); continue; }
                if (box != null && string.Equals(key, BoardKey, StringComparison.OrdinalIgnoreCase)) boards.Add(new Board { Source = mb, Box = box });
            }
        }

        void Apply(Board b)
        {
            TMP_Text box = b.Box;
            string current = box.text;
            float width = Arabic.WrapWidth(box);
            if (b.Version != version || b.Width != width)
            {
                if (b.Applied == null || current != b.Applied) b.GameText = current;   // what the game has there, to put back
                if (Arabic.IsArabic(text))
                {
                    Arabic.EnableFor(box);
                    b.Applied = Arabic.Render(box, text, width, Arabic.WantsRightAlign(box, true));
                }
                else b.Applied = text;
                b.Version = version;
                b.Width = width;
                box.text = b.Applied;
            }
            else if (current != b.Applied)   // the game wrote its own text again (a language change, new title data)
            {
                b.GameText = current;
                box.text = b.Applied;
            }

            if (color.HasValue)
            {
                if (!b.ColorTaken) { b.GameColor = box.color; b.ColorTaken = true; }
                if (box.color != color.Value) box.color = color.Value;   // the game turns the text magenta when its data is new
            }
            else if (b.ColorTaken) { box.color = b.GameColor; b.ColorTaken = false; }
        }

        static void Restore(Board b)
        {
            if (b.Box == null || b.Applied == null) return;
            if (b.Box.text == b.Applied) b.Box.text = b.GameText;
            if (b.ColorTaken) b.Box.color = b.GameColor;
            b.Applied = null;
            b.ColorTaken = false;
        }
    }
}
