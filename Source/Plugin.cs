using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using JetBrains.Annotations;
using RepoXR.Assets;
using RepoXR.Patches;
using RepoXR.Player.Camera;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.XR;

namespace RepoXR;

[PublicAPI]
[BepInPlugin(PLUGIN_GUID, PLUGIN_NAME, PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
    // Distinct from official io.daxcess.repoxr so this fork is not the Thunderstore package identity.
    public const string PLUGIN_GUID = "io.ashtonite.repoxr.fbt";
    public const string PLUGIN_NAME = "RepoFBT";
    public const string PLUGIN_VERSION = "1.3.18";

    public const string SUPPORTED_GAME_VERSION = "v0.5.1";

    public new static Config Config { get; private set; } = null!;
    public static Flags Flags { get; private set; } = 0;

    public static string GameVersion => Environment.GetEnvironmentVariable("REPO_VERSION") ?? "v?";

    public static bool GameDebugBuild => !Environment.GetCommandLineArgs().Contains("--repoxr-force-release-build") &&
                                         Debug.isDebugBuild;

    private void Awake()
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        InputSystem.PerformDefaultPluginInitialization();

        RepoXR.Logger.source = Logger;

        Config = new Config(Info.Location, base.Config);

        // Debug symbols are automatically embedded in debug builds, so we only load them in release builds
#if RELEASE
        if (Config.ExtendedDebugging.Value)
            LoadDebugSymbols();
#endif

        Logger.LogInfo(
            $"Starting {PLUGIN_NAME} v{PLUGIN_VERSION} ({GetCommitHash()}). FBT fork of DaXcess/RepoXR (GPL-3.0); not the official Thunderstore package.");

        // Allow disabling VR via config and command line
        var disableVr = Config.DisableVR.Value ||
                        Environment.GetCommandLineArgs().Contains("--disable-vr", StringComparer.OrdinalIgnoreCase);

        if (disableVr)
            Logger.LogWarning("VR has been disabled by config or the `--disable-vr` command line flag");

        launchArgs = Environment.GetCommandLineArgs();
        launchParents = VrLaunch.ReadParentChain();
        var immediate = VrLaunch.Collect(launchArgs, Environment.GetEnvironmentVariable, launchParents, "not asked");
        var launchVr = false;
        if (disableVr)
            Logger.LogInfo(immediate.DisabledLine);
        else if (immediate.StartOpenXr)
        {
            Logger.LogInfo(immediate.Line);
            launchVr = true;
        }
        else
        {
            watchSteamVrMode = true;
            steamWatchStart = Time.realtimeSinceStartup;
        }

        if (!PreloadRuntimeDependencies())
        {
            watchSteamVrMode = false;
            Logger.LogError("Disabling mod because required runtime dependencies could not be loaded!");
            return;
        }

        if (!AssetCollection.LoadAssets())
        {
            watchSteamVrMode = false;
            Logger.LogError("Disabling mod because assets could not be loaded!");
            return;
        }

        if (launchVr)
            launchVr = OpenXrRuntimeConnected();

        if (launchVr && InitializeVR(pendingRuntime))
            Flags |= Flags.VR;

        HarmonyPatcher.PatchUniversal();
        HarmonyPatcher.PatchNetworkRPCs();

        Logger.LogDebug("Inserted universal patches using Harmony");

#if DEBUG
        if (Environment.GetCommandLineArgs().Contains("--repoxr-enable-experiments", StringComparer.OrdinalIgnoreCase))
            HarmonyPatcher.PatchClass(typeof(Experiments));
#endif

        if (Environment.GetCommandLineArgs().Contains("--repoxr-debug-eyetracking", StringComparer.OrdinalIgnoreCase))
            Flags |= Flags.EyeTrackingDebug;

        Native.BringGameWindowToFront();
        Config.SetupGlobalCallbacks();

        SceneManager.sceneLoaded += (scene, _) => UniversalEntrypoint.OnSceneLoad(scene.name);
    }

    string[] launchArgs = [];
    IReadOnlyList<string> launchParents = [];
    bool watchSteamVrMode;
    float steamWatchStart;

    void Update()
    {
        if (!watchSteamVrMode)
            return;

        var steamReady = false;
        try
        {
            steamReady = Steamworks.SteamClient.IsValid;
        }
        catch (Exception ex)
        {
            RepoXR.Logger.LogDebug($"Steam client state could not be read: {ex.Message}");
        }

        if (VrLaunch.WaitingForSteam(steamReady, Time.realtimeSinceStartup - steamWatchStart,
                VrLaunch.SteamVrModeWaitSeconds))
            return;

        watchSteamVrMode = false;
        var evidence = VrLaunch.Collect(launchArgs, Environment.GetEnvironmentVariable, launchParents,
            steamReady ? VrLaunch.ReadSteamRunningInVr() : "unavailable");
        RepoXR.Logger.LogInfo(evidence.Line);
        if (!evidence.StartOpenXr)
        {
            RepoXR.Logger.LogInfo(
                "Desktop launch. OpenXR was not started. Start R.E.P.O. from the SteamVR dashboard, or add the Steam launch option --repoxr-vr, to play with a headset.");
            return;
        }

        if (OpenXrRuntimeConnected() && InitializeVR(pendingRuntime))
        {
            Flags |= Flags.VR;
            CameraAimPatches.AttachExisting();
            if (GameObject.Find("UI/HUD/HUD Canvas") != null)
                Entrypoint.SetupDefaultSceneVR();
        }
    }

    public static string GetCommitHash()
    {
        var version = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (string.IsNullOrEmpty(version))
            return "unknown";

        var plus = version.LastIndexOf('+');
        if (plus < 0 || plus >= version.Length - 1)
            return "unknown";

        var hash = version[(plus + 1)..];
        var dirty = hash.IndexOf('-');
        if (dirty >= 0)
            hash = hash[..dirty];

        return hash.Length >= 7 ? hash[..7] : hash;
    }

    private bool PreloadRuntimeDependencies()
    {
        try
        {
            var pluginDirectory = Path.GetDirectoryName(Info.Location)!;
            var deps = Path.Combine(pluginDirectory, "RuntimeDeps");

            foreach (var result in OpenXrPlayerFix.Apply(new[]
                     {
                         Path.Combine(deps, "Unity.XR.OpenXR.dll"),
                         Path.Combine(pluginDirectory, "Unity.XR.OpenXR.dll")
                     }))
            {
                if (result.Error != null)
                    Logger.LogWarning($"Could not bypass OpenXR analytics in {result.Path}: {result.Error}");
                else if (result.MethodsNeutralized > 0)
                    Logger.LogInfo(
                        $"Bypassed OpenXR analytics in {result.Path} ({result.MethodsNeutralized} methods). VR startup no longer depends on UnityEngine.Analytics.AnalyticsResult.");
                else
                    Logger.LogInfo($"OpenXR analytics bypass is already in place ({result.Path}).");
            }

            foreach (var file in Directory.GetFiles(deps, "*.dll"))
            {
                var filename = Path.GetFileName(file);

                try
                {
                    Assembly.LoadFile(file);
                }
                catch (Exception ex)
                {
                    Logger.LogWarning($"Failed to preload '{filename}': {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(
                $"Unexpected error occured while preloading runtime dependencies (incorrect folder structure?): {ex.Message}");

            return false;
        }

        return true;
    }

    static OpenXR.Runtime? pendingRuntime;

    /// <summary>
    /// VR was requested. Start OpenXR only when SteamVR is actually running, or another
    /// runtime can already see a headset. Otherwise stay on the desktop path with one
    /// info line and do not call the Unity loader.
    /// </summary>
    static bool OpenXrRuntimeConnected()
    {
        OpenXrSessionProbe.SessionDecision decision;
        try
        {
            var candidates = OpenXrSessionProbe.FromRuntimes(OpenXR.GetRuntimes());
            decision = OpenXrSessionProbe.Decide(candidates, OpenXrSessionProbe.SteamVrHostRunning(),
                OpenXrSessionProbe.HeadsetSessionAvailable);
        }
        catch (Exception ex)
        {
            RepoXR.Logger.LogDebug($"OpenXR runtime probe failed: {ex.Message}");
            decision = OpenXrSessionProbe.SteamVrHostRunning()
                ? new OpenXrSessionProbe.SessionDecision(true, false, "", "")
                : new OpenXrSessionProbe.SessionDecision(false, false, "", "");
        }

        if (!decision.StartOpenXr)
        {
            pendingRuntime = null;
            RepoXR.Logger.LogInfo(OpenXrSessionProbe.NotConnectedMessage);
            return false;
        }

        pendingRuntime = decision.OnlyThisRuntime
            ? new OpenXR.Runtime { Name = decision.RuntimeName, Path = decision.RuntimeJson }
            : null;
        return true;
    }

    private static bool InitializeVR(OpenXR.Runtime? onlyRuntime)
    {
        RepoXR.Logger.LogInfo("Loading VR...");

        if (onlyRuntime is { } runtime
                ? !OpenXR.Loader.InitializeRuntime(runtime)
                : !OpenXR.Loader.InitializeXR())
        {
            RepoXR.Logger.LogError("Failed to start in VR Mode! Only Non-VR features are available!");
            RepoXR.Logger.LogError("Make sure your headset is turned on and connected before starting the game.");
            RepoXR.Logger.LogWarning("You may ignore the previous error if you meant to play without VR");

            Flags |= Flags.StartupFailed;

            return false;
        }

        if (OpenXR.GetActiveRuntimeName(out var name) &&
            OpenXR.GetActiveRuntimeVersion(out var major, out var minor, out var patch))
            RepoXR.Logger.LogInfo($"OpenXR runtime being used: {name} ({major}.{minor}.{patch})");
        else
            RepoXR.Logger.LogError("Could not get OpenXR runtime info?");

        HarmonyPatcher.PatchVR();

        RepoXR.Logger.LogDebug("Inserted VR patches using Harmony");

        // Change render pipeline settings if needed
        XRSettings.eyeTextureResolutionScale = Config.CameraResolution.Value / 100f;

        // Input settings
        InputSystem.settings.backgroundBehavior =
            InputSettings.BackgroundBehavior.IgnoreFocus; // Prevent VR from getting disabled when losing focus

        return true;
    }

    public static MethodInfo GetConfigGetter()
    {
        return typeof(Plugin).GetProperty(nameof(Config),
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)!
            .GetGetMethod(true);
    }

#if RELEASE
    private void LoadDebugSymbols()
    {
        // This URL automatically downloads the symbols from this version's GitHub release
        const string debugSymbolsPath = $"https://repoxr.daxcess.io/api/symbols/{PLUGIN_VERSION}";

        if (!Config.ExtendedDebugging.Value)
            return;

        try
        {
            Mono.LoadSymbolsForAssembly(Assembly.GetExecutingAssembly(), debugSymbolsPath);
        }
        catch
        {
            Logger.LogWarning("Failed to load debug symbols: error information will be limited");
        }
    }
#endif
}

[Flags]
public enum Flags
{
    VR = 1 << 0,
    StartupFailed = 1 << 1,
    EyeTrackingDebug = 1 << 2
}