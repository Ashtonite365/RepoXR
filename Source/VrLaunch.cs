using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace RepoXR;

/// <summary>
/// Desktop Steam launches of R.E.P.O. must not enumerate OpenXR. The game is not a
/// SteamVR title, so a headset merely being installed still looks like a normal
/// desktop start. Hypothesis: a real VR launch is one of
/// a VR command-line flag, Steam's VR_LAUNCH / VR_OVERRIDE variables, REPOXR_VR,
/// or a SteamVR process (vrserver, vrmonitor, and similar) as a parent.
/// </summary>
internal static class VrLaunch
{
    static readonly string[] VrArguments =
    [
        "-vr", "--vr", "-openxr", "--openxr", "-steamvr", "--steamvr", "-vrmode", "--vrmode", "--repoxr-vr"
    ];

    static readonly string[] SteamVrProcessNames =
    [
        "vrmonitor.exe", "vrserver.exe", "vrwebhelper.exe", "vrcompositor.exe", "steamtours.exe"
    ];

    public static bool ShouldAttemptOpenXr() => !string.IsNullOrEmpty(Describe());

    public static string Describe() =>
        Describe(Environment.GetCommandLineArgs(), Environment.GetEnvironmentVariable, ParentLooksLikeSteamVr);

    internal static string Describe(IReadOnlyList<string> args, Func<string, string?> environment,
        Func<bool> parentLooksLikeSteamVr)
    {
        foreach (var arg in args)
        {
            if (VrArguments.Contains(arg, StringComparer.OrdinalIgnoreCase))
                return $"command line {arg}";
        }

        if (Truthy(environment("REPOXR_VR")))
            return "REPOXR_VR";

        if (Truthy(environment("VR_LAUNCH")))
            return "VR_LAUNCH";

        if (!string.IsNullOrWhiteSpace(environment("VR_OVERRIDE")))
            return "VR_OVERRIDE";

        if (parentLooksLikeSteamVr())
            return "SteamVR parent process";

        return "";
    }

    static bool Truthy(string? value) =>
        value is "1" or "true" or "True" or "TRUE" or "yes" or "Yes";

    static bool ParentLooksLikeSteamVr()
    {
        try
        {
            var pid = ParentPid(GetCurrentProcess());
            for (var depth = 0; depth < 4 && pid > 0; depth++)
            {
                var name = ImageName(pid);
                if (name != null &&
                    SteamVrProcessNames.Contains(name, StringComparer.OrdinalIgnoreCase))
                    return true;

                var handle = OpenProcess(0x1000, false, pid);
                if (handle == IntPtr.Zero)
                    break;

                try
                {
                    var next = ParentPid(handle);
                    if (next == pid)
                        break;
                    pid = next;
                }
                finally
                {
                    CloseHandle(handle);
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogDebug($"Could not read the parent process while deciding VR launch: {ex.Message}");
        }

        return false;
    }

    static int ParentPid(IntPtr process)
    {
        var info = new ProcessBasicInformation();
        var status = NtQueryInformationProcess(process, 0, ref info, Marshal.SizeOf<ProcessBasicInformation>(),
            out _);
        if (status != 0)
            return 0;

        return info.InheritedFromUniqueProcessId.ToInt32();
    }

    static string? ImageName(int pid)
    {
        var handle = OpenProcess(0x1000, false, pid);
        if (handle == IntPtr.Zero)
            return null;

        try
        {
            var capacity = 1024;
            var buffer = new StringBuilder(capacity);
            return QueryFullProcessImageName(handle, 0, buffer, ref capacity)
                ? System.IO.Path.GetFileName(buffer.ToString())
                : null;
        }
        finally
        {
            CloseHandle(handle);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    struct ProcessBasicInformation
    {
        public IntPtr Reserved1;
        public IntPtr PebBaseAddress;
        public IntPtr Reserved2_0;
        public IntPtr Reserved2_1;
        public IntPtr UniqueProcessId;
        public IntPtr InheritedFromUniqueProcessId;
    }

    [DllImport("ntdll.dll")]
    static extern int NtQueryInformationProcess(IntPtr processHandle, int processInformationClass,
        ref ProcessBasicInformation processInformation, int processInformationLength, out int returnLength);

    [DllImport("kernel32.dll")]
    static extern IntPtr GetCurrentProcess();

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern IntPtr OpenProcess(uint access, bool inheritHandle, int processId);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern bool QueryFullProcessImageName(IntPtr process, int flags, StringBuilder name, ref int size);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool CloseHandle(IntPtr handle);
}
