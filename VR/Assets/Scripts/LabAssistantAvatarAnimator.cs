using UnityEngine;

/// <summary>
/// Gives the humanoid lab assistant a life of its own, without a single animation clip.
///
/// The avatar .glb ships with a skinned Mixamo-style rig but <b>no animations</b> (Inworld used to
/// drive it with its own controllers, which are gone). Imported as-is it would stand in a rigid
/// T-pose. This poses the arms down at Start and then adds breathing, a gentle sway, and a head
/// that follows the player - livelier while the assistant is speaking.
///
/// Bones are matched by name and every lookup is optional: a rig missing a bone simply skips that
/// part rather than breaking.
/// </summary>
public class LabAssistantAvatarAnimator : MonoBehaviour
{
    [Header("Rest pose")]
    [Tooltip("How far the arms come down from the T-pose. 1 = flat against the sides.")]
    [Range(0.0f, 1.0f)] public float armsDown = 0.86f;
    [Tooltip("How far the elbows bend forward.")]
    [Range(0.0f, 1.0f)] public float elbowBend = 0.28f;

    [Header("Idle")]
    public float breathSpeed = 1.1f;
    public float breathAngle = 1.6f;
    public float swaySpeed = 0.55f;
    public float swayAngle = 1.1f;

    [Header("Attention")]
    [Tooltip("Turn the head towards the player.")]
    public bool headTracksPlayer = true;
    [Tooltip("Largest angle the head will turn away from straight ahead.")]
    public float maxHeadTurn = 55.0f;
    public float headTurnSpeed = 4.0f;

    [Header("Speaking")]
    public float talkNodSpeed = 7.0f;
    public float talkNodAngle = 3.5f;

    private ConvaiAssistantBackend voice;

    private Transform head;
    private Transform neck;
    private Transform spine;
    private Transform leftArm;
    private Transform rightArm;
    private Transform leftForeArm;
    private Transform rightForeArm;

    private Quaternion headBase;
    private Quaternion neckBase;
    private Quaternion spineBase;
    private bool posed;

    private float phase;

    public void Initialise(ConvaiAssistantBackend backend)
    {
        voice = backend;

        head = FindBone("Head");
        neck = FindBone("Neck");
        spine = FindBone("Spine1") ?? FindBone("Spine");
        leftArm = FindBone("LeftArm");
        rightArm = FindBone("RightArm");
        leftForeArm = FindBone("LeftForeArm");
        rightForeArm = FindBone("RightForeArm");

        ApplyRestPose();

        if (head != null) headBase = head.localRotation;
        if (neck != null) neckBase = neck.localRotation;
        if (spine != null) spineBase = spine.localRotation;

        posed = true;
    }

    /// <summary>Depth-first search by exact bone name; Transform.Find only looks at direct children.</summary>
    private Transform FindBone(string boneName)
    {
        Transform[] all = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i].name == boneName)
            {
                return all[i];
            }
        }
        return null;
    }

    /// <summary>
    /// Brings the arms down out of the T-pose.
    ///
    /// Done by rotating each bone so that the direction to its child lands on a target direction,
    /// rather than by setting Euler angles. Rigs disagree about which local axis runs along a bone,
    /// so working in world space keeps this correct whatever the export convention was.
    /// </summary>
    private void ApplyRestPose()
    {
        PointBoneAlong(leftArm, leftForeArm, Vector3.down, -transform.right, armsDown * 0.30f);
        PointBoneAlong(rightArm, rightForeArm, Vector3.down, transform.right, armsDown * 0.30f);

        // Elbows: slightly forward and inward so the hands rest near the hips.
        PointBoneAlong(leftForeArm, FindBone("LeftHand"), Vector3.down,
            transform.forward * 0.8f - transform.right * 0.2f, elbowBend);
        PointBoneAlong(rightForeArm, FindBone("RightHand"), Vector3.down,
            transform.forward * 0.8f + transform.right * 0.2f, elbowBend);
    }

    /// <summary>
    /// Rotates <paramref name="bone"/> so the line to <paramref name="child"/> points along
    /// <paramref name="primary"/>, leaned towards <paramref name="lean"/> by <paramref name="leanAmount"/>.
    /// </summary>
    private void PointBoneAlong(Transform bone, Transform child, Vector3 primary, Vector3 lean, float leanAmount)
    {
        if (bone == null || child == null)
        {
            return;
        }

        Vector3 current = child.position - bone.position;
        if (current.sqrMagnitude < 0.000001f)
        {
            return;
        }

        Vector3 target = (primary.normalized + lean.normalized * leanAmount).normalized;
        bone.rotation = Quaternion.FromToRotation(current.normalized, target) * bone.rotation;
    }

    void LateUpdate()
    {
        if (!posed)
        {
            return;
        }

        bool speaking = voice != null && voice.IsSpeaking;
        bool listening = voice != null && voice.IsRecording;

        phase += Time.deltaTime;

        // Breathing: a small, slow rise and fall through the chest.
        if (spine != null)
        {
            float breath = Mathf.Sin(phase * breathSpeed) * breathAngle;
            float sway = Mathf.Sin(phase * swaySpeed) * swayAngle;
            spine.localRotation = spineBase * Quaternion.Euler(breath, sway, 0.0f);
        }

        // A little nod cadence while it is talking, so the reply feels delivered rather than piped in.
        if (neck != null)
        {
            float nod = speaking ? Mathf.Sin(phase * talkNodSpeed) * talkNodAngle : 0.0f;
            neck.localRotation = neckBase * Quaternion.Euler(nod, 0.0f, 0.0f);
        }

        if (head == null || !headTracksPlayer)
        {
            return;
        }

        Camera camera = Camera.main;
        if (camera == null)
        {
            return;
        }

        // Look at the player, but never twist further than a neck actually would.
        Vector3 toPlayer = camera.transform.position - head.position;
        if (toPlayer.sqrMagnitude < 0.0001f)
        {
            return;
        }

        Quaternion straightAhead = head.parent != null ? head.parent.rotation * headBase : headBase;
        Quaternion wanted = Quaternion.LookRotation(toPlayer.normalized, Vector3.up);
        float angle = Quaternion.Angle(straightAhead, wanted);
        if (angle > maxHeadTurn)
        {
            wanted = Quaternion.Slerp(straightAhead, wanted, maxHeadTurn / angle);
        }

        float rate = (listening ? headTurnSpeed * 1.8f : headTurnSpeed) * Time.deltaTime;
        head.rotation = Quaternion.Slerp(head.rotation, wanted, rate);
    }
}
