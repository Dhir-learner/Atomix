using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Handles object interaction for keyboard and mouse gameplay.
/// Attach this to your main camera in desktop mode.
/// </summary>
public class ObjectInteraction : MonoBehaviour
{
    [Header("Interaction Settings")]
    [Tooltip("Maximum distance to interact with objects")]
    public float interactionDistance = 3f;

    [Tooltip("Distance from camera where held object will float")]
    public float holdDistance = 1.5f;

    [Tooltip("How smoothly the object follows the camera")]
    public float movementSmoothing = 10f;

    [Tooltip("How fast objects rotate when using the mouse wheel or keys")]
    public float rotationSpeed = 100f;

    [Tooltip("Shortcut to reset held object pose (position + rotation)")]
    public KeyCode resetHeldPoseKey = KeyCode.T;

    [Header("Held Object Rotation Keys")]
    public KeyCode yawLeftKey = KeyCode.Q;
    public KeyCode yawRightKey = KeyCode.E;
    public KeyCode pitchUpKey = KeyCode.Z;
    public KeyCode pitchDownKey = KeyCode.X;
    [Tooltip("Roll. Hold Shift to roll the other way - kept to one key so V stays free for push-to-talk.")]
    public KeyCode rollKey = KeyCode.C;

    [Tooltip("Keep released lab objects fixed in place instead of letting physics drop them")]
    public bool keepReleasedObjectsStatic = false;

    [Tooltip("How far inside the lab boundary a held object is kept (metres)")]
    public float heldObjectBoundaryMargin = 0.2f;

    [Header("Visual Feedback")]
    [Tooltip("Color to highlight interactable objects")]
    public Color highlightColor = Color.yellow;

    [Tooltip("LayerMask for interactable objects")]
    public LayerMask interactableLayer = ~0;

    private GameObject currentlyHeldObject;
    private ObjectGrabbable currentlyHeldGrabbable;
    private ObjectGrabbable highlightedGrabbable;
    private DesktopInteractable highlightedInteractable;
    [Header("Release")]
    [Tooltip("Lower a released object onto the surface beneath it instead of freezing it in " +
             "mid-air. Turn off to restore the old behaviour.")]
    public bool settleReleasedObjects = true;

    [Tooltip("Furthest a released object will be lowered, in metres. An object let go over a " +
             "large gap is left where it is rather than dropped a long way.")]
    public float maxSettleDrop = DesktopObjectSettler.DefaultMaxDrop;

    private Rigidbody heldObjectRigidbody;
    private bool heldOriginalUseGravity;
    private bool heldOriginalIsKinematic;
    private Quaternion heldInitialRotation;
    private Vector3 targetPosition;
    private Quaternion targetRotation;
    private Renderer[] highlightedRenderers;
    private Color[] originalColors;
    private readonly List<RaycastResult> uiRaycastResults = new List<RaycastResult>();

    void Update()
    {
        // A question is being typed into the assistant panel; every letter belongs to it.
        if (LabTextInput.IsCapturing)
        {
            return;
        }

        if (currentlyHeldObject == null)
        {
            if (CanProcessWorldInteraction())
            {
                CheckForInteractableObject();

                if (Input.GetMouseButtonDown(0))
                {
                    TryPrimaryInteraction();
                }
            }
            else
            {
                RemoveHighlight();
                SetCrosshairGrabbableHover(false);
            }
        }
        else
        {
            SetCrosshairGrabbableHover(false);

            if (CanProcessWorldInteraction())
            {
                MoveHeldObject();
                RotateHeldObject();
                HandleHeldObjectShortcuts();

                if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.R))
                {
                    ReleaseObject();
                }
            }
            else if (Input.GetKeyDown(KeyCode.R))
            {
                ReleaseObject();
            }
        }
    }

    void HandleHeldObjectShortcuts()
    {
        if (currentlyHeldObject == null)
        {
            return;
        }

        if (Input.GetKeyDown(resetHeldPoseKey))
        {
            ResetHeldObjectPose();
        }
    }

    bool CanProcessWorldInteraction()
    {
        return FirstPersonController.IsCursorLocked;
    }

    void CheckForInteractableObject()
    {
        if (HasUiInteractableAtCrosshair())
        {
            RemoveHighlight();
            SetCrosshairGrabbableHover(false);
            return;
        }

        Ray ray = new Ray(transform.position, transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, interactionDistance, interactableLayer))
        {
            DesktopInteractable desktopInteractable = hit.collider.GetComponentInParent<DesktopInteractable>();
            if (desktopInteractable != null && desktopInteractable.CanInteract)
            {
                if (highlightedInteractable != desktopInteractable)
                {
                    RemoveHighlight();
                    highlightedInteractable = desktopInteractable;
                    HighlightObject(desktopInteractable.transform);
                }

                highlightedGrabbable = null;
                SetCrosshairGrabbableHover(false);
                return;
            }

            ObjectGrabbable grabbable = hit.collider.GetComponentInParent<ObjectGrabbable>();
            if (grabbable != null && grabbable.canGrab)
            {
                if (highlightedGrabbable != grabbable)
                {
                    RemoveHighlight();
                    highlightedGrabbable = grabbable;
                    HighlightObject(grabbable.transform);
                }

                highlightedInteractable = null;
                SetCrosshairGrabbableHover(true);
                return;
            }
        }

        RemoveHighlight();
        SetCrosshairGrabbableHover(false);
    }

    void HighlightObject(Transform target)
    {
        highlightedRenderers = target.GetComponentsInChildren<Renderer>(true);
        originalColors = new Color[highlightedRenderers.Length];

        for (int i = 0; i < highlightedRenderers.Length; i++)
        {
            Renderer renderer = highlightedRenderers[i];
            if (renderer == null || !renderer.material.HasProperty("_Color"))
            {
                continue;
            }

            originalColors[i] = renderer.material.color;
            renderer.material.color = highlightColor;
        }
    }

    void RemoveHighlight()
    {
        if (highlightedRenderers != null)
        {
            for (int i = 0; i < highlightedRenderers.Length; i++)
            {
                Renderer renderer = highlightedRenderers[i];
                if (renderer == null || !renderer.material.HasProperty("_Color"))
                {
                    continue;
                }

                renderer.material.color = originalColors[i];
            }
        }

        highlightedRenderers = null;
        originalColors = null;
        highlightedInteractable = null;
        highlightedGrabbable = null;
    }

    void TryPrimaryInteraction()
    {
        if (TryUiInteraction())
        {
            RemoveHighlight();
            return;
        }

        if (highlightedInteractable != null)
        {
            highlightedInteractable.Interact(this);
            RemoveHighlight();
            return;
        }

        TryGrabObject();
    }

    bool HasUiInteractableAtCrosshair()
    {
        return TryGetUiInteractionTarget(out _);
    }

    bool TryUiInteraction()
    {
        if (!TryGetUiInteractionTarget(out GameObject uiTarget))
        {
            return false;
        }

        PointerEventData pointerData = CreateCrosshairPointerData();
        EventSystem.current.SetSelectedGameObject(uiTarget);

        ExecuteEvents.Execute(uiTarget, pointerData, ExecuteEvents.pointerDownHandler);
        ExecuteEvents.Execute(uiTarget, pointerData, ExecuteEvents.pointerUpHandler);

        bool clicked = ExecuteEvents.Execute(uiTarget, pointerData, ExecuteEvents.pointerClickHandler);
        if (!clicked)
        {
            ExecuteEvents.Execute(uiTarget, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
        }

        return true;
    }

    bool TryGetUiInteractionTarget(out GameObject uiTarget)
    {
        uiTarget = null;

        if (EventSystem.current == null)
        {
            return false;
        }

        PointerEventData pointerData = CreateCrosshairPointerData();
        uiRaycastResults.Clear();
        EventSystem.current.RaycastAll(pointerData, uiRaycastResults);

        foreach (RaycastResult raycastResult in uiRaycastResults)
        {
            if (raycastResult.gameObject == null || !raycastResult.gameObject.activeInHierarchy)
            {
                continue;
            }

            GameObject candidate = ExecuteEvents.GetEventHandler<IPointerClickHandler>(raycastResult.gameObject);
            if (candidate == null)
            {
                candidate = ExecuteEvents.GetEventHandler<ISubmitHandler>(raycastResult.gameObject);
            }

            if (candidate == null)
            {
                continue;
            }

            Selectable selectable = candidate.GetComponent<Selectable>();
            if (selectable != null && (!selectable.IsActive() || !selectable.IsInteractable()))
            {
                continue;
            }

            uiTarget = candidate;
            return true;
        }

        return false;
    }

    PointerEventData CreateCrosshairPointerData()
    {
        PointerEventData pointerData = new PointerEventData(EventSystem.current);
        pointerData.position = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        pointerData.button = PointerEventData.InputButton.Left;
        return pointerData;
    }

    void TryGrabObject()
    {
        if (highlightedGrabbable == null || !highlightedGrabbable.canGrab)
        {
            return;
        }

        currentlyHeldObject = highlightedGrabbable.gameObject;
        currentlyHeldGrabbable = highlightedGrabbable;
        heldObjectRigidbody = highlightedGrabbable.Rigidbody;

        if (heldObjectRigidbody != null)
        {
            currentlyHeldGrabbable.RestoreOriginalConstraints();
            heldOriginalUseGravity = heldObjectRigidbody.useGravity;
            heldOriginalIsKinematic = heldObjectRigidbody.isKinematic;
            heldObjectRigidbody.useGravity = false;
            heldObjectRigidbody.isKinematic = true;
        }

        targetPosition = ConfinedHoldPosition();
        targetRotation = currentlyHeldObject.transform.rotation;
        heldInitialRotation = currentlyHeldObject.transform.rotation;

        RemoveHighlight();
        SetCrosshairGrabbableHover(false);
        Debug.Log($"Grabbed: {currentlyHeldObject.name}");
    }

    /// <summary>
    /// Where a held object should float: in front of the camera, but never outside the
    /// lab. Held glassware follows the camera by transform assignment, so without this
    /// clamp it passes straight through a wall when the player faces one from close up.
    /// </summary>
    Vector3 ConfinedHoldPosition()
    {
        Vector3 position = transform.position + (transform.forward * holdDistance);

        LabBoundary boundary = LabBoundary.Active;
        if (boundary != null)
        {
            position = boundary.ClampHeldObjectPosition(position, heldObjectBoundaryMargin);
        }

        return position;
    }

    void MoveHeldObject()
    {
        if (currentlyHeldObject == null)
        {
            return;
        }

        targetPosition = ConfinedHoldPosition();

        currentlyHeldObject.transform.position = Vector3.Lerp(
            currentlyHeldObject.transform.position,
            targetPosition,
            Time.deltaTime * movementSmoothing
        );

        currentlyHeldObject.transform.rotation = Quaternion.Lerp(
            currentlyHeldObject.transform.rotation,
            targetRotation,
            Time.deltaTime * movementSmoothing
        );
    }

    void RotateHeldObject()
    {
        if (currentlyHeldObject == null)
        {
            return;
        }

        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll != 0)
        {
            targetRotation *= Quaternion.Euler(0f, scroll * rotationSpeed, 0f);
        }

        float delta = rotationSpeed * Time.deltaTime;

        if (Input.GetKey(yawLeftKey))
        {
            targetRotation *= Quaternion.Euler(0f, -delta, 0f);
        }

        if (Input.GetKey(yawRightKey))
        {
            targetRotation *= Quaternion.Euler(0f, delta, 0f);
        }

        if (Input.GetKey(pitchUpKey))
        {
            targetRotation *= Quaternion.Euler(delta, 0f, 0f);
        }

        if (Input.GetKey(pitchDownKey))
        {
            targetRotation *= Quaternion.Euler(-delta, 0f, 0f);
        }

        // Roll used to be C / V, but V is also the assistant's push-to-talk key - so asking the
        // assistant a question silently rolled whatever you were holding. Roll is now one key,
        // reversed with Shift, which leaves V free.
        if (Input.GetKey(rollKey))
        {
            bool reversed = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            targetRotation *= Quaternion.Euler(0f, 0f, reversed ? -delta : delta);
        }
    }

    void ReleaseObject()
    {
        if (currentlyHeldObject == null)
        {
            return;
        }

        Debug.Log($"Released: {currentlyHeldObject.name}");

        if (heldObjectRigidbody != null)
        {
            heldObjectRigidbody.linearVelocity = Vector3.zero;
            heldObjectRigidbody.angularVelocity = Vector3.zero;

            bool shouldKeepStatic = keepReleasedObjectsStatic ||
                (currentlyHeldGrabbable != null && currentlyHeldGrabbable.CreatedDesktopRigidbody);

            if (shouldKeepStatic)
            {
                heldObjectRigidbody.useGravity = heldOriginalUseGravity;
                heldObjectRigidbody.isKinematic = heldOriginalIsKinematic;

                // Put it down on whatever is under it BEFORE freezing it. Without this the
                // object is frozen exactly where the student let go - which is how glassware
                // ends up hanging in mid-air for the rest of the session.
                if (settleReleasedObjects)
                {
                    DesktopObjectSettler.Settle(currentlyHeldObject.transform, maxSettleDrop);
                }

                currentlyHeldGrabbable.StabilizeForDesktopResting();
            }
            else
            {
                heldObjectRigidbody.useGravity = heldOriginalUseGravity;
                heldObjectRigidbody.isKinematic = heldOriginalIsKinematic;
                currentlyHeldGrabbable.RestoreOriginalConstraints();
            }
        }

        currentlyHeldObject = null;
        currentlyHeldGrabbable = null;
        heldObjectRigidbody = null;
    }

    void ResetHeldObjectPose()
    {
        if (currentlyHeldObject == null)
        {
            return;
        }

        targetPosition = ConfinedHoldPosition();
        targetRotation = heldInitialRotation;

        currentlyHeldObject.transform.position = targetPosition;
        currentlyHeldObject.transform.rotation = targetRotation;

        if (heldObjectRigidbody != null)
        {
            heldObjectRigidbody.linearVelocity = Vector3.zero;
            heldObjectRigidbody.angularVelocity = Vector3.zero;
        }
    }

    public void ForceReleaseIfHolding(GameObject target)
    {
        if (target != null && currentlyHeldObject == target)
        {
            ReleaseObject();
        }
    }

    public static void ReleaseIfHolding(GameObject target)
    {
        ObjectInteraction[] interactors = FindObjectsByType<ObjectInteraction>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (ObjectInteraction interactor in interactors)
        {
            interactor.ForceReleaseIfHolding(target);
        }
    }

    void SetCrosshairGrabbableHover(bool isHoveringGrabbable)
    {
        DesktopCrosshairUI.SetHoveringGrabbable(isHoveringGrabbable);
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawRay(transform.position, transform.forward * interactionDistance);
    }
}
