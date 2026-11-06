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
}
