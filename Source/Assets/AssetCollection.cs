using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JetBrains.Annotations;
using RepoXR.Data;
using RepoXR.Input;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.InputSystem;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace RepoXR.Assets;

internal static class AssetCollection
{
    private static AssetBundle assetBundle;
    static readonly Dictionary<string, LocalizedAsset> localizedByKey = new();

    public static OpenXRFeaturePack OpenXRFeatures;

    public static RemappableControls RemappableControls;
    
    public static GameObject RebindHeader;
    public static GameObject RebindButton;
    public static GameObject RebindButtonToggle;
    public static GameObject VRRig;
    public static GameObject CustomCamera;
    public static GameObject VRTumble;
    public static GameObject Keyboard;
    public static GameObject ExpressionWheel;
    public static GameObject ValuableDiscover;
    public static GameObject FocusSphere;

    public static GameObject MenuSettings;
    public static GameObject MenuSettingsCategory;
    public static GameObject MenuShowcase;
    public static GameObject RuntimeSetting;
    public static GameObject BoolSetting;
    public static GameObject SliderSetting;
    public static GameObject VRSettingsButton;
    
    public static InputActionAsset DefaultXRActions;
    public static InputActionAsset VRInputs;

    public static Material DefaultLine;
    public static Material VideoOverlay;

    public static TMP_SpriteAsset TMPInputsSpriteAsset;

    public static Sprite Logo;

    public static Shader VignetteShader;

    public static AnimationCurveData OverchargeHapticCurve;
    public static AnimationCurveData GrabberHapticCurve;
    public static AnimationCurveData HurtHapticCurve;
    public static AnimationCurveData EyeAttachHapticCurve;
    public static AnimationCurveData KeyboardAnimation;

    public static GameObject Cube;

    [PublicAPI]
    public static string AddressablesPath =>
        Path.Combine(Path.GetDirectoryName(Plugin.Config.AssemblyPath)!, "Addressables");
    
    public static bool LoadAssets()
    {
        if (!ValidateFPTSVersion())
        {
            Logger.LogError("Failed to validate FixPluginTypesSerialization version. Make sure that this mod is installed and up-to-date before launching RepoXR.");
            Logger.LogError("In the case that the mod already appears to be up-to-date: uninstall it, clear your mod manager cache, and reinstall it.");

            return false;
        }

        assetBundle =
            AssetBundle.LoadFromFile(Path.Combine(Path.GetDirectoryName(Plugin.Config.AssemblyPath)!, "repoxrassets"));

        if (assetBundle == null)
        {
            Logger.LogError("Failed to load asset bundle!");
            return false;
        }

        // Translations only. Unity 6 players ship Addressables without ENABLE_JSON_CATALOG, so
        // catalog_0.1.json is rejected. That does not stop the asset bundle or OpenXR.
        LoadLocalizationCatalog();

        OpenXRFeatures = assetBundle.LoadAsset<OpenXRFeaturePack>("OpenXRFeatures");

        RemappableControls = assetBundle.LoadAsset<GameObject>("RemappableControls").GetComponent<RemappableControls>();
        
        RebindHeader = assetBundle.LoadAsset<GameObject>("Rebind Header");
        RebindButton = assetBundle.LoadAsset<GameObject>("Rebind Button");
        RebindButtonToggle = assetBundle.LoadAsset<GameObject>("Rebind Button Toggle");
        VRRig = assetBundle.LoadAsset<GameObject>("VRRig");
        CustomCamera = assetBundle.LoadAsset<GameObject>("Custom Camera");
        VRTumble = assetBundle.LoadAsset<GameObject>("VRTumble");
        Keyboard = assetBundle.LoadAsset<GameObject>("NonNativeKeyboard");
        ExpressionWheel = assetBundle.LoadAsset<GameObject>("Expression Radial");
        ValuableDiscover = assetBundle.LoadAsset<GameObject>("Valuable Discover");
        FocusSphere = assetBundle.LoadAsset<GameObject>("Focus Sphere");
        
        MenuSettings = assetBundle.LoadAsset<GameObject>("VR Settings Page");
        MenuSettingsCategory = assetBundle.LoadAsset<GameObject>("VR Settings Page - Category");
        MenuShowcase = assetBundle.LoadAsset<GameObject>("VR Showcase Page");
        RuntimeSetting = assetBundle.LoadAsset<GameObject>("Runtime Setting");
        BoolSetting = assetBundle.LoadAsset<GameObject>("Bool Setting");
        SliderSetting = assetBundle.LoadAsset<GameObject>("Slider Setting");
        VRSettingsButton = assetBundle.LoadAsset<GameObject>("VR Settings Button");
        
        DefaultXRActions = assetBundle.LoadAsset<InputActionAsset>("DefaultXRActions");
        VRInputs = assetBundle.LoadAsset<InputActionAsset>("VRInputs");
        
        DefaultLine = assetBundle.LoadAsset<Material>("Default-Line");
        VideoOverlay = assetBundle.LoadAsset<Material>("Video Overlay");
        
        TMPInputsSpriteAsset = assetBundle.LoadAsset<TMP_SpriteAsset>("TMPInputsSpriteAsset");

        Logo = assetBundle.LoadAsset<Sprite>("REPOXR_Splash_Logo");
        
        VignetteShader = assetBundle.LoadAsset<Shader>("VignetteVR");

        GrabberHapticCurve = assetBundle.LoadAsset<AnimationCurveData>("GrabberHapticCurve");
        OverchargeHapticCurve = assetBundle.LoadAsset<AnimationCurveData>("OverchargeHapticCurve");
        HurtHapticCurve = assetBundle.LoadAsset<AnimationCurveData>("HurtHapticCurve");
        EyeAttachHapticCurve = assetBundle.LoadAsset<AnimationCurveData>("EyeAttachHapticCurve");
        KeyboardAnimation = assetBundle.LoadAsset<AnimationCurveData>("KeyboardAnimation");

        Cube = assetBundle.LoadAsset<GameObject>("JustACube");

        if (RemappableControls?.controls == null)
        {
            Logger.LogError("Failed to load assets! FPTS may have failed to start up properly. Please check logs.");
            return false;
        }

        return true;
    }

    static void LoadLocalizationCatalog()
    {
        const string catalogMessage =
            "VR localization catalog did not load. This game expects a binary Addressables catalog and RepoXR ships catalog_0.1.json. Headset and controller VR still start. Tutorial text overrides stay on the desktop wording.";

        try
        {
            var catalog = Addressables.LoadContentCatalogAsync(Path.Combine(AddressablesPath, "catalog_0.1.json"), true);
            catalog.WaitForCompletion();

            if (catalog.Status != AsyncOperationStatus.Succeeded)
            {
                var indexed = IndexLocalizedBundles();
                Logger.LogInfo(indexed > 0
                    ? $"Addressables rejected catalog_0.1.json. Loaded {indexed} VR string asset(s) from the local bundles instead."
                    : catalogMessage);
            }
        }
        catch (Exception ex)
        {
            var indexed = IndexLocalizedBundles();
            Logger.LogInfo(indexed > 0
                ? $"Addressables rejected catalog_0.1.json ({ex.GetType().Name}). Loaded {indexed} VR string asset(s) from the local bundles instead."
                : $"{catalogMessage} ({ex.GetType().Name}: {ex.Message})");
        }
    }

    public static LocalizedAsset FindLocalizedInBundle(string key)
    {
        if (string.IsNullOrEmpty(key))
            return null;

        if (localizedByKey.TryGetValue(key, out var cached))
            return cached;

        var found = LoadLocalized(assetBundle, key);
        if (found != null)
        {
            localizedByKey[key] = found;
            return found;
        }

        if (!Directory.Exists(AddressablesPath))
            return null;

        foreach (var file in Directory.GetFiles(AddressablesPath, "*.bundle"))
        {
            var bundle = AssetBundle.LoadFromFile(file);
            found = LoadLocalized(bundle, key);
            if (found == null)
                continue;

            localizedByKey[key] = found;
            return found;
        }

        return null;
    }

    static int IndexLocalizedBundles()
    {
        var count = 0;
        if (!Directory.Exists(AddressablesPath))
            return count;

        foreach (var file in Directory.GetFiles(AddressablesPath, "*.bundle"))
        {
            var bundle = AssetBundle.LoadFromFile(file);
            if (bundle == null)
                continue;

            foreach (var path in bundle.GetAllAssetNames())
            {
                if (bundle.LoadAsset<LocalizedAsset>(path) != null)
                    count++;
            }
        }

        return count;
    }

    static LocalizedAsset LoadLocalized(AssetBundle bundle, string key)
    {
        if (bundle == null)
            return null;

        foreach (var path in bundle.GetAllAssetNames())
        {
            var file = Path.GetFileNameWithoutExtension(path);
            if (file.IndexOf(key, StringComparison.OrdinalIgnoreCase) < 0 &&
                path.IndexOf(key, StringComparison.OrdinalIgnoreCase) < 0)
                continue;

            var asset = bundle.LoadAsset<LocalizedAsset>(path);
            if (asset != null)
                return asset;
        }

        return null;
    }

    public static AsyncOperationHandle<LocalizedAsset> GetLocalizedAsset(string name)
    {
        return Addressables.LoadAssetAsync<LocalizedAsset>($"LocalizedAsset XR - {name}");
    }

    private static bool ValidateFPTSVersion()
    {
        var fpts = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(asm => asm.GetName().Name == "FixPluginTypesSerialization");
        if (fpts == null)
        {
            Logger.LogError("FPTS check failed: Assembly missing from AppDomain");
            return false;
        }

        // Interface was added with the switch to Unity 2022.3.67f3
        var hasInterface = fpts.DefinedTypes.Any(type =>
            type.FullName == "FixPluginTypesSerialization.UnityPlayer.Structs.Default.IIsFileCreatedParam");
        if (hasInterface) return true;

        Logger.LogError("FPTS check failed: Missing required interface (mod is outdated)");
        return false;
    }
}