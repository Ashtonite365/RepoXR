using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;

namespace RepoXR.Player.Body;

public struct TrackerSample
{
    public string Id;
    public string Label;
    public string RoleHint;
    public Vector3 LocalPosition;
    public Quaternion LocalRotation;
    public bool IsTracked;
}

/// <summary>
/// Reads extra tracked devices (Vive / SteamVR / SlimeVR virtual trackers).
/// Headset and hand controllers are ignored.
/// </summary>
public static class ViveTrackers
{
    const string CatalogSeparator = "\n";

    static readonly (string Usage, string Hint, string Binding)[] RoleActions =
    {
        ("Waist", "waist", "<XRViveTracker>{Waist}/"),
        ("Left Foot", "left-foot", "<XRViveTracker>{Left Foot}/"),
        ("Right Foot", "right-foot", "<XRViveTracker>{Right Foot}/"),
        ("Chest", "chest", "<XRViveTracker>{Chest}/"),
        ("Handheld", "handheld", "<XRViveTracker>{Handheld}/"),
        ("Camera", "camera", "<XRViveTracker>{Camera}/"),
        ("Keyboard", "keyboard", "<XRViveTracker>{Keyboard}/")
    };

    static InputAction?[]? positionActions;
    static InputAction?[]? rotationActions;
    static InputAction?[]? trackedActions;
    static readonly HashSet<string> ConnectedIds = new(StringComparer.Ordinal);
    static readonly List<CatalogEntry> Entries = [];
    static bool catalogLoaded;

    public struct CatalogEntry
    {
        public string Id;
        public string Label;
        public readonly string MenuLabel => ConnectedIds.Contains(Id) ? Label : Label + " (offline)";
    }

    public static IReadOnlyList<CatalogEntry> Catalog
    {
        get
        {
            EnsureCatalogLoaded();
            return Entries;
        }
    }

    public static List<TrackerSample> Poll()
    {
        EnsureCatalogLoaded();
        ConnectedIds.Clear();

        var samples = new List<TrackerSample>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var device in InputSystem.devices)
        {
            if (!TryReadDevice(device, out var sample))
                continue;

            if (!seen.Add(sample.Id))
                continue;

            samples.Add(sample);
            ConnectedIds.Add(sample.Id);
            Observe(sample.Id, sample.Label);
        }

        if (samples.Count == 0)
            PollRoleActions(samples, seen);

        return samples;
    }

    public static bool TryGetCatalogEntry(int configIndex, out CatalogEntry entry)
    {
        EnsureCatalogLoaded();
        var recordIndex = configIndex - 1;
        if (recordIndex < 0 || recordIndex >= Entries.Count)
        {
            entry = default;
            return false;
        }

        entry = Entries[recordIndex];
        return true;
    }

    static void PollRoleActions(List<TrackerSample> samples, HashSet<string> seen)
    {
        EnsureActions();

        for (var i = 0; i < RoleActions.Length; i++)
        {
            var position = positionActions![i];
            var rotation = rotationActions![i];
            var tracked = trackedActions![i];
            if (position == null || rotation == null || tracked == null)
                continue;

            if (position.controls.Count == 0 || rotation.controls.Count == 0 || tracked.controls.Count == 0)
                continue;

            var isTracked = tracked.ReadValue<float>() > 0.5f;
            var hint = RoleActions[i].Hint;
            var id = "role:" + hint;
            if (!seen.Add(id))
                continue;

            var sample = new TrackerSample
            {
                Id = id,
                Label = RoleActions[i].Usage,
                RoleHint = hint,
                LocalPosition = position.ReadValue<Vector3>(),
                LocalRotation = rotation.ReadValue<Quaternion>(),
                IsTracked = isTracked
            };

            samples.Add(sample);
            ConnectedIds.Add(id);
            Observe(id, sample.Label);
        }
    }

    static void EnsureActions()
    {
        if (positionActions != null)
            return;

        positionActions = new InputAction[RoleActions.Length];
        rotationActions = new InputAction[RoleActions.Length];
        trackedActions = new InputAction[RoleActions.Length];

        for (var i = 0; i < RoleActions.Length; i++)
        {
            var prefix = RoleActions[i].Binding;
            var position = new InputAction(binding: prefix + "devicePosition");
            var rotation = new InputAction(binding: prefix + "deviceRotation");
            var tracked = new InputAction(binding: prefix + "isTracked");
            position.Enable();
            rotation.Enable();
            tracked.Enable();
            positionActions[i] = position;
            rotationActions[i] = rotation;
            trackedActions[i] = tracked;
        }
    }

    static bool TryReadDevice(InputDevice device, out TrackerSample sample)
    {
        sample = default;

        if (device is XRHMD || IsHandOrHead(device) || IsBaseStation(device))
            return false;

        var product = device.description.product ?? "";
        var deviceName = device.name ?? "";
        var roleHint = RoleHintFromUsages(device);
        var trackerLike = device is ViveTrackerFeature.XRViveTracker
                          || product.Contains("Tracker", StringComparison.OrdinalIgnoreCase)
                          || deviceName.Contains("Tracker", StringComparison.OrdinalIgnoreCase)
                          || !string.IsNullOrEmpty(roleHint);

        if (!trackerLike || device is not TrackedDevice tracked)
            return false;

        var state = tracked.trackingState.ReadValue();
        var isTracked = tracked.isTracked.ReadValue() > 0.5f || (state & 3) == 3;
        if (string.IsNullOrEmpty(roleHint))
            roleHint = "tracker";

        var serial = ReadSerial(device);
        var id = string.IsNullOrEmpty(serial) ? "role:" + roleHint : "serial:" + serial;
        var label = string.IsNullOrEmpty(serial) ? Pretty(roleHint) : Pretty(roleHint) + " " + serial;

        sample = new TrackerSample
        {
            Id = id,
            Label = label,
            RoleHint = roleHint,
            LocalPosition = tracked.devicePosition.ReadValue(),
            LocalRotation = tracked.deviceRotation.ReadValue(),
            IsTracked = isTracked
        };
        return true;
    }

    static bool IsHandOrHead(InputDevice device)
    {
        foreach (var usage in device.usages)
        {
            var name = usage.ToString();
            if (name is "LeftHand" or "RightHand" or "Head")
                return true;
        }

        return false;
    }

    static bool IsBaseStation(InputDevice device)
    {
        var name = (device.name ?? "") + (device.description.product ?? "");
        return name.Contains("Base Station", StringComparison.OrdinalIgnoreCase)
               || name.Contains("Lighthouse", StringComparison.OrdinalIgnoreCase)
               || name.Contains("Tracking Reference", StringComparison.OrdinalIgnoreCase);
    }

    static string RoleHintFromUsages(InputDevice device)
    {
        foreach (var usage in device.usages)
        {
            var hint = NormalizeRole(usage.ToString());
            if (hint is "waist" or "left-foot" or "right-foot" or "chest" or "handheld" or "camera" or "keyboard")
                return hint;
        }

        return "";
    }

    public static string NormalizeRole(string value)
    {
        return value.Trim().ToLowerInvariant().Replace(" ", "-").Replace("_", "-");
    }

    public static string HintForBodyRole(BodyRole role)
    {
        return role switch
        {
            BodyRole.Waist => "waist",
            BodyRole.LeftFoot => "left-foot",
            BodyRole.RightFoot => "right-foot",
            BodyRole.Chest => "chest",
            _ => ""
        };
    }

    static string Pretty(string hint)
    {
        return hint switch
        {
            "left-foot" => "Left Foot",
            "right-foot" => "Right Foot",
            "waist" => "Waist",
            "chest" => "Chest",
            "handheld" => "Handheld",
            "camera" => "Camera",
            "keyboard" => "Keyboard",
            _ => hint
        };
    }

    static string ReadSerial(InputDevice device)
    {
        if (!string.IsNullOrWhiteSpace(device.description.serial))
            return device.description.serial.Trim();

        try
        {
            var descriptor = XRDeviceDescriptor.FromJson(device.description.capabilities);
            if (!string.IsNullOrWhiteSpace(descriptor?.serialNumber))
                return descriptor.serialNumber.Trim();
        }
        catch
        {
            // Capabilities are not always JSON.
        }

        return "";
    }

    static void EnsureCatalogLoaded()
    {
        if (catalogLoaded || Plugin.Config == null)
            return;

        catalogLoaded = true;
        Entries.Clear();

        var raw = Plugin.Config.FbtDeviceCatalog.Value;
        if (string.IsNullOrEmpty(raw))
            return;

        foreach (var line in raw.Split(CatalogSeparator))
        {
            var tab = line.IndexOf('\t');
            if (tab <= 0)
                continue;

            Entries.Add(new CatalogEntry
            {
                Id = line[..tab],
                Label = line[(tab + 1)..]
            });
        }
    }

    static void Observe(string id, string label)
    {
        EnsureCatalogLoaded();

        for (var i = 0; i < Entries.Count; i++)
        {
            if (!string.Equals(Entries[i].Id, id, StringComparison.Ordinal))
                continue;

            if (Entries[i].Label == label)
                return;

            Entries[i] = new CatalogEntry { Id = id, Label = label };
            SaveCatalog();
            return;
        }

        Entries.Add(new CatalogEntry { Id = id, Label = label });
        SaveCatalog();
    }

    static void SaveCatalog()
    {
        if (Plugin.Config == null)
            return;

        var lines = new string[Entries.Count];
        for (var i = 0; i < Entries.Count; i++)
            lines[i] = Entries[i].Id.Replace('\t', ' ').Replace('\n', ' ') + "\t" +
                       Entries[i].Label.Replace('\t', ' ').Replace('\n', ' ');

        Plugin.Config.FbtDeviceCatalog.Value = string.Join(CatalogSeparator, lines);
    }
}

/// <summary>
/// Marks an integer config entry whose menu slider lists discovered trackers.
/// Index 0 is automatic SteamVR-role matching.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class TrackerPickerAttribute : Attribute
{
}
