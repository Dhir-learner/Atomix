using UnityEngine;

/// <summary>
/// Everything the camera does that the player did not directly ask for: the walk bob, the sprint
/// lens kick, and the shake a reaction throws at it.
///
/// Atomix's camera was completely inert. Walking across the laboratory moved the view in a
/// perfectly straight line at a perfectly constant speed, sprinting looked identical to walking,
/// and a sodium explosion two feet away did not disturb the frame by a pixel. The lab is a
/// convincing space and the camera was giving none of it back.
///
/// <b>Where the offset is applied, and why it matters.</b> Three other systems write to this same
/// transform every frame:
///
/// <list type="bullet">
/// <item><see cref="FirstPersonController"/> moves it in <c>Update</c> and, while the cursor is
/// locked, rewrites its rotation outright;</item>
/// <item><see cref="LabBoundary"/> clamps its position in <c>LateUpdate</c>, on all three axes,
/// so the player cannot leave the room;</item>
/// <item><see cref="ObjectInteraction"/> reads it in <c>Update</c> to decide where held glassware
/// should float.</item>
/// </list>
///
/// A bob applied in <c>LateUpdate</c> would be inside all of that. The boundary would clamp a
/// bobbed position and give back less than was added, so the next frame's subtraction would
/// over-correct - and a player standing at the floor or ceiling limit would jitter against it.
/// Held glassware would inherit the bob and shake in the student's hands while they were trying
/// to read a graduation off it.
///
/// So the offset lives in the narrowest possible window: it is applied in
/// <c>Application.onBeforeRender</c>, which runs after every <c>LateUpdate</c> and immediately
/// before the frame is drawn, and it is taken away again at the very start of the next frame's
/// <c>Update</c> - this component runs at execution order -200, ahead of everything else. No
/// other system ever observes the camera in its offset state. The bob is therefore purely
/// visual, by construction, and cannot fight anything.
///
/// Every effect scales through <see cref="AtomixSettings.ScreenShake"/> and
/// <see cref="AtomixSettings.HeadBob"/>, and both go to zero. Motion sensitivity is common enough
/// in a classroom that a shake a student cannot switch off is a shake that should not ship.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-200)]
public class CameraJuice : MonoBehaviour
{
    // =========================================================
    // TUNING
    // =========================================================

    [Header("Walk bob")]
    [Tooltip("Vertical travel of the bob at a full walk, in metres, at 100%. Halved from the " +
             "first version, which was reported as causing motion sickness. The setting now " +
             "ships at 0 regardless, so this is the ceiling for someone who opts back in.")]
    public float bobHeight = 0.011f;

    [Tooltip("Sideways lean at the extremes of the bob, in degrees. Rotational movement is the " +
             "worse offender of the two for motion sickness, so this took the larger cut.")]
    public float bobRoll = 0.12f;

    [Tooltip("Bob cycles per second at full walking speed.")]
    public float bobFrequency = 1.85f;

    [Header("Sprint")]
    [Tooltip("Degrees of extra field of view while sprinting. Reads as speed without distorting " +
             "the room.")]
    public float sprintFovKick = 6.0f;

    [Tooltip("How quickly the lens reaches the sprint value and returns.")]
    public float fovResponse = 5.0f;

    [Header("Shake")]
    [Tooltip("Seconds for a shake to fall to roughly a twentieth of its starting strength.")]
    public float shakeDecay = 2.6f;

    [Tooltip("Hard ceiling on shake rotation, in degrees, before the player's own comfort scale " +
             "is applied. Stops two events on one frame from throwing the view around.")]
    public float maxShakeDegrees = 2.4f;

    // =========================================================
    // STATE
    // =========================================================

    /// <summary>The live instance, if the desktop rig has been built. Null in VR.</summary>
    public static CameraJuice Active { get; private set; }

    private FirstPersonController controller;
    private CharacterController body;
    private Camera lens;

    /// <summary>Exactly what was added before the last frame was drawn, so it can be taken back.</summary>
    private Vector3 appliedOffset;
    private Quaternion appliedRotation = Quaternion.identity;

    private float bobPhase;
    private float bobWeight;

    private float shakeStrength;
    private float shakeSeed;

    private float fovOffset;

    /// <summary>Set while a hit-stop is running, so the bob does not keep walking through it.</summary>
    private float freezeRemaining;

    void OnEnable()
    {
        Active = this;
        controller = GetComponent<FirstPersonController>();
        body = GetComponent<CharacterController>();
        lens = GetComponent<Camera>();
        shakeSeed = Random.value * 100.0f;

        Application.onBeforeRender += ApplyOffset;
    }

    void OnDisable()
    {
        Application.onBeforeRender -= ApplyOffset;

        // Hand the camera back exactly as it was found.
        RemoveOffset();

        if (lens != null && !lens.orthographic)
        {
            lens.fieldOfView = AtomixSettings.BaseFieldOfView;
        }

        if (Active == this)
        {
            Active = null;
        }
    }

    // =========================================================
    // PUBLIC API
    // =========================================================

    /// <summary>
    /// Shakes the camera. <paramref name="strength"/> is in the same units as
    /// <see cref="maxShakeDegrees"/> - roughly 0.1 for picking something up, 0.45 for a
    /// successful reaction, 1.6 for an explosion.
    ///
    /// Static and null-safe on purpose: every call site is a reaction script or a UI handler
    /// that has no idea whether the game is running on a desktop rig or in a headset.
    /// </summary>
    public static void Shake(float strength)
    {
        if (Active != null)
        {
            Active.AddShake(strength);
        }
    }

    /// <summary>
    /// A brief pause on the frame something lands, so the eye registers it. Used sparingly - on
    /// the verdict of an experiment, not on every pour.
    /// </summary>
    public static void Punch(float seconds)
    {
        if (Active != null)
        {
            Active.freezeRemaining = Mathf.Max(Active.freezeRemaining, seconds);
        }
    }

    private void AddShake(float strength)
    {
        // Take the larger rather than the sum: two events on one frame must not double up.
        shakeStrength = Mathf.Max(shakeStrength, Mathf.Max(0.0f, strength));
        shakeSeed = Random.value * 100.0f;
    }

    // =========================================================
    // FRAME
    // =========================================================

    /// <summary>
    /// Execution order -200, so this is the first thing that happens each frame. The camera is
    /// put back exactly where the controller left it before anything else looks at it.
    /// </summary>
    void Update()
    {
        RemoveOffset();

        float delta = Time.deltaTime;
        if (freezeRemaining > 0.0f)
        {
            freezeRemaining -= Time.unscaledDeltaTime;
            delta = 0.0f;
        }

        AdvanceBob(delta);
        AdvanceShake(delta);
        UpdateFov(delta);
    }

    private void RemoveOffset()
    {
        if (appliedOffset != Vector3.zero)
        {
            transform.position -= appliedOffset;
            appliedOffset = Vector3.zero;
        }

        if (appliedRotation != Quaternion.identity)
        {
            // The controller rewrites rotation outright every frame - but only while the cursor
            // is locked. With the pause menu open it does not, and an un-removed roll would
            // accumulate a degree per frame until the horizon was upside down.
            transform.rotation *= Quaternion.Inverse(appliedRotation);
            appliedRotation = Quaternion.identity;
        }
    }

    /// <summary>
    /// Runs after every LateUpdate, immediately before the frame is drawn. This is the only
    /// point in the frame at which the camera carries the offset.
    /// </summary>
    private void ApplyOffset()
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        Vector3 offset = Vector3.zero;
        Quaternion rotation = Quaternion.identity;

        // --- bob ---------------------------------------------------------------------
        float bobScale = AtomixSettings.HeadBob;
        if (bobWeight > 0.0005f && bobScale > 0.0f)
        {
            // The classic figure-of-eight: the head rises twice per stride but sways once, which
            // is what stops a bob from reading as a bounce.
            float vertical = Mathf.Sin(bobPhase * 2.0f) * bobHeight * bobWeight * bobScale;
            float roll = Mathf.Sin(bobPhase) * bobRoll * bobWeight * bobScale;

            offset += Vector3.up * vertical;
            rotation *= Quaternion.Euler(0.0f, 0.0f, roll);
        }

        // --- shake -------------------------------------------------------------------
        float shakeScale = AtomixSettings.ScreenShake;
        if (shakeStrength > 0.0005f && shakeScale > 0.0f)
        {
            float amount = Mathf.Min(shakeStrength, maxShakeDegrees) * shakeScale;
            float time = Time.unscaledTime;

            // Perlin rather than white noise: the shake has to be continuous frame to frame or
            // it reads as flicker instead of as motion.
            float pitch = (Mathf.PerlinNoise(shakeSeed, time * 22.0f) - 0.5f) * 2.0f;
            float yaw = (Mathf.PerlinNoise(shakeSeed + 31.7f, time * 19.0f) - 0.5f) * 2.0f;
            float roll = (Mathf.PerlinNoise(shakeSeed + 63.1f, time * 27.0f) - 0.5f) * 2.0f;

            rotation *= Quaternion.Euler(pitch * amount, yaw * amount, roll * amount * 1.4f);
        }

        if (offset != Vector3.zero)
        {
            transform.position += offset;
            appliedOffset = offset;
        }

        if (rotation != Quaternion.identity)
        {
            transform.rotation *= rotation;
            appliedRotation = rotation;
        }
    }

    /// <summary>
    /// Bob amplitude follows how fast the player is actually moving, not merely whether a key is
    /// down: walking into a bench should not keep bobbing.
    /// </summary>
    private void AdvanceBob(float delta)
    {
        float speed = 0.0f;
        if (body != null)
        {
            Vector3 planar = body.velocity;
            planar.y = 0.0f;
            speed = planar.magnitude;
        }

        // Normalised against the player's own configured walk speed, so raising the movement
        // speed setting does not turn the bob into a gallop.
        float reference = controller != null ? Mathf.Max(0.5f, controller.moveSpeed) : 2.2f;
        float target = Mathf.Clamp01(speed / reference);

        // Ease in and out, so starting and stopping is not a step change in the view.
        bobWeight = Mathf.MoveTowards(bobWeight, target, delta * 4.0f);

        if (bobWeight <= 0.0005f)
        {
            bobPhase = 0.0f;
            return;
        }

        bobPhase += delta * bobFrequency * 2.0f * Mathf.PI * Mathf.Max(0.35f, bobWeight);

        // Keep the phase bounded, or after an hour of walking the float loses the precision the
        // sine needs and the bob starts to stutter.
        if (bobPhase > Mathf.PI * 2.0f)
        {
            bobPhase -= Mathf.PI * 2.0f;
        }
    }

    private void AdvanceShake(float delta)
    {
        if (shakeStrength <= 0.0001f)
        {
            return;
        }

        // Decay runs even when the player's shake scale is zero, so turning shake back on
        // mid-reaction does not release one that should have finished long ago.
        shakeStrength *= Mathf.Exp(-delta * shakeDecay);
        if (shakeStrength < 0.0005f)
        {
            shakeStrength = 0.0f;
        }
    }

    /// <summary>
    /// The sprint kick, plus a touch of extra width while the camera is being shaken - a wider
    /// lens during an explosion is a cheap, well-worn way to make one feel bigger.
    /// </summary>
    private void UpdateFov(float delta)
    {
        if (lens == null || lens.orthographic)
        {
            return;
        }

        bool sprinting = controller != null && controller.enabled &&
                         bobWeight > 0.35f &&
                         (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) &&
                         !LabTextInput.IsCapturing;

        // Scaled by the walking-motion setting, not applied flat: a lens that zooms when you
        // start running is exactly as nauseating as a bob, and it belongs behind the same switch.
        float target = sprinting ? sprintFovKick * AtomixSettings.HeadBob : 0.0f;
        target += Mathf.Min(shakeStrength, 2.0f) * 1.6f * AtomixSettings.ScreenShake;

        // Frame-rate independent: the same fraction of the gap is closed per second whatever the
        // frame rate, rather than per frame.
        float blend = 1.0f - Mathf.Exp(-fovResponse * Mathf.Max(delta, Time.unscaledDeltaTime));
        fovOffset = Mathf.Lerp(fovOffset, target, blend);

        lens.fieldOfView = AtomixSettings.BaseFieldOfView + fovOffset;
    }
}
