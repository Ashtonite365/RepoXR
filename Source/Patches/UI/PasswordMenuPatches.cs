using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using RepoXR.UI;
using UnityEngine;
using static HarmonyLib.AccessTools;

namespace RepoXR.Patches.UI;

[RepoXRPatch]
internal static class PasswordMenuPatches
{
    /// <summary>
    /// Make the password menu work in VR
    /// </summary>
    [HarmonyPatch(typeof(MenuPagePassword), nameof(MenuPagePassword.Start))]
    [HarmonyPostfix]
    private static void OnMenuPagePasswordShown(MenuPagePassword __instance)
    {
        __instance.gameObject.AddComponent<PasswordUI>();
    }

    /// <summary>
    /// Detect when the password has been submitted
    /// </summary>
    [HarmonyPatch(typeof(MenuPagePassword), nameof(MenuPagePassword.ConfirmButton))]
    [HarmonyPostfix]
    private static void OnMenuPagePasswordConfirm(MenuPagePassword __instance)
    {
        __instance.GetComponent<PasswordUI>().OnConfirm();
    }
}

/// <summary>
/// 0.5.x removed PasswordTextSet. Skip this cursor fix instead of failing the password menu patches.
/// </summary>
[RepoXRPatch]
internal static class PasswordTextCursorPatches
{
    [HarmonyPrepare]
    static bool Prepare() =>
        AccessTools.DeclaredMethod(typeof(MenuPagePassword), nameof(MenuPagePassword.PasswordTextSet)) != null;

    [HarmonyPatch(typeof(MenuPagePassword), nameof(MenuPagePassword.PasswordTextSet))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> TextCursorPatch(IEnumerable<CodeInstruction> instructions)
    {
        return new CodeMatcher(instructions)
            .MatchForward(false,
                new CodeMatch(OpCodes.Callvirt, PropertyGetter(typeof(Transform), nameof(Transform.position))))
            .SetOperandAndAdvance(PropertyGetter(typeof(Transform), nameof(Transform.localPosition)))
            .MatchForward(false,
                new CodeMatch(OpCodes.Callvirt, PropertySetter(typeof(Transform), nameof(Transform.position))))
            .SetOperandAndAdvance(PropertySetter(typeof(Transform), nameof(Transform.localPosition)))
            .InstructionEnumeration();
    }
}