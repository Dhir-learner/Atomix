using System.Collections;
using System.Collections.Generic;
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
    [Tooltip("Radius kept clear of furniture around the assistant when choosing where it stands.")]
    public float clearanceRadius = 0.3f;

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
    private readonly List<Renderer> hiddenRenderers = new List<Renderer>();

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

        // The spot is chosen again once the player's rig and the room boundary exist, and the
        // model stays hidden until then. Coming from the main menu, the character is spawned on
        // activeSceneChanged - which fires before sceneLoaded, so before DesktopBootstrap has
        // reset the camera's XR offset and rotation, and before the room's floor collider has
        // been built. Placing against that camera is what put the assistant inside a table.
        SetModelVisible(false);
        StartCoroutine(SettlePlacement());

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
        StopAllCoroutines();
        hiddenRenderers.Clear();

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

    /// <summary>
    /// Distance and side multipliers tried in order, relative to <see cref="distanceInFront"/> and
    /// <see cref="sideOffset"/>. The authored spot comes first; then the other side; then further
    /// away, since standing behind a bench looks natural; then nearer.
    /// </summary>
    private static readonly float[] CandidateDistances = { 1.0f, 1.35f, 0.8f, 1.7f, 0.65f };
    private static readonly float[] CandidateSides = { 1.0f, -1.0f, 0.0f, 1.8f, -1.8f };

    private void PlaceInFrontOfPlayer()
    {
        if (modelTransform == null)
        {
            return;
        }

        Camera camera = Camera.main;

        Vector3 origin = camera != null ? camera.transform.position : Vector3.zero;
        Vector3 forward = camera != null ? camera.transform.forward : Vector3.forward;
        Vector3 right = camera != null ? camera.transform.right : Vector3.right;

        forward.y = 0.0f;
        right.y = 0.0f;
        forward = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
        right = right.sqrMagnitude > 0.0001f ? right.normalized : Vector3.right;

        float feetY = PlayerFeetY(camera, origin);
        float height = usingOriginalAvatar ? avatarTargetHeight : targetHeight;

        // The original spot is kept as the fallback, so if nothing tests clear the assistant
        // stands exactly where it always did.
        Vector3 position = origin + forward * distanceInFront + right * sideOffset;
        float floorY;
        if (!TryFindFloorAt(position, feetY, out floorY))
        {
            floorY = FallbackFloorY(feetY);
        }

        bool found = false;
        for (int d = 0; d < CandidateDistances.Length && !found; d++)
        {
            for (int side = 0; side < CandidateSides.Length && !found; side++)
            {
                Vector3 candidate = origin +
                                    forward * (distanceInFront * CandidateDistances[d]) +
                                    right * (sideOffset * CandidateSides[side]);

                float candidateFloor;
                if (!TryFindFloorAt(candidate, feetY, out candidateFloor))
                {
                    continue;
                }

                if (IsClearSpot(candidate, candidateFloor, height, origin))
                {
                    position = candidate;
                    floorY = candidateFloor;
                    found = true;
                }
            }
        }

        position.y = floorY + floorOffset + HalfHeight();

        modelTransform.position = position;
        restPosition = position;

        FacePlayerImmediately();
    }

    /// <summary>How far above the player's feet the floor probe starts.</summary>
    private const float FloorProbeLift = 0.25f;

    /// <summary>How far below the player's feet the floor may be found.</summary>
    private const float FloorProbeDepth = 1.0f;

    /// <summary>
    /// Where the player's feet are: the bottom of their CharacterController.
    ///
    /// Measuring from the eyes, as the previous version did, left the assistant standing in
    /// mid-air: whatever that ray met first was not the floor the assistant stands on. Probing at
    /// the assistant's own spot, from just above the feet, does not depend on what is under the
    /// player at all.
    /// </summary>
    private static float PlayerFeetY(Camera camera, Vector3 origin)
    {
        CharacterController body = camera != null ? camera.GetComponent<CharacterController>() : null;
        if (body != null && body.enabled)
        {
            return body.bounds.min.y;
        }

        return origin.y - 1.6f;
    }

    /// <summary>
    /// The floor at <paramref name="spot"/> itself.
    ///
    /// Probed at the spot the assistant will stand on - the original code probed one point and
    /// stood him at another - starting just above the player's feet. That is below every table
    /// top, so a table standing on the spot is never mistaken for the floor; the probe passes
    /// under it, finds the floor, and the clearance capsule then rejects the spot because the
    /// table is in it.
    /// </summary>
    private static bool TryFindFloorAt(Vector3 spot, float feetY, out float floorY)
    {
        floorY = 0.0f;

        Vector3 from = new Vector3(spot.x, feetY + FloorProbeLift, spot.z);
        RaycastHit[] hits = Physics.RaycastAll(from, Vector3.down, FloorProbeLift + FloorProbeDepth,
                                               ~0, QueryTriggerInteraction.Ignore);

        float nearest = float.MaxValue;
        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i].normal.y > 0.65f && hits[i].distance < nearest)
            {
                nearest = hits[i].distance;
                floorY = hits[i].point.y;
            }
        }

        return nearest < float.MaxValue;
    }

    /// <summary>Used only when no floor could be probed anywhere near the player.</summary>
    private static float FallbackFloorY(float feetY)
    {
        LabBoundary boundary = LabBoundary.Active;
        if (boundary != null && boundary.HasInterior)
        {
            return boundary.Interior.min.y;
        }

        return feetY;
    }

    /// <summary>
    /// A spot is usable if a body-sized capsule there touches nothing, it is inside the room, and
    /// the player can see the assistant's head from where they stand.
    /// </summary>
    private bool IsClearSpot(Vector3 spot, float floorY, float height, Vector3 eye)
    {
        float radius = Mathf.Max(0.05f, clearanceRadius);

        // Starts 10 cm up so the floor itself never counts as an obstruction; every table and
        // bench in the lab is far taller than that.
        Vector3 bottom = new Vector3(spot.x, floorY + 0.1f + radius, spot.z);
        Vector3 top = new Vector3(spot.x, floorY + Mathf.Max(height - radius, 0.2f + radius), spot.z);

        if (Physics.CheckCapsule(bottom, top, radius, ~0, QueryTriggerInteraction.Ignore))
        {
            return false;
        }

        LabBoundary boundary = LabBoundary.Active;
        if (boundary != null && boundary.HasInterior)
        {
            Bounds room = boundary.Interior;
            if (spot.x < room.min.x + radius || spot.x > room.max.x - radius ||
                spot.z < room.min.z + radius || spot.z > room.max.z - radius)
            {
                return false;
            }
        }

        Vector3 head = new Vector3(spot.x, floorY + height * 0.85f, spot.z);
        return !Physics.Linecast(eye, head, ~0, QueryTriggerInteraction.Ignore);
    }

    /// <summary>
    /// Waits for the player's rig and the room boundary, then chooses the spot for real.
    /// </summary>
    private IEnumerator SettlePlacement()
    {
        // The rest of this scene's load callbacks, then every Start - including
        // FirstPersonController's and LabBoundary's.
        yield return null;
        yield return null;

        float deadline = Time.unscaledTime + 1.0f;
        while (Time.unscaledTime < deadline && !RigIsReady())
        {
            yield return null;
        }

        if (modelTransform == null)
        {
            yield break;
        }

        PlaceInFrontOfPlayer();
        SetModelVisible(true);
    }

    private static bool RigIsReady()
    {
        Camera camera = Camera.main;
        bool desktopRig = camera != null && camera.GetComponent<FirstPersonController>() != null;
        bool room = LabBoundary.Active != null && LabBoundary.Active.HasInterior;
        return desktopRig && room;
    }

    /// <summary>
    /// Hides or shows the model. Only renderers that were visible when hidden are shown again, so
    /// anything the model's author switched off stays off.
    /// </summary>
    private void SetModelVisible(bool visible)
    {
        if (model == null)
        {
            return;
        }

        if (!visible)
        {
            hiddenRenderers.Clear();
            Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null && renderers[i].enabled)
                {
                    renderers[i].enabled = false;
                    hiddenRenderers.Add(renderers[i]);
                }
            }
            return;
        }

        for (int i = 0; i < hiddenRenderers.Count; i++)
        {
            if (hiddenRenderers[i] != null)
            {
                hiddenRenderers[i].enabled = true;
            }
        }
        hiddenRenderers.Clear();
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
