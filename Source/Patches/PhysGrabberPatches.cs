using System;
using System.Collections.Generic;
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
            matcher.MatchForward(false,
                new CodeMatch(OpCodes.Callvirt,
                    Method(typeof(PlayerLocalCamera), nameof(PlayerLocalCamera.GetOverrideTransform))));
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
        return new CodeMatcher(instructions)
            // Replace grabber checks from camera with checks from hand
            .MatchForward(false,
                new CodeMatch(OpCodes.Callvirt,
                    Method(typeof(PlayerLocalCamera), nameof(PlayerLocalCamera.GetOverrideTransform))))
            .Set(OpCodes.Call, PlayerLocalCameraExtensions.GetHandOverrideTransformMethod)
            .InstructionEnumeration();
    }

    /// <summary>
    /// Make the object turning input use the controller inputs instead of mouse inputs
    /// </summary>
    [HarmonyPatch(typeof(PhysGrabber), nameof(PhysGrabber.ObjectTurning))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> ObjectTurningPatches(IEnumerable<CodeInstruction> instructions)
    {
        // Hard-coded local slots from 0.4.4. If "Mouse X" moved, leave turning alone
        // instead of emitting ldloca against locals this build does not have.
        return GameSignatures.EditOrOriginal(instructions, "PhysGrabber object turning", matcher => matcher
            .MatchForward(false, new CodeMatch(OpCodes.Ldstr, "Mouse X"))
            .RemoveInstructions(10)
            .Insert(
                new CodeInstruction(OpCodes.Ldloca_S, Plugin.GameDebugBuild ? 13 : 5),
                new CodeInstruction(OpCodes.Ldloca_S, Plugin.GameDebugBuild ? 14 : 6),
                new CodeInstruction(OpCodes.Call, Method(typeof(PhysGrabberPatches), nameof(GetRotationInput)))
            ));
    }

    private static void GetRotationInput(out float x, out float y)
    {
        var input = Actions.Instance["Rotation"].ReadValue<Vector2>();

        x = input.x;
        y = input.y;
    }

    /// <summary>
    /// Allow a custom override to disable object turning
    /// </summary>
    [HarmonyPatch(typeof(PhysGrabber), nameof(PhysGrabber.ObjectTurning))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> DisableTurningPatch(IEnumerable<CodeInstruction> instructions)
    {
        var matcher = new CodeMatcher(instructions)
            .MatchForward(false, new CodeMatch(OpCodes.Call, Method(typeof(SemiFunc), nameof(SemiFunc.InputHold))))
            .Advance(1);

        var jmp = matcher.Instruction;

        matcher.Advance(1).InsertAndAdvance(
            new CodeInstruction(OpCodes.Call, PropertyGetter(typeof(VRSession), nameof(VRSession.Instance))),
            new CodeInstruction(OpCodes.Callvirt, PropertyGetter(typeof(VRSession), nameof(VRSession.Player))),
            new CodeInstruction(OpCodes.Ldfld, Field(typeof(VRPlayer), nameof(VRPlayer.disableRotateTimer))),
            new CodeInstruction(OpCodes.Ldc_R4, 0f),
            new CodeInstruction(OpCodes.Bgt_Un_S, jmp.operand)
        );

        return matcher
            .InstructionEnumeration();
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
    [HarmonyPatch(typeof(PhysGrabber), nameof(PhysGrabber.ObjectTurning))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> ObjectTurningPatches(IEnumerable<CodeInstruction> instructions)
    {
        return GameSignatures.EditOrOriginal(instructions, "PhysGrabber turning hand aim", matcher =>
        {
            var replacement = PlayerLocalCameraExtensions.GetHandOverrideTransformMethod;
            var match = new CodeMatch(OpCodes.Callvirt,
                Method(typeof(PlayerLocalCamera), nameof(PlayerLocalCamera.GetOverrideTransform)));
            matcher.MatchForward(false, match);
            if (matcher.IsInvalid)
                throw new InvalidOperationException("GetOverrideTransform was not called");

            return matcher.Repeat(hit => hit.Set(OpCodes.Call, replacement));
        });
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
    /// 0.5.1 still calls Quaternion.Angle, but the instructions around that call are not the
    /// 0.4.4 sequence. Rewriting them produced unverifiable IL (GrabStateClimb call 0x0000f1)
    /// and failed this whole patch class. Climb keeps the game's own rotation.
    /// </summary>
    [HarmonyPatch(typeof(PhysGrabber), nameof(PhysGrabber.GrabStateClimb))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> HandBasedTumbleClimbPatch(IEnumerable<CodeInstruction> instructions)
    {
        Logger.LogInfo(
            "Leaving PhysGrabber.GrabStateClimb unchanged. The 0.5.1 method body does not match the hand-rotation edit.");
        return instructions;
    }
}