# Full-body tracking (3 points)

This is an optional addition in the **RepoXR FBT** fork ([Ashtonite365/RepoXR](https://github.com/Ashtonite365/RepoXR)). The original VR mod is [RepoXR by DaXcess](https://github.com/DaXcess/RepoXR) (GPL-3.0). Install this fork **instead of** the official Thunderstore package `DaXcess-RepoXR`. Do not leave both DLLs in `BepInEx/plugins`; they both patch the game.

Headset and controller VR is unchanged while full-body tracking is off, which is the default. If the trackers are unplugged or not mapped, the semibot keeps the normal animation.

The BepInEx id is `io.ashtonite.repoxr.fbt` and the log name is `RepoXR FBT`. The built file is still `RepoXR.dll`, so it drops into the same folder layout as upstream RepoXR (`BepInEx/plugins/RepoXR/` and `BepInEx/patchers/RepoXR/`). You still need the upstream `RuntimeDeps` native OpenXR plugin and asset bundle from a RepoXR release; this repository does not vendor those binaries. See [COMPILING.md](COMPILING.md).

## What you can track

Exactly **three** slots. Each slot has a body role and a tracker.

| Slot | Default role |
|------|----------------|
| 1 | Waist (hips) |
| 2 | Left foot |
| 3 | Right foot |

Other roles you can assign to a slot: **Chest**, or **Unused** to turn that slot off. Elbows, knees, and shoulders are not body roles. This is not a 5-point or 6-point IK setup.

## SteamVR and SlimeVR

Trackers have to show up as OpenXR Vive tracker devices. That is the `XR_HTCX_vive_tracker_interaction` profile. SteamVR does this for Vive trackers and for SlimeVR when SlimeVR is feeding SteamVR virtual trackers. Use SteamVR as the OpenXR runtime (RepoXR's runtime picker can select it).

In SteamVR, give each tracker a **role**. The roles this fork reads are:

- Waist
- Left foot
- Right foot
- Chest
- Handheld (a tracker with no role; only one of these is visible)
- Camera or Keyboard, if you already used those SteamVR roles and want to map them onto a body slot by hand

Set SlimeVR's virtual trackers to those same SteamVR roles. Elbow, knee, and shoulder roles are ignored. Retag those trackers to waist, chest, or a foot.

**Auto** (the default device choice) pairs a slot with the tracker whose SteamVR role matches the body role. A waist slot looks for a waist tracker, a left-foot slot looks for a left-foot tracker, and so on. To use a different physical tracker, open the VR settings menu after the trackers are connected and pick it on that slot. The list is remembered in the config so the choice stays put if a tracker disconnects for a moment.

## Turning tracking on and off

OpenXR cannot add an interaction profile after the session has already started. This fork therefore registers the additive Vive tracker profile **every time VR starts**, whether or not full-body tracking is enabled. The in-game switch does not reload OpenXR. It only starts or stops applying tracker poses. Headset and controller bindings stay on their own profiles.

The profile is additive. If controllers ever stop responding, turn **Body Tracking** off in the pause menu. You should not need to restart to flip tracking itself. A restart is only needed if the tracker profile failed to register at launch (the log will say so).

## Pause menu

R.E.P.O. does not have a separate VR pause screen. The in-game pause UI is the game's escape menu (`MenuPageEsc`). RepoXR parents that page into the VR pause canvas when you open it. This fork adds two buttons on that page, under the existing list:

- **Body Tracking: On / Off** — applies or ignores tracker poses immediately
- **Calibrate Body** — closes the pause menu and starts the calibration preview

The same on/off flag is still **Gameplay → Full Body Tracking** if you want it in the settings list or the BepInEx config.

## Calibrate

1. In a level, open the pause menu and choose **Calibrate Body**. That also turns body tracking on if it was off. **F9** opens the same preview. If the preview is already open, F9 confirms it. The confirm you should use in VR is both triggers, not F9.
2. A copy of your semibot appears in front of you, held in a T-pose (arms out, legs straight). Colored circles sit where your live trackers are, relative to that model, so you can see each puck against the hips, chest, or feet.
3. Stand in that T-pose. Press **both controller triggers at the same time** (the Activate buttons, not the grips). The preview closes and the offsets are saved.

Calibration is stored in `BepInEx/config/io.ashtonite.repoxr.fbt.cfg` under `[Internal] FbtCalibrationData`. Delete that value to force a fresh calibration. Changing a slot's role or tracker invalidates that slot until you calibrate again.

Until a slot is calibrated, that bone stays on the normal animation even if the tracker is live.

The Gameplay setting **Calibrate Full Body** still opens this preview. It is not the only place to start it.

## Settings

In the VR settings menu, category **Gameplay**:

- **Full Body Tracking** — off by default, and safe to change during a session
- **Body Slot 1 / 2 / 3** — waist, left foot, right foot, chest, or unused
- **Slot 1 / 2 / 3 Tracker** — Auto, or a tracker that has been seen since launch
- **Calibrate Full Body** — opens the T-pose preview

The same keys live in the BepInEx config file if you would rather edit them by hand. Slot device `0` means Auto.

## What other players see

RepoXR already appends a small block to each VR player's Photon view (a magic number, protocol version 2, then custom RPCs). Arm and head-adjacent pose uses that block today. Full-body tracking adds three more RPCs on the **same** block and does **not** change the protocol version, so older readers do not lose arm sync.

| Who is watching | What they see |
|-----------------|---------------|
| Another player on **this fork** (VR or flat) | Hips, chest, and feet follow your trackers, on top of the usual VR arms |
| **Official RepoXR** without this fork | Arms still follow you. Unknown body RPCs are ignored. Legs stay on the normal walk animation |
| **Vanilla** (no RepoXR) | The extra block is never read. They keep the normal semibot animation. Your client still sends it at the end of the view, which is the same compatibility approach upstream uses for VR arms |

A vanilla host does not need this mod for the stream to arrive. Each player writes their own view. Vanilla clients simply do not apply the new channels. There is no separate pose channel that vanilla R.E.P.O. already understands for arbitrary hip and foot bones, so those clients cannot show tracker-accurate legs without a modded receiver.

The semibot is not a humanoid skeleton. Legs are the `ANIM LEG L/R BOT/TOP` bones. The mod points those bones at the calibrated foot targets and shortens or stretches them within a limited range. It does not add knee targets. Expect a readable follow of the hips and feet, not a perfect human gait. The gameplay capsule still moves with the stick and the existing room-scale crouch. Trackers do not move your hitbox around the room.

The face keeps the look direction the game already synced. Chest and hip tracking can still lean the torso under that.

## Build note

`dotnet build` from this tree compiles against the publicized R.E.P.O. 0.4.4 assembly from `nuget.daxcess.io` and Unity OpenXR 1.10. Those reference assemblies do not contain the game's method bodies. A headset, SteamVR, and the game itself are still required to confirm tracker binding and how the legs look in a lobby.
