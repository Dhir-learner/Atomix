using UnityEngine;

/// <summary>
/// Add this component to any object you want to be grabbable/interactable.
/// Child colliders are supported, and a Rigidbody is added automatically if needed.
/// </summary>
public class ObjectGrabbable : MonoBehaviour
{
    [Header("Grabbable Settings")]
    [Tooltip("Can this object be grabbed?")]
    public bool canGrab = true;

    [Tooltip("Should this object return to original position when released?")]
    public bool returnToOriginalPosition = false;

    private Vector3 originalPosition;
    private Quaternion originalRotation;
    private Rigidbody rb;
    private bool createdDesktopRigidbody;
    private RigidbodyConstraints originalConstraints;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.isKinematic = true;
            createdDesktopRigidbody = true;
            Debug.LogWarning($"Added Rigidbody to {gameObject.name}");
        }

        originalConstraints = rb.constraints;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        if (GetComponentInChildren<Collider>() == null)
        {
            Debug.LogWarning($"ObjectGrabbable on {gameObject.name} could not find a Collider on this object or its children.");
        }
    }

    void Start()
    {
        originalPosition = transform.position;
        originalRotation = transform.rotation;

        // Prevent annoying physical collisions between glassware (beakers, bottles, test tubes) during experiments
        Collider[] myColliders = GetComponentsInChildren<Collider>(true);
        ObjectGrabbable[] allGrabbables = FindObjectsByType<ObjectGrabbable>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (ObjectGrabbable other in allGrabbables)
        {
            if (other != null && other != this)
            {
                Collider[] otherColliders = other.GetComponentsInChildren<Collider>(true);
                foreach (Collider c1 in myColliders)
                {
                    foreach (Collider c2 in otherColliders)
                    {
                        if (c1 != null && c2 != null)
                        {
                            Physics.IgnoreCollision(c1, c2, true);
                        }
                    }
                }
            }
        }
    }

    public Rigidbody Rigidbody => rb;

    public bool CreatedDesktopRigidbody => createdDesktopRigidbody;

    public bool HasCollider => GetComponentInChildren<Collider>() != null;

    public Renderer[] GetRenderers()
    {
        return GetComponentsInChildren<Renderer>(true);
    }

    /// <summary>
    /// Call this to reset object to its original position.
    /// </summary>
    public void ResetPosition()
    {
        transform.position = originalPosition;
        transform.rotation = originalRotation;

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }

    /// <summary>
    /// Enable or disable grabbing on this object.
    /// </summary>
    public void SetGrabbable(bool grabbable)
    {
        canGrab = grabbable;
    }

    public void StabilizeForDesktopResting()
    {
        if (rb == null)
        {
            return;
        }

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        if (createdDesktopRigidbody)
        {
            rb.useGravity = false;
            rb.isKinematic = true;
        }

        rb.constraints = RigidbodyConstraints.FreezeAll;
    }

    public void RestoreOriginalConstraints()
    {
        if (rb == null)
        {
            return;
        }

        rb.constraints = originalConstraints;
    }
}
