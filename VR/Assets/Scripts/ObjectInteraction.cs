using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Handles object interaction for keyboard and mouse gameplay.
/// Attach this to your main camera in desktop mode.
/// </summary>
// After FirstPersonController (-100), so the held object is placed against the camera's pose for
// this frame - and before every script at the default order that follows a held object: the CaCO3
// balloon snaps onto its tube, and the pour streams track their vessels. An earlier version ran at
// +100 in LateUpdate, which put the tube a frame ahead of its own balloon.
[DefaultExecutionOrder(-50)]
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

    [Tooltip("Stop a held object short of walls, benches and equipment instead of letting it " +
             "pass through them. Turn off to restore the old pass-through behaviour.")]
    public bool blockHeldObjectsOnGeometry = true;

    [Tooltip("Clearance kept between a held object and whatever it is being pushed into.")]
    public float heldObjectSkin = 0.04f;

    [Header("Visual Feedback")]
    [Tooltip("Color to highlight interactable objects")]
    public Color highlightColor = Color.yellow;

    [Tooltip("LayerMask for interactable objects")]
    public LayerMask interactableLayer = ~0;

    private GameObject currentlyHeldObject;
    private ObjectGrabbable currentlyHeldGrabbable;
    private ObjectGrabbable highlightedGrabbable;
    private DesktopInteractable highlightedInteractable;

    /// <summary>
    /// A grabbable under the crosshair that is currently refusing to be picked up.
    ///
    /// Both laboratories deliberately lock equipment that does not belong to the experiment on
    /// the bench - it is what stops a student grabbing the CaCO3 test tube during the FeSO4 task.
    /// The lock worked; the <i>communication</i> of it did not. Aiming at a locked tube produced
    /// no highlight, and clicking it produced no highlight, no sound and no message, which is
    /// indistinguishable from the game having missed the click entirely.
    ///
    /// Tracked separately from <see cref="highlightedGrabbable"/> so it can be answered on click
    /// without ever being lit up as though it were available.
    /// </summary>
    private ObjectGrabbable blockedGrabbable;
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

    /// <summary>
    /// Where the held object sits relative to the camera, smoothed.
    ///
    /// The object used to be lerped in world space towards a point in front of the camera. That
    /// point moves whenever the camera does, so every time the player started walking, stopped,
    /// or turned, the object fell behind and then sprang back - an elastic, rubber-band motion.
    ///
    /// Now the object is carried rigidly by the camera, and only this camera-relative offset is
    /// smoothed. Walking and turning therefore move it with no lag at all, while the things that
    /// should ease - gliding into the hand on pickup, being pulled in short of the bench - still do.
    /// </summary>
    private Vector3 heldLocalOffset;

    /// <summary>
    /// Set while the held object is not being positioned (cursor unlocked, typing a question), so
    /// the offset is re-measured on resume instead of snapping the object to a stale spot.
    /// </summary>
    private bool heldFollowSuspended;
    private Quaternion targetRotation;
    private readonly List<HighlightedPart> highlightedParts = new List<HighlightedPart>();
    private readonly List<RaycastResult> uiRaycastResults = new List<RaycastResult>();

    /// <summary>
    /// One renderer under the crosshair, and everything needed to put it back exactly as it was.
    ///
    /// The old highlight overwrote <c>material.color</c> with flat yellow, which threw away the
    /// object's albedo entirely: a copper sulfate solution, a brown iodine crystal and a clear
    /// beaker all became the same shade of yellow plastic, and the tint had to be un-thrown-away
    /// from a parallel <c>Color[]</c> that could fall out of step with the renderer list. Worse,
    /// several reaction scripts swap materials while the student is holding the vessel, so the
    /// restore could write a colour belonging to a material that was no longer there.
    ///
    /// Emission is the right channel for this. It <i>adds</i> light rather than replacing the
    /// surface, so the object still looks like itself and simply glows - which is also how a
    /// highlight should read on transparent glassware, where a flat tint reads as dirt.
    /// </summary>
    private struct HighlightedPart
    {
        public Renderer Renderer;
        public Material Material;
        public bool UsedEmission;
        public Color OriginalEmission;
        public bool HadEmissionKeyword;
        public bool UsedTint;
        public Color OriginalTint;
    }

    private static readonly int EmissionColourId = Shader.PropertyToID("_EmissionColor");
    private static readonly int BaseColourId = Shader.PropertyToID("_Color");

    void Update()
    {
        // A question is being typed into the assistant panel; every letter belongs to it.
        if (LabTextInput.IsCapturing)
        {
            heldFollowSuspended = currentlyHeldObject != null;
            return;
        }

        if (currentlyHeldObject == null)
        {
            if (CanProcessWorldInteraction())
            {
                CheckForInteractableObject();
                PulseHighlight();

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
                RotateHeldObject();
                HandleHeldObjectShortcuts();
                MoveHeldObject();

                if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.R))
                {
                    ReleaseObject();
                }
            }
            else
            {
                heldFollowSuspended = true;

                if (Input.GetKeyDown(KeyCode.R))
                {
                    ReleaseObject();
                }
            }
        }
    }

    /// <summary>
    /// A slow breath on the highlight. Static highlights read as a texture change; a moving one
    /// reads as "this object is responding to you", which is the whole point of it.
    /// </summary>
    void PulseHighlight()
    {
        if (highlightedParts.Count == 0)
        {
            return;
        }

        float pulse = 0.78f + 0.22f * Mathf.Sin(Time.time * 4.2f);
        Color emission = HighlightEmission() * pulse;

        for (int i = 0; i < highlightedParts.Count; i++)
        {
            HighlightedPart part = highlightedParts[i];
            if (!part.UsedEmission || part.Material == null || part.Renderer == null)
            {
                continue;
            }

            part.Material.SetColor(EmissionColourId, part.OriginalEmission + emission);
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
                    AtomixAudio.UiHover();
                }

                highlightedInteractable = null;
                blockedGrabbable = null;
                SetCrosshairGrabbableHover(true);
                return;
            }

            if (grabbable != null)
            {
                // Deliberately locked. Remembered, but never highlighted - lighting it up would
                // be a promise the game is about to break.
                RemoveHighlight();
                blockedGrabbable = grabbable;
                SetCrosshairGrabbableHover(false);
                return;
            }
        }

        RemoveHighlight();
        blockedGrabbable = null;
        SetCrosshairGrabbableHover(false);
    }

    void HighlightObject(Transform target)
    {
        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            // Particle systems and trails have no meaningful surface to light up, and forcing an
            // instance of their material would break the batching they depend on.
            if (renderer is ParticleSystemRenderer || renderer is TrailRenderer)
            {
                continue;
            }

            Material material = renderer.material;      // the per-renderer instance
            if (material == null)
            {
                continue;
            }

            HighlightedPart part = new HighlightedPart
            {
                Renderer = renderer,
                Material = material
            };

            if (material.HasProperty(EmissionColourId))
            {
                part.UsedEmission = true;
                part.OriginalEmission = material.GetColor(EmissionColourId);
                part.HadEmissionKeyword = material.IsKeywordEnabled("_EMISSION");

                material.EnableKeyword("_EMISSION");

                // Also lift the global-illumination flag, or a material marked as not
                // contributing to GI will have its emission ignored entirely in some setups.
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;

                material.SetColor(EmissionColourId, part.OriginalEmission + HighlightEmission());
            }
            else if (material.HasProperty(BaseColourId))
            {
                // An unlit or custom shader with nowhere to put emission. Blend towards the
                // highlight rather than replacing, so the object still reads as itself.
                part.UsedTint = true;
                part.OriginalTint = material.GetColor(BaseColourId);
                material.SetColor(BaseColourId,
                    Color.Lerp(part.OriginalTint, highlightColor, 0.45f));
            }
            else
            {
                continue;
            }

            highlightedParts.Add(part);
        }
    }

    /// <summary>
    /// How much light the highlight adds. Kept below 1 so a highlighted object does not blow
    /// out into the bloom threshold and become a white blob.
    /// </summary>
    Color HighlightEmission()
    {
        Color emission = highlightColor * 0.55f;
        emission.a = 0.0f;
        return emission;
    }

    void RemoveHighlight()
    {
        for (int i = 0; i < highlightedParts.Count; i++)
        {
            HighlightedPart part = highlightedParts[i];

            // The renderer may have been destroyed, or a reaction script may have swapped the
            // material out from under us while the object was lit. Either way, restoring would
            // be writing to something that is no longer on screen.
            if (part.Renderer == null || part.Material == null ||
                part.Renderer.sharedMaterial == null)
            {
                continue;
            }

            if (part.UsedEmission)
            {
                part.Material.SetColor(EmissionColourId, part.OriginalEmission);
                if (!part.HadEmissionKeyword)
                {
                    part.Material.DisableKeyword("_EMISSION");
                }
            }
            else if (part.UsedTint)
            {
                part.Material.SetColor(BaseColourId, part.OriginalTint);
            }
        }

        highlightedParts.Clear();
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

    /// <summary>
    /// Bigger objects sound lower. Derived from renderer bounds rather than from a field, because
    /// nothing in the scene was authored with a size category and adding one to some forty
    /// prefabs to earn a pitch shift would not be worth the churn.
    /// </summary>
    static float PitchForSize(GameObject target)
    {
        if (target == null)
        {
            return 1.0f;
        }

        Renderer renderer = target.GetComponentInChildren<Renderer>();
        if (renderer == null)
        {
            return 1.0f;
        }

        // Lab glassware spans roughly 4 cm (a stopper) to 30 cm (a burette stand).
        float size = renderer.bounds.size.magnitude;
        return Mathf.Lerp(1.22f, 0.82f, Mathf.InverseLerp(0.04f, 0.30f, size));
    }

    void TryGrabObject()
    {
        if (highlightedGrabbable == null || !highlightedGrabbable.canGrab)
        {
            // Aiming at a piece of equipment that is locked for this task. Silence here is what
            // produced "clicking the test tube does nothing" - the click still does nothing, but
            // now the game says so instead of appearing to have missed it.
            if (blockedGrabbable != null || highlightedGrabbable != null)
            {
                AtomixAudio.UiDenied();
                LabHudController.Toast("That equipment is not part of this experiment.");
            }

            return;
        }

        currentlyHeldObject = highlightedGrabbable.gameObject;
        currentlyHeldGrabbable = highlightedGrabbable;
        heldObjectRigidbody = highlightedGrabbable.Rigidbody;
        cachedHeldRadius = -1.0f;

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

        // Start from where the object actually is, so it glides into the hand rather than jumping.
        heldLocalOffset = transform.InverseTransformPoint(currentlyHeldObject.transform.position);
        heldFollowSuspended = false;

        RemoveHighlight();
        blockedGrabbable = null;
        SetCrosshairGrabbableHover(false);

        // Pitch varies with size: a test tube ticks, a full berzelius thunks. Cheap, and it
        // stops sixty identical pickups an hour from wearing thin.
        AtomixAudio.Play(AtomixAudio.Cue.Grab, 1.0f, PitchForSize(currentlyHeldObject));
        CameraJuice.Shake(0.10f);
    }

    /// <summary>
    /// Where a held object should float: in front of the camera, but never outside the
    /// lab. Held glassware follows the camera by transform assignment, so without this
    /// clamp it passes straight through a wall when the player faces one from close up.
    /// </summary>
    Vector3 ConfinedHoldPosition()
    {
        float distance = holdDistance;

        // Stop short of anything solid between the camera and where the object wants to float.
        //
        // Held glassware is positioned by direct transform assignment, which does no collision of
        // any kind. Walking up to the bench therefore pushed whatever you were carrying straight
        // through it - and letting go at that moment left the object embedded in the table, which
        // is the other half of the "objects are inside the table" problem. Sweeping first keeps
        // the object on the near side of the surface, so it never gets into that state.
        if (blockHeldObjectsOnGeometry)
        {
            distance = SweptHoldDistance(distance);
        }

        Vector3 position = transform.position + (transform.forward * distance);

        LabBoundary boundary = LabBoundary.Active;
        if (boundary != null)
        {
            position = boundary.ClampHeldObjectPosition(position, heldObjectBoundaryMargin);
        }

        return position;
    }

    /// <summary>
    /// How far in front of the camera the held object can actually float without intersecting
    /// something. A sphere sweep sized to the object itself, so a large beaker stops further out
    /// than a stopper does.
    /// </summary>
    float SweptHoldDistance(float desired)
    {
        float radius = HeldObjectRadius();
        if (radius <= 0.0f)
        {
            return desired;
        }

        // Start the sweep just in front of the near plane, so the player's own body and the
        // camera's CharacterController are behind it.
        const float startOffset = 0.25f;
        float sweepLength = Mathf.Max(0.0f, desired - startOffset);
        if (sweepLength <= 0.0f)
        {
            return desired;
        }

        Vector3 origin = transform.position + (transform.forward * startOffset);

        int hitCount = Physics.SphereCastNonAlloc(
            origin, radius, transform.forward, holdSweepHits, sweepLength,
            interactableLayer, QueryTriggerInteraction.Ignore);

        float nearest = desired;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = holdSweepHits[i];
            if (hit.collider == null)
            {
                continue;
            }

            // The object being carried, and the player carrying it, are not obstacles.
            if (currentlyHeldObject != null &&
                hit.collider.transform.IsChildOf(currentlyHeldObject.transform))
            {
                continue;
            }

            if (hit.collider.transform.IsChildOf(transform))
            {
                continue;
            }

            // A zero distance means the sweep began already overlapping, which gives no usable
            // normal or point. Ignoring it is correct: pulling the object all the way back to the
            // camera would be worse than leaving it where it is for a frame.
            if (hit.distance <= 0.0f)
            {
                continue;
            }

            float allowed = startOffset + hit.distance - heldObjectSkin;
            if (allowed < nearest)
            {
                nearest = allowed;
            }
        }

        // Never pull it so close that it is inside the camera.
        return Mathf.Max(minimumHoldDistance, nearest);
    }

    [Tooltip("Closest a held object may be pulled towards the camera when it is blocked.")]
    public float minimumHoldDistance = 0.45f;

    private readonly RaycastHit[] holdSweepHits = new RaycastHit[16];
    private float cachedHeldRadius = -1.0f;

    /// <summary>
    /// A sweep radius for the held object, measured once when it is picked up. Clamped so an
    /// awkward bounding box - a burette stand is tall and thin - cannot produce a sphere so large
    /// that the object can never be brought near anything.
    /// </summary>
    float HeldObjectRadius()
    {
        if (cachedHeldRadius >= 0.0f)
        {
            return cachedHeldRadius;
        }

        if (currentlyHeldObject == null)
        {
            return 0.0f;
        }

        float radius = 0.06f;
        Collider[] colliders = currentlyHeldObject.GetComponentsInChildren<Collider>();
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] == null || colliders[i].isTrigger || !colliders[i].enabled)
            {
                continue;
            }

            Vector3 size = colliders[i].bounds.size;

            // Horizontal extent only: the height of a test tube should not decide how close to
            // the bench you are allowed to carry it.
            radius = Mathf.Max(radius, Mathf.Max(size.x, size.z) * 0.5f);
        }

        cachedHeldRadius = Mathf.Clamp(radius, 0.03f, 0.30f);
        return cachedHeldRadius;
    }

    void MoveHeldObject()
    {
        if (currentlyHeldObject == null)
        {
            return;
        }

        if (heldFollowSuspended)
        {
            // Coming back from a pause or from typing: measure from where the object was left.
            heldLocalOffset = transform.InverseTransformPoint(currentlyHeldObject.transform.position);
            heldFollowSuspended = false;
        }

        targetPosition = ConfinedHoldPosition();

        // Frame-rate independent: the same fraction of the remaining gap per second at any
        // frame rate.
        float blend = 1.0f - Mathf.Exp(-movementSmoothing * Time.deltaTime);

        // Smooth only the offset from the camera; the camera's own motion is applied rigidly.
        Vector3 desiredLocal = transform.InverseTransformPoint(targetPosition);
        heldLocalOffset = Vector3.Lerp(heldLocalOffset, desiredLocal, blend);

        Vector3 worldPosition = transform.TransformPoint(heldLocalOffset);

        // The boundary clamp is in world space, so it is re-applied after the camera transform.
        LabBoundary boundary = LabBoundary.Active;
        if (boundary != null)
        {
            worldPosition = boundary.ClampHeldObjectPosition(worldPosition, heldObjectBoundaryMargin);
        }

        currentlyHeldObject.transform.position = worldPosition;

        currentlyHeldObject.transform.rotation = Quaternion.Slerp(
            currentlyHeldObject.transform.rotation, targetRotation, blend);
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

        AtomixAudio.Play(AtomixAudio.Cue.Release, 1.0f, PitchForSize(currentlyHeldObject));

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
        cachedHeldRadius = -1.0f;
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
        heldLocalOffset = transform.InverseTransformPoint(targetPosition);
        heldFollowSuspended = false;

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
