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
    /// Furthest an object embedded in a surface will be lifted back out of it, in metres.
    ///
    /// Deliberately much smaller than <see cref="DefaultMaxDrop"/>. Something a few centimetres
    /// into the bench has been placed badly and should be rescued; something half a metre inside
    /// the geometry is a different problem, and hoisting it would be a guess.
    /// </summary>
    public const float DefaultMaxLift = 0.35f;

    /// <summary>An object must be embedded by more than this before it is lifted.</summary>
    public const float EmbedTolerance = 0.004f;

    /// <summary>
    /// Largest horizontal footprint, in metres, that this will move.
    ///
    /// A safety net rather than a tuning value. Everything here is meant to reposition a single
    /// piece of laboratory equipment, and no piece of equipment is a metre across. Anything
    /// bigger is a grouping object, a bench or the room itself, and moving one of those would be
    /// spectacular. The scene really does nest glassware under grouping transforms - there are
    /// objects called "Containers" and "BerzeliusGlasses" - so this is a live risk, not a
    /// hypothetical one.
    /// </summary>
    public const float MaxFootprint = 1.5f;

    /// <summary>Started slightly above the object, so a surface it is currently inside is seen.</summary>
    private const float CastEpsilon = 0.02f;

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
        int moved = 0;

        List<Transform> candidates = new List<Transform>();
        CollectSettleCandidates(candidates);

        for (int i = 0; i < candidates.Count; i++)
        {
            // SettleIfClearBy also lifts anything found sunk into the bench, using its own much
            // tighter limits - minGapToMove only governs how far something may be lowered.
            if (SettleIfClearBy(candidates[i], maxDrop, minGapToMove))
            {
                moved++;
            }
        }

        return moved;
    }

    /// <summary>
    /// Everything worth checking: the loose glassware, and the fixed controls.
    ///
    /// Controls have to be in this list even though they are never <i>lowered</i>, because they
    /// can still be found sunk into the bench and lifting them out is always right. Leaving them
    /// out is what kept the Bunsen burner standing inside the table: <c>LightFire.burnerSupport</c>
    /// is the burner's own GameObject, so once <see cref="DesktopBootstrap"/> made it clickable
    /// it stopped receiving an <see cref="ObjectGrabbable"/> - and a scan over grabbables alone
    /// never saw it again.
    ///
    /// Active objects only. <c>Collider.bounds</c> on an object that has never been enabled can
    /// come back empty and centred on the origin, and settling against that would fling the
    /// object across the level rather than place it on a bench.
    /// </summary>
    public static void CollectSettleCandidates(List<Transform> results)
    {
        // --- whole-object cases go in first ------------------------------------------
        //
        // The Bunsen burner is the one piece of equipment whose script does not sit on the thing
        // that needs moving. LightFire lives on `burnerSupport`, which is a *child* inside the
        // burner prefab - so settling the object the script is attached to would lift the support
        // ring out of the burner and leave the burner in the table. The burner as a whole is what
        // rests on the bench, so that is what gets measured.
        //
        // transform.root is safe here specifically because the burner prefab instance is a
        // top-level scene object. It would not be safe in general - this scene nests glassware
        // under grouping transforms called "Containers" and "BerzeliusGlasses" - which is what
        // the MaxFootprint ceiling in SettleIfClearBy is there to catch.
        LightFire[] burners = Object.FindObjectsByType<LightFire>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        for (int i = 0; i < burners.Length; i++)
        {
            LightFire burner = burners[i];
            if (burner == null)
            {
                continue;
            }

            Transform support = burner.burnerSupport != null
                ? burner.burnerSupport.transform
                : burner.transform;

            AddIfNotNested(results, support.root);
        }

        // --- loose glassware ----------------------------------------------------------
        ObjectGrabbable[] grabbables = Object.FindObjectsByType<ObjectGrabbable>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.None);

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

            AddIfNotNested(results, grabbable.transform);
        }

        // --- fixed controls -----------------------------------------------------------
        DesktopInteractable[] controls = Object.FindObjectsByType<DesktopInteractable>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        for (int i = 0; i < controls.Length; i++)
        {
            DesktopInteractable control = controls[i];
            if (control == null)
            {
                continue;
            }

            AddIfNotNested(results, control.transform);
        }
    }

    /// <summary>
    /// Adds a transform unless it is already listed, or sits inside something already listed.
    ///
    /// Without the nesting test the burner would be settled twice - once as a whole object and
    /// again as its own support child - and the second pass would pull the support out of the
    /// burner it had just placed.
    /// </summary>
    private static void AddIfNotNested(List<Transform> results, Transform candidate)
    {
        if (candidate == null)
        {
            return;
        }

        for (int i = 0; i < results.Count; i++)
        {
            if (results[i] == null)
            {
                continue;
            }

            // IsChildOf is true for the transform itself, so this covers duplicates too.
            if (candidate.IsChildOf(results[i]))
            {
                return;
            }
        }

        results.Add(candidate);
    }

    /// <summary>
    /// Settles only if the object is floating by more than <paramref name="minGapToMove"/>.
    /// </summary>
    public static bool SettleIfClearBy(Transform root, float maxDrop, float minGapToMove)
    {
        float gap;
        Collider surface;
        if (!TryMeasureGap(root, maxDrop, out gap, out surface))
        {
            return false;
        }

        // Never move something the size of a room, a bench or a grouping object.
        Bounds sizeCheck;
        if (!TryGetWorldBounds(root, out sizeCheck) ||
            sizeCheck.size.x > MaxFootprint || sizeCheck.size.z > MaxFootprint)
        {
            return false;
        }

        // Controls are a special case, and the two directions are not equally safe for them.
        //
        // A tap handle, a burner support or a container lid is authored exactly where it belongs,
        // often mounted on or into something else, so *lowering* one is meddling: at best it looks
        // wrong, at worst the movement actuates the thing it controls. But an object sunk into the
        // bench is never correct, control or not - so lifting is still allowed.
        //
        // An earlier attempt at this excluded controls from settling altogether, and that is what
        // left the Bunsen burner standing inside the table: LightFire's burnerSupport is a child
        // inside the burner prefab, so making it clickable also made the burner un-settleable.
        //
        // Children as well as parents. The burner is submitted as a whole object, and its control
        // hangs *below* the transform being measured - a parents-only test would call the burner
        // "not a control" and allow it to be lowered. Looking downwards as well means anything
        // containing a control is lift-only, which also makes this safe if `transform.root` ever
        // resolves to a grouping node in some future scene: the worst it could then do is lift a
        // group that was genuinely buried in the floor, capped at DefaultMaxLift.
        bool isControl = root.GetComponentInParent<DesktopInteractable>() != null ||
                         root.GetComponentInChildren<DesktopInteractable>(true) != null;

        // --- floating above the surface: lower it ------------------------------------
        if (gap > 0.0f)
        {
            if (isControl || gap < minGapToMove || gap > maxDrop)
            {
                return false;
            }

            root.position += Vector3.down * gap;
            return true;
        }

        // --- embedded in the surface: lift it back out --------------------------------
        //
        // This case did not exist before, and its absence is what left glassware sunk into the
        // bench for the whole session: a negative gap simply failed the range test and the object
        // was left where it was, half inside the table.
        float depth = -gap;
        if (depth <= EmbedTolerance || depth > DefaultMaxLift)
        {
            return false;
        }

        // Only ever climb out of static scene geometry - a bench, a shelf, the floor.
        //
        // Plenty of the lab's equipment is *meant* to interpenetrate: a test tube sits inside its
        // clamp, a stopper inside a flask neck, a balloon stretched over a tube mouth. Those are
        // all grabbables or controls, and pushing a tube up out of the clamp that is holding it
        // would be a far worse bug than the one being fixed here.
        if (surface == null ||
            surface.GetComponentInParent<ObjectGrabbable>() != null ||
            surface.GetComponentInParent<DesktopInteractable>() != null)
        {
            return false;
        }

        root.position += Vector3.up * depth;
        return true;
    }

    /// <summary>Backwards-compatible overload; discards which surface was found.</summary>
    public static bool TryMeasureGap(Transform root, float maxDrop, out float gap)
    {
        Collider surface;
        return TryMeasureGap(root, maxDrop, out gap, out surface);
    }

    /// <summary>
    /// Signed distance between the bottom of the object and the surface it belongs on.
    /// Positive means it is hanging that far above it; negative means it is that far inside it.
    ///
    /// <b>The cast starts above the object, and takes the highest surface rather than the
    /// nearest.</b> Both of those are corrections to a real bug, and it is worth being explicit
    /// about what it was:
    ///
    /// The cast used to start at <c>bounds.center</c> - the middle of the object - and take the
    /// first surface it met. For an object resting correctly that is fine. For an object even
    /// slightly sunk into the bench it is a disaster: the ray starts <i>below</i> the bench top,
    /// so it never sees it, travels on down and hits <b>the floor</b>. The gap came back as the
    /// height of the table, roughly 0.75 m, which is inside the 1.2 m release limit - so letting
    /// go of a piece of glassware that was a centimetre into the bench teleported it to the floor.
    /// That is the "objects fall below the table" report, exactly.
    ///
    /// Starting at <c>bounds.max.y</c> means a surface the object is currently inside is still
    /// found, and taking the highest qualifying surface means the bench always wins over the
    /// floor beneath it.
    /// </summary>
    public static bool TryMeasureGap(Transform root, float maxDrop, out float gap, out Collider surface)
    {
        gap = 0.0f;
        surface = null;

        if (root == null)
        {
            return false;
        }

        Bounds bounds;
        if (!TryGetWorldBounds(root, out bounds))
        {
            return false;
        }

        // From just above the object, down past its own height and the furthest it may be moved.
        Vector3 origin = new Vector3(bounds.center.x, bounds.max.y + CastEpsilon, bounds.center.z);
        float castLength = bounds.size.y + maxDrop + (CastEpsilon * 2.0f);

        RaycastHit[] hits = Physics.RaycastAll(
            origin, Vector3.down, castLength, ~0, QueryTriggerInteraction.Ignore);

        if (hits == null || hits.Length == 0)
        {
            return false;
        }

        float highestY = float.MinValue;
        bool found = false;

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

            // Anything level with or above the object's own top is something sitting on it - a
            // lid, a stopper, a balloon over the mouth - not something it can rest on.
            if (hit.point.y > bounds.max.y - CastEpsilon)
            {
                continue;
            }

            if (hit.point.y > highestY)
            {
                highestY = hit.point.y;
                surface = hit.collider;
                found = true;
            }
        }

        if (!found)
        {
            return false;
        }

        gap = bounds.min.y - highestY;
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
            if (colliders[i] == null || colliders[i].isTrigger || !colliders[i].enabled ||
                !colliders[i].gameObject.activeInHierarchy)
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

        // Hidden geometry must not count. The Bunsen burner's flame is a child of the burner
        // and is switched off until the student lights it; including its particle bounds would
        // measure the burner as reaching up to the top of a flame that is not there.
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null || !renderers[i].enabled ||
                !renderers[i].gameObject.activeInHierarchy)
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
    private readonly List<Transform> scratch = new List<Transform>();
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

        scratch.Clear();
        DesktopObjectSettler.CollectSettleCandidates(scratch);

        for (int i = 0; i < scratch.Count; i++)
        {
            Transform candidate = scratch[i];
            if (candidate == null || !handled.Add(candidate.GetInstanceID()))
            {
                continue;
            }

            DesktopObjectSettler.SettleIfClearBy(candidate, maxDrop, minGapToSettle);
        }
    }
}
