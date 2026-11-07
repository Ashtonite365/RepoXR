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
}
