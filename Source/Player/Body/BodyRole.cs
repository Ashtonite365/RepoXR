namespace RepoXR.Player.Body;

/// <summary>
/// Body part a full-body slot can drive. At most three slots are active;
/// elbow and knee chains are intentionally not roles.
/// </summary>
public enum BodyRole
{
    Unused = 0,
    Waist = 1,
    LeftFoot = 2,
    RightFoot = 3,
    Chest = 4
}

/// <summary>
/// World-space target for one body role, already corrected by calibration.
/// </summary>
public struct BodyPose
{
    public BodyRole Role;
    public bool Active;
    public UnityEngine.Vector3 Position;
    public UnityEngine.Quaternion Rotation;
}
