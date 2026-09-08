using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Puts a released object down on whatever is underneath it, instead of leaving it hanging in
/// mid-air.
///
/// The bug this fixes is in <see cref="ObjectGrabbable"/>. Any scene object that
/// <see cref="DesktopBootstrap"/> makes grabbable but which has no authored Rigidbody gets one
/// created for it:
///
///     rb = gameObject.AddComponent&lt;Rigidbody&gt;();
///     rb.useGravity = false;
///     rb.isKinematic = true;
///
/// and every release then calls <c>StabilizeForDesktopResting()</c>, which re-applies exactly that
/// and adds <c>RigidbodyConstraints.FreezeAll</c>. So a test tube let go halfway across the room
/// stays precisely where it was dropped, at whatever angle it was held, for the rest of the
/// session - and nothing will ever bring it down again.
///
/// Why a raycast and not simply switching gravity on: the no-gravity choice is deliberate and
/// worth keeping. Several of the lab's tables and shelves have imperfect or missing colliders, and
/// letting every auto-created Rigidbody fall would send glassware through benches and through the
/// floor - a far worse bug than the one being fixed. A single downward cast at the moment of
/// release places the object on the surface below it and changes nothing else.
///
/// The rules are deliberately conservative, so this can only ever make placement more sensible:
///
///  - Only surfaces roughly facing up are landed on, never walls.
///  - Trigger colliders are ignored, so the pour and drop zones are never treated as shelves.
///  - The object's own colliders are skipped.
///  - Nothing moves further than <see cref="DefaultMaxDrop"/>; an object over a big empty gap is
///    left where it is rather than teleported across the room.
///  - An object already resting - or already intersecting a surface - is left untouched.
/// </summary>
public static class DesktopObjectSettler
{
    /// <summary>Furthest an object will be lowered, in metres.</summary>
    public const float DefaultMaxDrop = 1.2f;

    /// <summary>Gaps smaller than this count as already resting.</summary>
    public const float RestingTolerance = 0.015f;

    /// <summary>A surface must face up at least this much to be landed on.</summary>
    private const float MinUpwardNormal = 0.65f;

    /// <summary>
    /// Lowers the object onto the nearest surface beneath it.
    /// </summary>
    /// <returns>True when the object was actually moved.</returns>
    public static bool Settle(Transform root)
    {
        return Settle(root, DefaultMaxDrop);
    }

    public static bool Settle(Transform root, float maxDrop)
    {
        return SettleIfClearBy(root, maxDrop, RestingTolerance);
    }

    /// <summary>
    /// Settles every grabbable in the scene that is hanging clear of the surface below it.
    ///
    /// Run once shortly after a scene loads, to catch glassware that was authored in mid-air.
    /// Several pieces in the testing scene are placed for VR reach rather than sitting on the
    /// bench, and because their Rigidbodies are kinematic with gravity off, nothing ever brings
    /// them down - so they simply hang there for the whole session.
    ///
    /// <paramref name="minGapToMove"/> is the important safety valve, and is much larger than the
    /// per-release tolerance. A test tube standing in its clamp is a few millimetres clear of it;
    /// nudging that would be meddling with correct authored placement. Only something floating by
    /// a visible margin is worth touching.
    /// </summary>
    /// <returns>How many objects were moved.</returns>
    public static int SettleScene(float maxDrop, float minGapToMove)
    {
        // Active objects only. Collider.bounds on an object that has never been enabled can come
        // back empty and centred on the origin, and settling against that would fling the object
        // across the level rather than lower it onto a bench.
        ObjectGrabbable[] grabbables = Object.FindObjectsByType<ObjectGrabbable>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        int moved = 0;

        for (int i = 0; i < grabbables.Length; i++)
        {
            ObjectGrabbable grabbable = grabbables[i];
            if (grabbable == null)
            {
                continue;
            }

            // An object the author gave real physics to will fall on its own; leave it be.
            Rigidbody body = grabbable.Rigidbody;
            if (body != null && !body.isKinematic && body.useGravity)
            {
                continue;
            }

            if (SettleIfClearBy(grabbable.transform, maxDrop, minGapToMove))
            {
                moved++;
            }
        }

        return moved;
    }

    /// <summary>
    /// Settles only if the object is floating by more than <paramref name="minGapToMove"/>.
    /// </summary>
    public static bool SettleIfClearBy(Transform root, float maxDrop, float minGapToMove)
    {
        float gap;
        if (!TryMeasureGap(root, maxDrop, out gap))
        {
            return false;
        }

        if (gap < minGapToMove || gap > maxDrop)
        {
            return false;
        }

        root.position += Vector3.down * gap;
        return true;
    }

    /// <summary>
    /// How far the object is hanging above the nearest upward-facing surface beneath it.
    /// </summary>
    public static bool TryMeasureGap(Transform root, float maxDrop, out float gap)
    {
        gap = 0.0f;

        if (root == null)
        {
            return false;
        }

        Bounds bounds;
        if (!TryGetWorldBounds(root, out bounds))
        {
            return false;
        }

        Vector3 origin = bounds.center;
        float castLength = bounds.extents.y + maxDrop;

        RaycastHit[] hits = Physics.RaycastAll(
            origin, Vector3.down, castLength, ~0, QueryTriggerInteraction.Ignore);

        if (hits == null || hits.Length == 0)
        {
            return false;
        }

        float bestDistance = float.MaxValue;
        bool found = false;
        Vector3 landingPoint = Vector3.zero;

        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit hit = hits[i];

            if (hit.collider == null || hit.collider.transform.IsChildOf(root))
            {
                continue;
            }

            if (hit.normal.y < MinUpwardNormal)
            {
                continue;
            }

            if (hit.distance < bestDistance)
            {
                bestDistance = hit.distance;
                landingPoint = hit.point;
                found = true;
            }
        }

        if (!found)
        {
            return false;
        }

        gap = bounds.min.y - landingPoint.y;
        return true;
    }

    /// <summary>
    /// World bounds from the object's colliders, falling back to its renderers.
    ///
    /// Colliders first because they are what actually rests on a surface; a renderer bound can
    /// include a particle effect or a halo that would hold the object up in the air.
    /// </summary>
    private static bool TryGetWorldBounds(Transform root, out Bounds bounds)
    {
        bounds = new Bounds();
        bool started = false;

        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] == null || colliders[i].isTrigger || !colliders[i].enabled)
            {
                continue;
            }

            if (!started)
            {
                bounds = colliders[i].bounds;
                started = true;
            }
            else
            {
                bounds.Encapsulate(colliders[i].bounds);
            }
        }

        if (started)
        {
            return true;
        }

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null || !renderers[i].enabled)
            {
                continue;
            }

            if (!started)
            {
                bounds = renderers[i].bounds;
                started = true;
            }
            else
            {
                bounds.Encapsulate(renderers[i].bounds);
            }
        }

        return started;
    }
}

/// <summary>
/// Settles each grabbable once, the first time it is seen active.
///
/// A single pass at scene load is not enough on its own. Both labs reveal their equipment a task
/// at a time - <see cref="Randomize"/> in the testing scene, ControlReactions in the lab - so most
/// glassware is still inactive when the scene finishes loading, and an inactive object cannot be
/// measured reliably. This watches for each piece as it appears and puts it down then.
///
/// Each object is handled exactly once, whether or not it needed moving. That matters: a student
/// who deliberately balances something somewhere should not have the game quietly arguing with
/// them about it every half second. Re-releasing an object settles it again through
/// <see cref="ObjectInteraction"/>, which is the right moment for it.
/// </summary>
public class DesktopSettleWatcher : MonoBehaviour
{
    public float interval = 0.5f;
    public float maxDrop = 0.75f;
    public float minGapToSettle = 0.06f;

    private readonly HashSet<int> handled = new HashSet<int>();
    private float nextScan;

    /// <summary>Forget everything, so a freshly loaded scene is settled from scratch.</summary>
    public void Reset()
    {
        handled.Clear();
        nextScan = 0.0f;
    }

    void Update()
    {
        if (Time.unscaledTime < nextScan)
        {
            return;
        }

        nextScan = Time.unscaledTime + Mathf.Max(0.1f, interval);

        ObjectGrabbable[] grabbables = FindObjectsByType<ObjectGrabbable>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        for (int i = 0; i < grabbables.Length; i++)
        {
            ObjectGrabbable grabbable = grabbables[i];
            if (grabbable == null)
            {
                continue;
            }

            int id = grabbable.GetInstanceID();
            if (!handled.Add(id))
            {
                continue;
            }

            Rigidbody body = grabbable.Rigidbody;
            if (body != null && !body.isKinematic && body.useGravity)
            {
                continue;   // real physics; it will fall by itself
            }

            DesktopObjectSettler.SettleIfClearBy(grabbable.transform, maxDrop, minGapToSettle);
        }
    }
}
