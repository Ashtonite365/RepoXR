using RepoXR;
using Xunit;

namespace RepoXR.Tests;

public class VrLaunchTests
{
    [Fact]
    public void Desktop_launch_does_not_start_openxr()
    {
        var reason = VrLaunch.Describe(["R.E.P.O.exe"], _ => null, () => false);

        Assert.Equal("", reason);
    }

    [Fact]
    public void Vr_command_line_runtime_env_and_steamvr_parent_start_openxr()
    {
        Assert.Equal("command line -vr", VrLaunch.Describe(["game.exe", "-vr"], _ => null, () => false));
        Assert.Equal("command line --repoxr-vr",
            VrLaunch.Describe(["game.exe", "--repoxr-vr"], _ => null, () => false));
        Assert.Equal("REPOXR_VR", VrLaunch.Describe(["game.exe"], name => name == "REPOXR_VR" ? "1" : null, () => false));
        Assert.Equal("VR_LAUNCH", VrLaunch.Describe(["game.exe"], name => name == "VR_LAUNCH" ? "1" : null, () => false));
        Assert.Equal("VR_OVERRIDE",
            VrLaunch.Describe(["game.exe"], name => name == "VR_OVERRIDE" ? @"C:\SteamVR" : null, () => false));
        Assert.Equal("SteamVR parent process", VrLaunch.Describe(["game.exe"], _ => null, () => true));
    }

    [Fact]
    public void Vr_request_without_a_connected_runtime_does_not_start_openxr()
    {
        var probed = new List<string>();
        var decision = OpenXrSessionProbe.Decide(
            [
                new OpenXrSessionProbe.RuntimeCandidate("SteamVR", @"C:\Steam\steamxr_win64.json", true),
                new OpenXrSessionProbe.RuntimeCandidate("Oculus OpenXR", @"C:\Oculus\oculus_openxr_64.json", false)
            ],
            steamVrHostRunning: false,
            candidate =>
            {
                probed.Add(candidate.Name);
                return false;
            });

        Assert.False(decision.StartOpenXr);
        Assert.Equal(["Oculus OpenXR"], probed);
        Assert.Equal(OpenXrSessionProbe.NotConnectedMessage,
            "VR was requested, but no OpenXR runtime was connected.");
    }

    [Fact]
    public void Running_steamvr_starts_openxr_without_probing_a_dead_runtime()
    {
        var probed = false;
        var decision = OpenXrSessionProbe.Decide(
            [new OpenXrSessionProbe.RuntimeCandidate("SteamVR", @"C:\Steam\steamxr_win64.json", true)],
            steamVrHostRunning: true,
            _ => probed = true);

        Assert.True(decision.StartOpenXr);
        Assert.True(decision.OnlyThisRuntime);
        Assert.Equal("SteamVR", decision.RuntimeName);
        Assert.False(probed);
    }

    [Fact]
    public void A_headset_session_on_another_runtime_starts_only_that_runtime()
    {
        var decision = OpenXrSessionProbe.Decide(
            [
                new OpenXrSessionProbe.RuntimeCandidate("SteamVR", @"C:\Steam\steamxr_win64.json", true),
                new OpenXrSessionProbe.RuntimeCandidate("Oculus OpenXR", @"C:\Oculus\oculus_openxr_64.json", false)
            ],
            steamVrHostRunning: false,
            candidate => candidate.Name == "Oculus OpenXR");

        Assert.True(decision.StartOpenXr);
        Assert.True(decision.OnlyThisRuntime);
        Assert.Equal("Oculus OpenXR", decision.RuntimeName);
        Assert.Equal(@"C:\Oculus\oculus_openxr_64.json", decision.RuntimeJson);
    }

    [Fact]
    public void Steamvr_host_with_no_manifest_still_uses_the_normal_loader()
    {
        var decision = OpenXrSessionProbe.Decide([], steamVrHostRunning: true, _ => false);

        Assert.True(decision.StartOpenXr);
        Assert.False(decision.OnlyThisRuntime);
    }

    [Fact]
    public void Steamvr_manifests_are_recognized_by_name_or_file()
    {
        Assert.True(OpenXrSessionProbe.IsSteamVr("SteamVR", @"C:\other\runtime.json"));
        Assert.True(OpenXrSessionProbe.IsSteamVr("OpenXR", @"C:\Steam\steamxr_win64.json"));
        Assert.False(OpenXrSessionProbe.IsSteamVr("Oculus OpenXR", @"C:\Oculus\oculus_openxr_64.json"));
    }
}
