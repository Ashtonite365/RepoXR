using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace RepoXR;

/// <summary>
/// Unity's OpenXR loader logs xrCreateInstance / "all runtimes were attempted" when a VR
/// flag is set but no headset session exists. Decide whether to call that loader at all.
/// SteamVR that is not running is not probed: its runtime prints XR_ERROR_RUNTIME_FAILURE
/// as soon as xrCreateInstance is called. Other installed runtimes are asked, outside
/// Unity, whether a head-mounted system exists.
/// </summary>
internal static class OpenXrSessionProbe
{
    internal readonly struct RuntimeCandidate
    {
        public RuntimeCandidate(string name, string jsonPath, bool steamVr)
        {
            Name = name;
            JsonPath = jsonPath;
            SteamVr = steamVr;
        }

        public string Name { get; }
        public string JsonPath { get; }
        public bool SteamVr { get; }
    }

    internal readonly struct SessionDecision
    {
        public SessionDecision(bool startOpenXr, bool onlyThisRuntime, string runtimeName, string runtimeJson)
        {
            StartOpenXr = startOpenXr;
            OnlyThisRuntime = onlyThisRuntime;
            RuntimeName = runtimeName;
            RuntimeJson = runtimeJson;
        }

        public bool StartOpenXr { get; }
        public bool OnlyThisRuntime { get; }
        public string RuntimeName { get; }
        public string RuntimeJson { get; }
    }

    static readonly string[] SteamVrHosts = ["vrmonitor", "vrserver", "vrwebhelper", "vrcompositor", "steamtours"];

    internal const string NotConnectedMessage = "VR was requested, but no OpenXR runtime was connected.";

    internal static bool IsSteamVr(string? name, string? jsonPath)
    {
        if (!string.IsNullOrEmpty(name) && name.Contains("SteamVR", StringComparison.OrdinalIgnoreCase))
            return true;

        var file = Path.GetFileName(jsonPath ?? "");
        return file.Contains("steamxr", StringComparison.OrdinalIgnoreCase);
    }

    internal static SessionDecision Decide(IReadOnlyList<RuntimeCandidate> candidates, bool steamVrHostRunning,
        Func<RuntimeCandidate, bool> headsetConnected)
    {
        foreach (var candidate in candidates)
        {
            if (candidate.SteamVr && steamVrHostRunning)
                return new SessionDecision(true, true, candidate.Name, candidate.JsonPath);
        }

        if (steamVrHostRunning)
            return new SessionDecision(true, false, "", "");

        foreach (var candidate in candidates)
        {
            if (candidate.SteamVr)
                continue;

            if (headsetConnected(candidate))
                return new SessionDecision(true, true, candidate.Name, candidate.JsonPath);
        }

        return new SessionDecision(false, false, "", "");
    }

    internal static List<RuntimeCandidate> FromRuntimes(OpenXR.Runtimes runtimes)
    {
        var list = new List<RuntimeCandidate>();
        if (runtimes.Default is { } active)
            list.Add(ToCandidate(active));

        foreach (var runtime in runtimes)
        {
            if (list.Any(candidate =>
                    string.Equals(candidate.JsonPath, runtime.Path, StringComparison.OrdinalIgnoreCase)))
                continue;

            list.Add(ToCandidate(runtime));
        }

        return list;
    }

    internal static bool SteamVrHostRunning()
    {
        try
        {
            foreach (var name in SteamVrHosts)
            {
                var found = Process.GetProcessesByName(name);
                var running = found.Length > 0;
                foreach (var process in found)
                    process.Dispose();

                if (running)
                    return true;
            }
        }
        catch (Exception ex)
        {
            Logger.LogDebug($"Could not check for a SteamVR process: {ex.Message}");
        }

        return false;
    }

    internal static bool HeadsetSessionAvailable(RuntimeCandidate candidate) =>
        NativeSession.Try(candidate.JsonPath);

    static RuntimeCandidate ToCandidate(OpenXR.Runtime runtime) =>
        new(runtime.Name ?? "", runtime.Path ?? "", IsSteamVr(runtime.Name, runtime.Path));

    static class NativeSession
    {
        const int TypeInstanceCreateInfo = 3;
        const int TypeSystemGetInfo = 4;
        const int FormFactorHeadMounted = 1;
        const int Success = 0;

        static bool loaderLoaded;

        public static bool Try(string? jsonPath)
        {
            if (string.IsNullOrWhiteSpace(jsonPath) || !File.Exists(jsonPath))
                return false;

            try
            {
                if (!EnsureLoader())
                    return false;

                var previous = Environment.GetEnvironmentVariable("XR_RUNTIME_JSON");
                Environment.SetEnvironmentVariable("XR_RUNTIME_JSON", jsonPath);
                ulong instance = 0;
                try
                {
                    if (!CreateInstance(out instance))
                        return false;

                    return SystemAvailable(instance);
                }
                finally
                {
                    if (instance != 0)
                        xrDestroyInstance(instance);

                    Environment.SetEnvironmentVariable("XR_RUNTIME_JSON", previous);
                }
            }
            catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or TypeLoadException)
            {
                Logger.LogDebug($"OpenXR session probe could not load the loader: {ex.Message}");
                return false;
            }
        }

        static bool EnsureLoader()
        {
            if (loaderLoaded)
                return true;

            var pluginDir = Path.GetDirectoryName(typeof(OpenXrSessionProbe).Assembly.Location);
            string[] candidates =
            [
                Path.Combine(BepInEx.Paths.GameRootPath, "REPO_Data", "Plugins", "openxr_loader.dll"),
                pluginDir == null ? "" : Path.Combine(pluginDir, "RuntimeDeps", "openxr_loader.dll")
            ];

            foreach (var path in candidates)
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path))
                    continue;

                if (LoadLibrary(path) != IntPtr.Zero)
                {
                    loaderLoaded = true;
                    return true;
                }
            }

            return false;
        }

        static unsafe bool CreateInstance(out ulong instance)
        {
            var app = new XrApplicationInfo
            {
                ApplicationVersion = 1,
                EngineVersion = 1,
                ApiVersion = (1UL << 48) | 29UL
            };
            WriteAscii(app.ApplicationName, "RepoXR");
            WriteAscii(app.EngineName, "Unity");
            var info = new XrInstanceCreateInfo
            {
                Type = TypeInstanceCreateInfo,
                ApplicationInfo = app
            };
            var result = xrCreateInstance(ref info, out instance);
            return result == Success && instance != 0;
        }

        static unsafe void WriteAscii(byte* destination, string text)
        {
            var count = Math.Min(text.Length, 127);
            for (var i = 0; i < count; i++)
                destination[i] = (byte)text[i];
            destination[count] = 0;
        }

        static bool SystemAvailable(ulong instance)
        {
            var info = new XrSystemGetInfo
            {
                Type = TypeSystemGetInfo,
                FormFactor = FormFactorHeadMounted
            };
            return xrGetSystem(instance, ref info, out _) == Success;
        }

        [StructLayout(LayoutKind.Sequential)]
        unsafe struct XrApplicationInfo
        {
            public fixed byte ApplicationName[128];
            public uint ApplicationVersion;
            public fixed byte EngineName[128];
            public uint EngineVersion;
            public ulong ApiVersion;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct XrInstanceCreateInfo
        {
            public int Type;
            public IntPtr Next;
            public ulong CreateFlags;
            public XrApplicationInfo ApplicationInfo;
            public uint EnabledApiLayerCount;
            public IntPtr EnabledApiLayerNames;
            public uint EnabledExtensionCount;
            public IntPtr EnabledExtensionNames;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct XrSystemGetInfo
        {
            public int Type;
            public IntPtr Next;
            public int FormFactor;
        }

        [DllImport("openxr_loader", CallingConvention = CallingConvention.Winapi)]
        static extern int xrCreateInstance(ref XrInstanceCreateInfo info, out ulong instance);

        [DllImport("openxr_loader", CallingConvention = CallingConvention.Winapi)]
        static extern int xrDestroyInstance(ulong instance);

        [DllImport("openxr_loader", CallingConvention = CallingConvention.Winapi)]
        static extern int xrGetSystem(ulong instance, ref XrSystemGetInfo info, out ulong systemId);

        [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern IntPtr LoadLibrary(string path);
    }
}
