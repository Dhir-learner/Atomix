using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// Gives the laboratory something of its own to reflect.
///
/// Seventy-two of this project's materials use the built-in Standard shader, which takes its
/// specular reflection from the nearest reflection probe - and neither laboratory scene contains
/// one. With no probe, Standard falls back to <c>RenderSettings</c>'s default reflection, which
/// in both scenes is <c>DefaultReflectionMode.Skybox</c> pointed at Unity's stock procedural sky.
///
/// So every polished surface in a windowless basement laboratory - the glassware, the tap, the
/// steel sink, the burner - was mirroring a clear blue sky with a sun in it. It is the single
/// most obvious "this is untextured Unity" tell in the room, and it is most visible on exactly
/// the objects the student spends the whole game looking at.
///
/// One probe, rendered once when the scene settles, fixes all of it: the glassware picks up the
/// white walls, the dark bench and the warm light of the burner instead.
///
/// <b>Rendered once, not continuously.</b> The room is static - the walls, benches and shelving
/// never move - so a single capture is as correct as a per-frame one and costs one cubemap render
/// for the whole session rather than six faces every frame.
///
/// Built at runtime, like every system added since Task 1, so neither scene file changes and the
/// VR authoring is untouched.
/// </summary>
[DisallowMultipleComponent]
public class LabReflections : MonoBehaviour
{
    [Tooltip("Scenes that get a room reflection probe.")]
    public string[] scenes = { "LabScene", "TestingPhaseLab", "LabAssistantScene" };

    [Tooltip("Cubemap resolution. 128 is plenty for rough indoor specular and costs 128 kB.")]
    public int resolution = 128;

    [Tooltip("Seconds to wait before capturing, so the scene has finished assembling itself. " +
             "Both labs hide equipment on their first Update and reveal it per experiment.")]
    public float captureDelay = 1.25f;

    [Tooltip("Fallback half-extents used when the room cannot be measured, in metres.")]
    public Vector3 fallbackExtents = new Vector3(6.0f, 3.0f, 6.0f);

    private ReflectionProbe probe;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (FindAnyObjectByType<LabReflections>() != null)
        {
            return;
        }

        GameObject host = new GameObject("LabReflections");
        host.AddComponent<LabReflections>();
        DontDestroyOnLoad(host);
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
        StartCoroutine(CaptureWhenSettled(SceneManager.GetActiveScene().name));
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // The old probe belonged to the scene that has just been unloaded.
        probe = null;
        StopAllCoroutines();
        StartCoroutine(CaptureWhenSettled(scene.name));
    }

    private bool IsTargetScene(string sceneName)
    {
        if (scenes == null)
        {
            return false;
        }

        for (int i = 0; i < scenes.Length; i++)
        {
            if (scenes[i] == sceneName)
            {
                return true;
            }
        }
        return false;
    }

    private IEnumerator CaptureWhenSettled(string sceneName)
    {
        if (!IsTargetScene(sceneName))
        {
            yield break;
        }

        yield return new WaitForSeconds(captureDelay);

        Build();
        Capture();
    }

    private void Build()
    {
        if (probe != null)
        {
            return;
        }

        Bounds room = MeasureRoom();

        GameObject probeObject = new GameObject("LabReflectionProbe");
        probeObject.transform.position = room.center;

        probe = probeObject.AddComponent<ReflectionProbe>();
        probe.mode = ReflectionProbeMode.Realtime;

        // ViaScripting, not EveryFrame: the room is static, so one capture is as correct as
        // sixty a second and costs a fraction of a millisecond for the whole session.
        probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
        probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;

        probe.resolution = Mathf.Clamp(Mathf.ClosestPowerOfTwo(resolution), 16, 512);
        probe.size = room.size;
        probe.center = Vector3.zero;

        // Box projection makes the reflection follow the walls as the student walks, instead of
        // behaving like an infinitely distant environment. It is what stops a probe in a small
        // room from looking like a sticker on the glass.
        probe.boxProjection = true;

        probe.clearFlags = ReflectionProbeClearFlags.Skybox;
        probe.intensity = 1.0f;
        probe.importance = 1;

        // Near plane tight enough to see the bench in front of the probe; far plane only as
        // large as the room, so nothing outside the building leaks into the reflection.
        probe.nearClipPlane = 0.08f;
        probe.farClipPlane = Mathf.Max(12.0f, room.size.magnitude * 1.5f);

        // The player's own rig, the HUD and the boundary cage must not appear in a reflection.
        LabBoundary boundary = LabBoundary.Active;
        int boundaryLayer = LayerMask.NameToLayer(
            boundary != null ? boundary.boundaryLayerName : "LabBoundary");
        int cullingMask = ~0;
        cullingMask &= ~(1 << 5);                        // UI
        if (boundaryLayer >= 0)
        {
            cullingMask &= ~(1 << boundaryLayer);
        }
        probe.cullingMask = cullingMask;
    }

    private void Capture()
    {
        if (probe != null)
        {
            probe.RenderProbe();
        }
    }

    /// <summary>
    /// Where the room is. <see cref="LabBoundary"/> has already measured the shell precisely -
    /// including the room's awkward negative parent scale - so reuse that rather than measuring
    /// it a second time with a different answer.
    /// </summary>
    private Bounds MeasureRoom()
    {
        LabBoundary boundary = LabBoundary.Active;
        if (boundary != null && boundary.HasInterior)
        {
            return boundary.Interior;
        }

        // No boundary, or it failed to resolve. Fall back to a box around the player.
        Camera camera = Camera.main;
        Vector3 centre = camera != null ? camera.transform.position : Vector3.zero;
        centre.y = Mathf.Max(centre.y, 1.5f);
        return new Bounds(centre, fallbackExtents * 2.0f);
    }
}
