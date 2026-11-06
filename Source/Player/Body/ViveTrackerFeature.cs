using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.XR;
using UnityEngine.Scripting;
using UnityEngine.XR;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;

namespace RepoXR.Player.Body;

/// <summary>
/// OpenXR interaction profile for HTC Vive / SteamVR / SlimeVR trackers
/// (<c>XR_HTCX_vive_tracker_interaction</c>). Added only when full-body tracking
/// is enabled at startup, and marked additive so it does not replace headset
/// or controller profiles.
/// </summary>
public class ViveTrackerFeature : OpenXRInteractionFeature
{
    public const string FeatureId = "io.ashtonite.repoxr.fbt.vivetracker";
    public const string ExtensionName = "XR_HTCX_vive_tracker_interaction";
    public const string Profile = "/interaction_profiles/htc/vive_tracker_htcx";
    public const string ProductName = "HTC Vive Tracker OpenXR";

    const uint LeftFootBit = 0x1000;
    const uint RightFootBit = 0x2000;
    const uint WaistBit = 0x100000;
    const uint ChestBit = 0x200000;
    const uint HandheldBit = 0x400000;
    const uint CameraBit = 0x800000;
    const uint KeyboardBit = 0x1000000;

    public static bool Injected { get; private set; }

    static bool layoutsRegistered;
    static bool mapsRegistered;

    public ViveTrackerFeature()
    {
        openxrExtensionStrings = ExtensionName;
        featureIdInternal = FeatureId;
        nameUi = "RepoXR FBT Vive Trackers";
        version = "1.0.0";
        company = "Ashtonite365";
        m_enabled = true;
        required = false;
    }

    public static ViveTrackerFeature CreateInjected()
    {
        var feature = CreateInstance<ViveTrackerFeature>();
        feature.m_enabled = true;
        Injected = true;
        return feature;
    }

    public override bool IsAdditive => true;

    public override string GetDeviceLayoutName() => nameof(XRViveTracker);

    public override InteractionProfileType GetInteractionProfileType() => InteractionProfileType.Device;

    public override void RegisterDeviceLayout()
    {
        if (layoutsRegistered)
            return;

        layoutsRegistered = true;
        InputSystem.RegisterLayout(typeof(XRViveTracker),
            nameof(XRViveTracker),
            new InputDeviceMatcher()
                .WithInterface(XRUtilities.InterfaceMatchAnyVersion)
                .WithProduct(ProductName));
    }

    public override void UnregisterDeviceLayout()
    {
        if (!layoutsRegistered)
            return;

        layoutsRegistered = false;
        InputSystem.RemoveLayout(nameof(XRViveTracker));
    }

    public override void RegisterActionMapsWithRuntime()
    {
        if (mapsRegistered)
            return;

        mapsRegistered = true;

        var devices = new List<DeviceConfig>();
        AddDevice(devices, LeftFootBit, "/user/vive_tracker_htcx/role/left_foot");
        AddDevice(devices, RightFootBit, "/user/vive_tracker_htcx/role/right_foot");
        AddDevice(devices, WaistBit, "/user/vive_tracker_htcx/role/waist");
        AddDevice(devices, ChestBit, "/user/vive_tracker_htcx/role/chest");
        AddDevice(devices, HandheldBit, "/user/vive_tracker_htcx/role/handheld_object");
        AddDevice(devices, CameraBit, "/user/vive_tracker_htcx/role/camera");
        AddDevice(devices, KeyboardBit, "/user/vive_tracker_htcx/role/keyboard");

        AddActionMap(new ActionMapConfig
        {
            name = "repoxr-fbt-vive-tracker",
            localizedName = ProductName,
            desiredInteractionProfile = Profile,
            manufacturer = "HTC",
            serialNumber = "",
            deviceInfos = devices,
            actions = new List<ActionConfig>
            {
                new()
                {
                    name = "devicePose",
                    localizedName = "Device Pose",
                    type = ActionType.Pose,
                    usages = new List<string> { "Device" },
                    bindings = new List<ActionBinding>
                    {
                        new()
                        {
                            interactionPath = "/input/grip/pose",
                            interactionProfileName = Profile
                        }
                    }
                }
            }
        });
    }

    public override bool OnInstanceCreate(ulong xrInstance)
    {
        try
        {
            base.OnInstanceCreate(xrInstance);
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Vive tracker profile failed to attach: {ex.Message}");
        }

        if (OpenXRRuntime.IsExtensionEnabled(ExtensionName))
            Logger.LogInfo("OpenXR Vive tracker extension is available for full-body tracking.");
        else
            Logger.LogWarning(
                "The OpenXR Vive tracker extension is not active. SteamVR (or another runtime that exposes XR_HTCX_vive_tracker_interaction) is required for SlimeVR and Vive trackers. Headset VR is unchanged.");

        // Never fail XR startup because trackers are missing.
        return true;
    }

    static void AddDevice(List<DeviceConfig> devices, uint roleBit, string userPath)
    {
        devices.Add(new DeviceConfig
        {
            characteristics = InputDeviceCharacteristics.TrackedDevice | (InputDeviceCharacteristics)roleBit,
            userPath = userPath
        });
    }

    [Preserve]
    [InputControlLayout(displayName = "HTC Vive Tracker (OpenXR)",
        commonUsages = new[] { "Left Foot", "Right Foot", "Waist", "Chest", "Handheld", "Camera", "Keyboard" })]
    public class XRViveTracker : TrackedDevice
    {
        // Offsets match the Vive controller pose layout shipped with OpenXR 1.10,
        // which is the plugin version RepoXR loads.
        [Preserve]
        [InputControl(offset = 0, aliases = new[] { "device", "gripPose" }, usage = "Device")]
        public PoseControl devicePose { get; private set; } = null!;

        [Preserve]
        [InputControl(offset = 26)]
        public new ButtonControl isTracked { get; private set; } = null!;

        [Preserve]
        [InputControl(offset = 28)]
        public new IntegerControl trackingState { get; private set; } = null!;

        [Preserve]
        [InputControl(offset = 32, alias = "gripPosition")]
        public new Vector3Control devicePosition { get; private set; } = null!;

        [Preserve]
        [InputControl(offset = 44, alias = "gripOrientation")]
        public new QuaternionControl deviceRotation { get; private set; } = null!;

        public override void FinishSetup()
        {
            base.FinishSetup();

            devicePose = GetChildControl<PoseControl>("devicePose");
            devicePosition = GetChildControl<Vector3Control>("devicePosition");
            deviceRotation = GetChildControl<QuaternionControl>("deviceRotation");
            isTracked = GetChildControl<ButtonControl>("isTracked");
            trackingState = GetChildControl<IntegerControl>("trackingState");

            var usage = UsageFor(ReadCharacteristics());
            if (!string.IsNullOrEmpty(usage))
                InputSystem.SetDeviceUsage(this, usage);
        }

        uint ReadCharacteristics()
        {
            try
            {
                var descriptor = XRDeviceDescriptor.FromJson(description.capabilities);
                return descriptor == null ? 0 : (uint)descriptor.characteristics;
            }
            catch
            {
                return 0;
            }
        }

        static string? UsageFor(uint characteristics)
        {
            if ((characteristics & LeftFootBit) != 0) return "Left Foot";
            if ((characteristics & RightFootBit) != 0) return "Right Foot";
            if ((characteristics & WaistBit) != 0) return "Waist";
            if ((characteristics & ChestBit) != 0) return "Chest";
            if ((characteristics & HandheldBit) != 0) return "Handheld";
            if ((characteristics & CameraBit) != 0) return "Camera";
            if ((characteristics & KeyboardBit) != 0) return "Keyboard";
            return null;
        }
    }
}
