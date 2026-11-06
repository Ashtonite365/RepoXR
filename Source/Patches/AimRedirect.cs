using System;
using System.Reflection;
using HarmonyLib;
using RepoXR.Managers;
using RepoXR.Networking;
using UnityEngine;

namespace RepoXR.Patches;

/// <summary>
/// For the length of a grab, vehicle, or snow-bike method, the player's camera reports the
/// hand pose. Position and rotation are restored before the method returns, including when
/// the method throws. No game IL is rewritten, so a 0.5.1 layout change cannot fail verification.
/// </summary>
internal sealed class AimRedirect
{
    static Camera? proxy;
    static int depth;
    static bool logged;

    static readonly FieldInfo? PlayerCameraField = AccessTools.Field(typeof(PhysGrabber), "playerCamera");
    static readonly FieldInfo? GamepadDriverField = AccessTools.Field(typeof(ItemVehicle), "gamepadDriver");
    static readonly MethodInfo? SnowBikeDriverMethod = AccessTools.DeclaredMethod(typeof(ValuableArcticSnowBike), "Driver");

    Transform? cameraTransform;
    Vector3 cameraPosition;
    Quaternion cameraRotation;
    PhysGrabber? grabber;
    Camera? previousGrabberCamera;
    bool restoreGrabberCamera;

    public static AimRedirect None { get; } = new();

    public static AimRedirect ForVehicle(ItemVehicle? vehicle)
    {
        if (vehicle == null)
            return new AimRedirect();

        var driver = vehicle.currentDriver;
        if (driver == null && GamepadDriverField != null)
            driver = GamepadDriverField.GetValue(vehicle) as PlayerAvatar;

        return Begin(driver, null, respectVehicleHead: true);
    }

    public static AimRedirect ForSnowBike(ValuableArcticSnowBike? bike) =>
        Begin(SnowBikeDriver(bike), null, respectVehicleHead: false);

    public static AimRedirect ForGrabTurn(PhysGrabber? grabber) =>
        Begin(grabber != null ? grabber.playerAvatar : null, grabber, respectVehicleHead: false);

    public void Restore()
    {
        if (cameraTransform != null)
            cameraTransform.SetPositionAndRotation(cameraPosition, cameraRotation);

        if (restoreGrabberCamera && grabber != null && PlayerCameraField != null)
            PlayerCameraField.SetValue(grabber, previousGrabberCamera);

        if (cameraTransform != null || restoreGrabberCamera)
            depth = Math.Max(0, depth - 1);
    }

    /// <summary>
    /// 0.4.4 fed this quaternion into the climb angle check. VR uses the hand, turned
    /// around so it faces the same way as the old camera. Desktop keeps the climb stick.
    /// </summary>
    internal static Quaternion ClimbRotation(Quaternion original, PhysGrabber? grabber)
    {
        if (grabber?.playerAvatar == null)
            return original;

        if (TryHandRotation(grabber, out var hand))
            return hand;

        return grabber.climbStickTransform != null ? grabber.climbStickTransform.rotation : original;
    }

    internal static void PatchHelpers(Harmony harmony)
    {
        Logger.LogInfo(
            "Hand aim hooks registered for PhysGrabber.ObjectTurning, PhysGrabber.GrabStateClimb, ValuableArcticSnowBike.UpdateSteering, ItemVehicle.ApplyVehicleMovement, ApplyVehicleSteering, ApplyVehicleDrift, and UpdateSteering.");

        TryPatch(harmony, typeof(ItemVehicle), "GamepadInputDirectionGet",
            [typeof(PlayerAvatar), typeof(Vector3)], nameof(VehiclePlayerPrefix), nameof(RestorePostfix));
        TryPatch(harmony, typeof(ItemVehicle), "GamepadCameraUpdate",
            [typeof(PlayerAvatar)], nameof(VehiclePlayerPrefix), nameof(RestorePostfix));
        TryPatch(harmony, typeof(ValuableArcticSnowBike), "DriveHeadingUpdate",
            [], nameof(SnowInstancePrefix), nameof(RestorePostfix));
        TryPatch(harmony, typeof(ValuableArcticSnowBike), "DriveInputLogic",
            [], nameof(SnowInstancePrefix), nameof(RestorePostfix));
        TryPatch(harmony, typeof(ValuableArcticSnowBike), "GamepadHeadingUpdate",
            [typeof(PlayerAvatar)], nameof(SnowDriverPrefix), nameof(RestorePostfix));
        TryPatch(harmony, typeof(PhysGrabber), "ClimbOrbitRotationGet",
            [typeof(Quaternion)], nameof(ClimbOrbitPrefix), null);
    }

    static void VehiclePlayerPrefix(PlayerAvatar __0, out AimRedirect __state) =>
        __state = Begin(__0, null, respectVehicleHead: true);

    static void SnowDriverPrefix(PlayerAvatar __0, out AimRedirect __state) =>
        __state = Begin(__0, null, respectVehicleHead: false);

    static void SnowInstancePrefix(ValuableArcticSnowBike __instance, out AimRedirect __state) =>
        __state = ForSnowBike(__instance);

    static void RestorePostfix(AimRedirect? __state) => __state?.Restore();

    static void ClimbOrbitPrefix(PhysGrabber __instance, ref Quaternion __0)
    {
        if (TryHandRotation(__instance, out var hand))
            __0 = hand;
    }

    static AimRedirect Begin(PlayerAvatar? avatar, PhysGrabber? grabber, bool respectVehicleHead)
    {
        var state = new AimRedirect();
        if (depth > 0 || avatar == null || !TryAimPose(avatar, respectVehicleHead, out var position, out var rotation))
            return state;

        var camera = avatar.localCamera;
        if (camera != null && camera.GetOverrideActive())
            return state;

        if (camera != null)
        {
            state.cameraTransform = camera.transform;
            state.cameraPosition = state.cameraTransform.position;
            state.cameraRotation = state.cameraTransform.rotation;
            state.cameraTransform.SetPositionAndRotation(position, rotation);
        }

        if (grabber != null && PlayerCameraField != null)
        {
            state.grabber = grabber;
            state.previousGrabberCamera = PlayerCameraField.GetValue(grabber) as Camera;
            state.restoreGrabberCamera = true;
            PlayerCameraField.SetValue(grabber, Proxy(position, rotation));
        }

        if (state.cameraTransform != null || state.restoreGrabberCamera)
        {
            depth++;
            if (!logged)
            {
                logged = true;
                Logger.LogInfo("VR hand aim is active for grab turning, vehicles, and the snow bike.");
            }
        }

        return state;
    }

    static bool TryAimPose(PlayerAvatar avatar, bool respectVehicleHead, out Vector3 position, out Quaternion rotation)
    {
        position = default;
        rotation = default;
        var aim = AimTransform(avatar, respectVehicleHead);
        if (aim == null || avatar.localCamera == null || aim == avatar.localCamera.transform)
            return false;

        position = aim.position;
        rotation = aim.rotation;
        return true;
    }

    static bool TryHandRotation(PhysGrabber? grabber, out Quaternion rotation)
    {
        rotation = default;
        var avatar = grabber != null ? grabber.playerAvatar : null;
        var aim = AimTransform(avatar, respectVehicleHead: false);
        if (aim == null)
            return false;

        rotation = aim.rotation * Quaternion.Euler(0f, 180f, 0f);
        return true;
    }

    static Transform? AimTransform(PlayerAvatar? avatar, bool respectVehicleHead)
    {
        if (avatar == null)
            return null;

        if (avatar.isLocal && VRSession.InVR && VRSession.Instance?.Player?.MainHand != null)
        {
            if (respectVehicleHead && VRSession.Instance.Player.NetworkPlayer is { VehicleHeadForward: true })
                return null;

            return VRSession.Instance.Player.MainHand;
        }

        if (NetworkSystem.instance != null &&
            NetworkSystem.instance.GetNetworkPlayer(avatar, out var networkPlayer) &&
            networkPlayer.PrimaryHand != null)
        {
            if (respectVehicleHead && networkPlayer.VehicleHeadForward)
                return null;

            return networkPlayer.PrimaryHand;
        }

        return null;
    }

    static PlayerAvatar? SnowBikeDriver(ValuableArcticSnowBike? bike)
    {
        if (bike == null || SnowBikeDriverMethod == null)
            return null;

        try
        {
            return SnowBikeDriverMethod.Invoke(bike, null) as PlayerAvatar;
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Snow bike Driver() could not be read ({ex.GetType().Name}).");
            return null;
        }
    }

    static void TryPatch(Harmony harmony, Type type, string name, Type[] args, string prefix, string? postfix)
    {
        try
        {
            var method = AccessTools.DeclaredMethod(type, name, args);
            if (method == null)
                return;

            harmony.Patch(method,
                prefix: new HarmonyMethod(AccessTools.Method(typeof(AimRedirect), prefix)),
                postfix: postfix == null ? null : new HarmonyMethod(AccessTools.Method(typeof(AimRedirect), postfix)));
            Logger.LogInfo($"Hand aim helper applied: {type.Name}.{name}");
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Hand aim helper {type.Name}.{name} was not patched ({ex.GetType().Name}).");
        }
    }

    static Camera Proxy(Vector3 position, Quaternion rotation)
    {
        if (proxy == null)
        {
            var marker = new GameObject("RepoXR Hand Aim Proxy");
            marker.hideFlags = HideFlags.HideAndDontSave;
            proxy = marker.AddComponent<Camera>();
            proxy.enabled = false;
        }

        proxy.transform.SetPositionAndRotation(position, rotation);
        return proxy;
    }
}
