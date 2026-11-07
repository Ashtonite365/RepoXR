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
    /// Canvas-local position that puts content, measured from the canvas pivot, on the view centre.
    /// Positive content Y is above the pivot, so the canvas moves down.
    /// </summary>
    internal static (float x, float y, float z) LocalPositionForContent(float contentCenterX, float contentCenterY,
        float scale, float distance) =>
        (-contentCenterX * scale, -contentCenterY * scale, distance);
}
