using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace GorillaHurouf
{
    // Opt-in (Arabic.EnableAutoFix): fixes Arabic in any TextMeshPro or legacy Text as it is drawn, with the text its owner set left as it was
    // (.text still returns the original, so a networked name or anything sent anywhere is never touched).
    // TextMeshPro: every way of setting a text (text, SetText(string), measuring with GetPreferredValues(string)) ends in the private
    // TMP_Text.PopulateTextBackingArray(string, int, int); a prefix there swaps in the shaped string. The legacy Text half is in AutoFix.LegacyText.cs.
    // Not covered: SetText with a StringBuilder or char[], and a text with Arabic that is already fixed (it is left alone, as Fix does).
    static partial class AutoFix
    {
        sealed class State { public string Source, Result; public float Width; public bool Right; public TMP_FontAsset Font; }

        static Harmony harmony;
        static bool busy, failed;
        static ConditionalWeakTable<TMP_Text, State> states = new ConditionalWeakTable<TMP_Text, State>();

        // Texts a mod asked auto-fix to leave alone (Arabic.SkipAutoFix). A text is a key, so a destroyed text drops out by itself.
        static readonly ConditionalWeakTable<UnityEngine.Object, object> skipped = new ConditionalWeakTable<UnityEngine.Object, object>();
        static readonly object Marker = new object();

        /// <summary>Arabic.AutoFixRightAlign: a left-aligned text that gets Arabic is drawn right-aligned (TextMeshPro only).</summary>
        internal static bool RightAlign;

        internal static void Skip(UnityEngine.Object text, bool skip)
        {
            if (text == null) return;
            skipped.Remove(text);
            if (skip) skipped.Add(text, Marker);
        }

        static bool IsSkipped(UnityEngine.Object text)
        {
            object ignored;
            return skipped.TryGetValue(text, out ignored);
        }

        /// <summary>How many texts were actually shaped. The demo checks that English text does not move it.</summary>
        internal static int Shaped;

        /// <summary>How many times the hook ran at all (the demo checks that it sees the game's own texts).</summary>
        internal static int Calls;

        internal static bool Enabled { get { return harmony != null; } }

        internal static void Enable()
        {
            if (harmony != null) return;
            MethodInfo populate = AccessTools.Method(typeof(TMP_Text), "PopulateTextBackingArray", new[] { typeof(string), typeof(int), typeof(int) });
            if (populate == null) { Arabic.Log.LogError("Auto-fix stays off: TMP_Text.PopulateTextBackingArray(string, int, int) was not found (a different TextMeshPro version?)"); return; }
            var h = new Harmony(PluginInfo.GUID + ".autofix");
            try
            {
                h.Patch(populate, prefix: new HarmonyMethod(typeof(AutoFix), nameof(PopulatePrefix)));
                PatchLegacyText(h);
            }
            catch (Exception e)
            {
                h.UnpatchSelf();
                Arabic.Log.LogError("Auto-fix stays off: " + e.Message);
                return;
            }
            harmony = h;
            failed = false;
            Arabic.Log.LogInfo("Auto-fix is on");
        }

        internal static void Disable()
        {
            if (harmony == null) return;
            harmony.UnpatchSelf();   // ScriptEngine does not clean up after a reload, so Plugin.OnDestroy comes here
            harmony = null;
            states = new ConditionalWeakTable<TMP_Text, State>();
            Arabic.Log.LogInfo("Auto-fix is off");
        }

        // Runs for every TextMeshPro text each time its mesh is built, so the common case (no Arabic) is one scan of the string and nothing else.
        static void PopulatePrefix(TMP_Text __instance, ref string sourceText, ref int start, ref int length)
        {
            Calls++;
            // busy: our own measuring (GetPreferredValues) comes back through here. Only whole strings are texts; the other overloads are left alone.
            if (busy || sourceText == null || start != 0 || length != sourceText.Length || !ArabicShaper.NeedsShaping(sourceText)) return;
            if (IsSkipped(__instance)) return;
            busy = true;
            try
            {
                State st = states.GetOrCreateValue(__instance);
                TMP_FontAsset font = __instance.font;
                if (st.Font != font)   // first Arabic for this text, or its font was changed: give it the Arabic font and turn kerning off
                {
                    Arabic.EnableFor(__instance, true);
                    st.Font = font;
                    st.Source = null;
                }

                // Wrapping and the <align=right> tag: see Arabic.WrapWidth and Arabic.WantsRightAlign. The tag is drawn, not part of .text, and the text's
                // own alignment is not touched (changing it while the text is built would queue a rebuild inside the rebuild).
                float width = Arabic.WrapWidth(__instance);
                bool right = Arabic.WantsRightAlign(__instance, RightAlign);

                if (st.Source != sourceText || st.Width != width || st.Right != right)   // the same text drawn again (alpha, a colour change) costs nothing
                {
                    st.Result = Arabic.Render(__instance, sourceText, width, right);
                    st.Source = sourceText;
                    st.Width = width;
                    st.Right = right;
                    Shaped++;
                }
                sourceText = st.Result;
                length = sourceText.Length;
            }
            catch (Exception e)   // a failed fix must leave the game's text as it was, and say so once
            {
                if (!failed) { failed = true; Arabic.Log.LogError("Auto-fix skipped a text: " + e); }
            }
            finally { busy = false; }
        }
    }
}
