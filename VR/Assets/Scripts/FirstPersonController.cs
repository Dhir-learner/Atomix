using UnityEngine;

/// <summary>
/// First-person controller for keyboard (WASD) and mouse movement.
/// Attach this to the scene camera or player object in desktop mode.
/// </summary>
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

    [Header("Mouse Look Settings")]
    [Tooltip("How sensitive the mouse is for looking around")]
    public float mouseSensitivity = 2f;

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

    [Header("Physics Settings")]
    [Tooltip("Enable gravity for desktop mode. Keep this off if the lab floor has no reliable collider.")]
    public bool useGravity = false;

    [Tooltip("Simple gravity applied to the character controller")]
    public float gravity = -20f;

    private float pitch;
    private float yaw;
    private CharacterController characterController;
    private Vector3 velocity;
    private float fixedWorldHeight;

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

        Vector3 motion = moveDirection * currentSpeed;

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
