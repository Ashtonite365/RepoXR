using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RepoXR.Managers;
using RepoXR.Networking;
using RepoXR.Patches;
using UnityEngine;
using static HarmonyLib.AccessTools;

namespace RepoXR.Patches.Item;

[RepoXRPatch(RepoXRPatchTarget.Universal)]
internal static class ItemVehiclePatches
{
    /// <summary>
    /// Require a higher amount of X axis input before initiating a drift for VR players
    /// </summary>
    [HarmonyPatch(typeof(ItemVehicle), nameof(ItemVehicle.ApplyVehicleDrift))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> DriftDeadzonePatch(IEnumerable<CodeInstruction> instructions)
    {
        return GameSignatures.EditOrOriginal(instructions, "Vehicle drift deadzone", matcher => matcher
            .MatchForward(false, new CodeMatch(OpCodes.Ldc_R4, 0.1f))
            .SetOpcodeAndAdvance(OpCodes.Ldarg_0)
            .Insert(new CodeInstruction(OpCodes.Call, ((Func<ItemVehicle, float>)GetDriftDeadzone).Method)));

        static float GetDriftDeadzone(ItemVehicle vehicle) => vehicle.currentDriver.IsVRPlayer() ? 0.5f : 0.1f;
    }

    static bool movementUsesOverrideCall;
    static bool steeringUsesOverrideCall;
    static bool driftUsesOverrideCall;
    static bool steeringUpdateUsesOverrideCall;

    [HarmonyPatch(typeof(ItemVehicle), nameof(ItemVehicle.ApplyVehicleMovement))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> MovementAimSwap(IEnumerable<CodeInstruction> instructions) =>
        SwapOverrideTransform(instructions, nameof(ItemVehicle.ApplyVehicleMovement), out movementUsesOverrideCall);

    [HarmonyPatch(typeof(ItemVehicle), nameof(ItemVehicle.ApplyVehicleMovement))]
    [HarmonyPrefix]
    private static void MovementAimPrefix(ItemVehicle __instance, out AimRedirect __state) =>
        __state = movementUsesOverrideCall ? AimRedirect.None : AimRedirect.ForVehicle(__instance);

    [HarmonyPatch(typeof(ItemVehicle), nameof(ItemVehicle.ApplyVehicleMovement))]
    [HarmonyPostfix]
    private static void MovementAimPostfix(AimRedirect __state) => __state?.Restore();

    [HarmonyPatch(typeof(ItemVehicle), nameof(ItemVehicle.ApplyVehicleSteering))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> SteeringAimSwap(IEnumerable<CodeInstruction> instructions) =>
        SwapOverrideTransform(instructions, nameof(ItemVehicle.ApplyVehicleSteering), out steeringUsesOverrideCall);

    [HarmonyPatch(typeof(ItemVehicle), nameof(ItemVehicle.ApplyVehicleSteering))]
    [HarmonyPrefix]
    private static void SteeringAimPrefix(ItemVehicle __instance, out AimRedirect __state) =>
        __state = steeringUsesOverrideCall ? AimRedirect.None : AimRedirect.ForVehicle(__instance);

    [HarmonyPatch(typeof(ItemVehicle), nameof(ItemVehicle.ApplyVehicleSteering))]
    [HarmonyPostfix]
    private static void SteeringAimPostfix(AimRedirect __state) => __state?.Restore();

    [HarmonyPatch(typeof(ItemVehicle), nameof(ItemVehicle.ApplyVehicleDrift))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> DriftAimSwap(IEnumerable<CodeInstruction> instructions) =>
        SwapOverrideTransform(instructions, nameof(ItemVehicle.ApplyVehicleDrift), out driftUsesOverrideCall);

    [HarmonyPatch(typeof(ItemVehicle), nameof(ItemVehicle.ApplyVehicleDrift))]
    [HarmonyPrefix]
    private static void DriftAimPrefix(ItemVehicle __instance, out AimRedirect __state) =>
        __state = driftUsesOverrideCall ? AimRedirect.None : AimRedirect.ForVehicle(__instance);

    [HarmonyPatch(typeof(ItemVehicle), nameof(ItemVehicle.ApplyVehicleDrift))]
    [HarmonyPostfix]
    private static void DriftAimPostfix(AimRedirect __state) => __state?.Restore();

    [HarmonyPatch(typeof(ItemVehicle), nameof(ItemVehicle.UpdateSteering))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> SteeringUpdateSwap(IEnumerable<CodeInstruction> instructions) =>
        SwapOverrideTransform(instructions, nameof(ItemVehicle.UpdateSteering), out steeringUpdateUsesOverrideCall);

    [HarmonyPatch(typeof(ItemVehicle), nameof(ItemVehicle.UpdateSteering))]
    [HarmonyPrefix]
    private static void SteeringUpdatePrefix(ItemVehicle __instance, out AimRedirect __state) =>
        __state = steeringUpdateUsesOverrideCall ? AimRedirect.None : AimRedirect.ForVehicle(__instance);

    [HarmonyPatch(typeof(ItemVehicle), nameof(ItemVehicle.UpdateSteering))]
    [HarmonyPostfix]
    private static void SteeringUpdatePostfix(AimRedirect __state) => __state?.Restore();

    // TODO: Should we override GetOverrideTransform in LocalMeshUpdate?
    // Looks like LocalMeshUpdate does whatever LateUpdate did before, so we can maybe keep this code as-is
    [HarmonyPatch(typeof(ItemVehicle), nameof(ItemVehicle.LocalMeshUpdate))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> LateUpdatePatch(IEnumerable<CodeInstruction> instructions)
    {
        return GameSignatures.EditOrOriginal(instructions, "Vehicle mesh aim", matcher => matcher
            .MatchForward(false, OverrideTransformCall())
            .Advance(1)
            .MatchForward(false, OverrideTransformCall())
            .Set(OpCodes.Call, ((Func<PlayerLocalCamera, Transform>)GetVehicleTargetForward).Method));
    }

    static CodeMatch OverrideTransformCall() => new(OpCodes.Callvirt,
        Method(typeof(PlayerLocalCamera), nameof(PlayerLocalCamera.GetOverrideTransform)));

    /// <summary>
    /// When this build still calls GetOverrideTransform, retarget that call. When it does
    /// not, the instructions are left untouched and the prefix aims the camera instead.
    /// </summary>
    static IEnumerable<CodeInstruction> SwapOverrideTransform(IEnumerable<CodeInstruction> instructions, string name,
        out bool swapped)
    {
        var list = new List<CodeInstruction>(instructions);
        var replacement = ((Func<PlayerLocalCamera, Transform>)GetVehicleTargetForward).Method;
        swapped = false;
        foreach (var ins in list)
        {
            if (ins.operand is not MethodInfo method)
                continue;
            if (method.DeclaringType != typeof(PlayerLocalCamera) ||
                method.Name != nameof(PlayerLocalCamera.GetOverrideTransform))
                continue;
            if (ins.opcode != OpCodes.Call && ins.opcode != OpCodes.Callvirt)
                continue;

            ins.opcode = OpCodes.Call;
            ins.operand = replacement;
            swapped = true;
        }

        Logger.LogInfo(swapped
            ? $"ItemVehicle.{name} hand aim swapped GetOverrideTransform."
            : $"ItemVehicle.{name} hand aim follows the driver's hand for each call.");
        return list;
    }

    /// <summary>
    /// Get the correct forward based on the target player's setting and whether they're in VR
    /// </summary>
    private static Transform GetVehicleTargetForward(PlayerLocalCamera camera)
    {
        var player = camera.playerAvatar;

        if (player.isLocal && VRSession.InVR)
            return
                VRSession.Instance.Player.NetworkPlayer.VehicleHeadForward
                    ? player.localCamera.GetOverrideTransform()
                    : player.localCamera.GetHandOverrideTransform();

        if (NetworkSystem.instance.GetNetworkPlayer(player, out var networkPlayer))
            return
                networkPlayer.VehicleHeadForward
                    ? player.localCamera.GetOverrideTransform()
                    : player.localCamera.GetHandOverrideTransform();

        return player.localCamera.GetOverrideTransform();
    }
}