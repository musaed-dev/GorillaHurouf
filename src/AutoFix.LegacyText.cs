using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine.UI;

namespace GorillaHurouf
{
    // The legacy Text half of auto-fix. Text reads its private m_Text when it builds its mesh and when it reports its preferred size, so
    // for those three calls m_Text holds the shaped string and is put back afterwards: .text keeps returning what the owner set.
    // No wrapping here: a long legacy Text needs Arabic.Fix per line from its owner (the game uses it for short names).
    static partial class AutoFix
    {
        static FieldInfo textField;

        static void PatchLegacyText(Harmony h)
        {
            textField = AccessTools.Field(typeof(Text), "m_Text");
            if (textField == null) throw new MissingFieldException("UnityEngine.UI.Text.m_Text was not found");
            var prefix = new HarmonyMethod(typeof(AutoFix), nameof(TextPrefix));
            var finalizer = new HarmonyMethod(typeof(AutoFix), nameof(TextFinalizer));
            MethodBase[] targets =
            {
                AccessTools.Method(typeof(Text), "OnPopulateMesh", new[] { typeof(VertexHelper) }),
                AccessTools.PropertyGetter(typeof(Text), "preferredWidth"),
                AccessTools.PropertyGetter(typeof(Text), "preferredHeight"),
            };
            foreach (MethodBase m in targets)
            {
                if (m == null) throw new MissingMethodException("a UnityEngine.UI.Text member was not found");
                h.Patch(m, prefix: prefix, finalizer: finalizer);
            }
        }

        static void TextPrefix(Text __instance, out string __state)
        {
            __state = null;
            var original = (string)textField.GetValue(__instance);
            if (!ArabicShaper.NeedsShaping(original) || IsSkipped(__instance)) return;
            __state = original;
            textField.SetValue(__instance, ArabicShaper.Shape(original));
            Shaped++;
        }

        // A finalizer, not a postfix, so an exception inside the mesh build cannot leave the shaped string behind.
        static Exception TextFinalizer(Exception __exception, Text __instance, string __state)
        {
            if (__state != null) textField.SetValue(__instance, __state);
            return __exception;
        }
    }
}
