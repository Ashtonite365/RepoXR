# 1.3.22 (RepoFBT)

**Fixes**:
- 1.3.21 was visible and only slightly offset. The same HUD capture and `frontPanelTransform` anchor stay. The plane is now 3 m in front of that panel (was 1.2 m, and the 0.6 m right shift is gone), 1.8 m above it (was 0.4 m), and five times the size: 8 m by 4.5 m. The visible face points at the camera position taken when the menu appears. The plane is unparented and not moved again. Local rect stays 1600 by 900 so a ray hit still maps to the same canvas point.

# 1.3.21 (RepoFBT)

**Fixes**:
- The menu presentation is the 1.3.17 implementation again (`8412213`): the game's HUD canvas is drawn into a texture and shown on a world plane, the ray on that plane is still the game's hover coordinate, and the plane is unparented after it is placed. The 1.3.18–1.3.20 renderer search, view cone, and bounds clearance are not used.
- The plane is then moved once from the menu truck's `frontPanelTransform`: 1.2 m along that panel's horizontal forward, 0.6 m to its right, yawed 21.6° so the visible face points toward the truck's left and a little forward, and raised 0.4 m. If that panel is missing, the plane stays on the 1.3.17 camera pose. The truck and the camera are not moved.

# 1.3.20 (RepoFBT)

**Fixes**:
- The 1.3.19 line is `anchor='CONTROLLER mesh' truckPos=(-17.754,0.501,4.503) planePos=(-26.806,2.107,3.467) planeViewport=(-26.145,8.26,0.36) cameraFwd=(0.034,-0.511,0.859)`. That center is 1.8 m along the camera's horizontal forward and 17.8 m to the side. The gate only tested the forward component, so it passed, and the plane was then pushed another 9 m along camera-left. `planeViewport` x is -26 in the same world space as the camera. The pose is now the sketch's right-hand truck: just past the nose of a mesh whose bounds are in the view, slightly to the truck's right, yawed 21.6° so the face points toward the truck's left and a little forward, and 0.4 m above the camera height. The pose is taken once. Extents are capped at 4 m. The log records the mesh, its viewport, the sideways distance, the facing, and the controller position that was not used. A viewport point is not evidence the texture was drawn.

# 1.3.19 (RepoFBT)

**Fixes**:
- 1.3.18 anchored the plane to `CONTROLLER` at `(-20, 0.5, 0)` and stood at `(0, 0, 0)`, so the plane landed at `(-20, 0, -8.424)`, behind the camera and on the floor (`planeInView=False`). That root is not the truck in front of the camera. The plane now uses only renderers in front of the camera, within 40 m, and sits just to the camera's left of those meshes at the camera's height. The pose is taken once and then left in the world. The truck and the camera are not moved.

# 1.3.18 (RepoFBT)

**Fixes**:
- The menu plane is no longer placed 1.4 m in front of the headset. It is placed once to the left of the main-menu truck, using the truck and the camera rig's standing position, and it stays there. The game's HUD canvas is still only drawn onto that plane. The controller ray on the plane is still the coordinate the game's hover already reads. The log records the truck, the standing position, and `leftOfTruck`, not a view-centre success.

# 1.3.17 (RepoFBT)

**Fixes**:
- 1.3.16 logged `pivot=(0,0)`, stretched anchors, `contentCenter=(0,0,0)`, `localOffset=(0,0,1.4)`, and `contentViewport=(0.5,0.5,1.4)`, then the cursor did meet the ray. `contentCenter` in that line was the canvas pivot after it was forced to the centre, not a measured button. The page stayed stretched from the bottom-left, and rewriting that rect is what stopped the game's UI from drawing. The HUD canvas is no longer reparented or resized. It is drawn, as the game laid it out, into a texture on a plane fixed 1.4 m in front of the camera. The controller ray on that plane is converted to the canvas coordinates the game's hover already uses. The Windows product name is RepoFBT. The file is still `RepoXR.dll` and the id is still `io.ashtonite.repoxr.fbt`.

# 1.3.16 (RepoFBT)

**Fixes**:
- The menu is the game's own HUD Canvas, including `Menu Page Main(Clone)` and whatever else that canvas is already drawing. It is not a rebuilt button list. 1.3.15 shifted it by child bounds `contentCenter=(345,202.5)` and then detached a stretch rect (`pivot=(0,0)`, anchors `(0,0)`–`(1,1)`). The content point still projected to `(0.5,0.5,1.4)`, and the cursor-follow line never logged, because that rect does not survive leaving its parent. The canvas size is frozen to a fixed centre pivot before it is placed, the whole canvas is centred 1.4 m in front of the camera, and it stays in the world.
- The controller ray is tested against that plane. The hit is the position the game's own hover and click already read. The trigger is still the mouse button. The visible menu cursor moves to the same hit. A miss leaves the mouse position.
- The display name is RepoFBT. The id stays `io.ashtonite.repoxr.fbt`, the file stays `RepoXR.dll`, and the config file is unchanged. DaXcess remains the upstream author.
- `UI/UI/Canvas` is disabled at scene load and that disable is logged. The 1.3.14 `xrEndFrame` `XR_ERROR_HANDLE_INVALID` lines start after focus was lost at 14:34:17. They are not treated as the cause of the missing menu.

# 1.3.15 (RepoXR FBT fork)

**Fixes**:
- The 14:35 headset photo shows the button list high in the lens. The 1.3.14 log put the HUD Canvas pivot at viewport `(0.5,0.5,1.4)` while `Menu Page Main(Clone)` is nested on that canvas, so the buttons are not at the pivot. The canvas is shifted by the menu content's centre, then left in the world on the camera rig. It is not parented to the headset.
- The desktop mirror is black while the headset still draws the scene. `UI/UI/Canvas` is a screen overlay and was left enabled in 1.3.14. That overlay draws on the game window after the headset image. It is disabled again, and any other screen-space overlay is disabled once, with its name logged. `CustomCamera` is unchanged.

# 1.3.14 (RepoXR FBT fork)

**Fixes**:
- The 14:25 VR line is `Menu view: canvas 'HUD Canvas' ... viewport=(5.336,0.569,-3.492) canvasCenterInView=False menuPage=missing`. That canvas is the game HUD, its center is behind Camera Main, and `MenuPageMain` did not exist one frame after setup. The camera parent was not restored because the origin did not move it. HUD Canvas is no longer moved to `(-45,-0.75,6)`. `UI/UI/Canvas` is no longer disabled at scene load. When `MenuPageMain` starts, or within 8 seconds of it existing, the canvas that page is under is placed in front of Camera Main. If the page never appears, HUD stays where the game left it and one line says so. The pointer aims at that menu canvas, not at the HUD.

# 1.3.13 (RepoXR FBT fork)

**Fixes**:
- The 14:15 1.3.12 log reaches `Leave to Main Menu`, `Menu controller pointer is ready.`, and `Menu cursor is following the controller ray.` with no setup exception. That shows the ray met the menu canvas. It does not show the canvas was in front of the headset. `XROrigin.Camera` is no longer assigned, because that setter reparents `Camera Main` under the floor offset. If enabling the origin still moves or deactivates that camera, the game parent and local pose are put back. One line, `Menu view:`, records canvas active/enabled/position/scale, camera pose and parent chain, whether the canvas center is in the camera view, and whether `MenuPageMain` is active.
- A second `defaultlocalgroup_assets_all_<other hash>.bundle` is the same group as `..._644635e9bfc6a9f81b1adcaf94351997.bundle`. 1.3.12 only reused a bundle whose name was the group or the same hash, so `LoadFromFile` still ran and Unity logged the duplicate. Opening a localization file now logs the bundle names already in memory.

# 1.3.12 (RepoXR FBT fork)

**Fixes**:
- The menu cursor follows the controller ray. 1.3.11 left `MenuCursor.Update` on the mouse, so the visible cursor stayed put while the laser could still move. After that update, a ray that meets the menu canvas moves the cursor there and keeps it shown. A miss leaves the mouse position. The laser line is unchanged. The first time this happens the log says `Menu cursor is following the controller ray.`
- `XROrigin` is configured while its object is inactive, then enabled. 1.3.11 assigned the floor offset after `AddComponent`, and `Awake` logged `No Camera Floor Offset GameObject specified` before that assignment.
- UI reparenting uses `SetParent` instead of the `parent` property, which is what logged `Parent of RectTransform is being set with parent property` on the menu.
- Field of view is not assigned on a stereo camera. Unity ignores that assignment and logs `Cannot set field of view on camera with name 'Camera Main' while VR is enabled.` Flat cameras still accept it.
- A second `defaultlocalgroup_assets_all_<hash>.bundle` is reused when a loaded bundle already has that group name. 1.3.11 still called `LoadFromFile` on the hash file, which is the remaining duplicate-bundle error.
- The Vive tracker profile is registered only when full-body tracking is on at launch. It adds seven role paths, and the 1.3.11 Player log has seven `xrGetCurrentInteractionProfile` `XR_ERROR_PATH_UNSUPPORTED` lines from Unity while that profile was registered. Turning body tracking on after a launch that did not register the profile takes effect on the next launch.
- A build with no `+revision` in its version no longer logs a warning. The startup line still shows `(unknown)` in that case. Release builds stamp the git revision when git is available.

# 1.3.11 (RepoXR FBT fork)

**Fixes**:
- Menu buttons follow the controller ray. 1.3.10 drew the line, then hover gave up unless the interaction toolkit had already stored a UI hit, and `XROrigin.Origin` was never assigned. The origin now has a floor-offset object and its Origin transform. Hover and the pointer position test the ray against the button rect. The trigger is read from the controller as well as the UI-press action. The first button the ray crosses is logged once.
- `catalog_0.1.json` is not passed to Addressables. That call was the JSON catalog error, the two chain failures, and the second load of `defaultlocalgroup_assets_all_644635e9bfc6a9f81b1adcaf94351997.bundle`. Bundles that are already open are reused.
- `PhysGrabber.Update` no longer has `GetOverrideTransform` on 0.5.1, which is why hand aim stayed unmatched after the `call`/`callvirt` check. The grabber now aims from the hand for the length of `Update`.

# 1.3.10 (RepoXR FBT fork)

**Fixes**:
- The 1.3.9 menu created the controller pointer and then the line visual logged `XROrigin is not found` twice, once per hand. The menu rig now has an `XROrigin` and an `XRUIInputModule` before the rays are created. Each hand is posed from the tracked pointer, and the beam stays bright when it is not touching a button. The game's own menu cursor is no longer turned off, so a mouse still has its cursor.
- `CameraAim.SetPlayerAim` no longer throws when `VRCameraAim` was not attached yet. Existing aim objects are picked up when VR starts after the scene.
- PhysGrabber hand aim matches both `call` and `callvirt` to `GetOverrideTransform`. The 1.3.9 log only matched `callvirt`, so the Update grab ray was left unchanged.
- Localization bundles are opened once and reused. A failed catalog no longer calls Addressables again for the same seven tutorial keys, which was logging `InvalidKeyException` and "bundle already loaded" for every key.

# 1.3.9 (RepoXR FBT fork)

**Fixes**:
- The main-menu controller pointer. `InputDisplayReplaceTags` is void on 0.5.1, and the old prefix asked Harmony for a string result. That aborted every later patch in `InputPatches`, including the trigger rewrites menu hover and clicks use. The void and string signatures are patched separately, and the trigger rewrites live in their own class. The flat menu cursor stays until the controller ray exists. Menu canvas setup can fail without skipping ray creation, and a missing ray is logged once.
- `GetOverrideActive` no longer throws every frame when a menu camera has no player or the VR position driver is not ready. A real local override still wins.
- `PhysGrabber.ObjectTurning` no longer deletes a fixed 10-instruction window. Mouse X/Y axis calls are replaced in place, and a miss cannot take down the rest of the grabber patches.
- Save rename and server-list keyboards close through the 0.5.1 page methods. `PasswordTextSet` and `PostProcessing.Start` stay on their 0.5.1 equivalents (`MenuTextInput.InputTextSet`, `GraphicsManager` lens and chromatic prefixes) when those old methods are absent.
- The debug tester label adds `(VR)` to whatever `{0}` / `{1}` format this build uses.
- Tutorial strings are loaded from the local asset bundles when Unity 6 rejects `catalog_0.1.json`. Desktop wording is left in place when a string asset is not actually there.

# 1.3.8 (RepoXR FBT fork)

**Fixes**:
- A 1.3.7 launch that logged `Desktop launch. OpenXR was not started.` never reached the runtime probe. The command line, `REPOXR_VR` / `VR_LAUNCH` / `VR_OVERRIDE`, and the parent process names are now written on one line so the next launch shows which check missed. If none of those match, startup waits up to 3 seconds for the game's own Steam init and then reads `SteamUtils.IsSteamRunningInVR`. That is Steam's "Steam is in VR mode" flag, not "a SteamVR process exists." A desktop launch whose flag is false still does not start OpenXR. A flag that is true continues into the existing runtime check, which still stays non-VR with one info line when no headset session exists, and still reports a real Unity OpenXR failure when a session was started and then failed.

# 1.3.7 (RepoXR FBT fork)

**Fixes**:
- `--repoxr-vr` (and the other VR launch signals) no longer starts OpenXR when no runtime is actually up. SteamVR is used only while `vrserver` / `vrmonitor` / `vrcompositor` (or the other SteamVR host processes) are running. Any other installed runtime is asked, outside Unity, whether a headset system exists. If none does, startup logs one info line and stays non-VR, so the OpenXR loader does not print `xrCreateInstance` / "all runtimes were attempted". A desktop launch with no VR flag is unchanged. A running SteamVR host, or another runtime that can already see a headset, still starts OpenXR.

# 1.3.6 (RepoXR FBT fork)

**Fixes**:
- Hand aim for grab turning, tumble climb, the snow bike, and vehicle drift, movement, steering, and steering update now runs on R.E.P.O. v0.5.1. Those methods no longer call `GetOverrideTransform`, so the old anchors missed. Each one now aims the live camera from the hand for the length of the call and restores it afterward. Tumble climb no longer deletes instructions in front of `Quaternion.Angle`; it replaces the quaternion already on the stack. Builds that still contain the 0.4.4 calls keep that direct swap.

# 1.3.5 (RepoXR FBT fork)

**Fixes**:
- The compatibility popup now says R.E.P.O. v0.5.1. `PhysGrabber.GrabStateClimb` is left on the game's own climb rotation. The 0.5.1 instructions around `Quaternion.Angle` are not the 0.4.4 sequence, and rewriting them made unverifiable IL that failed the whole universal grabber patch class.
- A desktop Steam launch does not enumerate OpenXR. Headset VR still starts when the game is launched from SteamVR, with `-vr` / `--repoxr-vr`, or with `VR_LAUNCH`, `VR_OVERRIDE`, or `REPOXR_VR`.

# 1.3.4 (RepoXR FBT fork)

**Fixes**:
- The rest of the v0.5.1 launch errors after the CameraShake pin. `InputDisplayGetString` now binds its action and index by position, so the `_action` / `_index` rename no longer aborts the input patches that menu clicks depend on. Menu-button clicks are left on the normal pointer unless the VR ray is actually up.
- `Quaternion.Angle` is pinned to `(Quaternion, Quaternion)`.
- `PostProcessing.Start` is skipped when that method is gone. Lens and chromatic prefixes still apply.
- Save-rename keyboard closing no longer asks 0Harmony to load `MenuPageSavesRename`.
- Vehicle, snow-bike, debug-label, push/pull, and tumble-climb IL edits that miss their 0.4.4 anchors keep the original method instead of failing the patch class. The phys-grabber force-hold insert is not applied; it compiled into unverifiable IL on 0.5.1 and took the rest of the grabber patches down with it.

# 1.3.3 (RepoXR FBT fork)

**Fixes**:
- The "patches failed to apply" popup on R.E.P.O. v0.5.1. `CameraShake.Shake` now has `Shake(float, float)` and `Shake(float, float, float rumbleMultiplier)`, and the old name-only Harmony patch threw `AmbiguousMatchException`. Both overloads are patched. The same kind of unqualified match is pinned down for `InputToggleGet`, `UIMousePosToUIPos`, `UIMouseGetLocalPositionWithinRectTransform`, and `CameraAim.AimTargetSoftSet`. Menu pages removed in 0.5.x (`MenuPageSavesRename`, the server-list text pages, `PasswordTextSet`) are skipped instead of failing the rest of their patch class.

# 1.3.2 (RepoXR FBT fork)

**Fixes**:
- R.E.P.O. v0.5.1 (Unity 6000.3) can finish OpenXR startup. The bundled OpenXR 1.10 player plugin calls `OpenXRAnalytics.SendInitializeEvent` after the headset session is created, and that method needs `UnityEngine.Analytics.AnalyticsResult`, which this player no longer contains. The resulting `TypeLoadException` tore the session down and left SteamVR in theater mode. Those analytics methods are emptied in `RuntimeDeps/Unity.XR.OpenXR.dll` before the assembly loads. Full-body tracking is unchanged and still off by default.
- A JSON Addressables catalog no longer risks aborting mod startup. Unity 6 rejects `catalog_0.1.json` (binary catalogs only). Localization strings for the tutorial stay on the desktop wording; headset and controller VR do not depend on that catalog.

# 1.3.1 (RepoXR FBT fork)

**Changes**:
- Pause menu (the game's escape page) has Body Tracking on/off and Calibrate Body buttons
- Full-body tracking can be toggled during a session. The Vive tracker profile is registered when VR starts, and the switch only applies or ignores poses
- Calibration shows a T-pose copy of the semibot with circles on the live tracker positions. Both controller triggers confirm it

# 1.3.0 (RepoXR FBT fork)

This build is Ashtonite365's fork of DaXcess/RepoXR. It is not the official Thunderstore package.

**Additions**:
- Optional 3-slot full-body tracking from SteamVR, Vive, and SlimeVR trackers (waist, left foot, and right foot by default; chest or unused as alternatives)
- Gameplay settings to enable tracking, map each slot to a tracker, and calibrate while standing straight (F9 does the same)
- Body pose sync for other players who have this fork installed, on the existing RepoXR Photon stream

**Notes**:
- Full-body tracking is off by default and needs a restart after you enable it so the Vive tracker OpenXR profile can load
- Vanilla clients and official RepoXR builds keep working. They do not show the tracker-driven hips and legs. See FBT.md

# 1.2.3

**Changes**:
- The verbose logging option has been renamed and will now also try to load debug symbols

**Fixes**:
- Fixed the map tool rotations not being restored after experiencing a camera override
- Fixed item slots still having a visible battery indicator during camera overrides
- Fixed staff items charge progress based on head rotation instead of hand rotation
- Fixed staff items not properly aligning with world up when not charged

# 1.2.2

**Hotfix**:
- Fixed not being able to unequip melee items without hurt colliders

# 1.2.1

**Changes:**
- Made clipping through the floor more difficult

**Fixes**:
- Fixed the grabber not properly grabbing items at their current position
- Fixed the map player count upgrade not being visible in VR
- Fixed some issues with the on-screen keyboard when renaming a save

# 1.2.0

**Additions**:
- Added support for new valuable interactions
- Added support for new level UI in the map tool
- Added support for the new walkie-talkie item
- Added support for the new scooters
- Added support for the new cosmetics system
- Added VR presence to Steam and Discord (configurable)

**Changes**:
- Left-handed mode now also swaps your controller bindings around
- Left- and right-handed mode now both have separated controller binding overrides
- Restructured mouse input patching so testing with UnityExplorer is easier
- Made some changes to the arms in multiplayer
- Reworked networking internals, should not affect gameplay
- Reworked compatible version checking

**Removals**:
- Removed F8 hotswapping completely
- Removed support for v0.3.x

**Fixes**:
- Smooth turn should feel a bit smoother now when not in the center of your play area
- Fixed opening the VR settings menu causing the screen to reset position

# 1.1.2

**Changes**:
- Disabled movement normalization (configurable in input settings)
- Hotswapping can now only be performed in the main menu, not in lobbies anymore

**Fixes**:
- Fixed LobbyImprovements VR prefix issue

# 1.1.1

**Additions**:
- Added a \[VR\] prefix to VR players in the lobby menu
- Added a warning popup if the host does not have the VR mod

**Fixes**:
- Fixed hotswapping breaking the menu lobby in some circumstances

# 1.1.0

## Detached Arms

You can now detach your arms from your body, giving you more control over your aim, not being constrained by the maximum length of the Semibot's arms anymore. You can change this setting at any time, no restarts or rejoins needed.

## Eye Tracking

RepoXR v1.1.0 adds support for eye tracking (for the three people that have it). 

#### Singleplayer

Players that use eye tracking will have enhanced immersion when it comes to "looking at" things. A few of the enemies in R.E.P.O. behave differently depending on if you're looking at them or not. This detection now factors in where you are looking with your eyes!

Discovering valuables, or your lost friends' heads will also make use of eye tracking. Just look at the items and the game will discover them without you having to move your head.

#### Multiplayer

When playing with other people, other people will see your pupils move based on your real eye movement. Looking down? People will see you look down. Looking straight through your friend's soul because they broke something? Yup, they'll see that!

#### Configuration

Eye tracking can be enabled and disabled mid-game, there's no need to restart. If your headset does not support eye tracking, it will be considered disabled (even when eye tracking is enabled in the config), so no need to change the settings if you don't have eye tracking.

## Hotswap

You can now swap between VR mode and flatscreen mode by pressing the F8 button on your keyboard while you are in the main menu or in the lobby menu. This works even when you are the host (though the lobby will reload for everybody).

**Additions**:
- Added eye tracking support
- Added an option to detach arms from body
- Added support for the new climbing mechanic
- Added support for spectating your death head
- Added support for the monster update
- Added hotswapping in the main menu (F8)

**Changes**:
- The ceiling eye now darkens the world except for where the eye is (no more cheating hihi)
- Removed the performance tab and replaced it with UI in the settings
- Replaced the valuable discover overlay with a new 3D graphic (supports custom colors)
- You now look at the enemy/object that killed you while the death animation plays (if possible)
- Slightly optimized the custom camera by adding a frame rate limiter (disabled by default)
- Optimized framerate by forcibly disabling ambient occlusion (20%-40% less render time)
- Renamed "Dynamic Smooth Speed" to "Analog Smooth Turn"
- Changed the minimum possible HUD height value to account for detached hands
- The map tool can now also be grabbed from behind your head (near the shoulders)
- You can now unbind controls at your leisure

**Removals**:
- Removed support for REPO v0.2.x

# 1.0.3

**Additions**:
- Added a keyboard shortcut (CTRL+C) to the lobby menu for easy copying of a steam lobby link
- Added support for late joining

**Removals**:
- Removed redundant checks during startup

# 1.0.2

**Additions**:
- Added keybind options for turning
- Added a new option for configuring the distance of the camera canvas in the gameplay settings

# 1.0.1

**Additions**:
- Added support for v0.2.1

**Bug fixes**:
- Fixed the small moons in the moon UI rotating weirdly
- Fixed a bug in HarmonyX that would generate invalid code during transpiling

**Removals**:
- Removed support for v0.2.0

# 1.0.0

**The VR mod has been released!!**

It took a bit, but after 3 months <sub><sup>_(started March 21st 2025)_</sup></sub> the R.E.P.O. VR mod has finally released!

No changelogs are necessary for this version, as it is the first version. Subsequent versions will contain a list of changes and new contributors.

### Verifying mod signature

RepoXR comes pre-packaged with a digital signature. You can use tools like GPG to verify the `RepoXR.dll.sig` and `RepoXR.Preload.dll.sig` signatures.

The public key which can be used to verify the file is [9422426F6125277B82CC477DCF78CC72F0FD5EAD (OpenPGP Key Server)](https://keys.openpgp.org/vks/v1/by-fingerprint/9422426F6125277B82CC477DCF78CC72F0FD5EAD).