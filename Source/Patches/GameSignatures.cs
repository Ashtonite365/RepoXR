using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace RepoXR.Patches;

/// <summary>
/// R.E.P.O. 0.5.x added overloads next to methods this mod used to name without
/// argument types. Harmony's <c>AccessTools.DeclaredMethod</c> then throws
/// <see cref="AmbiguousMatchException"/> and the startup popup appears.
/// Match the intended overload by name and parameter count instead.
/// </summary>
internal static class GameSignatures
{
    internal static bool IsCameraShakeCall(CodeInstruction instruction, out int argumentCount)
    {
        argumentCount = 0;
        if (instruction.opcode != OpCodes.Call && instruction.opcode != OpCodes.Callvirt)
            return false;
        if (instruction.operand is not MethodInfo method)
            return false;
        if (method.DeclaringType != typeof(CameraShake) || method.Name != nameof(CameraShake.Shake))
            return false;

        argumentCount = method.GetParameters().Length;
        return argumentCount is 2 or 3;
    }

    internal static bool IsAimTargetSoftSet(CodeInstruction instruction)
    {
        return instruction.opcode == OpCodes.Callvirt
               && instruction.operand is MethodInfo method
               && method.DeclaringType == typeof(CameraAim)
               && method.Name == nameof(CameraAim.AimTargetSoftSet);
    }

    /// <summary>
    /// The original soft-aim call takes six arguments. 0.5.x added a follow-options
    /// argument after those. Drop the extras so the VR rewrite still sees the old stack.
    /// </summary>
    internal static void DropAddedSoftFollowArgument(CodeMatcher matcher)
    {
        if (matcher.Instruction.operand is not MethodInfo method)
            return;

        var extra = method.GetParameters().Length - 6;
        if (extra <= 0)
            return;

        // Insert leaves the cursor on the first new instruction. Step back onto the call.
        for (var i = 0; i < extra; i++)
            matcher.Insert(new CodeInstruction(OpCodes.Pop));
        matcher.Advance(extra);
    }

    /// <summary>
    /// Run an IL edit that was written against an older game build. If the anchor is gone,
    /// keep the original method instead of walking off the end of the instruction list.
    /// </summary>
    internal static IEnumerable<CodeInstruction> EditOrOriginal(IEnumerable<CodeInstruction> instructions, string name,
        Func<CodeMatcher, CodeMatcher> edit)
    {
        var original = instructions.ToList();
        try
        {
            var matcher = edit(new CodeMatcher(original));
            if (matcher.IsInvalid)
            {
                Logger.LogWarning($"{name} did not match this game build. Leaving that method unchanged.");
                return original;
            }

            return matcher.InstructionEnumeration().ToList();
        }
        catch (Exception ex) when (ex is ArgumentOutOfRangeException or InvalidOperationException
                                       or IndexOutOfRangeException or NullReferenceException)
        {
            Logger.LogWarning(
                $"{name} did not match this game build ({ex.GetType().Name}). Leaving that method unchanged.");
            return original;
        }
    }
}
