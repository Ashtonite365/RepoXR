namespace RepoXR.Patches;

/// <summary>
/// Menu cameras call GetOverrideActive before they have a player. A null avatar or a
/// missing VR position used to throw every frame from camera bob, crouch, jump, and the flashlight.
/// </summary>
internal static class CameraOverride
{
    internal static bool IsActive(bool clientPositionOverride, bool hasAvatar, bool avatarIsLocal,
        bool hasPositionDriver, bool positionOverrideActive) =>
        clientPositionOverride || (hasAvatar && avatarIsLocal && hasPositionDriver && positionOverrideActive);
}
