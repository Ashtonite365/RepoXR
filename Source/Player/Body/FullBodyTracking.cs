using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using RepoXR.Managers;
using RepoXR.Networking;
using NetworkPlayer = RepoXR.Networking.NetworkPlayer;
using RepoXR.Player.Body;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RepoXR.Player;

/// <summary>
/// Local 3-slot full-body tracking. Disabled unless Gameplay &gt; Full Body Tracking is on.
/// </summary>
[DefaultExecutionOrder(5000)]
public class FullBodyTracking : MonoBehaviour
{
    public const int SlotCount = 3;

    public static bool CalibrationRequested { get; private set; }

    readonly SemibotBodySolver solver = new();
    readonly BodyPose[] poses = new BodyPose[SlotCount];
    readonly ResolvedSlot[] resolved = new ResolvedSlot[SlotCount];

    Transform? trackingOrigin;
    CalibrationStore calibration = new();
    InputAction? calibrateKey;
    bool sentActive;
    bool loggedRestart;
    bool loggedUncalibrated;
    bool loggedNoDevices;
    bool loggedDuplicate;
    bool loggedWaitingToCalibrate;

    struct ResolvedSlot
    {
        public BodyRole Role;
        public bool HasSample;
        public TrackerSample Sample;
    }

    class CalibrationStore
    {
        public List<CalibratedSlot> Slots = [];
    }

    class CalibratedSlot
    {
        public int Slot;
        public int Role;
        public string DeviceId = "";
        public float Px, Py, Pz;
        public float Rx, Ry, Rz, Rw;
    }

    public static void RequestCalibration()
    {
        CalibrationRequested = true;
        Logger.LogInfo("Full body calibration requested. Stand straight in-game with your trackers on.");
    }

    public void Initialize(Transform origin)
    {
        trackingOrigin = origin;
        LoadCalibration();
    }

    void Awake()
    {
        calibrateKey = new InputAction(binding: "<Keyboard>/f9");
        calibrateKey.performed += _ => RequestCalibration();
        calibrateKey.Enable();
    }

    void OnDestroy()
    {
        if (calibrateKey == null)
            return;

        calibrateKey.Disable();
        calibrateKey.Dispose();
    }

    void LateUpdate()
    {
        var player = VRSession.Instance ? VRSession.Instance.Player : null;
        var avatar = PlayerController.instance ? PlayerController.instance.playerAvatarScript : null;

        if (!Plugin.Config.EnableFullBodyTracking.Value)
        {
            if (sentActive)
                PublishInactive(player);
            return;
        }

        if (!ViveTrackerFeature.Injected && !loggedRestart)
        {
            loggedRestart = true;
            Logger.LogWarning(
                "Full body tracking was turned on after startup. Restart the game so SteamVR/SlimeVR trackers can be bound. Headset and controller VR keep working.");
        }

        if (!trackingOrigin || avatar == null)
            return;

        var visuals = avatar.playerAvatarVisuals;
        if (visuals == null)
            return;
        ResolveSlots();

        if (CalibrationRequested)
            TryCalibrate(visuals);

        BuildPoses();
        solver.Apply(avatar, visuals, poses);
        Publish(player);
    }

    void ResolveSlots()
    {
        var samples = ViveTrackers.Poll();
        if (samples.Count == 0 && !loggedNoDevices && ViveTrackerFeature.Injected)
        {
            loggedNoDevices = true;
            Logger.LogInfo(
                "Full body tracking is on, but no extra trackers are visible yet. Assign SteamVR roles (waist, left foot, right foot, or chest) or wait for SlimeVR to connect.");
        }

        if (samples.Count > 0)
            loggedNoDevices = false;

        var roles = new[]
        {
            Plugin.Config.FbtSlot1Role.Value,
            Plugin.Config.FbtSlot2Role.Value,
            Plugin.Config.FbtSlot3Role.Value
        };
        var devices = new[]
        {
            Plugin.Config.FbtSlot1Device.Value,
            Plugin.Config.FbtSlot2Device.Value,
            Plugin.Config.FbtSlot3Device.Value
        };

        var usedRoles = new HashSet<BodyRole>();
        var usedDevices = new HashSet<string>(StringComparer.Ordinal);

        for (var i = 0; i < SlotCount; i++)
        {
            resolved[i] = default;
            var role = roles[i];
            if (role == BodyRole.Unused)
                continue;

            if (!usedRoles.Add(role))
            {
                if (!loggedDuplicate)
                {
                    loggedDuplicate = true;
                    Logger.LogWarning(
                        $"Full body slot {i + 1} uses {role}, which another slot already drives. The extra slot is ignored.");
                }

                continue;
            }

            if (!TrySelectSample(devices[i], role, samples, usedDevices, out var sample))
                continue;

            usedDevices.Add(sample.Id);
            resolved[i] = new ResolvedSlot
            {
                Role = role,
                HasSample = true,
                Sample = sample
            };
        }
    }

    static bool TrySelectSample(int deviceIndex, BodyRole role, List<TrackerSample> samples,
        HashSet<string> usedDevices, out TrackerSample sample)
    {
        sample = default;

        if (deviceIndex <= 0)
        {
            var hint = ViveTrackers.HintForBodyRole(role);
            foreach (var candidate in samples)
            {
                if (usedDevices.Contains(candidate.Id))
                    continue;

                if (candidate.RoleHint == hint)
                {
                    sample = candidate;
                    return true;
                }
            }

            return false;
        }

        if (!ViveTrackers.TryGetCatalogEntry(deviceIndex, out var entry))
            return false;

        foreach (var candidate in samples)
        {
            if (candidate.Id == entry.Id && !usedDevices.Contains(candidate.Id))
            {
                sample = candidate;
                return true;
            }
        }

        return false;
    }

    void TryCalibrate(PlayerAvatarVisuals visuals)
    {
        CalibrationRequested = false;

        if (SemiFunc.MenuLevel())
        {
            if (!loggedWaitingToCalibrate)
            {
                loggedWaitingToCalibrate = true;
                Logger.LogInfo("Full body calibration will run once you are in a level, standing straight.");
            }

            CalibrationRequested = true;
            return;
        }

        loggedWaitingToCalibrate = false;

        if (visuals.currentPose != PlayerAvatarVisuals.Pose.Stand)
        {
            Logger.LogWarning("Full body calibration needs you standing straight (not crouching or tumbling). Request it again with F9 or the Gameplay setting.");
            return;
        }

        if (!trackingOrigin)
            return;

        var calibrated = 0;
        for (var i = 0; i < SlotCount; i++)
        {
            if (!resolved[i].HasSample || !resolved[i].Sample.IsTracked)
                continue;

            if (!solver.TryGetAnchor(visuals, resolved[i].Role, out var anchorPos, out var anchorRot))
                continue;

            var trackerPos = trackingOrigin!.TransformPoint(resolved[i].Sample.LocalPosition);
            var trackerRot = trackingOrigin.rotation * resolved[i].Sample.LocalRotation;
            var offsetPos = Quaternion.Inverse(trackerRot) * (anchorPos - trackerPos);
            var offsetRot = Quaternion.Inverse(trackerRot) * anchorRot;

            UpsertCalibration(new CalibratedSlot
            {
                Slot = i,
                Role = (int)resolved[i].Role,
                DeviceId = resolved[i].Sample.Id,
                Px = offsetPos.x,
                Py = offsetPos.y,
                Pz = offsetPos.z,
                Rx = offsetRot.x,
                Ry = offsetRot.y,
                Rz = offsetRot.z,
                Rw = offsetRot.w
            });
            calibrated++;
        }

        if (calibrated == 0)
        {
            Logger.LogWarning(
                "Full body calibration found no tracked slot. Turn tracking on, map the three slots, and stand inside the playspace.");
            return;
        }

        Plugin.Config.FbtCalibrationData.Value = JsonConvert.SerializeObject(calibration);
        loggedUncalibrated = false;
        Logger.LogInfo($"Full body calibration saved for {calibrated} tracker slot(s).");
    }

    void UpsertCalibration(CalibratedSlot slot)
    {
        calibration.Slots.RemoveAll(existing => existing.Slot == slot.Slot);
        calibration.Slots.Add(slot);
    }

    void BuildPoses()
    {
        var anyUncalibrated = false;

        for (var i = 0; i < SlotCount; i++)
        {
            poses[i] = default;
            if (!resolved[i].HasSample || !resolved[i].Sample.IsTracked || !trackingOrigin)
                continue;

            if (!TryGetCalibration(i, resolved[i], out var offsetPos, out var offsetRot))
            {
                anyUncalibrated = true;
                continue;
            }

            var trackerPos = trackingOrigin!.TransformPoint(resolved[i].Sample.LocalPosition);
            var trackerRot = trackingOrigin.rotation * resolved[i].Sample.LocalRotation;
            poses[i] = new BodyPose
            {
                Role = resolved[i].Role,
                Active = true,
                Position = trackerPos + trackerRot * offsetPos,
                Rotation = trackerRot * offsetRot
            };
        }

        if (anyUncalibrated && !loggedUncalibrated)
        {
            loggedUncalibrated = true;
            Logger.LogInfo("A full-body tracker is connected but not calibrated. Stand straight and calibrate (Gameplay settings, or F9).");
        }
    }

    bool TryGetCalibration(int slot, ResolvedSlot resolvedSlot, out Vector3 position, out Quaternion rotation)
    {
        position = default;
        rotation = Quaternion.identity;

        foreach (var entry in calibration.Slots)
        {
            if (entry.Slot != slot || entry.Role != (int)resolvedSlot.Role || entry.DeviceId != resolvedSlot.Sample.Id)
                continue;

            position = new Vector3(entry.Px, entry.Py, entry.Pz);
            rotation = new Quaternion(entry.Rx, entry.Ry, entry.Rz, entry.Rw);
            if (rotation.x == 0f && rotation.y == 0f && rotation.z == 0f && rotation.w == 0f)
                rotation = Quaternion.identity;

            return true;
        }

        return false;
    }

    void Publish(VRPlayer? player)
    {
        if (player?.NetworkPlayer == null)
            return;

        var any = false;
        for (var i = 0; i < SlotCount; i++)
        {
            var pose = poses[i];
            if (pose.Active)
                any = true;

            SendSlot(player, i, pose);
        }

        sentActive = any;
    }

    void PublishInactive(VRPlayer? player)
    {
        sentActive = false;
        if (player?.NetworkPlayer == null)
            return;

        for (var i = 0; i < SlotCount; i++)
            SendSlot(player, i, default);
    }

    static void SendSlot(VRPlayer player, int slot, BodyPose pose)
    {
        var role = (int)pose.Role;
        switch (slot)
        {
            case 0:
                player.NetworkPlayer.UpdateBodySlot0RPC(role, pose.Position, pose.Rotation, pose.Active);
                break;
            case 1:
                player.NetworkPlayer.UpdateBodySlot1RPC(role, pose.Position, pose.Rotation, pose.Active);
                break;
            default:
                player.NetworkPlayer.UpdateBodySlot2RPC(role, pose.Position, pose.Rotation, pose.Active);
                break;
        }
    }

    void LoadCalibration()
    {
        calibration = new CalibrationStore();
        var raw = Plugin.Config.FbtCalibrationData.Value;
        if (string.IsNullOrWhiteSpace(raw))
            return;

        try
        {
            calibration = JsonConvert.DeserializeObject<CalibrationStore>(raw) ?? new CalibrationStore();
            calibration.Slots ??= [];
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Ignoring saved full-body calibration ({ex.Message}). Calibrate again.");
            calibration = new CalibrationStore();
        }
    }
}

/// <summary>
/// Applies body poses on remote semibots after the game's animation pass.
/// </summary>
[DefaultExecutionOrder(5000)]
public class RemoteBodyApplier : MonoBehaviour
{
    NetworkPlayer? networkPlayer;
    readonly SemibotBodySolver solver = new();
    readonly BodyPose[] shown = new BodyPose[FullBodyTracking.SlotCount];

    public void Bind(NetworkPlayer owner)
    {
        networkPlayer = owner;
    }

    void LateUpdate()
    {
        if (networkPlayer == null || networkPlayer.playerAvatar == null ||
            !networkPlayer.playerAvatar.playerAvatarVisuals)
            return;

        var incoming = networkPlayer.BodyPoses;
        var any = false;
        for (var i = 0; i < shown.Length; i++)
        {
            var pose = incoming[i];
            if (!pose.Active || pose.Role == BodyRole.Unused)
            {
                shown[i] = default;
                continue;
            }

            any = true;
            if (!shown[i].Active || shown[i].Role != pose.Role)
            {
                shown[i] = pose;
                continue;
            }

            var blend = 1f - Mathf.Exp(-18f * Time.deltaTime);
            shown[i].Position = Vector3.Lerp(shown[i].Position, pose.Position, blend);
            shown[i].Rotation = Quaternion.Slerp(shown[i].Rotation, pose.Rotation, blend);
            shown[i].Active = true;
            shown[i].Role = pose.Role;
        }

        if (!any)
            return;

        solver.Apply(networkPlayer.playerAvatar, networkPlayer.playerAvatar.playerAvatarVisuals, shown);
    }
}
