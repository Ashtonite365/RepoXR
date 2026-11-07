namespace RepoXR.UI;

/// <summary>
/// Plain checks for the main-menu camera and canvas. The headset log can say the
/// pointer exists without saying the menu is in front of the camera.
/// </summary>
internal static class MenuView
{
    internal static bool CameraStayed(string parentBefore, string parentAfter, bool activeBefore, bool activeAfter) =>
        parentBefore == parentAfter && activeBefore == activeAfter;

    internal static bool CenterInView(float viewportX, float viewportY, float viewportZ) =>
        viewportZ > 0.01f && viewportX >= 0f && viewportX <= 1f && viewportY >= 0f && viewportY <= 1f;
}
