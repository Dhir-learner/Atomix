using UnityEngine;

/// <summary>
/// Detects the moment a hand lets go of a control: the frame its transform stops moving.
///
/// This is how the laboratory's four physical controls are operated in VR - you grab the tap
/// handle, the burner support or a container lid, move it, and releasing it actuates the thing.
/// The logic was written out four separate times, in <see cref="RotateButton"/>,
/// <see cref="LightFire"/>, <see cref="NatriumContainerScriptAnimation"/> and
/// <see cref="UnscrewPotassiumContainer"/>, identically each time.
///
/// <b>All four carried the same bug, and it was not a theoretical one.</b> The detector cannot
/// tell a hand from any other source of movement, and on desktop the rig moves things during
/// startup: <see cref="DesktopBootstrap"/> makes scene objects grabbable and
/// <see cref="DesktopSettleWatcher"/> lowers anything left hanging in mid-air. A control that gets
/// settled has moved, and then stopped - which every one of these four scripts read as the player
/// operating it. The observable results were the tap running from the moment any lab scene loaded
/// and the Bunsen burner already lit, with the sodium and potassium lids able to unscrew
/// themselves the same way.
///
/// The fix is the <see cref="settleInSeconds"/> window: movement is watched, but nothing actuates
/// until the scene has finished assembling itself. Desktop players click these controls anyway -
/// <see cref="DesktopBootstrap"/> wires a <see cref="DesktopInvokeInteractable"/> onto each - so
/// the window costs desktop nothing at all, and in VR two seconds passes long before a headset
/// user has reached the bench.
/// </summary>
[System.Serializable]
public class MovementLatch
{
    [Tooltip("Seconds after the control first appears during which movement is ignored.\n\n" +
             "The desktop rig spends its opening moments placing objects, and this cannot tell " +
             "that movement from a hand turning the control.")]
    public float settleInSeconds = 2.0f;

    [Tooltip("Metres of travel that counts as movement.")]
    public float positionThreshold = 0.0005f;

    [Tooltip("Degrees of rotation that counts as movement.")]
    public float rotationThreshold = 0.1f;

    private Vector3 previousPosition;
    private Quaternion previousRotation;
    private bool wasMoving;
    private float ignoreUntil;
    private bool begun;

    /// <summary>
    /// Starts watching. Call from <c>Start</c>, passing the control's transform. Safe with null,
    /// because every one of these fields is wired in a scene and may not be.
    /// </summary>
    public void Begin(Transform control)
    {
        begun = true;
        wasMoving = false;
        ignoreUntil = Time.time + Mathf.Max(0.0f, settleInSeconds);

        if (control != null)
        {
            previousPosition = control.position;
            previousRotation = control.rotation;
        }
    }

    /// <summary>
    /// True on the single frame the control comes to rest after having been moved - and never
    /// during the settle-in window.
    /// </summary>
    public bool StoppedThisFrame(Transform control)
    {
        if (control == null)
        {
            return false;
        }

        if (!begun)
        {
            // Whoever owns this forgot to call Begin. Start the window now rather than letting
            // the very first frame count as movement from an uninitialised pose.
            Begin(control);
            return false;
        }

        bool isMoving =
            Vector3.Distance(previousPosition, control.position) > positionThreshold ||
            Quaternion.Angle(previousRotation, control.rotation) > rotationThreshold;

        if (Time.time < ignoreUntil)
        {
            // Keep tracking the pose, so the first real movement after the window is measured
            // from where the control actually ended up - but let nothing through.
            previousPosition = control.position;
            previousRotation = control.rotation;
            wasMoving = false;
            return false;
        }

        bool stopped = wasMoving && !isMoving;

        previousPosition = control.position;
        previousRotation = control.rotation;
        wasMoving = isMoving;

        return stopped;
    }
}
