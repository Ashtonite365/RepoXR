# RepoXR FBT

**RepoXR FBT** is a GPL-3.0 fork of [RepoXR by DaXcess](https://github.com/DaXcess/RepoXR) with optional 3-point full-body tracking. This repository is [Ashtonite365/RepoXR](https://github.com/Ashtonite365/RepoXR). It is not the official Thunderstore package [`DaXcess-RepoXR`](https://thunderstore.io/c/repo/p/DaXcess/RepoXR). Do not install both plugins in the same BepInEx folder.

The BepInEx plugin id is `io.ashtonite.repoxr.fbt` (`RepoXR FBT`). It still builds `RepoXR.dll` / `RepoXR.Preload.dll` and uses the same `BepInEx/plugins/RepoXR` install layout, including the upstream runtime dependencies and asset bundle. Upstream's preload path is the one historically shipped as LCVR; keep that attribution when you redistribute a build.

Original RepoXR is a fully fledged R.E.P.O. VR mod that adds full 6-DoF motion controlled VR support to R.E.P.O. Full-body tracking is off unless you enable it. Setup, SteamVR/SlimeVR roles, calibration, and who can see the extra bones are documented in [FBT.md](FBT.md).

The mod is powered by Unity's OpenXR plugin and is thereby compatible with a wide range of headsets, controllers and runtimes, like Oculus, Virtual Desktop, SteamVR and many more!

RepoXR is compatible with multiplayer and works in a lobby comprised of both flatscreen **and** VR players. Running this mod without having a VR headset will allow you to see the arm movements of any VR semibots in the same lobby, all while still being compatible with vanilla clients (even if the host is using no mods, though there is a small catch with that). Tracker-driven hips and legs are visible to other people running **this fork**. Vanilla clients and official RepoXR builds keep the normal leg animation. Details are in [FBT.md](FBT.md).

### Discord Server

Facing issues, have some mod (in)compatibility to report or just want to hang out?

You can join the [Discord Server](https://discord.gg/2DxNgpPZUF)!

# Compatibility

At the time of writing, no explicit compatibility exists yet for RepoXR, so you will have to manually figure out what mod works in VR and which ones don't.

# Installing and using the mod

The original mod is on Thunderstore as [DaXcess/RepoXR](https://thunderstore.io/c/repo/p/DaXcess/RepoXR) and can be installed with a launcher such as [Gale](https://kesomannen.com/gale). This fork is not that listing. Build it from source ([COMPILING.md](COMPILING.md)) and replace `RepoXR.dll` (and `RepoXR.Preload.dll` if you rebuilt the patcher) inside an existing RepoXR install. Remove the official plugin DLL first so both copies are not loaded.

# Versions

Here is a list of RepoXR versions and which version(s) of R.E.P.O. it supports

| RepoXR FBT | R.E.P.O. Version |
|------------|------------------|
| v1.3.6 (hand aim on 0.5.1) | v0.5.1 |
| v1.3.5 (0.5.1 target, desktop skips OpenXR) | v0.5.1 |
| v1.3.4 (0.5.1 patch sweep) | v0.5.1. Failed input, grabber, vehicle, and menu patches from 1.3.3 no longer abort startup |
| v1.3.3 (0.5.1 Harmony overloads) | v0.5.1 VR startup. Shake and the other new overloads are named explicitly |
| v1.3.2 (OpenXR startup on Unity 6) | v0.5.1 VR startup. Gameplay patches are still the v0.4.4 set |
| v1.3.1 (pause-menu calibrate, live toggle) | v0.4.4 |
| v1.3.0 (this fork, adds 3-point FBT) | v0.4.4 |
| v1.2.3 (upstream baseline) | v0.4.4 |
| v1.2.2 | v0.4.3           |
| v1.2.1 | v0.4.3           |
| v1.2.0 | v0.4.3           |
| v1.1.2 | v0.3.0 - v0.3.2  |
| v1.1.1 | v0.3.0 - v0.3.1  |
| v1.1.0 | v0.3.0 - v0.3.1  |
| v1.0.3 | v0.2.1           |
| v1.0.2 | v0.2.1           |
| v1.0.1 | v0.2.1           |
| v1.0.0 | v0.2.0           |

> RepoXR dynamically measures compatibility with the game during startup, meaning newer R.E.P.O. versions might be supported even though they aren't listed here.

# Install from source

> The easiest way to install the mod is by downloading it from Thunderstore. You only need to follow these steps if you are planning on installing the mod by building the source code and without a mod manager.

To install the mod from the source code, you will first have to compile the mod. Instructions for this are available in [COMPILING.md](COMPILING.md).

Next up you'll need to grab a copy of a previous release of RepoXR either from [Releases](https://github.com/DaXcess/RepoXR/releases) or from [Thunderstore](https://thunderstore.io/c/repo/p/DaXcess/RepoXR) (manual download). Extract this zip file and replace the `LCVR.dll` and/or the `LCVR.Preload.dll` files with the new files that you obtained after following the guide in [COMPILING.md](COMPILING.md).

You can *also* manually build the required plugins, asset bundles and addressables by cloning the [RepoXR-Unity](https://github.com/DaXcess/RepoXR-Unity) and performing the necessary steps (which I am not planning on documenting).