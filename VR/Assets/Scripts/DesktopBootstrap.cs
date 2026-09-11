using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Adds keyboard and mouse support to the existing XR scenes at runtime.
/// </summary>
public class DesktopBootstrap : MonoBehaviour
{
    private static DesktopBootstrap instance;
    private const BindingFlags FieldFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private const float MinimumDesktopEyeHeight = 1.6f;
    private const string MainMenuSceneName = "MainMenuScene";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        if (instance != null)
        {
            instance.SetupScene(SceneManager.GetActiveScene());
            return;
        }

        GameObject bootstrapObject = new GameObject("DesktopBootstrap");
        DontDestroyOnLoad(bootstrapObject);
        instance = bootstrapObject.AddComponent<DesktopBootstrap>();
        instance.SetupScene(SceneManager.GetActiveScene());
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void Update()
    {
        // A question is being typed into the assistant panel; every letter belongs to it.
        if (LabTextInput.IsCapturing)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.B))
        {
            ToggleBook();
        }
    }

    [Header("Floating object cleanup")]
    [Tooltip("Lower glassware that is authored hanging in mid-air onto the bench below it, once " +
             "per scene load. Turn off to keep the authored positions exactly as they are.")]
    public bool settleFloatingObjectsOnLoad = true;

    [Tooltip("Furthest an object will be lowered on load. Anything higher than this above a " +
             "surface is treated as deliberate - on a shelf, say - and left alone.")]
    public float maxSettleDropOnLoad = 0.75f;

    [Tooltip("An object must be floating by at least this much before it is touched. Kept well " +
             "above zero so correctly placed items - a tube sitting in its clamp - are not nudged.")]
    public float minFloatGapToSettle = 0.06f;

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        SetupScene(scene);
    }

    void SetupScene(Scene scene)
    {
        if (!scene.isLoaded)
        {
            return;
        }

        if (scene.name == MainMenuSceneName)
        {
            SetupMainMenu();
            return;
        }

        FirstPersonController.SetCursorLock(true);
        SetupDesktopPlayer();
        SetupInteractables(scene);
        SetupCrosshairUi(scene);
        SetupUiInput(false);
        SetupLabBoundary(scene);

        // This pass runs before Start(), so any script that resolves its object references there
        // is still holding nulls when we reflect over it. Randomize is the one that matters:
        // it locates all ~30 pieces of testing-scene equipment with GameObject.Find in Start,
        // which is why the CaCO3 and FeSO4 test tubes could never be picked up. Re-scan once the
        // frame has settled so late-bound references become grabbable too.
        StartCoroutine(RescanInteractables(scene));
    }

    /// <summary>
    /// Second and third passes over the scene's interactables. <see cref="EnsureGrabbable"/> and
    /// <see cref="AttachInvokeAction"/> both no-op on anything already set up, so repeating the
    /// scan is safe; it only ever adds what the first pass could not see yet.
    /// </summary>
    IEnumerator RescanInteractables(Scene scene)
    {
        yield return null;                       // Start() has now run
        if (scene.isLoaded)
        {
            SetupInteractables(scene);
        }

        // A second, later pass catches anything bound from a coroutine or a first-Update branch -
        // Randomize hides its equipment on the first Update, and hidden objects are still scanned.
        yield return new WaitForSeconds(0.5f);
        if (scene.isLoaded)
        {
            SetupInteractables(scene);
        }

        // Now that everything is grabbable and Randomize has laid the bench out, put down
        // anything left hanging in mid-air. This has to come last: an object is only settled if
        // it has an ObjectGrabbable, and the passes above are what add them.
        if (scene.isLoaded && settleFloatingObjectsOnLoad)
        {
            DesktopObjectSettler.SettleScene(maxSettleDropOnLoad, minFloatGapToSettle);

            // Most glassware is still hidden at this point - both labs reveal equipment one task
            // at a time - so the watcher keeps settling each piece as it appears.
            DesktopSettleWatcher watcher = GetComponent<DesktopSettleWatcher>();
            if (watcher == null)
            {
                watcher = gameObject.AddComponent<DesktopSettleWatcher>();
            }

            watcher.maxDrop = maxSettleDropOnLoad;
            watcher.minGapToSettle = minFloatGapToSettle;
            watcher.Reset();
        }
    }

    /// <summary>
    /// The main menu is authored for VR ray pointers: its EventSystem carries an XR UI
    /// input module that does not exist in a desktop build, so no click ever reaches the
    /// menu. Give the scene a real mouse cursor and a working input module instead.
    /// </summary>
    void SetupMainMenu()
    {
        FirstPersonController.SetCursorLock(false);
        SetupUiInput(true);

        // The menu has no FirstPersonController, so nothing else would ever grade its camera.
        AttachPostFx(true);
    }

    /// <summary>
    /// Puts the post-processing stack on the player's camera. Any camera rendering to a texture
    /// is skipped - the molecular animation stage owns one, and grading a small off-screen
    /// target would cost a full post stack for nothing anyone can see.
    /// </summary>
    void AttachPostFx(bool isMenuScene)
    {
        Camera camera = Camera.main;
        if (camera == null || camera.targetTexture != null)
        {
            Camera[] cameras = FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            camera = null;
            for (int i = 0; i < cameras.Length; i++)
            {
                if (cameras[i].targetTexture == null)
                {
                    camera = cameras[i];
                    break;
                }
            }
        }

        if (camera != null)
        {
            AtomixPostFx.Attach(camera, isMenuScene);
        }
    }

    void SetupUiInput(bool menuMode)
    {
        DesktopUIInput.Ensure(menuMode);
    }

    void SetupLabBoundary(Scene scene)
    {
        LabBoundary[] existing = FindObjectsByType<LabBoundary>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (LabBoundary boundary in existing)
        {
            if (boundary != null && boundary.gameObject.scene == scene)
            {
                return;
            }
        }

        GameObject boundaryObject = new GameObject("LabBoundary");
        boundaryObject.AddComponent<LabBoundary>();
    }

    void SetupDesktopPlayer()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            Camera[] cameras = FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            // A camera with a targetTexture renders off-screen and is never the player's view -
            // the molecular animation stage owns one. Attaching FirstPersonController to it would
            // put the student inside the RenderTexture instead of the laboratory.
            mainCamera = null;
            for (int i = 0; i < cameras.Length; i++)
            {
                if (cameras[i].targetTexture == null)
                {
                    mainCamera = cameras[i];
                    break;
                }
            }

            if (mainCamera == null)
            {
                return;
            }
        }

        DisableXRBehaviours(mainCamera.gameObject);
        NormalizeDesktopCameraTransform(mainCamera.transform);

        if (mainCamera.GetComponent<FirstPersonController>() == null)
        {
            FirstPersonController controller = mainCamera.gameObject.AddComponent<FirstPersonController>();
            controller.cameraTransform = mainCamera.transform;
        }

        if (mainCamera.GetComponent<ObjectInteraction>() == null)
        {
            mainCamera.gameObject.AddComponent<ObjectInteraction>();
        }

        // Added after the controller on purpose: CameraJuice resolves it in OnEnable.
        if (mainCamera.GetComponent<CameraJuice>() == null)
        {
            mainCamera.gameObject.AddComponent<CameraJuice>();
        }

        AtomixPostFx.Attach(mainCamera, false);

        GameObject xrRig = GameObject.Find("XR Rig");
        if (xrRig != null)
        {
            DisableXRBehaviours(xrRig);
        }
    }

    void NormalizeDesktopCameraTransform(Transform cameraTransform)
    {
        if (cameraTransform == null)
        {
            return;
        }

        Transform parent = cameraTransform.parent;
        if (parent != null && parent.name.IndexOf("Camera Offset", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            // Desktop mode should start from the rig's standing height, not the last tracked HMD offset.
            cameraTransform.localPosition = Vector3.zero;
            cameraTransform.localRotation = Quaternion.identity;
            return;
        }

        if (cameraTransform.position.y < MinimumDesktopEyeHeight)
        {
            Vector3 worldPosition = cameraTransform.position;
            worldPosition.y = MinimumDesktopEyeHeight;
            cameraTransform.position = worldPosition;
        }
    }

    void SetupInteractables(Scene scene)
    {
        HashSet<GameObject> candidateObjects = new HashSet<GameObject>();

        MonoBehaviour[] behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (behaviour == null || behaviour.gameObject.scene != scene)
            {
                continue;
            }

            if (behaviour.GetType().Assembly != typeof(DesktopBootstrap).Assembly)
            {
                continue;
            }

            RegisterGameObjectFields(behaviour, candidateObjects, scene);
        }

        // Actuators are claimed FIRST, before anything is made grabbable.
        //
        // This ordering is load-bearing. EnsureGrabbable skips anything that already carries a
        // DesktopInteractable, but the invoke actions used to be attached *after* this loop - so
        // on the first pass the tap handle, the burner support and the container lids all picked
        // up an ObjectGrabbable, and DesktopSettleWatcher then treated them as loose glassware
        // and lowered them onto the nearest surface.
        //
        // For the tap that was not merely untidy, it turned the water on: RotateButton toggles
        // the flow whenever its handle *stops moving*, and being settled is movement that stops.
        // The result was running water from the moment any lab scene loaded.
        AttachActuators(scene);

        foreach (GameObject candidate in candidateObjects)
        {
            EnsureGrabbable(candidate);
        }
    }

    /// <summary>
    /// Marks every click-to-operate control in the scene. Separated from
    /// <see cref="SetupInteractables"/> so it can run before anything is made grabbable.
    /// </summary>
    void AttachActuators(Scene scene)
    {
        foreach (LightFire lightFire in FindObjectsByType<LightFire>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (lightFire.gameObject.scene == scene)
            {
                AttachInvokeAction(lightFire.burnerSupport, lightFire.ToggleFire);
            }
        }

        foreach (RotateButton rotateButton in FindObjectsByType<RotateButton>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (rotateButton.gameObject.scene == scene)
            {
                AttachInvokeAction(rotateButton.rotatingButton, rotateButton.ToggleWaterFlow);
            }
        }

        foreach (NatriumContainerScriptAnimation container in FindObjectsByType<NatriumContainerScriptAnimation>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (container.gameObject.scene == scene)
            {
                AttachInvokeAction(container.lid, container.TriggerUnscrew);
            }
        }

        foreach (UnscrewPotassiumContainer container in FindObjectsByType<UnscrewPotassiumContainer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (container.gameObject.scene == scene)
            {
                AttachInvokeAction(container.lid, container.TriggerUnscrew);
            }
        }

        foreach (BookCanvasManager manager in FindObjectsByType<BookCanvasManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (manager.gameObject.scene == scene)
            {
                AttachBookInteractables(manager, scene);
            }
        }
    }

    void SetupCrosshairUi(Scene scene)
    {
        DesktopCrosshairUI[] existingCrosshairs = FindObjectsByType<DesktopCrosshairUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (DesktopCrosshairUI crosshair in existingCrosshairs)
        {
            if (crosshair != null && crosshair.gameObject.scene == scene)
            {
                return;
            }
        }

        GameObject crosshairObject = new GameObject("DesktopCrosshairUI");
        crosshairObject.AddComponent<DesktopCrosshairUI>();
    }

    void RegisterGameObjectFields(MonoBehaviour behaviour, HashSet<GameObject> candidateObjects, Scene scene)
    {
        FieldInfo[] fields = behaviour.GetType().GetFields(FieldFlags);
        foreach (FieldInfo field in fields)
        {
            if (field.FieldType != typeof(GameObject))
            {
                continue;
            }

            GameObject fieldObject = field.GetValue(behaviour) as GameObject;
            if (!IsLikelyGameplayObject(fieldObject, scene))
            {
                continue;
            }

            candidateObjects.Add(fieldObject);
        }
    }

    bool IsLikelyGameplayObject(GameObject gameObject, Scene scene)
    {
        if (gameObject == null || gameObject.scene != scene)
        {
            return false;
        }

        if (gameObject.layer == 5)
        {
            return false;
        }

        if (gameObject.GetComponent<Canvas>() != null || gameObject.GetComponent<RectTransform>() != null)
        {
            return false;
        }

        if (gameObject.GetComponentInChildren<Collider>(true) == null)
        {
            return false;
        }

        string name = gameObject.name.ToLowerInvariant();
        string[] excludedKeywords =
        {
            "canvas",
            "button",
            "text",
            "popup",
            "window",
            "eventsystem",
            "camera",
            "explosion",
            "particle",
            "fume",
            "score",
            "audio"
        };

        foreach (string keyword in excludedKeywords)
        {
            if (name.Contains(keyword))
            {
                return false;
            }
        }

        return true;
    }

    void EnsureGrabbable(GameObject target)
    {
        if (target == null)
        {
            return;
        }

        if (target.GetComponent<DesktopInteractable>() != null)
        {
            return;
        }

        ObjectGrabbable grabbable = target.GetComponent<ObjectGrabbable>();
        if (grabbable == null)
        {
            grabbable = target.AddComponent<ObjectGrabbable>();
        }

        if (grabbable.CreatedDesktopRigidbody)
        {
            grabbable.StabilizeForDesktopResting();
        }
    }

    void AttachInvokeAction(GameObject target, Action callback)
    {
        if (target == null || callback == null)
        {
            return;
        }

        DesktopInvokeInteractable interactable = target.GetComponent<DesktopInvokeInteractable>();
        if (interactable == null)
        {
            interactable = target.AddComponent<DesktopInvokeInteractable>();
            interactable.AddListener(() => callback());
        }
    }

    void AttachBookInteractables(BookCanvasManager manager, Scene scene)
    {
        Transform[] transforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Transform sceneTransform in transforms)
        {
            if (sceneTransform == null || sceneTransform.gameObject.scene != scene)
            {
                continue;
            }

            string name = sceneTransform.name.ToLowerInvariant();
            if (!name.Contains("book") || name.Contains("canvas"))
            {
                continue;
            }

            if (sceneTransform.GetComponentInChildren<Collider>(true) == null)
            {
                continue;
            }

            DesktopInvokeInteractable interactable = sceneTransform.gameObject.GetComponent<DesktopInvokeInteractable>();
            if (interactable == null)
            {
                interactable = sceneTransform.gameObject.AddComponent<DesktopInvokeInteractable>();
                interactable.AddListener(manager.openTheBook);
            }
        }
    }

    void ToggleBook()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        BookCanvasManager[] managers = FindObjectsByType<BookCanvasManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (BookCanvasManager manager in managers)
        {
            if (manager == null || manager.gameObject.scene != activeScene || manager.bookCanvas == null)
            {
                continue;
            }

            if (manager.bookCanvas.activeInHierarchy)
            {
                manager.CloseBook();
            }
            else
            {
                manager.openTheBook();
            }

            return;
        }
    }

    void DisableXRBehaviours(GameObject root)
    {
        if (root == null)
        {
            return;
        }

        Behaviour[] behaviours = root.GetComponentsInChildren<Behaviour>(true);
        foreach (Behaviour behaviour in behaviours)
        {
            if (behaviour == null)
            {
                continue;
            }

            string typeName = behaviour.GetType().FullName ?? behaviour.GetType().Name;
            if (typeName.IndexOf("XR", StringComparison.OrdinalIgnoreCase) >= 0 ||
                typeName.IndexOf("TrackedPose", StringComparison.OrdinalIgnoreCase) >= 0 ||
                typeName.IndexOf("ContinuousMove", StringComparison.OrdinalIgnoreCase) >= 0 ||
                typeName.IndexOf("ContinuousTurn", StringComparison.OrdinalIgnoreCase) >= 0 ||
                typeName.IndexOf("Teleport", StringComparison.OrdinalIgnoreCase) >= 0 ||
                typeName.IndexOf("Locomotion", StringComparison.OrdinalIgnoreCase) >= 0 ||
                typeName.IndexOf("InputActionManager", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                behaviour.enabled = false;
            }
        }
    }
}
