using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using RepoXR.Assets;
using RepoXR.Managers;
using RepoXR.Networking;
using NetworkPlayer = RepoXR.Networking.NetworkPlayer;
using RepoXR.Player.Body;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RepoXR.Player;

/// <summary>
/// Local 3-slot full-body tracking. The OpenXR tracker profile is registered at
/// startup; this switch only controls whether tracker poses are applied.
/// </summary>
[DefaultExecutionOrder(5000)]
public class FullBodyTracking : MonoBehaviour
{
    public const int SlotCount = 3;

    public static bool CalibrationRequested => previewRequested;

    static bool previewRequested;

    readonly SemibotBodySolver solver = new();
    readonly BodyPose[] poses = new BodyPose[SlotCount];
    readonly ResolvedSlot[] resolved = new ResolvedSlot[SlotCount];
    readonly TrackerDot[] dots = new TrackerDot[SlotCount];

    Transform? trackingOrigin;
    CalibrationStore calibration = new();
    BodyCalibrationPreview preview;
    InputAction? calibrateKey;
    InputAction? leftTrigger;
    InputAction? rightTrigger;
    bool sentActive;
    bool loggedFeature;
    bool loggedUncalibrated;
    bool loggedNoDevices;
    bool loggedDuplicate;
    bool loggedWaitingToCalibrate;
    bool triggersReleased;

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
        BeginCalibrationPreview();
    }

    public static void BeginCalibrationPreview()
    {
        if (!Plugin.Config.EnableFullBodyTracking.Value)
            Plugin.Config.EnableFullBodyTracking.Value = true;

        previewRequested = true;
        Logger.LogInfo("Full body calibration started. Match the T-pose, then press both triggers.");
    }

    public void Initialize(Transform origin)
    {
        trackingOrigin = origin;
        LoadCalibration();
    }

    void Awake()
    {
        calibrateKey = new InputAction(binding: "<Keyboard>/f9");
        calibrateKey.performed += _ => OnCalibrateKey();
        calibrateKey.Enable();

        leftTrigger = new InputAction(binding: "<XRController>{LeftHand}/triggerPressed");
        leftTrigger.AddBinding("<XRController>{LeftHand}/triggerButton");
        rightTrigger = new InputAction(binding: "<XRController>{RightHand}/triggerPressed");
        rightTrigger.AddBinding("<XRController>{RightHand}/triggerButton");
        leftTrigger.Enable();
        rightTrigger.Enable();

        preview = gameObject.AddComponent<BodyCalibrationPreview>();
    }

    void OnDestroy()
    {
        DisposeAction(calibrateKey);
        DisposeAction(leftTrigger);
        DisposeAction(rightTrigger);
    }

    static void DisposeAction(InputAction? action)
    {
        if (action == null)
            return;

        action.Disable();
        action.Dispose();
    }

    void OnCalibrateKey()
    {
        if (preview && preview.IsShown)
            CommitPreview(PlayerController.instance ? PlayerController.instance.playerAvatarScript : null!);
        else
            BeginCalibrationPreview();
    }

    void LateUpdate()
    {
        var player = VRSession.Instance ? VRSession.Instance.Player : null;
        var avatar = PlayerController.instance ? PlayerController.instance.playerAvatarScript : null;

        if (previewRequested)
            TryStartPreview(avatar);

        if (!Plugin.Config.EnableFullBodyTracking.Value)
        {
            if (preview && preview.IsShown)
                preview.Hide();

            if (sentActive)
                PublishInactive(player);
            return;
        }

        if (!ViveTrackerFeature.Injected && !loggedFeature)
        {
            loggedFeature = true;
            Logger.LogWarning(
                "The Vive tracker profile was not registered. Restart the game once so trackers can bind. The pause-menu switch still enables and disables posing.");
        }

        if (!trackingOrigin || avatar == null)
            return;

        var visuals = avatar.playerAvatarVisuals;
        if (visuals == null)
            return;

        ResolveSlots();
        UpdatePreview(avatar);
        BuildPoses();
        solver.Apply(avatar, visuals, poses);
        Publish(player);
    }

    void TryStartPreview(PlayerAvatar? avatar)
    {
        if (SemiFunc.MenuLevel() || avatar == null || !avatar.playerAvatarVisuals || avatar.isTumbling || avatar.deadSet)
        {
            if (!loggedWaitingToCalibrate)
            {
                loggedWaitingToCalibrate = true;
                Logger.LogInfo("Full body calibration will open once you are in a level and not tumbling.");
            }

            return;
        }

        loggedWaitingToCalibrate = false;
        triggersReleased = false;
        preview.Show(avatar);
        previewRequested = !preview.IsShown;
    }

    void UpdatePreview(PlayerAvatar avatar)
    {
        if (!preview || !preview.IsShown || !trackingOrigin)
            return;

        for (var i = 0; i < SlotCount; i++)
        {
            var slot = resolved[i];
            dots[i] = new TrackerDot
            {
                Label = slot.Role.ToString(),
                Active = slot.HasSample && slot.Sample.IsTracked && slot.Role != BodyRole.Unused,
                WorldPosition = trackingOrigin!.TransformPoint(slot.Sample.LocalPosition)
            };
        }

        preview.Tick(avatar, dots);

        if (!BothTriggersPressed())
        {
            triggersReleased = true;
            return;
        }

        if (triggersReleased && !EscapeMenuOpen())
            CommitPreview(avatar);
    }

    bool BothTriggersPressed()
    {
        var assetLeft = AssetCollection.DefaultXRActions
            ? AssetCollection.DefaultXRActions.FindAction("Left/Activate")
            : null;
        var assetRight = AssetCollection.DefaultXRActions
            ? AssetCollection.DefaultXRActions.FindAction("Right/Activate")
            : null;

        var left = (assetLeft != null && assetLeft.IsPressed()) || (leftTrigger != null && leftTrigger.IsPressed());
        var right = (assetRight != null && assetRight.IsPressed()) || (rightTrigger != null && rightTrigger.IsPressed());
        return left && right;
    }

    static bool EscapeMenuOpen()
    {
        return MenuManager.instance && MenuManager.instance.currentMenuPage &&
               MenuManager.instance.currentMenuPage.menuPageIndex == MenuPageIndex.Escape;
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

    void CommitPreview(PlayerAvatar avatar)
    {
        if (!preview || !preview.IsShown || avatar == null || !trackingOrigin)
            return;

        var calibrated = 0;
        for (var i = 0; i < SlotCount; i++)
        {
            if (!resolved[i].HasSample || !resolved[i].Sample.IsTracked || resolved[i].Role == BodyRole.Unused)
                continue;

            if (!preview.TryGetReference(resolved[i].Role, avatar.transform, out var anchorPos, out var anchorRot))
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
                "Calibration found no tracked slot. Map the three slots, stand in the T-pose, then press both triggers again.");
            triggersReleased = false;
            return;
        }

        Plugin.Config.FbtCalibrationData.Value = JsonConvert.SerializeObject(calibration);
        loggedUncalibrated = false;
        preview.Hide();
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
            Logger.LogInfo("A full-body tracker is connected but not calibrated. Open the pause menu and choose Calibrate Body, or press F9.");
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
