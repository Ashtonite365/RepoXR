using HarmonyLib;
using RepoXR.UI.Menu;

using static HarmonyLib.AccessTools;

namespace RepoXR.Patches.UI;

[RepoXRPatch]
internal static class MenuPageSavesPatches
{
    /// <summary>
    /// 0.5.x removed MenuPageSavesRename. Skip instead of failing the whole patch class.
    /// </summary>
    [HarmonyPrepare]
    static bool Prepare() => AccessTools.TypeByName("MenuPageSavesRename") != null;

    /// <summary>
    /// Make sure to close the keyboard after renaming
    /// </summary>
    [HarmonyPatch("MenuPageSavesRename", "ButtonConfirm")]
    [HarmonyPostfix]
    private static void OnConfirm()
    {
        if (InputKeyboard.instance is {} instance && instance != null)
            instance.Close();
    }

    /// <summary>
    /// Make sure to close the keyboard after canceling the renaming
    /// </summary>
    [HarmonyPatch("MenuPageSavesRename", "ExitPage")]
    [HarmonyPostfix]
    private static void OnExitPage()
    {
        if (InputKeyboard.instance is {} instance && instance != null)
            instance.Close();
    }
}