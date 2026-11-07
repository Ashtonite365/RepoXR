using System;

namespace RepoXR.UI;

/// <summary>
/// The 1.3.13 diagnostic selected HUD Canvas while MenuPageMain was missing.
/// That canvas is the game HUD. The main menu is whichever canvas the page is under
/// once the page exists.
/// </summary>
internal static class MenuTarget
{
    internal static bool PageReady(bool pageExists) => pageExists;

    internal static bool IsHudCanvas(string canvasName) =>
        string.Equals(canvasName, "HUD Canvas", StringComparison.Ordinal);

    internal static bool Accept(bool pageExists, string canvasName, bool pageIsUnderCanvas)
    {
        if (!PageReady(pageExists) || string.IsNullOrEmpty(canvasName) || !pageIsUnderCanvas)
            return false;

        return true;
    }

    internal static float ScaleForWidth(float pixelWidth, float widthMeters)
    {
        if (pixelWidth < 1f || widthMeters <= 0f)
            return 0.001f;

        return widthMeters / pixelWidth;
    }

    /// <summary>
    /// Canvas-local position that puts a point measured from the pivot on the view centre.
    /// </summary>
    internal static (float x, float y, float z) LocalPositionForContent(float contentCenterX, float contentCenterY,
        float scale, float distance) =>
        (-contentCenterX * scale, -contentCenterY * scale, distance);

    internal static float ScaleToFit(float pixelWidth, float pixelHeight, float maxWidth, float maxHeight)
    {
        var sx = ScaleForWidth(pixelWidth, maxWidth);
        var sy = ScaleForWidth(pixelHeight, maxHeight);
        return sx < sy ? sx : sy;
    }

    internal static bool RayPlane(float ox, float oy, float oz, float dx, float dy, float dz,
        float px, float py, float pz, float nx, float ny, float nz, float maxDistance, out float distance)
    {
        var denom = nx * dx + ny * dy + nz * dz;
        if (denom > -1e-5f && denom < 1e-5f)
        {
            distance = 0f;
            return false;
        }

        distance = (nx * (px - ox) + ny * (py - oy) + nz * (pz - oz)) / denom;
        return distance > 0.02f && distance < maxDistance;
    }

    internal static bool CoversDesktopMirror(bool isPresentedMenu, bool screenOverlay) =>
        !isPresentedMenu && screenOverlay;

    internal static (float x, float y) LocalFromUv(float u, float v, float xMin, float yMin, float xMax, float yMax) =>
        (xMin + (xMax - xMin) * u, yMin + (yMax - yMin) * v);

    internal static (float u, float v) UvFromLocal(float localX, float localY, float width, float height)
    {
        if (width < 0.001f || height < 0.001f)
            return (0.5f, 0.5f);

        return (localX / width + 0.5f, localY / height + 0.5f);
    }

    /// <summary>
    /// Horizontal left of the camera's view. <paramref name="rightX"/> and <paramref name="rightZ"/>
    /// are the camera's right vector with vertical removed. Looking along +Z, right is +X, so left is -X.
    /// This is not the truck's local left.
    /// </summary>
    internal static (float x, float y, float z) CameraViewLeft(float rightX, float rightZ)
    {
        var mag = MathF.Sqrt(rightX * rightX + rightZ * rightZ);
        if (mag < 0.01f)
            return (-1f, 0f, 0f);

        return (-rightX / mag, 0f, -rightZ / mag);
    }

    internal static (float x, float y, float z) LeftOfTruck(float truckX, float truckZ, float leftX, float leftZ,
        float gap, float eyeY) =>
        (truckX + leftX * gap, eyeY, truckZ + leftZ * gap);

    internal static bool IsLeftOfTruck(float alongLeft) => alongLeft > 0.25f;

    internal static float Ahead(float camX, float camZ, float fwdX, float fwdZ, float pointX, float pointZ) =>
        (pointX - camX) * fwdX + (pointZ - camZ) * fwdZ;

    internal static bool InFrontOfCamera(float ahead, float maxDistance) => ahead > 1f && ahead < maxDistance;

    /// <summary>
    /// The 1.3.19 gate used <see cref="Ahead"/> alone. The controller mesh at
    /// (-17.754, 4.503) is only about 1.8 m along the camera's horizontal forward
    /// and about 18 m to the side, so that gate accepted it.
    /// </summary>
    internal static (float ahead, float lateral) ViewOffset(float camX, float camZ, float fwdX, float fwdZ,
        float pointX, float pointZ)
    {
        var mag = MathF.Sqrt(fwdX * fwdX + fwdZ * fwdZ);
        if (mag < 0.01f)
            return (0f, 999f);

        var fx = fwdX / mag;
        var fz = fwdZ / mag;
        var dx = pointX - camX;
        var dz = pointZ - camZ;
        var ahead = dx * fx + dz * fz;
        var sideX = dx - fx * ahead;
        var sideZ = dz - fz * ahead;
        return (ahead, MathF.Sqrt(sideX * sideX + sideZ * sideZ));
    }

    internal static bool InViewCone(float ahead, float lateral) =>
        ahead > 0.5f && ahead < 18f && lateral < 8f && lateral < ahead * 1.25f;

    internal const float SketchYawDegrees = 21.6f;
    internal const float SketchRaiseMeters = 0.4f;
    internal const float SketchForwardGap = 0.7f;
    internal const float SketchRightFraction = 0.24f;
    internal const float MaxHalfExtent = 4f;

    /// <summary>
    /// Overhead pose from the sketch's right-hand truck. Forward is the truck's green arrow.
    /// The menu sits just past the nose, slightly to the truck's right, and its visible face
    /// points toward the truck's left, yawed <see cref="SketchYawDegrees"/> toward forward.
    /// Extents are capped so a controller-sized bounds cannot push the plane tens of metres away.
    /// </summary>
    internal static (float x, float y, float z, float faceX, float faceZ) SketchPose(
        float centerX, float centerZ, float forwardX, float forwardZ, float rightX, float rightZ,
        float halfForward, float halfRight, float eyeY)
    {
        if (halfForward > MaxHalfExtent)
            halfForward = MaxHalfExtent;
        if (halfRight > MaxHalfExtent)
            halfRight = MaxHalfExtent;

        var rightShift = halfRight * 2f * SketchRightFraction;
        if (rightShift > 1.2f)
            rightShift = 1.2f;

        var along = halfForward + SketchForwardGap;
        var x = centerX + forwardX * along + rightX * rightShift;
        var z = centerZ + forwardZ * along + rightZ * rightShift;
        var yaw = SketchYawDegrees * MathF.PI / 180f;
        var faceX = -rightX * MathF.Cos(yaw) + forwardX * MathF.Sin(yaw);
        var faceZ = -rightZ * MathF.Cos(yaw) + forwardZ * MathF.Sin(yaw);
        return (x, eyeY + SketchRaiseMeters, z, faceX, faceZ);
    }
}
