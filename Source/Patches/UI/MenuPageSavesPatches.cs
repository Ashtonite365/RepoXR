using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RepoXR.UI.Menu;

namespace RepoXR.Patches.UI;

/// <summary>
/// Close the VR keyboard when a save is renamed. 0.5.x removed MenuPageSavesRename.
/// The type name must be resolved with AccessTools, not [HarmonyPatch("MenuPageSavesRename")],
/// which looks the name up in 0Harmony and throws TypeLoadException.
/// </summary>
[RepoXRPatch]
internal static class MenuPageSavesPatches
{
    [HarmonyPrepare]
    static bool Prepare()
    {
        foreach (var _ in TargetMethods())
            return true;

        return false;
    }

    [HarmonyTargetMethods]
    static IEnumerable<MethodBase> TargetMethods()
    {
        var type = AccessTools.TypeByName("MenuPageSavesRename");
        if (type == null)
            yield break;

        foreach (var name in new[] { "ButtonConfirm", "ExitPage" })
        {
            var method = AccessTools.DeclaredMethod(type, name);
            if (method != null)
                yield return method;
        }
    }

    [HarmonyPostfix]
    static void CloseKeyboard()
    {
        if (InputKeyboard.instance is { } instance)
            instance.Close();
    }
}
