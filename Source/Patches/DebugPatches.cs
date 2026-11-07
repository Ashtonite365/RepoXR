using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;

namespace RepoXR.Patches;

[RepoXRPatch]
internal static class DebugPatches
{
    /// <summary>
    /// Add "(VR)" to the version number in the tester debug UI
    /// </summary>
    [HarmonyPatch(typeof(DebugTesterUI), nameof(DebugTesterUI.OnGUI))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> InjectVRTextPatch(IEnumerable<CodeInstruction> instructions)
    {
        var list = new System.Collections.Generic.List<CodeInstruction>(instructions);
        foreach (var instruction in list)
        {
            if (instruction.operand is not string text || !text.Contains("{0}") || !text.Contains("{1}") ||
                text.Contains("(VR)"))
                continue;

            instruction.operand = text.Replace("{0}", "{0} (VR)");
            Logger.LogInfo("Debug tester version label includes VR.");
            return list;
        }

        Logger.LogInfo("Debug tester OnGUI has no {0}/{1} version format. The label stays on the game's text.");
        return list;
    }
}