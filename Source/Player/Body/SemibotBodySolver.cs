using System.Collections.Generic;
using UnityEngine;

namespace RepoXR.Player.Body;

/// <summary>
/// Poses the semibot hips, chest, and legs from up to three calibrated targets.
/// Leg bones are the game's ANIM LEG blocks, not a humanoid knee chain.
/// </summary>
public class SemibotBodySolver
{
    const float MaxRootDelta = 1.5f;
    const float MinLegScale = 0.25f;
    const float MaxLegScale = 1.75f;

    PlayerAvatarVisuals visuals;
    Transform hip;
    Transform chest;
    Transform legTwist;
    Transform leftThigh;
    Transform leftShin;
    Transform rightThigh;
    Transform rightShin;
    Transform head;
    bool reportedMissing;

    public bool TryGetAnchor(PlayerAvatarVisuals avatarVisuals, BodyRole role, out Vector3 position,
        out Quaternion rotation)
    {
        position = default;
        rotation = Quaternion.identity;

        if (!Bind(avatarVisuals))
            return false;

        var bone = BoneFor(role);
        if (!bone)
            return false;

        position = bone!.position;
        rotation = bone.rotation;
        return true;
    }

    public void Apply(PlayerAvatar avatar, PlayerAvatarVisuals avatarVisuals, IReadOnlyList<BodyPose> poses)
    {
        if (!avatar || !avatarVisuals || avatarVisuals.isMenuAvatar || avatar.isTumbling || avatar.deadSet)
            return;

        if (avatar.localCamera && avatar.localCamera.GetOverrideActive())
            return;

        if (!Bind(avatarVisuals))
            return;

        BodyPose waist = default, chestPose = default, left = default, right = default;
        foreach (var pose in poses)
        {
            if (!pose.Active || pose.Role == BodyRole.Unused)
                continue;

            switch (pose.Role)
            {
                case BodyRole.Waist when !waist.Active:
                    waist = pose;
                    break;
                case BodyRole.Chest when !chestPose.Active:
                    chestPose = pose;
                    break;
                case BodyRole.LeftFoot when !left.Active:
                    left = pose;
                    break;
                case BodyRole.RightFoot when !right.Active:
                    right = pose;
                    break;
            }
        }

        if (!waist.Active && !chestPose.Active && !left.Active && !right.Active)
            return;

        var headRotation = head ? head.rotation : Quaternion.identity;

        if (waist.Active && hip)
        {
            var delta = waist.Position - hip.position;
            if (delta.sqrMagnitude <= MaxRootDelta * MaxRootDelta)
            {
                hip.position += delta;
                if (legTwist)
                    legTwist.position += delta;

                ShiftIfNeeded(leftThigh, delta);
                ShiftIfNeeded(rightThigh, delta);
                hip.rotation = waist.Rotation;
            }
        }
        else if (chestPose.Active && chest && hip)
        {
            var delta = chestPose.Position - chest.position;
            delta.x = Mathf.Clamp(delta.x, -0.35f, 0.35f);
            delta.y = Mathf.Clamp(delta.y, -MaxRootDelta, MaxRootDelta);
            delta.z = Mathf.Clamp(delta.z, -0.35f, 0.35f);

            hip.position += delta;
            if (legTwist)
                legTwist.position += delta;

            ShiftIfNeeded(leftThigh, delta);
            ShiftIfNeeded(rightThigh, delta);
        }

        if (chestPose.Active && chest)
            chest.rotation = chestPose.Rotation;

        if (left.Active)
            PoseLeg(leftThigh, leftShin, left.Position, left.Rotation);

        if (right.Active)
            PoseLeg(rightThigh, rightShin, right.Position, right.Rotation);

        if (head)
            head.rotation = headRotation;
    }

    void ShiftIfNeeded(Transform thigh, Vector3 delta)
    {
        if (!thigh || !hip)
            return;

        if (thigh.IsChildOf(hip) || (legTwist && thigh.IsChildOf(legTwist)))
            return;

        thigh.position += delta;
    }

    static void PoseLeg(Transform thigh, Transform shin, Vector3 footPosition, Quaternion footRotation)
    {
        if (!thigh || !shin)
            return;

        var toTarget = footPosition - thigh.position;
        var distance = toTarget.magnitude;
        if (distance < 0.0001f)
            return;

        var rest = shin.position - thigh.position;
        var restLength = rest.magnitude;
        if (restLength < 0.0001f)
        {
            shin.SetPositionAndRotation(footPosition, footRotation);
            return;
        }

        thigh.rotation = Quaternion.FromToRotation(rest, toTarget) * thigh.rotation;

        var reach = Mathf.Clamp(distance, restLength * MinLegScale, restLength * MaxLegScale);
        shin.position = thigh.position + toTarget / distance * reach;
        shin.rotation = footRotation;
    }

    Transform BoneFor(BodyRole role)
    {
        return role switch
        {
            BodyRole.Waist => hip,
            BodyRole.Chest => chest,
            BodyRole.LeftFoot => leftShin ? leftShin : leftThigh,
            BodyRole.RightFoot => rightShin ? rightShin : rightThigh,
            _ => null!
        };
    }

    bool Bind(PlayerAvatarVisuals avatarVisuals)
    {
        if (visuals == avatarVisuals && hip)
            return true;

        visuals = avatarVisuals;
        var root = avatarVisuals.transform;
        hip = Find(root, "ANIM BODY BOT");
        chest = avatarVisuals.bodyTopUpTransform
                ? avatarVisuals.bodyTopUpTransform
                : Find(root, "ANIM BODY TOP");
        legTwist = avatarVisuals.legTwistTransform
            ? avatarVisuals.legTwistTransform
            : Find(root, "code_leg_twist");
        leftThigh = Find(root, "ANIM LEG L BOT");
        leftShin = Find(root, "ANIM LEG L TOP");
        rightThigh = Find(root, "ANIM LEG R BOT");
        rightShin = Find(root, "ANIM LEG R TOP");
        head = avatarVisuals.headUpTransform ? avatarVisuals.headUpTransform : Find(root, "ANIM HEAD TOP");

        if (hip && (leftThigh || leftShin) && (rightThigh || rightShin))
            return true;

        if (!reportedMissing)
        {
            reportedMissing = true;
            Logger.LogWarning(
                "Full-body tracking could not find the semibot hip/leg bones (ANIM BODY BOT, ANIM LEG L/R BOT/TOP). Body pose will stay on the normal animation.");
        }

        return hip || leftThigh || rightThigh || chest;
    }

    static Transform Find(Transform root, string name)
    {
        var transforms = root.GetComponentsInChildren<Transform>(true);
        foreach (var transform in transforms)
        {
            if (transform.name == name)
                return transform;
        }

        return null!;
    }
}
