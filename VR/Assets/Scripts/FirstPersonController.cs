using UnityEngine;

/// <summary>
/// First-person controller for keyboard (WASD) and mouse movement.
/// Attach this to the scene camera or player object in desktop mode.
/// </summary>
// Before ObjectInteraction (-50), which places held glassware against the camera's pose for this
// frame, and after CameraJuice (-200), which restores the camera before anything reads it.
[DefaultExecutionOrder(-100)]
public class FirstPersonController : MonoBehaviour
{
    [Header("Movement Settings")]
    [Tooltip("How fast the player moves")]
    public float moveSpeed = 2.2f;

    [Tooltip("Multiplier for sprint speed (hold Left Shift)")]
    public float sprintMultiplier = 1.7f;

    [Tooltip("Allow vertical movement in desktop mode (Space up, Ctrl down)")]
    public bool enableVerticalFlyMovement = true;

    [Tooltip("Speed for vertical up/down movement")]
    public float verticalMoveSpeed = 2f;

    [Tooltip("How quickly the player reaches full speed. Higher is snappier; below about 6 it " +
             "starts to feel like walking on ice.")]
    public float acceleration = 14f;

    [Tooltip("How quickly the player comes to a stop. Kept faster than acceleration, because a " +
             "slide-to-halt makes precise positioning at the bench harder than it should be.")]
    public float deceleration = 20f;

    [Header("Mouse Look Settings")]
    [Tooltip("How sensitive the mouse is for looking around")]
    public float mouseSensitivity = 2f;

    [Tooltip("Invert the vertical mouse axis (flight-sim style look)")]
    public bool invertLook = false;

    [Tooltip("Maximum angle you can look up (degrees)")]
    public float maxLookUpAngle = 80f;

    [Tooltip("Maximum angle you can look down (degrees)")]
    public float maxLookDownAngle = 80f;

    [Header("Camera Settings")]
    [Tooltip("The camera transform - if empty, will use this GameObject")]
    public Transform cameraTransform;

    [Tooltip("Lock the cursor automatically when gameplay starts")]
    public bool lockCursorOnStart = true;

    [Tooltip("Allow WASD movement even while the cursor is unlocked")]
    public bool allowMovementWhenCursorUnlocked = true;

    [Header("Character Body Settings")]
    [Tooltip("Capsule height used for desktop collision")]
    public float characterHeight = 1.8f;

    [Tooltip("Capsule radius used for desktop collision")]
    public float characterRadius = 0.3f;

    [Tooltip("How high the camera sits above the floor in desktop mode")]
    public float eyeHeight = 1.75f;

    [Tooltip("Maximum height the controller can step up")]
    public float stepOffset = 0.35f;

    [Tooltip("Additional world-space height added when desktop mode starts")]
    public float desktopStartHeightOffset = 0.35f;

    [Tooltip("On start, lift the player until their collision capsule rests on the floor, instead " +
             "of starting partly inside it and being pushed up the first time they walk.")]
    public bool settleOntoFloorOnStart = true;

    [Header("Physics Settings")]
    [Tooltip("Enable gravity for desktop mode. Keep this off if the lab floor has no reliable collider.")]
    public bool useGravity = false;

    [Tooltip("Simple gravity applied to the character controller")]
    public float gravity = -20f;

    private float pitch;
    private float yaw;

    /// <summary>
    /// Smoothed planar move direction. Input used to be applied raw, so the player reached full
    /// speed and full stop within a single frame: walking across the lab was a rectangle pulse
    /// rather than a movement, and it is the main reason the camera read as a floating tripod
    /// rather than as a person. Held separately from the CharacterController's own velocity
    /// because that also carries gravity, which must not be smoothed.
    /// </summary>
    private Vector3 smoothedMove;
    private CharacterController characterController;
    private Vector3 velocity;
    private float fixedWorldHeight;

    /// <summary>Furthest the start-up settle will lift the player, in metres.</summary>
    private const float MaxFloorSettleLift = 1.0f;

    /// <summary>Give up looking for a floor after this long.</summary>
    private const float FloorSettleTimeout = 1.5f;

    private bool floorSettlePending;
    private float floorSettleDeadline;

    void Start()
    {
        if (cameraTransform == null)
        {
            cameraTransform = transform;
        }

        characterController = GetComponent<CharacterController>();
        if (characterController == null)
        {
            characterController = gameObject.AddComponent<CharacterController>();
            Debug.LogWarning("No CharacterController found. Added one automatically for desktop movement.");
        }

        ConfigureCharacterController();
        fixedWorldHeight = transform.position.y + desktopStartHeightOffset;
        Vector3 startPosition = transform.position;
        startPosition.y = fixedWorldHeight;
        transform.position = startPosition;

        floorSettlePending = settleOntoFloorOnStart;
        floorSettleDeadline = Time.unscaledTime + FloorSettleTimeout;
        TrySettleOntoFloor();

        yaw = NormalizeAngle(transform.localEulerAngles.y);
        pitch = NormalizeAngle(cameraTransform.localEulerAngles.x);

        if (lockCursorOnStart)
        {
            LockCursor();
        }
        else
        {
            UnlockCursor();
        }
    }

    void Update()
    {
        // Before anything that can move the player, so the first Move starts from the settled
        // height. Not input, so it is not held up by typing.
        if (floorSettlePending)
        {
            TrySettleOntoFloor();
        }

        // A question is being typed into the assistant panel; every letter belongs to it.
        if (LabTextInput.IsCapturing)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (IsCursorLocked)
            {
                UnlockCursor();
            }
            else
            {
                LockCursor();
            }
        }

        if (IsCursorLocked)
        {
            HandleMouseLook();
        }

        if (allowMovementWhenCursorUnlocked || IsCursorLocked)
        {
            HandleMovement();
        }
    }

    void HandleMouseLook()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;
        if (invertLook)
        {
            mouseY = -mouseY;
        }

        pitch -= mouseY;
        pitch = Mathf.Clamp(pitch, -maxLookDownAngle, maxLookUpAngle);
        yaw += mouseX;

        if (cameraTransform == transform)
        {
            transform.localRotation = Quaternion.Euler(pitch, yaw, 0f);
            return;
        }

        transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        cameraTransform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    void HandleMovement()
    {
        if (characterController == null)
        {
            return;
        }

        float horizontal = 0f;
        float vertical = 0f;

        if (Input.GetKey(KeyCode.A))
        {
            horizontal -= 1f;
        }

        if (Input.GetKey(KeyCode.D))
        {
            horizontal += 1f;
        }

        if (Input.GetKey(KeyCode.S))
        {
            vertical -= 1f;
        }

        if (Input.GetKey(KeyCode.W))
        {
            vertical += 1f;
        }

        Vector3 forward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
        Vector3 right = Vector3.ProjectOnPlane(cameraTransform.right, Vector3.up).normalized;

        if (forward.sqrMagnitude < 0.001f)
        {
            forward = transform.forward;
        }

        if (right.sqrMagnitude < 0.001f)
        {
            right = transform.right;
        }

        Vector3 moveDirection = (right * horizontal) + (forward * vertical);
        moveDirection = Vector3.ClampMagnitude(moveDirection, 1f);

        float currentSpeed = moveSpeed;
        if (Input.GetKey(KeyCode.LeftShift))
        {
            currentSpeed *= sprintMultiplier;
        }

        // Ease in and out of the target direction. Frame-rate independent: the same fraction of
        // the remaining gap is closed per second whatever the frame rate.
        Vector3 targetMove = moveDirection * currentSpeed;
        float rate = targetMove.sqrMagnitude > smoothedMove.sqrMagnitude ? acceleration : deceleration;
        float blend = 1f - Mathf.Exp(-Mathf.Max(0.01f, rate) * Time.deltaTime);
        smoothedMove = Vector3.Lerp(smoothedMove, targetMove, blend);

        // Park it exactly at rest rather than creeping towards zero forever, so a released key
        // really does mean stopped.
        if (targetMove.sqrMagnitude < 0.0001f && smoothedMove.sqrMagnitude < 0.0004f)
        {
            smoothedMove = Vector3.zero;
        }

        Vector3 motion = smoothedMove;

        if (!useGravity)
        {
            velocity.y = 0f;
            float verticalAxis = 0f;
            if (enableVerticalFlyMovement)
            {
                if (Input.GetKey(KeyCode.Space))
                {
                    verticalAxis += 1f;
                }

                if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
                {
                    verticalAxis -= 1f;
                }
            }

            Vector3 flyMotion = (motion + (Vector3.up * verticalAxis * verticalMoveSpeed)) * Time.deltaTime;
            flyMotion.y = enableVerticalFlyMovement ? flyMotion.y : 0f;

            // A CharacterController only sweeps and resolves collisions inside Move().
            // Assigning transform.position directly teleported the player through walls,
            // benches and the outside of the building.
            characterController.Move(flyMotion);

            if (!enableVerticalFlyMovement)
            {
                Vector3 constrainedPosition = transform.position;
                constrainedPosition.y = fixedWorldHeight;
                transform.position = constrainedPosition;
            }
            return;
        }

        if (characterController.isGrounded && velocity.y < 0f)
        {
            velocity.y = -2f;
        }

        velocity.y += gravity * Time.deltaTime;

        motion.y = velocity.y;
        characterController.Move(motion * Time.deltaTime);
    }

    /// <summary>
    /// Lifts the player so the bottom of their collision capsule rests on the floor.
    ///
    /// The start height used to be a fixed guess: wherever the XR rig was authored (y = 1.91 in all
    /// three labs), plus <see cref="desktopStartHeightOffset"/>. The capsule's bottom is always
    /// <see cref="eyeHeight"/> below the eyes, and nothing checked where the floor actually was -
    /// so the capsule started partly inside it. A CharacterController does not push itself out of
    /// the floor while standing still, only when it is given real movement; so the player began
    /// a little low and then rose suddenly on the first step.
    ///
    /// The laboratory mesh has no floor collider of its own. The floor is the slab
    /// <see cref="LabBoundary"/> builds during scene setup, which may not exist on the very first
    /// frame - so this keeps trying each frame until it finds one or times out, and always runs
    /// before movement. Only ever lifts: a capsule already clear of the floor is not moved,
    /// because walking would not move it either (there is no gravity in fly mode).
    /// </summary>
    void TrySettleOntoFloor()
    {
        if (characterController == null || !characterController.enabled ||
            Time.unscaledTime > floorSettleDeadline)
        {
            floorSettlePending = false;
            return;
        }

        Vector3 bottomLocal = characterController.center -
                              (Vector3.up * (characterController.height * 0.5f));
        float bottomY = transform.TransformPoint(bottomLocal).y;

        // Straight down from the eyes: the nearest upward-facing surface that is not the player.
        float castLength = (transform.position.y - bottomY) + MaxFloorSettleLift;
        RaycastHit[] hits = Physics.RaycastAll(transform.position, Vector3.down, castLength,
                                               ~0, QueryTriggerInteraction.Ignore);

        float nearest = float.MaxValue;
        float floorY = 0f;
        for (int i = 0; i < hits.Length; i++)
        {
            Collider hitCollider = hits[i].collider;
            if (hitCollider == null || hitCollider == characterController ||
                hitCollider.transform.IsChildOf(transform) || hits[i].normal.y < 0.65f)
            {
                continue;
            }

            if (hits[i].distance < nearest)
            {
                nearest = hits[i].distance;
                floorY = hits[i].point.y;
            }
        }

        if (nearest == float.MaxValue)
        {
            return;   // no floor yet - the boundary may still be building; try next frame
        }

        floorSettlePending = false;

        // Where the controller itself would come to rest once it had been pushed out.
        float lift = (floorY + characterController.skinWidth + 0.005f) - bottomY;
        if (lift <= 0f || lift > MaxFloorSettleLift)
        {
            return;
        }

        Vector3 position = transform.position;
        position.y += lift;
        transform.position = position;
        fixedWorldHeight += lift;

        // Auto-sync of transforms is off in this project, so without this the controller would
        // keep its old, sunken position until the next physics step.
        Physics.SyncTransforms();
    }

    void LockCursor()
    {
        SetCursorLock(true);
    }

    void UnlockCursor()
    {
        SetCursorLock(false);
    }

    public static bool IsCursorLocked => Cursor.lockState == CursorLockMode.Locked;

    public static void SetCursorLock(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    void ConfigureCharacterController()
    {
        if (characterController == null)
        {
            return;
        }

        characterController.height = Mathf.Max(1f, characterHeight);
        characterController.radius = Mathf.Clamp(characterRadius, 0.1f, characterController.height * 0.5f);
        characterController.stepOffset = Mathf.Clamp(stepOffset, 0.05f, characterController.height - 0.05f);

        if (cameraTransform == null || cameraTransform == transform)
        {
            float centerY = (characterController.height * 0.5f) - eyeHeight;
            characterController.center = new Vector3(0f, centerY, 0f);
        }
        else
        {
            characterController.center = new Vector3(0f, characterController.height * 0.5f, 0f);
        }
    }

    static float NormalizeAngle(float angle)
    {
        while (angle > 180f)
        {
            angle -= 360f;
        }

        return angle;
    }
}
