using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace RepoXR.Player.Body;

/// <summary>
/// Local calibration mannequin. A copy of the semibot is held in a T-pose and
/// live tracker positions are drawn on it as circles. This is a preview only.
/// </summary>
public class BodyCalibrationPreview : MonoBehaviour
{
    const int CircleSegments = 28;

    Transform holder = null!;
    Transform anchor = null!;
    Transform model = null!;
    Transform hint = null!;
    readonly List<TrackerCircle> circles = new();
    bool shown;

    struct TrackerCircle
    {
        public Transform Root;
        public TextMesh Label;
    }

    public bool IsShown => shown;

    public void Show(PlayerAvatar avatar)
    {
        Hide();

        var visuals = avatar.playerAvatarVisuals;
        if (!visuals)
            return;

        var source = visuals.transform;
        if (visuals.meshParent && Find(visuals.meshParent.transform, "ANIM BODY BOT"))
            source = visuals.meshParent.transform;

        holder = new GameObject("FBT Calibration Preview").transform;
        anchor = new GameObject("FBT Preview Anchor").transform;
        anchor.SetParent(holder, false);

        var clone = Instantiate(source.gameObject, holder);
        clone.name = "FBT T-Pose";
        model = clone.transform;

        foreach (var behaviour in clone.GetComponentsInChildren<MonoBehaviour>(true))
            behaviour.enabled = false;

        foreach (var animator in clone.GetComponentsInChildren<Animator>(true))
            animator.enabled = false;

        foreach (var collider in clone.GetComponentsInChildren<Collider>(true))
            collider.enabled = false;

        StripComponents(clone);

        var forward = FlatForward(avatar.transform.forward);
        var yaw = Quaternion.LookRotation(-forward, Vector3.up);
        model.SetPositionAndRotation(avatar.transform.position + forward * 1.7f, yaw);
        ForceTPose(model);

        var previewHip = Find(model, "ANIM BODY BOT");
        if (previewHip)
            AlignAnchor(avatar.transform, previewHip);
        else
            anchor.SetPositionAndRotation(model.position, yaw);

        foreach (var renderer in clone.GetComponentsInChildren<Renderer>(true))
        {
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.gameObject.layer = 0;
        }

        hint = CreateHint(model.position + Vector3.up * 2.1f);
        hint.SetParent(holder, true);
        shown = true;
    }

    public void Hide()
    {
        shown = false;
        if (holder)
        {
            foreach (var line in holder.GetComponentsInChildren<LineRenderer>(true))
            {
                if (line.material)
                    Destroy(line.material);
            }

            Destroy(holder.gameObject);
        }

        circles.Clear();

        holder = null!;
        anchor = null!;
        model = null!;
        hint = null!;
    }

    public void Tick(PlayerAvatar avatar, IReadOnlyList<TrackerDot> dots)
    {
        if (!shown || !model || !anchor)
            return;

        ForceTPose(model);

        var liveHip = Find(avatar.playerAvatarVisuals.transform, "ANIM BODY BOT");
        var previewHip = Find(model, "ANIM BODY BOT");
        if (liveHip && previewHip)
            AlignAnchor(avatar.transform, previewHip, liveHip);
        else
            anchor.rotation = Quaternion.Euler(0f, model.eulerAngles.y, 0f);

        EnsureCircles(dots.Count);

        var camera = UnityEngine.Camera.main ? UnityEngine.Camera.main.transform : null;
        for (var i = 0; i < circles.Count; i++)
        {
            var visible = i < dots.Count && dots[i].Active;
            circles[i].Root.gameObject.SetActive(visible);
            if (!visible)
                continue;

            circles[i].Root.position = ToPreview(avatar.transform, dots[i].WorldPosition);
            if (camera)
                circles[i].Root.rotation = Quaternion.LookRotation(circles[i].Root.position - camera!.position, Vector3.up);

            circles[i].Label.text = dots[i].Label;
        }

        if (hint && camera)
            hint.rotation = Quaternion.LookRotation(hint.position - camera!.position);
    }

    public bool TryGetReference(BodyRole role, Transform avatarRoot, out Vector3 worldPosition, out Quaternion worldRotation)
    {
        worldPosition = default;
        worldRotation = Quaternion.identity;

        if (!shown || !model || !anchor)
            return false;

        var bone = BoneFor(model, role);
        if (!bone)
            return false;

        worldPosition = ToPlayer(avatarRoot, bone.position);
        worldRotation = ToPlayerRotation(avatarRoot, bone.rotation);
        return true;
    }

    void AlignAnchor(Transform avatarRoot, Transform previewHip, Transform liveHip = null!)
    {
        var yaw = Quaternion.Euler(0f, model.eulerAngles.y, 0f);
        anchor.rotation = yaw;

        var hipWorld = liveHip ? liveHip.position : avatarRoot.position + Vector3.up;
        var hipLocal = AvatarLocal(avatarRoot, hipWorld);
        anchor.position = previewHip.position - yaw * hipLocal;
    }

    Vector3 ToPreview(Transform avatarRoot, Vector3 world)
    {
        return anchor.position + anchor.rotation * AvatarLocal(avatarRoot, world);
    }

    Vector3 ToPlayer(Transform avatarRoot, Vector3 previewWorld)
    {
        var local = Quaternion.Inverse(anchor.rotation) * (previewWorld - anchor.position);
        return avatarRoot.position + Yaw(avatarRoot) * local;
    }

    Quaternion ToPlayerRotation(Transform avatarRoot, Quaternion previewWorld)
    {
        var local = Quaternion.Inverse(anchor.rotation) * previewWorld;
        return Yaw(avatarRoot) * local;
    }

    static Vector3 AvatarLocal(Transform avatarRoot, Vector3 world)
    {
        return Quaternion.Inverse(Yaw(avatarRoot)) * (world - avatarRoot.position);
    }

    static Quaternion Yaw(Transform avatarRoot)
    {
        return Quaternion.Euler(0f, avatarRoot.eulerAngles.y, 0f);
    }

    void EnsureCircles(int count)
    {
        var colors = new[]
        {
            new Color(1f, 0.85f, 0.2f),
            new Color(0.3f, 0.85f, 1f),
            new Color(1f, 0.45f, 0.75f)
        };

        while (circles.Count < count)
        {
            var index = circles.Count;
            var root = new GameObject($"FBT Tracker Circle {index + 1}");
            root.transform.SetParent(holder, true);

            var line = root.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = CircleSegments;
            line.widthMultiplier = 0.012f;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            var color = colors[index % colors.Length];
            line.startColor = color;
            line.endColor = color;

            var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color") ?? Shader.Find("UI/Default");
            if (shader)
                line.material = new Material(shader) { color = color };

            for (var i = 0; i < CircleSegments; i++)
            {
                var angle = i / (float)CircleSegments * Mathf.PI * 2f;
                line.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * 0.07f);
            }

            var labelObject = new GameObject("Label");
            labelObject.transform.SetParent(root.transform, false);
            labelObject.transform.localPosition = Vector3.up * 0.1f;
            var label = labelObject.AddComponent<TextMesh>();
            label.characterSize = 0.04f;
            label.fontSize = 48;
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.color = color;
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                       ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (font)
                label.font = font;

            circles.Add(new TrackerCircle { Root = root.transform, Label = label });
        }
    }

    static Transform CreateHint(Vector3 position)
    {
        var root = new GameObject("FBT Calibrate Hint");
        root.transform.position = position;
        var text = root.AddComponent<TextMesh>();
        text.text = "T-pose\nPress both triggers";
        text.characterSize = 0.045f;
        text.fontSize = 56;
        text.anchor = TextAnchor.MiddleCenter;
        text.alignment = TextAlignment.Center;
        text.color = Color.white;
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                   ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
        if (font)
            text.font = font;

        return root.transform;
    }

    static void ForceTPose(Transform modelRoot)
    {
        var forward = FlatForward(modelRoot.forward);
        var right = Vector3.Cross(Vector3.up, forward);

        AimBone(Find(modelRoot, "ANIM LEG L BOT"), Find(modelRoot, "ANIM LEG L TOP"), Vector3.down);
        AimBone(Find(modelRoot, "ANIM LEG R BOT"), Find(modelRoot, "ANIM LEG R TOP"), Vector3.down);
        AimBone(FindArm(modelRoot, true), ChildBone(FindArm(modelRoot, true)), -right);
        AimBone(FindArm(modelRoot, false), ChildBone(FindArm(modelRoot, false)), right);
    }

    static void AimBone(Transform upper, Transform lower, Vector3 worldDirection)
    {
        if (!upper || worldDirection.sqrMagnitude < 0.0001f)
            return;

        var current = lower && lower != upper ? lower.position - upper.position : upper.forward;
        if (current.sqrMagnitude < 0.0001f)
            current = upper.forward;

        upper.rotation = Quaternion.FromToRotation(current, worldDirection) * upper.rotation;
    }

    static Transform ChildBone(Transform bone)
    {
        if (!bone)
            return null!;

        for (var i = 0; i < bone.childCount; i++)
        {
            var child = bone.GetChild(i);
            if (child.name.Contains("SCALE"))
                continue;

            return child;
        }

        return null!;
    }

    static Transform FindArm(Transform root, bool left)
    {
        var exact = left
            ? new[] { "ANIM ARM L", "code_arm_l" }
            : new[] { "ANIM ARM R", "code_arm_r" };

        foreach (var name in exact)
        {
            var found = Find(root, name);
            if (found)
                return found;
        }

        foreach (var transform in root.GetComponentsInChildren<Transform>(true))
        {
            var name = transform.name;
            if (left && name.Contains("ARM L") && !name.Contains("SCALE"))
                return transform;

            if (!left && name.Contains("ARM R") && !name.Contains("SCALE"))
                return transform;
        }

        return null!;
    }

    static Transform BoneFor(Transform root, BodyRole role)
    {
        return role switch
        {
            BodyRole.Waist => Find(root, "ANIM BODY BOT"),
            BodyRole.Chest => Find(root, "code_body_top_up") ?? Find(root, "ANIM BODY TOP"),
            BodyRole.LeftFoot => Find(root, "ANIM LEG L TOP") ?? Find(root, "ANIM LEG L BOT"),
            BodyRole.RightFoot => Find(root, "ANIM LEG R TOP") ?? Find(root, "ANIM LEG R BOT"),
            _ => null!
        };
    }

    static Transform Find(Transform root, string name)
    {
        if (!root)
            return null!;

        foreach (var transform in root.GetComponentsInChildren<Transform>(true))
        {
            if (transform.name == name)
                return transform;
        }

        return null!;
    }

    static Vector3 FlatForward(Vector3 forward)
    {
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f)
            return Vector3.forward;

        return forward.normalized;
    }

    static void StripComponents(GameObject clone)
    {
        foreach (var behaviour in clone.GetComponentsInChildren<MonoBehaviour>(true))
            Destroy(behaviour);
    }

    void OnDestroy()
    {
        Hide();
    }
}

public struct TrackerDot
{
    public string Label;
    public Vector3 WorldPosition;
    public bool Active;
}
