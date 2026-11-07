using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RepoXR.Managers;
using RepoXR.Networking;
using UnityEngine;

using static HarmonyLib.AccessTools;

namespace RepoXR.Patches.Valuables;

[RepoXRPatch(RepoXRPatchTarget.Universal)]
internal static class ValuableArcticSnowBikePatches
{
    /// <summary>
    /// Steer the arctic bike from the hand. Builds that still load PlayerAvatar.localCamera
    /// get that load swapped for the hand. 0.5.1 does not, so the prefix aims the driver's
    /// camera for the call and restores it afterward. DriveHeadingUpdate is patched the
    /// same way when that method exists.
    /// </summary>
    static bool steeringUsesCameraField;

    [HarmonyPatch(typeof(ValuableArcticSnowBike), nameof(ValuableArcticSnowBike.UpdateSteering))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> HandSteeringSwap(IEnumerable<CodeInstruction> instructions)
    {
        var list = new List<CodeInstruction>(instructions);
        var replacement = ((Func<PlayerAvatar, Transform>)GetSourceTransform).Method;
        steeringUsesCameraField = false;
        foreach (var ins in list)
        {
            if (ins.opcode != OpCodes.Ldfld || ins.operand is not FieldInfo field)
                continue;
            if (field.DeclaringType != typeof(PlayerAvatar) || field.Name != nameof(PlayerAvatar.localCamera))
                continue;

            ins.opcode = OpCodes.Call;
            ins.operand = replacement;
            steeringUsesCameraField = true;
            break;
        }

        Logger.LogInfo(steeringUsesCameraField
            ? "ValuableArcticSnowBike.UpdateSteering hand aim swapped the camera load."
            : "ValuableArcticSnowBike.UpdateSteering hand aim follows the driver's hand for each call.");
        return list;
    }

    [HarmonyPatch(typeof(ValuableArcticSnowBike), nameof(ValuableArcticSnowBike.UpdateSteering))]
    [HarmonyPrefix]
    private static void HandSteeringPrefix(ValuableArcticSnowBike __instance, out AimRedirect __state) =>
        __state = steeringUsesCameraField ? AimRedirect.None : AimRedirect.ForSnowBike(__instance);

    [HarmonyPatch(typeof(ValuableArcticSnowBike), nameof(ValuableArcticSnowBike.UpdateSteering))]
    [HarmonyPostfix]
    private static void HandSteeringPostfix(AimRedirect __state) => __state?.Restore();

    static Transform GetSourceTransform(PlayerAvatar player)
    {
        if (player.isLocal)
            return VRSession.InVR && VRSession.Instance?.Player?.MainHand != null
                ? VRSession.Instance.Player.MainHand
                : player.localCamera.transform;

        if (NetworkSystem.instance != null &&
            NetworkSystem.instance.GetNetworkPlayer(player, out var networkPlayer) &&
            networkPlayer.PrimaryHand != null)
            return networkPlayer.PrimaryHand;

        return player.localCamera.transform;
    }
}