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
    /// Placement-only offset from the right-hand sketch. The truck arrow points forward.
    /// The menu is 1.2 m in front of the truck point and 0.6 m to the truck's right.
    /// Its visible face points toward the truck's left, 21.6° toward forward.
    /// Height is the truck point plus a 0.4 m raise. No bounds or view test.
    /// </summary>
    internal const float MenuForwardMeters = 1.2f;
    internal const float MenuRightMeters = 0.6f;
    internal const float MenuRaiseMeters = 0.4f;
    internal const float MenuYawDegrees = 21.6f;

    internal static (float x, float y, float z, float faceX, float faceZ) MenuPose(
        float truckX, float truckY, float truckZ, float forwardX, float forwardZ, float rightX, float rightZ)
    {
        var x = truckX + forwardX * MenuForwardMeters + rightX * MenuRightMeters;
        var z = truckZ + forwardZ * MenuForwardMeters + rightZ * MenuRightMeters;
        var yaw = MenuYawDegrees * MathF.PI / 180f;
        var faceX = -rightX * MathF.Cos(yaw) + forwardX * MathF.Sin(yaw);
        var faceZ = -rightZ * MathF.Cos(yaw) + forwardZ * MathF.Sin(yaw);
        return (x, truckY + MenuRaiseMeters, z, faceX, faceZ);
    }
}
