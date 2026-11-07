using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RepoXR.Assets;
using RepoXR.Input;
using RepoXR.Managers;
using RepoXR.Networking;
using RepoXR.Player;
using UnityEngine;

using static HarmonyLib.AccessTools;

namespace RepoXR.Patches;

[RepoXRPatch]
internal static class PhysGrabberPatches
{
    /// <summary>
    /// Make certain phys grabber operations operate from the hand transform instead of the camera transform
    /// </summary>
    [HarmonyPatch(typeof(PhysGrabber), nameof(PhysGrabber.Update))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> UpdatePatches(IEnumerable<CodeInstruction> instructions)
    {
        return GameSignatures.EditOrOriginal(instructions, "PhysGrabber hand aim", matcher =>
        {
            matcher.MatchForward(false, new CodeMatch(IsOverrideTransformCall));
            if (matcher.IsInvalid)
                throw new InvalidOperationException("GetOverrideTransform was not called");

            return matcher.Repeat(match =>
                match.Set(OpCodes.Call, PlayerLocalCameraExtensions.GetHandOverrideTransformMethod));
        });
    }

    /// <summary>
    /// Slow down the push/pull logic since it's way too fast in VR
    /// </summary>
    [HarmonyPatch(typeof(PhysGrabber), nameof(PhysGrabber.Update))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> PushPullSlowdownPatch(IEnumerable<CodeInstruction> instructions)
    {
        return GameSignatures.EditOrOriginal(instructions, "PhysGrabber push/pull slowdown", matcher => matcher
            .MatchForward(false, new CodeMatch(OpCodes.Ldc_R4, 0.2f))
            .SetOperandAndAdvance(0.1f)
            .Insert(
                new CodeInstruction(OpCodes.Ldsfld, Field(typeof(InputManager), nameof(InputManager.instance))),
                new CodeInstruction(OpCodes.Callvirt,
                    Method(typeof(InputManager), nameof(InputManager.KeyPullAndPush))),
                new CodeInstruction(OpCodes.Mul)
            )
            .MatchForward(false, new CodeMatch(OpCodes.Ldc_R4, 0.2f))
            .SetOperandAndAdvance(-0.1f)
            .Insert(
                new CodeInstruction(OpCodes.Ldsfld, Field(typeof(InputManager), nameof(InputManager.instance))),
                new CodeInstruction(OpCodes.Callvirt,
                    Method(typeof(InputManager), nameof(InputManager.KeyPullAndPush))),
                new CodeInstruction(OpCodes.Mul)
            ));
    }

    /// <summary>
    /// Provide haptic feedback while something is grabbed
    /// </summary>
    [HarmonyPatch(typeof(PhysGrabber), nameof(PhysGrabber.Update))]
    [HarmonyPostfix]
    private static void HapticFeedbackPatch(PhysGrabber __instance)
    {
        if (!__instance.isLocal)
            return;

        // Do not provide haptic feedback if we're being forced to hold something
        if (__instance.overrideGrabTimer > 0)
            return;

        var grabbed = __instance.grabbed
            ? AssetCollection.GrabberHapticCurve.EvaluateTimed(__instance.loopSound.Source.pitch * 1.12667f) * 0.1f
            : 0;
        var overcharge = __instance.physGrabBeamOverChargeFloat * 0.4f *
                         AssetCollection.OverchargeHapticCurve.EvaluateTimed(__instance.physGrabBeamOverChargeFloat *
                                                                             3);

        if (grabbed + overcharge <= 0)
            return;

        HapticManager.Impulse(HapticManager.Hand.Dominant, HapticManager.Type.Continuous, grabbed + overcharge);
    }

    /// <summary>
    /// When grabbing items, shoot rays out of the hand, instead of the camera
    /// </summary>
    [HarmonyPatch(typeof(PhysGrabber), nameof(PhysGrabber.RayCheck))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> RayCheckPatches(IEnumerable<CodeInstruction> instructions)
    {
        return GameSignatures.EditOrOriginal(instructions, "PhysGrabber grab ray", matcher =>
        {
            matcher.MatchForward(false, new CodeMatch(IsOverrideTransformCall));
            if (matcher.IsInvalid)
                throw new InvalidOperationException("GetOverrideTransform was not called");

            return matcher.Set(OpCodes.Call, PlayerLocalCameraExtensions.GetHandOverrideTransformMethod);
        });
    }

    static bool IsOverrideTransformCall(CodeInstruction instruction)
    {
        return (instruction.opcode == OpCodes.Call || instruction.opcode == OpCodes.Callvirt)
               && instruction.operand is MethodInfo method
               && method.DeclaringType == typeof(PlayerLocalCamera)
               && method.Name == nameof(PlayerLocalCamera.GetOverrideTransform);
    }

    /// <summary>
    /// Make the object turning input use the controller inputs instead of mouse inputs
    /// </summary>
    [HarmonyPatch(typeof(PhysGrabber), nameof(PhysGrabber.ObjectTurning))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> ObjectTurningPatches(IEnumerable<CodeInstruction> instructions)
    {
        var list = new List<CodeInstruction>(instructions);
        var replaced = MouseAxisRewrite.Replace(list,
            Method(typeof(PhysGrabberPatches), nameof(StickX)),
            Method(typeof(PhysGrabberPatches), nameof(StickY)));
        if (replaced == 0)
            Logger.LogInfo(
                "PhysGrabber.ObjectTurning has no Mouse X/Y axis read on this build. Turning stays on the game's own input.");
        else
            Logger.LogInfo($"PhysGrabber.ObjectTurning reads the controller for {replaced} mouse axis call(s).");

        return list;
    }

    static float StickX() => Actions.Instance["Rotation"].ReadValue<Vector2>().x;

    static float StickY() => Actions.Instance["Rotation"].ReadValue<Vector2>().y;

    /// <summary>
    /// Allow a custom override to disable object turning
    /// </summary>
    [HarmonyPatch(typeof(PhysGrabber), nameof(PhysGrabber.ObjectTurning))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> DisableTurningPatch(IEnumerable<CodeInstruction> instructions)
    {
        return GameSignatures.EditOrOriginal(instructions, "PhysGrabber disable turning", matcher =>
        {
            matcher.MatchForward(false, new CodeMatch(OpCodes.Call, Method(typeof(SemiFunc), nameof(SemiFunc.InputHold))))
                .Advance(1);

            var jmp = matcher.Instruction;
            return matcher.Advance(1).InsertAndAdvance(
                new CodeInstruction(OpCodes.Call, PropertyGetter(typeof(VRSession), nameof(VRSession.Instance))),
                new CodeInstruction(OpCodes.Callvirt, PropertyGetter(typeof(VRSession), nameof(VRSession.Player))),
                new CodeInstruction(OpCodes.Ldfld, Field(typeof(VRPlayer), nameof(VRPlayer.disableRotateTimer))),
                new CodeInstruction(OpCodes.Ldc_R4, 0f),
                new CodeInstruction(OpCodes.Bgt_Un_S, jmp.operand)
            );
        });
    }

    /// <summary>
    /// Detect item release and try to equip item if possible
    /// </summary>
    [HarmonyPatch(typeof(PhysGrabber), nameof(PhysGrabber.ReleaseObject))]
    [HarmonyPrefix]
    private static void OnReleaseObject(PhysGrabber __instance)
    {
        if (!__instance.grabbed || !__instance.isLocal || !__instance.grabbedObject ||
            !__instance.grabbedObject.TryGetComponent<ItemEquippable>(out var item))
            return;

        if (VRSession.Instance is not { } session)
            return;

        session.Player.Rig.inventoryController.TryEquipItem(item);
    }

    /// <summary>
    /// Make sure the force grab timer also works in VR, where the grab key is the same as the "take from inventory" key
    /// </summary>
    [HarmonyPatch(typeof(PhysGrabber), nameof(PhysGrabber.Update))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> ForceHoldPatch(IEnumerable<CodeInstruction> instructions)
    {
        // 0.5.1 moved the auto-hold check. The 0.4.4 insert compiled into a call the
        // verifier rejected (PhysGrabber.Update IL_069b) and that aborted every other
        // grabber patch. Leave Update's grab toggle on the game's own path.
        _ = instructions;
        return instructions;
    }
}

[RepoXRPatch(RepoXRPatchTarget.Universal)]
internal static class PhysGrabberUniversalPatches
{
    static bool turningUsesOverrideCall;

    [HarmonyPatch(typeof(PhysGrabber), nameof(PhysGrabber.ObjectTurning))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> HandAimTurningSwap(IEnumerable<CodeInstruction> instructions)
    {
        // 0.4.4 called GetOverrideTransform here. Swap that call when it is still present.
        // 0.5.1 removed it; the prefix below aims the camera for the length of the call.
        var edited = SwapOverrideTransform(instructions, PlayerLocalCameraExtensions.GetHandOverrideTransformMethod,
            out turningUsesOverrideCall);
        Logger.LogInfo(turningUsesOverrideCall
            ? "PhysGrabber.ObjectTurning hand aim swapped GetOverrideTransform."
            : "PhysGrabber.ObjectTurning hand aim follows the hand for each call.");
        return edited;
    }

    [HarmonyPatch(typeof(PhysGrabber), nameof(PhysGrabber.ObjectTurning))]
    [HarmonyPrefix]
    private static void HandAimTurningPrefix(PhysGrabber __instance, out AimRedirect __state)
    {
        __state = turningUsesOverrideCall ? AimRedirect.None : AimRedirect.ForGrabTurn(__instance);
    }

    [HarmonyPatch(typeof(PhysGrabber), nameof(PhysGrabber.ObjectTurning))]
    [HarmonyPostfix]
    private static void HandAimTurningPostfix(AimRedirect __state)
    {
        __state?.Restore();
    }

    /// <summary>
    /// Force the grabbing of objects to start from the hand
    /// </summary>
    [HarmonyPatch(typeof(PhysGrabber), nameof(PhysGrabber.StartGrabbingPhysObject))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> GrabFromHandPatch(IEnumerable<CodeInstruction> instructions)
    {
        return GameSignatures.EditOrOriginal(instructions, "PhysGrabber grab-from-hand", matcher =>
        {
            matcher.MatchForward(false,
                new CodeMatch(OpCodes.Callvirt,
                    Method(typeof(PlayerLocalCamera), nameof(PlayerLocalCamera.GetOverrideTransform))));
            if (matcher.IsInvalid)
                throw new InvalidOperationException("GetOverrideTransform was not called");

            return matcher.Set(OpCodes.Call, PlayerLocalCameraExtensions.GetHandOverrideTransformMethod);
        });
    }

    /// <summary>
    /// Replace the camera quaternion used by the first Quaternion.Angle call. The previous
    /// edit deleted the instructions in front of that call and left an unverifiable call
    /// (GrabStateClimb IL_0409). This insert only consumes the quaternion already on the
    /// stack and pushes the hand rotation, so the method stays verifiable.
    /// </summary>
    [HarmonyPatch(typeof(PhysGrabber), nameof(PhysGrabber.GrabStateClimb))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> HandBasedTumbleClimbPatch(IEnumerable<CodeInstruction> instructions)
    {
        var list = new List<CodeInstruction>(instructions);
        var matcher = new CodeMatcher(list);
        matcher.MatchForward(false, new CodeMatch(IsQuaternionAngle));
        if (matcher.IsInvalid)
        {
            Logger.LogInfo(
                "PhysGrabber.GrabStateClimb has no Quaternion.Angle on this build. Hand rotation is applied through ClimbOrbitRotationGet when that method exists.");
            return list;
        }

        matcher.Insert(
            new CodeInstruction(OpCodes.Ldarg_0),
            new CodeInstruction(OpCodes.Call, Method(typeof(AimRedirect), nameof(AimRedirect.ClimbRotation))));
        Logger.LogInfo("PhysGrabber.GrabStateClimb hand rotation applies at Quaternion.Angle.");
        return matcher.InstructionEnumeration();
    }

    static bool IsQuaternionAngle(CodeInstruction instruction)
    {
        return (instruction.opcode == OpCodes.Call || instruction.opcode == OpCodes.Callvirt)
               && instruction.operand is MethodInfo method
               && method.DeclaringType == typeof(Quaternion)
               && method.Name == nameof(Quaternion.Angle)
               && method.GetParameters().Length == 2;
    }

    static IEnumerable<CodeInstruction> SwapOverrideTransform(IEnumerable<CodeInstruction> instructions,
        MethodInfo replacement, out bool swapped)
    {
        var list = new List<CodeInstruction>(instructions);
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

        return list;
    }
}