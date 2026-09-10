using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Keeps the player inside the playable interior of the chemistry laboratory.
///
/// The lab room mesh (Environment/Laboratory/Cube) ships without a collider, so nothing
/// physically stops a player from walking or flying out of the building. This component
/// measures the room at runtime and builds an invisible collider cage around it, then
/// acts as a second line of defence by clamping the player transform every frame.
///
/// It is built from code by <see cref="DesktopBootstrap"/>, so no scene or prefab edits
/// are required and the same logic covers LabScene, LabAssistantScene and TestingPhaseLab.
/// </summary>
public class LabBoundary : MonoBehaviour
{
    public static LabBoundary Active { get; private set; }

    [Header("Room Detection")]
    [Tooltip("Name of the GameObject that holds the laboratory shell geometry")]
    public string roomRootName = "Laboratory";

    [Tooltip("How far inside the measured room shell the playable interior starts (metres)")]
    public float wallInset = 0.35f;

    [Tooltip("Headroom kept below the measured ceiling (metres)")]
    public float ceilingHeadroom = 0.30f;

    [Header("Fallback Interior (world space)")]
    [Tooltip("Used when the room shell cannot be measured, or measures implausibly")]
    public Vector3 fallbackMin = new Vector3(1.35f, 0f, 2.9f);

    [Tooltip("Used when the room shell cannot be measured, or measures implausibly")]
    public Vector3 fallbackMax = new Vector3(8f, 4.4f, 9.3f);

    [Tooltip("Reject a measured room whose floor area differs from the fallback by more than this factor")]
    public float maxFootprintRatio = 2.5f;

    [Header("Collider Cage")]
    [Tooltip("Build invisible boundary colliders around the interior")]
    public bool buildColliders = true;

    [Tooltip("Thickness of each boundary slab (metres)")]
    public float wallThickness = 0.5f;

    [Tooltip("Layer the boundary colliders are placed on. Falls back to Default if absent.")]
    public string boundaryLayerName = "LabBoundary";

    [Header("Safety Clamp")]
    [Tooltip("Clamp the registered player transform into the interior every frame")]
    public bool clampPlayer = true;

    [Tooltip("Horizontal distance kept between the player and the boundary (metres)")]
    public float playerRadius = 0.35f;

    [Tooltip("Lowest the player's eye is allowed to sit above the interior floor")]
    public float minEyeHeight = 0.6f;

    [Tooltip("Distance kept between the player's eye and the interior ceiling")]
    public float eyeHeadroom = 0.15f;

    [Header("Diagnostics")]
    [Tooltip("Draw the measured interior in the Scene view for this many seconds on start")]
    public float debugDrawSeconds = 0f;

    private Bounds interior;
    private bool interiorResolved;
    private bool usedFallback;
    private Transform playerTransform;
    private bool playerIsRigRoot;
    private readonly List<Transform> extraConfinedTransforms = new List<Transform>();

    /// <summary>Playable interior of the lab in world space.</summary>
    public Bounds Interior => interior;

    /// <summary>
    /// Whether <see cref="Interior"/> holds a real measurement yet. Until the room shell has
    /// been found it is a default Bounds at the origin, which is not something another system
    /// should build anything from.
    /// </summary>
    public bool HasInterior => interiorResolved;

    /// <summary>True when the room shell could not be measured and the fallback box is in use.</summary>
    public bool UsedFallback => usedFallback;

    void Awake()
    {
        Active = this;
        ResolveInterior();
    }

    void Start()
    {
        if (buildColliders)
        {
            BuildCage();
        }

        ExcludeBoundaryFromInteractionRaycasts();

        if (debugDrawSeconds > 0f)
        {
            DrawInterior(debugDrawSeconds);
        }
    }

    void OnDestroy()
    {
        if (Active == this)
        {
            Active = null;
        }
    }

    void LateUpdate()
    {
        if (!clampPlayer || !interiorResolved)
        {
            return;
        }

        if (playerTransform == null)
        {
            ResolvePlayerTransform();
        }

        if (playerTransform != null)
        {
            playerTransform.position = playerIsRigRoot
                ? ClampHorizontally(playerTransform.position)
                : ClampEyePosition(playerTransform.position);
        }

        for (int i = extraConfinedTransforms.Count - 1; i >= 0; i--)
        {
            Transform confined = extraConfinedTransforms[i];
            if (confined == null)
            {
                extraConfinedTransforms.RemoveAt(i);
                continue;
            }

            // Registered rigs sit on the floor, so only their footprint is confined.
            confined.position = ClampHorizontally(confined.position);
        }
    }

    /// <summary>
    /// Registers the transform that represents the player's head. Desktop mode passes the
    /// camera; a VR rig would pass its origin so continuous locomotion is confined too.
    /// </summary>
    public void RegisterPlayer(Transform player)
    {
        playerTransform = player;
        playerIsRigRoot = false;
    }

    /// <summary>
    /// Finds the transform to confine when nothing registered one. Desktop mode uses the
    /// FirstPersonController. In VR the head is driven by tracking, so the rig root is
    /// clamped instead — clamping the camera itself would fight the tracked pose.
    /// </summary>
    void ResolvePlayerTransform()
    {
        FirstPersonController controller = FindFirstObjectByType<FirstPersonController>(FindObjectsInactive.Exclude);
        if (controller != null)
        {
            playerTransform = controller.transform;
            playerIsRigRoot = false;
            return;
        }

        foreach (string rigName in RigRootNames)
        {
            GameObject rig = GameObject.Find(rigName);
            if (rig != null)
            {
                playerTransform = rig.transform;
                playerIsRigRoot = true;
                return;
            }
        }
    }

    /// <summary>Clamps only the footprint, leaving height untouched.</summary>
    public Vector3 ClampHorizontally(Vector3 worldPosition)
    {
        if (!interiorResolved)
        {
            return worldPosition;
        }

        Vector3 clamped = ClampEyePosition(worldPosition);
        clamped.y = worldPosition.y;
        return clamped;
    }

    static readonly string[] RigRootNames = { "XR Rig", "XR Origin", "XROrigin", "XR Origin (XR Rig)" };

    /// <summary>Confines an additional transform (for example an XR rig root).</summary>
    public void ConfineTransform(Transform target)
    {
        if (target != null && target != playerTransform && !extraConfinedTransforms.Contains(target))
        {
            extraConfinedTransforms.Add(target);
        }
    }

    /// <summary>Clamps an eye/head position so the player's body stays inside the lab.</summary>
    public Vector3 ClampEyePosition(Vector3 eyePosition)
    {
        if (!interiorResolved)
        {
            return eyePosition;
        }

        Vector3 min = interior.min;
        Vector3 max = interior.max;
        float radius = Mathf.Max(0f, playerRadius);

        // Never let the inset collapse the box in a tight room.
        float xPadding = Mathf.Min(radius, Mathf.Max(0f, (max.x - min.x) * 0.5f - 0.05f));
        float zPadding = Mathf.Min(radius, Mathf.Max(0f, (max.z - min.z) * 0.5f - 0.05f));

        eyePosition.x = Mathf.Clamp(eyePosition.x, min.x + xPadding, max.x - xPadding);
        eyePosition.z = Mathf.Clamp(eyePosition.z, min.z + zPadding, max.z - zPadding);

        float lowestEye = min.y + minEyeHeight;
        float highestEye = max.y - eyeHeadroom;
        if (highestEye < lowestEye)
        {
            highestEye = lowestEye;
        }

        eyePosition.y = Mathf.Clamp(eyePosition.y, lowestEye, highestEye);
        return eyePosition;
    }

    /// <summary>
    /// Clamps a held object's target position so glassware cannot be pushed through a wall.
    /// </summary>
    public Vector3 ClampHeldObjectPosition(Vector3 position, float margin)
    {
        if (!interiorResolved)
        {
            return position;
        }

        Vector3 min = interior.min;
        Vector3 max = interior.max;
        float xPadding = Mathf.Min(margin, Mathf.Max(0f, (max.x - min.x) * 0.5f - 0.05f));
        float zPadding = Mathf.Min(margin, Mathf.Max(0f, (max.z - min.z) * 0.5f - 0.05f));
        float yPadding = Mathf.Min(margin, Mathf.Max(0f, (max.y - min.y) * 0.5f - 0.05f));

        position.x = Mathf.Clamp(position.x, min.x + xPadding, max.x - xPadding);
        position.y = Mathf.Clamp(position.y, min.y + yPadding, max.y - yPadding);
        position.z = Mathf.Clamp(position.z, min.z + zPadding, max.z - zPadding);
        return position;
    }

    /// <summary>True when the point is inside the playable interior.</summary>
    public bool Contains(Vector3 worldPosition)
    {
        return interiorResolved && interior.Contains(worldPosition);
    }

    // ------------------------------------------------------------------
    // Room measurement
    // ------------------------------------------------------------------

    void ResolveInterior()
    {
        Bounds fallback = BoundsFromMinMax(fallbackMin, fallbackMax);

        if (TryMeasureRoomShell(out Bounds shell))
        {
            Bounds measured = InsetShell(shell);
            if (IsPlausible(measured) && IsComparableTo(measured, fallback))
            {
                interior = measured;
                interiorResolved = true;
                usedFallback = false;
                Debug.Log($"[LabBoundary] Measured lab interior from '{roomRootName}': " +
                          $"min {Format(interior.min)} max {Format(interior.max)}");
                return;
            }

            Debug.LogWarning($"[LabBoundary] Measured room shell was implausible " +
                             $"(min {Format(measured.min)} max {Format(measured.max)}). Using the fallback interior.");
        }
        else
        {
            Debug.LogWarning($"[LabBoundary] Could not find room geometry named '{roomRootName}'. Using the fallback interior.");
        }

        interior = fallback;
        interiorResolved = true;
        usedFallback = true;
        Debug.Log($"[LabBoundary] Fallback lab interior: min {Format(interior.min)} max {Format(interior.max)}");
    }

    bool TryMeasureRoomShell(out Bounds shell)
    {
        shell = new Bounds();

        Transform roomRoot = FindRoomRoot();
        if (roomRoot == null)
        {
            return false;
        }

        // The room shell is the single largest renderer under the room root. Taking the
        // largest one rather than the union keeps ceiling lamps, window frames and the
        // door group from inflating the box.
        MeshRenderer[] renderers = roomRoot.GetComponentsInChildren<MeshRenderer>(true);
        MeshRenderer largest = null;
        float largestVolume = 0f;

        foreach (MeshRenderer renderer in renderers)
        {
            if (renderer == null)
            {
                continue;
            }

            Vector3 size = renderer.bounds.size;
            float volume = size.x * size.y * size.z;
            if (volume > largestVolume)
            {
                largestVolume = volume;
                largest = renderer;
            }
        }

        if (largest == null)
        {
            return false;
        }

        shell = largest.bounds;

        // If the largest renderer is a flat slab (a floor-only mesh) grow the vertical
        // span using every renderer under the room root so the cage still has height.
        if (shell.size.y < 2f)
        {
            foreach (MeshRenderer renderer in renderers)
            {
                if (renderer != null)
                {
                    shell.Encapsulate(new Bounds(renderer.bounds.center, new Vector3(0f, renderer.bounds.size.y, 0f)));
                }
            }
        }

        return true;
    }

    Transform FindRoomRoot()
    {
        if (!string.IsNullOrEmpty(roomRootName))
        {
            GameObject byName = GameObject.Find(roomRootName);
            if (byName != null)
            {
                return byName.transform;
            }

            // GameObject.Find skips inactive objects; fall back to a full scan.
            Transform[] transforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (Transform candidate in transforms)
            {
                if (candidate != null && candidate.name == roomRootName)
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    Bounds InsetShell(Bounds shell)
    {
        Vector3 min = shell.min;
        Vector3 max = shell.max;

        min.x += wallInset;
        max.x -= wallInset;
        min.z += wallInset;
        max.z -= wallInset;
        max.y -= ceilingHeadroom;

        // The floor is left where the shell puts it; sinking through it is what the
        // floor slab of the cage prevents.
        return BoundsFromMinMax(min, max);
    }

    bool IsPlausible(Bounds candidate)
    {
        Vector3 size = candidate.size;
        return size.x >= 2f && size.z >= 2f && size.y >= 2f &&
               size.x <= 200f && size.z <= 200f && size.y <= 200f;
    }

    /// <summary>
    /// Guards against the shell mesh turning out to be the whole building rather than the
    /// room: a measured footprint far larger than the known-good fallback would leave the
    /// player free to walk outside, which is the exact bug this component exists to fix.
    /// </summary>
    bool IsComparableTo(Bounds measured, Bounds reference)
    {
        float measuredFootprint = measured.size.x * measured.size.z;
        float referenceFootprint = reference.size.x * reference.size.z;
        if (referenceFootprint <= 0f)
        {
            return true;
        }

        float ratio = measuredFootprint / referenceFootprint;
        if (ratio > maxFootprintRatio || ratio < 1f / maxFootprintRatio)
        {
            Debug.LogWarning($"[LabBoundary] Measured footprint is {ratio:0.00}x the expected size " +
                             "- the detected mesh is probably not the room shell.");
            return false;
        }

        return true;
    }

    static Bounds BoundsFromMinMax(Vector3 min, Vector3 max)
    {
        Bounds bounds = new Bounds();
        bounds.SetMinMax(Vector3.Min(min, max), Vector3.Max(min, max));
        return bounds;
    }

    // ------------------------------------------------------------------
    // Collider cage
    // ------------------------------------------------------------------

    void BuildCage()
    {
        if (!interiorResolved)
        {
            return;
        }

        int layer = LayerMask.NameToLayer(boundaryLayerName);
        if (layer < 0)
        {
            layer = gameObject.layer;
        }

        Vector3 min = interior.min;
        Vector3 max = interior.max;
        Vector3 center = interior.center;
        Vector3 size = interior.size;
        float t = Mathf.Max(0.05f, wallThickness);

        // Slabs sit just outside the interior so the whole interior stays free.
        CreateSlab("Boundary_Wall_NegX", new Vector3(min.x - t * 0.5f, center.y, center.z),
                   new Vector3(t, size.y + t * 2f, size.z + t * 2f), layer);
        CreateSlab("Boundary_Wall_PosX", new Vector3(max.x + t * 0.5f, center.y, center.z),
                   new Vector3(t, size.y + t * 2f, size.z + t * 2f), layer);
        CreateSlab("Boundary_Wall_NegZ", new Vector3(center.x, center.y, min.z - t * 0.5f),
                   new Vector3(size.x + t * 2f, size.y + t * 2f, t), layer);
        CreateSlab("Boundary_Wall_PosZ", new Vector3(center.x, center.y, max.z + t * 0.5f),
                   new Vector3(size.x + t * 2f, size.y + t * 2f, t), layer);
        CreateSlab("Boundary_Floor", new Vector3(center.x, min.y - t * 0.5f, center.z),
                   new Vector3(size.x + t * 2f, t, size.z + t * 2f), layer);
        CreateSlab("Boundary_Ceiling", new Vector3(center.x, max.y + t * 0.5f, center.z),
                   new Vector3(size.x + t * 2f, t, size.z + t * 2f), layer);
    }

    void CreateSlab(string slabName, Vector3 worldCenter, Vector3 worldSize, int layer)
    {
        GameObject slab = new GameObject(slabName);
        slab.transform.SetParent(transform, false);
        slab.transform.position = worldCenter;
        slab.transform.rotation = Quaternion.identity;
        slab.layer = layer;

        BoxCollider collider = slab.AddComponent<BoxCollider>();
        collider.size = worldSize;
        collider.isTrigger = false;
    }

    /// <summary>
    /// Keeps the boundary cage out of the crosshair interaction raycast so it can never
    /// steal a grab from an object standing close to a wall.
    /// </summary>
    void ExcludeBoundaryFromInteractionRaycasts()
    {
        int layer = LayerMask.NameToLayer(boundaryLayerName);
        if (layer < 0)
        {
            return;
        }

        int mask = ~(1 << layer);
        ObjectInteraction[] interactors = FindObjectsByType<ObjectInteraction>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (ObjectInteraction interactor in interactors)
        {
            if (interactor != null)
            {
                interactor.interactableLayer &= mask;
            }
        }
    }

    // ------------------------------------------------------------------
    // Diagnostics
    // ------------------------------------------------------------------

    void DrawInterior(float duration)
    {
        Vector3 min = interior.min;
        Vector3 max = interior.max;
        Color color = usedFallback ? Color.yellow : Color.cyan;

        Vector3[] bottom =
        {
            new Vector3(min.x, min.y, min.z), new Vector3(max.x, min.y, min.z),
            new Vector3(max.x, min.y, max.z), new Vector3(min.x, min.y, max.z)
        };
        Vector3[] top =
        {
            new Vector3(min.x, max.y, min.z), new Vector3(max.x, max.y, min.z),
            new Vector3(max.x, max.y, max.z), new Vector3(min.x, max.y, max.z)
        };

        for (int i = 0; i < 4; i++)
        {
            int next = (i + 1) % 4;
            Debug.DrawLine(bottom[i], bottom[next], color, duration);
            Debug.DrawLine(top[i], top[next], color, duration);
            Debug.DrawLine(bottom[i], top[i], color, duration);
        }
    }

    static string Format(Vector3 v)
    {
        return $"({v.x:0.00}, {v.y:0.00}, {v.z:0.00})";
    }

    void OnDrawGizmosSelected()
    {
        if (!interiorResolved)
        {
            return;
        }

        Gizmos.color = usedFallback ? Color.yellow : Color.cyan;
        Gizmos.DrawWireCube(interior.center, interior.size);
    }
}
