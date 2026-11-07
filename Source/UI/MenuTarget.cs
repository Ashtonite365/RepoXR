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
    /// 1.3.21 sat 1.2 m forward and 0.6 m to the right of the front panel, 0.4 m up.
    /// That read as a small nudge. This centers the plane on the nose, 3 m in front,
    /// and 1.8 m above the panel. The visible face points at the camera position
    /// passed in, which the caller samples once.
    /// </summary>
    internal const float MenuForwardMeters = 3f;
    internal const float MenuRaiseMeters = 1.8f;
    internal const float MenuScale = 5f;

    internal static (float x, float y, float z, float faceX, float faceZ) MenuPose(
        float truckX, float truckY, float truckZ, float forwardX, float forwardZ, float cameraX, float cameraZ)
    {
        var x = truckX + forwardX * MenuForwardMeters;
        var z = truckZ + forwardZ * MenuForwardMeters;
        var y = truckY + MenuRaiseMeters;
        var dx = cameraX - x;
        var dz = cameraZ - z;
        var mag = MathF.Sqrt(dx * dx + dz * dz);
        if (mag < 0.01f)
            return (x, y, z, -forwardX, -forwardZ);

        return (x, y, z, dx / mag, dz / mag);
    }

    internal static (float width, float height) PhysicalSize(float widthMeters, float heightMeters, float menuScale) =>
        (widthMeters * menuScale, heightMeters * menuScale);

    /// <summary>
    /// Ray hits use the plane's local rect. Uniform scale changes world size only,
    /// so a point on the physical edge still lands on the same local edge.
    /// </summary>
    internal static (float x, float y) LocalOnScaledPlane(float worldX, float worldY, float uniformScale) =>
        (worldX / uniformScale, worldY / uniformScale);
}
