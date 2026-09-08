using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Restores mouse and keyboard interaction for the scene's uGUI canvases.
///
/// The scenes were authored for VR and their EventSystem carries an XR UI input module.
/// That module is not available in a desktop build, which leaves the EventSystem with no
/// input module at all, so no pointer or navigation event is ever dispatched. This
/// component adds a StandaloneInputModule at runtime when — and only when — nothing else
/// is driving the EventSystem, so an XR build that supplies its own module is untouched.
///
/// In the lab scenes the module is kept switched off while the cursor is locked for
/// crosshair aiming, so it can never compete with
/// <see cref="ObjectInteraction.TryUiInteraction"/>.
/// </summary>
public class DesktopUIInput : MonoBehaviour
{
    [Tooltip("Menu mode keeps the cursor free and enables keyboard navigation. " +
             "Gameplay mode only lets the mouse drive UI while the cursor is unlocked.")]
    public bool menuMode = true;

    [Tooltip("Select the first interactable control so the keyboard can drive the menu")]
    public bool selectFirstControlInMenu = true;

    [Tooltip("Escape closes an open settings panel before doing anything else")]
    public KeyCode closePanelKey = KeyCode.Escape;

    private EventSystem eventSystem;
    private StandaloneInputModule createdModule;
    private bool selectionInitialised;

    /// <summary>
    /// Creates (or reuses) the desktop UI input support for the active scene.
    /// Never creates a second EventSystem if the scene already has one.
    /// </summary>
    public static DesktopUIInput Ensure(bool menuMode)
    {
        DesktopUIInput existing = FindFirstObjectByType<DesktopUIInput>(FindObjectsInactive.Include);
        if (existing != null)
        {
            existing.menuMode = menuMode;
            return existing;
        }

        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
        {
            eventSystem = FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include);
        }

        GameObject host;
        if (eventSystem != null)
        {
            host = eventSystem.gameObject;
        }
        else
        {
            host = new GameObject("DesktopEventSystem");
            host.AddComponent<EventSystem>();
        }

        DesktopUIInput input = host.AddComponent<DesktopUIInput>();
        input.menuMode = menuMode;
        return input;
    }

    void Awake()
    {
        eventSystem = GetComponent<EventSystem>();
        if (eventSystem == null)
        {
            eventSystem = gameObject.AddComponent<EventSystem>();
        }

        EnsureInputModule();

        // Only the menu needs its canvases repaired. The lab scenes drive their UI
        // through ObjectInteraction's crosshair raycast, and adding a GraphicRaycaster
        // to a canvas that deliberately has none would start swallowing grabs.
        if (menuMode)
        {
            RepairCanvases();
        }
    }

    void Start()
    {
        ApplyMode();
    }

    void Update()
    {
        // A question is being typed into the assistant panel; every letter belongs to it.
        if (LabTextInput.IsCapturing)
        {
            return;
        }

        ApplyMode();

        if (!menuMode)
        {
            return;
        }

        if (selectFirstControlInMenu && !selectionInitialised)
        {
            SelectFirstControl();
        }

        if (Input.GetKeyDown(closePanelKey))
        {
            TryCloseOpenPanel();
        }
    }

    // ------------------------------------------------------------------

    void EnsureInputModule()
    {
        createdModule = GetComponent<StandaloneInputModule>();
        if (createdModule != null)
        {
            return;
        }

        // An XR build supplies its own module (XRUIInputModule). If any live module is
        // already present we leave the EventSystem exactly as authored. A missing script
        // — which is what the XR module is in a desktop build — is not returned here.
        BaseInputModule[] modules = GetComponents<BaseInputModule>();
        foreach (BaseInputModule module in modules)
        {
            if (module != null)
            {
                return;
            }
        }

        createdModule = gameObject.AddComponent<StandaloneInputModule>();
    }

    void ApplyMode()
    {
        if (eventSystem == null)
        {
            return;
        }

        if (menuMode)
        {
            // The menu is mouse-driven: cursor free, keyboard navigation on.
            if (FirstPersonController.IsCursorLocked)
            {
                FirstPersonController.SetCursorLock(false);
            }

            eventSystem.sendNavigationEvents = true;
            SetModuleEnabled(true);
            return;
        }

        // In the lab the crosshair owns UI clicks while the cursor is locked. Navigation
        // events stay off so movement keys (Space, Enter) can never activate a control.
        bool cursorFree = !FirstPersonController.IsCursorLocked;
        eventSystem.sendNavigationEvents = false;
        SetModuleEnabled(cursorFree);

        if (!cursorFree && eventSystem.currentSelectedGameObject != null)
        {
            eventSystem.SetSelectedGameObject(null);
        }
    }

    void SetModuleEnabled(bool enabledState)
    {
        if (createdModule != null && createdModule.enabled != enabledState)
        {
            createdModule.enabled = enabledState;
        }
    }

    /// <summary>
    /// Makes sure every active canvas can actually be hit by a mouse ray: world-space
    /// canvases need an event camera, and every canvas needs a GraphicRaycaster.
    /// </summary>
    void RepairCanvases()
    {
        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Canvas canvas in canvases)
        {
            // Only the canvases authored into this scene. Runtime panels that live on a
            // DontDestroyOnLoad object build and configure themselves.
            if (canvas == null || !canvas.isRootCanvas || canvas.gameObject.scene != gameObject.scene)
            {
                continue;
            }

            if (canvas.GetComponent<GraphicRaycaster>() == null)
            {
                canvas.gameObject.AddComponent<GraphicRaycaster>();
            }

            if (canvas.renderMode == RenderMode.WorldSpace && canvas.worldCamera == null)
            {
                // GraphicRaycaster falls back to Camera.main for world-space canvases,
                // but assigning it explicitly keeps the fallback from silently breaking
                // if the tag ever changes.
                canvas.worldCamera = Camera.main;
            }
        }
    }

    void SelectFirstControl()
    {
        if (eventSystem == null)
        {
            return;
        }

        if (eventSystem.currentSelectedGameObject != null)
        {
            selectionInitialised = true;
            return;
        }

        Selectable first = null;
        Selectable[] selectables = FindObjectsByType<Selectable>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (Selectable selectable in selectables)
        {
            if (selectable == null || !selectable.IsActive() || !selectable.IsInteractable())
            {
                continue;
            }

            if (first == null || selectable.transform.position.y > first.transform.position.y)
            {
                first = selectable;
            }
        }

        if (first != null)
        {
            eventSystem.SetSelectedGameObject(first.gameObject);
            selectionInitialised = true;
        }
    }

    void TryCloseOpenPanel()
    {
        MainMenu[] menus = FindObjectsByType<MainMenu>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (MainMenu menu in menus)
        {
            if (menu == null || menu.settingsPanel == null || !menu.settingsPanel.activeInHierarchy)
            {
                continue;
            }

            menu.CloseSettingsPanel();
            selectionInitialised = false;
            return;
        }
    }
}
