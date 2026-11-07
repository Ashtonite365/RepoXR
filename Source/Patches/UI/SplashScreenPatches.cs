using HarmonyLib;
using RepoXR.Assets;
using UnityEngine;
using UnityEngine.UI;

namespace RepoXR.Patches.UI;

[RepoXRPatch]
internal static class SplashScreenPatches
{
    private static RectTransform logoTransform;
    
    /// <summary>
    /// Shameless self promo
    /// </summary>
    [HarmonyPatch(typeof(SplashScreenUI), nameof(SplashScreenUI.Awake))]
    [HarmonyPostfix]
    private static void AddRepoXRLogoPatch(SplashScreenUI __instance)
    {
        var logoObject = new GameObject("RepoXR Logo")
        {
            transform =
            {
                parent = __instance.semiworkTransform.parent,
                localPosition = Vector3.zero,
                localRotation = Quaternion.identity,
                localScale = Vector3.one * 0.8f
            }
        };
        
        logoObject.AddComponent<Image>().sprite = AssetCollection.Logo;
        logoObject.SetActive(false);
        
        logoTransform = logoObject.GetComponent<RectTransform>();
        logoTransform.sizeDelta = new Vector2(530, 155) * 0.4f;
    }

    [HarmonyPatch(typeof(SplashScreen), nameof(SplashScreen.SemiworkAnimation))]
    [HarmonyPostfix]
    private static void LogoAnimationPatch(SplashScreen __instance)
    {
        if (logoTransform == null || SplashScreenUI.instance == null || SplashScreenUI.instance.semiworkTransform == null)
            return;

        logoTransform.localPosition = SplashScreenUI.instance.semiworkTransform.localPosition + Vector3.down * 80;
    }
    
    /// <summary>
    /// Make sure to darken the rest of the scene while the splash screen is showing
    /// </summary>
    [HarmonyPatch(typeof(SplashScreen), nameof(SplashScreen.Update))]
    [HarmonyPostfix]
    private static void SetFadeOverlayPatch(SplashScreen __instance)
    {
        if (logoTransform == null || SplashScreenUI.instance == null || SplashScreenUI.instance.semiworkTransform == null)
            return;

        logoTransform.gameObject.SetActive(SplashScreenUI.instance.semiworkTransform.gameObject.activeSelf);
        
        FadeOverlay.Instance.Image.color = new Color(0, 0, 0, 1);
    }
}