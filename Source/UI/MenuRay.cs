using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace RepoXR.UI;

/// <summary>
/// The visible controller line does not mean the interaction toolkit recorded a UI hit.
/// Menu buttons are tested against the ray and the rect directly.
/// </summary>
internal static class MenuRay
{
    internal const float MaxDistance = 30f;

    internal static bool Hits(Transform ray, RectTransform rect, out Vector3 world)
    {
        world = default;
        if (ray == null || rect == null)
            return false;

        var beam = new Ray(ray.position, ray.forward);
        if (!TryPlane(beam, rect.position, -rect.forward, out var distance) &&
            !TryPlane(beam, rect.position, rect.forward, out distance))
            return false;

        if (distance < 0f || distance > MaxDistance)
            return false;

        world = beam.GetPoint(distance);
        var local = rect.InverseTransformPoint(world);
        return rect.rect.Contains(new Vector2(local.x, local.y));
    }

    internal static bool TryPlane(Ray ray, Vector3 point, Vector3 normal, out float distance)
    {
        var plane = new Plane(normal, point);
        return plane.Raycast(ray, out distance);
    }
}
