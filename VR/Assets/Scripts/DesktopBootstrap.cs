using System;
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
        if (Input.GetKeyDown(KeyCode.B))
        {
            ToggleBook();
        }
    }

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

        if (scene.name == "MainMenuScene")
        {
            FirstPersonController.SetCursorLock(false);
            return;
        }

        SetupDesktopPlayer();
        SetupInteractables(scene);
        SetupHelpUi(scene);
        SetupCrosshairUi(scene);
        SetupLabAssistantSubtitleUi(scene);
        SetupLabAssistantPushToTalkUi(scene);
    }

    void SetupDesktopPlayer()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            Camera[] cameras = FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (cameras.Length == 0)
            {
                return;
            }

            mainCamera = cameras[0];
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

        foreach (GameObject candidate in candidateObjects)
        {
            EnsureGrabbable(candidate);
        }

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

    void SetupHelpUi(Scene scene)
    {
        ControlsHelpUI[] existingHelp = FindObjectsByType<ControlsHelpUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (ControlsHelpUI help in existingHelp)
        {
            if (help != null && help.gameObject.scene == scene)
            {
                return;
            }
        }

        GameObject helpObject = new GameObject("DesktopControlsHelpUI");
        helpObject.AddComponent<ControlsHelpUI>();
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

    void SetupLabAssistantSubtitleUi(Scene scene)
    {
        if (scene.name != "LabAssistantScene")
        {
            return;
        }

        LabAssistantSubtitleUI[] existingSubtitleUis = FindObjectsByType<LabAssistantSubtitleUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (LabAssistantSubtitleUI subtitleUi in existingSubtitleUis)
        {
            if (subtitleUi != null && subtitleUi.gameObject.scene == scene)
            {
                return;
            }
        }

        GameObject subtitleObject = new GameObject("LabAssistantSubtitleUI");
        subtitleObject.AddComponent<LabAssistantSubtitleUI>();
    }

    void SetupLabAssistantPushToTalkUi(Scene scene)
    {
        if (scene.name != "LabAssistantScene")
        {
            return;
        }

        LabAssistantPushToTalkUI[] existingPushToTalkUis = FindObjectsByType<LabAssistantPushToTalkUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (LabAssistantPushToTalkUI pushToTalkUi in existingPushToTalkUis)
        {
            if (pushToTalkUi != null && pushToTalkUi.gameObject.scene == scene)
            {
                return;
            }
        }

        GameObject pushToTalkObject = new GameObject("LabAssistantPushToTalkUI");
        pushToTalkObject.AddComponent<LabAssistantPushToTalkUI>();
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
