using HarmonyLib;
using static HarmonyLib.AccessTools;

namespace RepoXR.Patches;

[RepoXRPatch]
internal static class PostProcessingPatches
{
    [HarmonyPatch(typeof(GraphicsManager), nameof(GraphicsManager.UpdateLensDistortion))]
    [HarmonyPrefix]
    private static bool DisableLensDistortion()
    {
        return false;
    }

    [HarmonyPatch(typeof(GraphicsManager), nameof(GraphicsManager.UpdateChromaticAberration))]
    [HarmonyPrefix]
    private static bool DisableChromaticAberration()
    {
        return false;
    }
}

/// <summary>
/// 0.5.1 no longer has PostProcessing.Start. Skip that one hook; the GraphicsManager
/// prefixes above still disable the effects that fight with VR.
/// </summary>
[RepoXRPatch]
internal static class PostProcessingStartPatches
{
    [HarmonyPrepare]
    private static bool Prepare() =>
        AccessTools.DeclaredMethod(typeof(PostProcessing), nameof(PostProcessing.Start)) != null;

    [HarmonyPatch(typeof(PostProcessing), nameof(PostProcessing.Start))]
    [HarmonyPostfix]
    private static void PostProcessingPatch(PostProcessing __instance)
    {
        __instance.lensDistortion.active = false;
        __instance.chromaticAberration.active = false;
    }
}