using UnityEngine;

/// <summary>
/// The visible lab assistant: a robot that stands in front of the student, turns to face them, and
/// comes to life while the assistant is speaking.
///
/// Spawned from code so no scene edits are needed, and only in the scenes listed on
/// <see cref="InLabAssistantController"/> - the main lab keeps the panel alone.
///
/// The model is Assets/Resources/LabAssistantRobot.fbx, a copy of the robot already used elsewhere
/// in the project. The old Inworld avatar could not be reused: it was a .glb that needed the
/// scripted glTF importer bundled with the Inworld SDK, which no longer exists in the project.
/// </summary>
public class LabAssistantCharacter : MonoBehaviour
{
    /// <summary>Your original assistant avatar, if it has been restored. Tried first.</summary>
    public const string AvatarResourceName = "LabAssistantAvatar";

    /// <summary>Fallback model that is always present in the project.</summary>
    public const string ModelResourceName = "LabAssistantRobot";

    [Header("Placement (relative to where the player starts)")]
    [Tooltip("Metres in front of the player.")]
    public float distanceInFront = 2.2f;
    [Tooltip("Metres to the player's right, so it does not block the walkway.")]
    public float sideOffset = 0.9f;
    [Tooltip("Metres above the floor the model's feet sit.")]
    public float floorOffset = 0.0f;

    [Header("Size")]
    [Tooltip("The model is auto-scaled to this height in metres, whatever its native size is.")]
    public float targetHeight = 1.5f;
    [Tooltip("Height used instead when the original humanoid avatar is found.")]
    public float avatarTargetHeight = 1.7f;

    [Header("Life")]
    [Tooltip("Turn to face the player.")]
    public bool faceThePlayer = true;
    public float turnSpeed = 3.0f;
    [Tooltip("Height of the idle hover bob, in metres.")]
    public float idleBobHeight = 0.03f;
    [Tooltip("Extra bob height while the assistant is talking.")]
    public float talkingBobHeight = 0.07f;
    public float idleBobSpeed = 1.4f;
    public float talkingBobSpeed = 6.0f;

    private GameObject model;
    private Transform modelTransform;
    private Vector3 restPosition;
    private ConvaiAssistantBackend voice;
    private float bobPhase;
    private bool usingOriginalAvatar;
    private Transform voiceAnchor;

    /// <summary>True when the student's own avatar was found rather than the fallback robot.</summary>
    public bool IsOriginalAvatar { get { return usingOriginalAvatar; } }

    /// <summary>
    /// Where the assistant's voice should come from - the head when there is one, so the sound is
    /// not coming out of the character's feet.
    /// </summary>
    public Transform VoiceAnchor
    {
        get { return voiceAnchor != null ? voiceAnchor : modelTransform; }
    }

    public bool HasModel { get { return model != null; } }

    /// <summary>
    /// Builds the character in front of the player. Returns false when the model is missing, so the
    /// caller can carry on with the panel alone rather than failing.
    /// </summary>
    public bool Spawn(ConvaiAssistantBackend backend)
    {
        voice = backend;

        if (model != null)
        {
            return true;
        }

        // Prefer the original avatar when it is in the project, otherwise use the robot. The
        // avatar is a .glb, which only imports once a glTF importer package (com.unity.cloud.gltfast)
        // is installed - the one that used to do it was bundled inside the Inworld SDK.
        GameObject prefab = Resources.Load<GameObject>(AvatarResourceName);
        usingOriginalAvatar = prefab != null;

        if (prefab == null)
        {
            prefab = Resources.Load<GameObject>(ModelResourceName);
        }

        if (prefab == null)
        {
            Debug.LogWarning("[LabAssistant] Neither Resources/" + AvatarResourceName +
                             " nor Resources/" + ModelResourceName + " was found - no character will appear.");
            return false;
        }

        model = Instantiate(prefab);
        model.name = "LabAssistantCharacter";
        modelTransform = model.transform;

        StripColliders();
        FitToTargetHeight();
        PlaceInFrontOfPlayer();

        // The avatar .glb has a rig but no animation clips, so it would stand in a T-pose.
        // This poses the arms down and adds breathing / head tracking procedurally.
        if (usingOriginalAvatar)
        {
            LabAssistantAvatarAnimator animator = model.AddComponent<LabAssistantAvatarAnimator>();
            animator.Initialise(voice);

            Transform[] bones = model.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < bones.Length; i++)
            {
                if (bones[i].name == "Head")
                {
                    voiceAnchor = bones[i];
                    break;
                }
            }
        }

        return true;
    }

    public void Despawn()
    {
        if (model != null)
        {
            Destroy(model);
            model = null;
            modelTransform = null;
        }
    }

    void OnDestroy()
    {
        Despawn();
    }

    /// <summary>An imported FBX can bring colliders that would block the player. Take them out.</summary>
    private void StripColliders()
    {
        Collider[] colliders = model.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Destroy(colliders[i]);
        }
    }

    /// <summary>
    /// Scales by measured render bounds rather than a hard-coded number, because this model is
    /// authored at an odd native size (the copy in LabScene sits at a non-uniform scale of ~12x23x27).
    /// </summary>
    private void FitToTargetHeight()
    {
        Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            return;
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        if (bounds.size.y <= 0.0001f)
        {
            return;
        }

        float wanted = usingOriginalAvatar ? avatarTargetHeight : targetHeight;
        float scale = Mathf.Max(0.01f, wanted) / bounds.size.y;
        modelTransform.localScale = modelTransform.localScale * scale;
    }

    private void PlaceInFrontOfPlayer()
    {
        Camera camera = Camera.main;

        Vector3 origin = camera != null ? camera.transform.position : Vector3.zero;
        Vector3 forward = camera != null ? camera.transform.forward : Vector3.forward;
        Vector3 right = camera != null ? camera.transform.right : Vector3.right;

        forward.y = 0.0f;
        right.y = 0.0f;
        forward = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
        right = right.sqrMagnitude > 0.0001f ? right.normalized : Vector3.right;

        // Drop to the floor beneath the camera, then stand the model on it.
        float floorY = origin.y - 1.6f + floorOffset;
        RaycastHit hit;
        if (Physics.Raycast(origin + forward * distanceInFront + Vector3.up * 0.5f,
                            Vector3.down, out hit, 6.0f))
        {
            floorY = hit.point.y + floorOffset;
        }

        Vector3 position = origin + forward * distanceInFront + right * sideOffset;
        position.y = floorY + HalfHeight();

        modelTransform.position = position;
        restPosition = position;

        FacePlayerImmediately();
    }

    private float HalfHeight()
    {
        // A humanoid glTF avatar is authored with its pivot between the feet, so it needs no lift.
        // The robot's pivot sits at its centre, so it has to be raised by half its height.
        return usingOriginalAvatar ? 0.0f : Mathf.Max(0.01f, targetHeight) * 0.5f;
    }

    private void FacePlayerImmediately()
    {
        Camera camera = Camera.main;
        if (camera == null || modelTransform == null)
        {
            return;
        }

        Vector3 toPlayer = camera.transform.position - modelTransform.position;
        toPlayer.y = 0.0f;
        if (toPlayer.sqrMagnitude > 0.0001f)
        {
            modelTransform.rotation = Quaternion.LookRotation(toPlayer.normalized);
        }
    }

    void Update()
    {
        if (modelTransform == null)
        {
            return;
        }

        bool speaking = voice != null && voice.IsSpeaking;
        bool listening = voice != null && voice.IsRecording;

        // Hover: a calm idle, livelier while it is talking to you. A humanoid avatar gets a much
        // smaller motion - a person who bobs like a drone looks wrong.
        float bobScale = usingOriginalAvatar ? 0.25f : 1.0f;
        float height = (speaking ? talkingBobHeight : idleBobHeight) * bobScale;
        float speed = speaking ? talkingBobSpeed : idleBobSpeed;
        bobPhase += Time.deltaTime * speed;

        Vector3 position = restPosition;
        position.y += Mathf.Sin(bobPhase) * height;
        modelTransform.position = position;

        if (!faceThePlayer)
        {
            return;
        }

        Camera camera = Camera.main;
        if (camera == null)
        {
            return;
        }

        Vector3 toPlayer = camera.transform.position - modelTransform.position;
        toPlayer.y = 0.0f;
        if (toPlayer.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        // Turns towards you a little faster when it is listening, so it reads as paying attention.
        float rate = (listening ? turnSpeed * 2.0f : turnSpeed) * Time.deltaTime;
        modelTransform.rotation = Quaternion.Slerp(modelTransform.rotation,
            Quaternion.LookRotation(toPlayer.normalized), rate);
    }
}
