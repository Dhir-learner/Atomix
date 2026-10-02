using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// The phone version of the keyboard and mouse: a joystick, drag-to-look, tap-to-use, and a few
/// buttons. Created only on a mobile device (see <see cref="AtomixInput.IsMobile"/>), so the
/// desktop build never sees any of it.
///
/// Layout follows the usual mobile first-person game:
///   - left thumb: a floating joystick anywhere in the lower left (W A S D), with RUN above it;
///   - right thumb: drag anywhere on the view to look around, tap something to pick it up or use
///     it; AI, BOOK, UP and DOWN sit at the right edge;
///   - while something is held, a small cluster appears for turning, tilting (pouring), resetting
///     and dropping it, and goes away again when it is put down;
///   - everything else - periodic table, graphs, history, labels, retry, settings - lives in the
///     menu behind the button in the top left, so the screen stays clear.
///
/// It feeds <see cref="AtomixInput"/> rather than calling into the game, so each feature still
/// runs through exactly the code its keyboard shortcut does.
/// </summary>
[DefaultExecutionOrder(-10000)]
public class MobileControls : MonoBehaviour
{
    private const string MainMenuSceneName = "MainMenuScene";
    private const string LabSceneName = "LabScene";
    private const string TestingSceneName = "TestingPhaseLab";

    /// <summary>A drag of the full screen height turns this many degrees at sensitivity 1.</summary>
    private const float DegreesPerScreenHeight = 120.0f;

    /// <summary>A touch shorter than this, that barely moved, is a tap rather than a look.</summary>
    private const float MaxTapSeconds = 0.35f;

    /// <summary>How long the AI button must be held before it starts listening.</summary>
    private const float HoldToTalkSeconds = 0.3f;

    /// <summary>
    /// The other on-screen panels were sized for a monitor. On a phone they are enlarged by this
    /// much so their text stays readable on a screen a third of the size.
    /// </summary>
    private const float OverlayTextBoost = 1.3f;

    private static readonly Vector2 ReferenceResolution = new Vector2(1920.0f, 1080.0f);

    // Joystick, in canvas units.
    private const float JoystickRadius = 125.0f;
    private const float JoystickDeadZone = 0.12f;
    private static readonly Vector2 JoystickHome = new Vector2(250.0f, 230.0f);

    private static MobileControls instance;

    private Canvas canvas;
    private CanvasScaler scaler;
    private RectTransform safeRoot;
    private Rect appliedSafeArea;
    private float appliedControlsSize = -1.0f;

    private GameObject hudRoot;
    private GameObject heldCluster;
    private GameObject menuSheet;
    private RectTransform menuGrid;

    private RectTransform joystickBase;
    private RectTransform joystickKnob;
    private Image joystickBaseImage;
    private Image joystickKnobImage;

    private MobileHudButton aiButton;
    private MobileHudButton sprintButton;
    private MobileHudButton bookButton;

    private int joystickFinger = -1;
    private Vector2 joystickOrigin;
    private int lookFinger = -1;
    private float lookStartTime;
    private float lookTravel;

    private bool aiHeld;
    private bool aiTalking;
    private float aiDownTime;

    private FirstPersonController controller;
    private ObjectInteraction interaction;
    private bool sceneHasBook;
    private float nextControllerSearch;
    private float nextCanvasAdapt;

    private readonly HashSet<int> adaptedScalers = new HashSet<int>();
    private readonly List<RaycastResult> uiHits = new List<RaycastResult>();
    private PointerEventData uiProbe;

    private static Sprite circleSprite;
    private static Sprite ringSprite;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (!AtomixInput.IsMobile || instance != null)
        {
            return;
        }

        // Unity otherwise reports the first finger as a mouse click as well, which would make
        // every touch on the screen also count as a crosshair click in the desktop code paths.
        Input.simulateMouseWithTouches = false;
        Input.multiTouchEnabled = true;

        // An experiment involves long stretches of watching a reaction without touching anything.
        Screen.sleepTimeout = SleepTimeout.NeverSleep;

        GameObject host = new GameObject("MobileControls");
        DontDestroyOnLoad(host);
        instance = host.AddComponent<MobileControls>();
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    void Start()
    {
        BuildCanvas();
        BuildHud();
        BuildMenuSheet();
        SetHudVisible(false);
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        controller = null;
        interaction = null;
        nextControllerSearch = 0.0f;
        nextCanvasAdapt = 0.0f;
        sceneHasBook = false;
        CloseMenu();
        AtomixInput.SprintToggled = false;
    }

    void OnApplicationPause(bool paused)
    {
        // Fingers lifted while the app was in the background never send Ended.
        if (paused)
        {
            ResetTouches();
        }
    }

    // =========================================================
    // FRAME
    // =========================================================

    void Update()
    {
        FindPlayer();
        ApplySafeAreaAndScale();
        AdaptOtherCanvases();
        HandleBackButton();

        bool gameplay = IsGameplayVisible();
        SetHudVisible(gameplay);

        if (!gameplay)
        {
            CloseMenu();
        }

        if (!gameplay || menuSheet.activeSelf)
        {
            ResetTouches();
            return;
        }

        RefreshContextualButtons();
        ProcessTouches();
        UpdateAiHold();
    }

    private void FindPlayer()
    {
        if (controller != null || Time.unscaledTime < nextControllerSearch)
        {
            return;
        }

        nextControllerSearch = Time.unscaledTime + 0.5f;

        if (SceneManager.GetActiveScene().name == MainMenuSceneName)
        {
            return;
        }

        controller = FindFirstObjectByType<FirstPersonController>(FindObjectsInactive.Include);
        interaction = controller != null ? controller.GetComponent<ObjectInteraction>() : null;
        sceneHasBook = FindFirstObjectByType<BookCanvasManager>(FindObjectsInactive.Include) != null;
    }

    /// <summary>
    /// The HUD shows only while the player is actually in control: not in the main menu, not
    /// while the pause menu, the exam setup or the results screen has frozen the player, and not
    /// while the on-screen keyboard is up.
    /// </summary>
    private bool IsGameplayVisible()
    {
        return controller != null &&
               controller.isActiveAndEnabled &&
               FirstPersonController.IsCursorLocked &&
               !LabTextInput.IsCapturing;
    }

    private void SetHudVisible(bool visible)
    {
        if (hudRoot != null && hudRoot.activeSelf != visible)
        {
            hudRoot.SetActive(visible);
            if (!visible)
            {
                ResetTouches();
            }
        }
    }

    private void ResetTouches()
    {
        if (joystickFinger >= 0 || lookFinger >= 0)
        {
            joystickFinger = -1;
            lookFinger = -1;
            RecentreJoystick();
        }

        AtomixInput.MoveAxis = Vector2.zero;
    }

    /// <summary>
    /// Android's back button arrives as Escape. It closes this menu, and the pause menu - which
    /// has no Escape handling of its own because on the desktop F1 toggles it.
    /// </summary>
    private void HandleBackButton()
    {
        if (!Input.GetKeyDown(KeyCode.Escape))
        {
            return;
        }

        if (menuSheet != null && menuSheet.activeSelf)
        {
            CloseMenu();
            return;
        }

        PauseMenuUI pause = FindFirstObjectByType<PauseMenuUI>(FindObjectsInactive.Exclude);
        if (pause != null && pause.IsOpen)
        {
            pause.Close();
        }
    }

    // =========================================================
    // TOUCH
    // =========================================================

    private void ProcessTouches()
    {
        if (Input.touchCount == 0)
        {
            // Nothing on the glass: whatever was being tracked has gone, Ended or not.
            ResetTouches();
            return;
        }

        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);

            switch (touch.phase)
            {
                case TouchPhase.Began:
                    BeginTouch(touch);
                    break;

                case TouchPhase.Moved:
                case TouchPhase.Stationary:
                    MoveTouch(touch);
                    break;

                case TouchPhase.Ended:
                case TouchPhase.Canceled:
                    EndTouch(touch);
                    break;
            }
        }
    }

    private void BeginTouch(Touch touch)
    {
        // A button or a panel owns this finger; the UI module is already handling it.
        if (IsOverUi(touch.position))
        {
            return;
        }

        if (joystickFinger < 0 && IsInJoystickZone(touch.position))
        {
            joystickFinger = touch.fingerId;
            StartJoystick(touch.position);
            return;
        }

        if (lookFinger < 0)
        {
            lookFinger = touch.fingerId;
            lookStartTime = Time.unscaledTime;
            lookTravel = 0.0f;
        }
    }

    private void MoveTouch(Touch touch)
    {
        if (touch.fingerId == joystickFinger)
        {
            UpdateJoystick(touch.position);
        }
        else if (touch.fingerId == lookFinger)
        {
            lookTravel += touch.deltaPosition.magnitude;

            float degreesPerPixel = DegreesPerScreenHeight * AtomixSettings.TouchSensitivity /
                                    Mathf.Max(1.0f, Screen.height);
            AtomixInput.AddLook(touch.deltaPosition * degreesPerPixel);
        }
    }

    private void EndTouch(Touch touch)
    {
        if (touch.fingerId == joystickFinger)
        {
            joystickFinger = -1;
            RecentreJoystick();
            AtomixInput.MoveAxis = Vector2.zero;

            // Like most mobile games: stopping ends the sprint, so the next walk starts at a walk.
            SetSprint(false);
            return;
        }

        if (touch.fingerId != lookFinger)
        {
            return;
        }

        lookFinger = -1;

        bool quick = Time.unscaledTime - lookStartTime <= MaxTapSeconds;
        if (touch.phase == TouchPhase.Ended && quick && lookTravel <= TapSlopPixels())
        {
            AtomixInput.PushTap(touch.position);
        }
    }

    /// <summary>About 2.5 mm of finger wobble is still a tap.</summary>
    private static float TapSlopPixels()
    {
        return Screen.dpi > 0.0f ? Screen.dpi * 0.1f : Screen.height * 0.025f;
    }

    private bool IsInJoystickZone(Vector2 screenPosition)
    {
        return screenPosition.x < Screen.width * 0.4f && screenPosition.y < Screen.height * 0.6f;
    }

    private bool IsOverUi(Vector2 screenPosition)
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
        {
            return false;
        }

        // Asked directly rather than through IsPointerOverGameObject: on the frame a finger lands,
        // the event system has not processed it yet, so that would answer for the previous frame.
        if (uiProbe == null || uiProbe.currentInputModule != eventSystem.currentInputModule)
        {
            uiProbe = new PointerEventData(eventSystem);
        }

        uiProbe.Reset();
        uiProbe.position = screenPosition;
        uiHits.Clear();
        eventSystem.RaycastAll(uiProbe, uiHits);

        for (int i = 0; i < uiHits.Count; i++)
        {
            if (uiHits[i].module is GraphicRaycaster)
            {
                return true;
            }
        }
        return false;
    }

    // =========================================================
    // JOYSTICK
    // =========================================================

    private void StartJoystick(Vector2 screenPosition)
    {
        // Floating: the stick centres itself wherever the thumb lands, so it is never "missed".
        Vector2 local = ScreenToSafeLocal(screenPosition);
        Vector2 size = safeRoot.rect.size;
        local.x = Mathf.Clamp(local.x, JoystickRadius, Mathf.Max(JoystickRadius, size.x - JoystickRadius));
        local.y = Mathf.Clamp(local.y, JoystickRadius, Mathf.Max(JoystickRadius, size.y - JoystickRadius));

        joystickBase.anchoredPosition = local;
        joystickOrigin = SafeLocalToScreen(local);
        joystickKnob.anchoredPosition = Vector2.zero;
        SetJoystickActive(true);
        UpdateJoystick(screenPosition);
    }

    private void UpdateJoystick(Vector2 screenPosition)
    {
        float radiusPixels = JoystickRadius * Mathf.Max(0.01f, canvas.scaleFactor);
        Vector2 axis = Vector2.ClampMagnitude((screenPosition - joystickOrigin) / radiusPixels, 1.0f);

        joystickKnob.anchoredPosition = axis * JoystickRadius;

        // Rescale past the dead zone so a small push still gives a slow walk, not nothing then a jump.
        float magnitude = axis.magnitude;
        if (magnitude < JoystickDeadZone)
        {
            AtomixInput.MoveAxis = Vector2.zero;
            return;
        }

        float scaled = (magnitude - JoystickDeadZone) / (1.0f - JoystickDeadZone);
        AtomixInput.MoveAxis = axis / magnitude * scaled;
    }

    private void RecentreJoystick()
    {
        if (joystickBase == null)
        {
            return;
        }

        joystickBase.anchoredPosition = JoystickHome;
        joystickKnob.anchoredPosition = Vector2.zero;
        SetJoystickActive(false);
    }

    private void SetJoystickActive(bool active)
    {
        joystickBaseImage.color = new Color(1.0f, 1.0f, 1.0f, active ? 0.55f : 0.28f);
        joystickKnobImage.color = new Color(1.0f, 1.0f, 1.0f, active ? 0.85f : 0.45f);
    }

    private Vector2 ScreenToSafeLocal(Vector2 screenPosition)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(safeRoot, screenPosition, null, out Vector2 local);
        return local;
    }

    private Vector2 SafeLocalToScreen(Vector2 local)
    {
        return RectTransformUtility.WorldToScreenPoint(null, safeRoot.TransformPoint(local));
    }

    // =========================================================
    // BUTTON BEHAVIOUR
    // =========================================================

    private void RefreshContextualButtons()
    {
        bool holding = interaction != null && interaction.HeldObject != null;
        if (heldCluster.activeSelf != holding)
        {
            heldCluster.SetActive(holding);
        }

        InLabAssistantController assistant = InLabAssistantController.Instance;
        bool assistantHere = assistant != null && assistant.IsAvailableHere;
        if (aiButton.gameObject.activeSelf != assistantHere)
        {
            aiButton.gameObject.SetActive(assistantHere);
        }

        if (assistantHere)
        {
            aiButton.Highlighted = assistant.IsExpanded || assistant.IsListening;
            aiButton.ActiveColour = assistant.IsListening
                ? new Color(0.85f, 0.20f, 0.22f, 0.92f)
                : new Color(0.24f, 0.52f, 0.78f, 0.85f);
        }

        if (bookButton.gameObject.activeSelf != sceneHasBook)
        {
            bookButton.gameObject.SetActive(sceneHasBook);
        }

        sprintButton.Highlighted = AtomixInput.SprintToggled;
    }

    /// <summary>Tap the AI button to open or close the chat; hold it to talk.</summary>
    private void UpdateAiHold()
    {
        if (!aiHeld || aiTalking || Time.unscaledTime - aiDownTime < HoldToTalkSeconds)
        {
            return;
        }

        aiTalking = true;
        AtomixInput.SetHeld(AtomixAction.PushToTalk, true);

        // Open the chat too, so the reply is there to read and not only to hear.
        InLabAssistantController assistant = InLabAssistantController.Instance;
        if (assistant != null && !assistant.IsExpanded)
        {
            AtomixInput.Press(AtomixAction.AssistantToggle);
        }
    }

    private void OnAiPressed()
    {
        aiHeld = true;
        aiTalking = false;
        aiDownTime = Time.unscaledTime;
    }

    private void OnAiReleased()
    {
        aiHeld = false;

        if (aiTalking)
        {
            aiTalking = false;
            AtomixInput.SetHeld(AtomixAction.PushToTalk, false);
            return;
        }

        AtomixInput.Press(AtomixAction.AssistantToggle);
    }

    /// <summary>The AI button was hidden under the finger: stop listening, but do not toggle the panel.</summary>
    private void CancelAiHold()
    {
        aiHeld = false;
        if (aiTalking)
        {
            aiTalking = false;
            AtomixInput.SetHeld(AtomixAction.PushToTalk, false);
        }
    }

    private static void DoNothing()
    {
    }

    private void SetSprint(bool on)
    {
        AtomixInput.SprintToggled = on;
        if (sprintButton != null)
        {
            sprintButton.Highlighted = on;
        }
    }

    // =========================================================
    // MENU
    // =========================================================

    private void OpenMenu()
    {
        ResetTouches();
        AtomixInput.ReleaseAll();
        PopulateMenu();
        menuSheet.SetActive(true);
    }

    private void CloseMenu()
    {
        if (menuSheet != null)
        {
            menuSheet.SetActive(false);
        }
    }

    /// <summary>
    /// Rebuilt on every open, because what belongs in it depends on the scene: the test has a
    /// coin shop and no periodic table, the main lab has graphs and retry, and so on. The scene
    /// lists mirror the panels' own defaults.
    /// </summary>
    private void PopulateMenu()
    {
        LabPanelBuilder.ClearChildren(menuGrid);

        string scene = SceneManager.GetActiveScene().name;
        bool lab = scene == LabSceneName;
        bool testing = scene == TestingSceneName;

        List<KeyValuePair<string, Action>> items = new List<KeyValuePair<string, Action>>();

        if (testing)
        {
            items.Add(Item("Buy a hint", AtomixAction.ExamHint));
            items.Add(Item("Buy +30 seconds", AtomixAction.ExamExtraTime));
            items.Add(Item("Skip this task", AtomixAction.ExamSkip));
        }

        if (!testing)
        {
            items.Add(Item("Periodic table", AtomixAction.PeriodicTable));
            items.Add(Item("Experiment history", AtomixAction.History));
        }

        if (lab)
        {
            items.Add(Item("Scientific graphs", AtomixAction.Graphs));
            items.Add(Item("Retry experiment", AtomixAction.Retry));
            items.Add(Item("Bring video panel here", AtomixAction.RecentrePanel));
        }

        if (lab || testing)
        {
            items.Add(Item("Change measurement label", AtomixAction.CycleLabels));
        }

        items.Add(Item("Settings and controls", AtomixAction.PauseMenu));

        const float buttonWidth = 440.0f;
        const float buttonHeight = 92.0f;
        const float gap = 22.0f;
        int rows = (items.Count + 1) / 2;
        float top = (rows - 1) * (buttonHeight + gap) * 0.5f;

        for (int i = 0; i < items.Count; i++)
        {
            int column = i % 2;
            int row = i / 2;

            // An odd last item sits in the middle rather than hanging off the left column.
            bool alone = i == items.Count - 1 && column == 0;
            float x = alone ? 0.0f : (column == 0 ? -1.0f : 1.0f) * (buttonWidth + gap) * 0.5f;
            float y = top - row * (buttonHeight + gap);

            KeyValuePair<string, Action> item = items[i];
            LabPanelBuilder.CreateButton("Item" + i, menuGrid, item.Key, new Vector2(x, y),
                new Vector2(buttonWidth, buttonHeight), 30.0f, item.Value);
        }
    }

    private KeyValuePair<string, Action> Item(string label, AtomixAction action)
    {
        return new KeyValuePair<string, Action>(label, () =>
        {
            CloseMenu();
            AtomixInput.Press(action);
        });
    }

    // =========================================================
    // PHONE ADAPTATION OF THE OTHER PANELS
    // =========================================================

    private void ApplySafeAreaAndScale()
    {
        Rect safe = Screen.safeArea;
        if (safe != appliedSafeArea && Screen.width > 0 && Screen.height > 0)
        {
            // Keeps the buttons out from under a notch or a rounded corner.
            appliedSafeArea = safe;
            safeRoot.anchorMin = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
            safeRoot.anchorMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
            safeRoot.offsetMin = Vector2.zero;
            safeRoot.offsetMax = Vector2.zero;
        }

        float size = AtomixSettings.TouchControlsSize;
        if (!Mathf.Approximately(size, appliedControlsSize))
        {
            appliedControlsSize = size;
            scaler.referenceResolution = ReferenceResolution / size;
        }
    }

    /// <summary>
    /// Every other screen-space panel was laid out for a 16:9 monitor. Phones are wider, which
    /// with the default width matching pushed text off the top and bottom of the screen; and they
    /// are far smaller, which made the corner widgets unreadable. Fit them all to the height, and
    /// enlarge the small ones. Runs periodically because most panels build themselves lazily.
    /// </summary>
    private void AdaptOtherCanvases()
    {
        if (Time.unscaledTime < nextCanvasAdapt)
        {
            return;
        }

        nextCanvasAdapt = Time.unscaledTime + 1.0f;

        CanvasScaler[] scalers = FindObjectsByType<CanvasScaler>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < scalers.Length; i++)
        {
            CanvasScaler other = scalers[i];
            if (other == null || other == scaler || adaptedScalers.Contains(other.GetInstanceID()))
            {
                continue;
            }

            Canvas otherCanvas = other.GetComponent<Canvas>();
            if (otherCanvas == null || !otherCanvas.isRootCanvas ||
                otherCanvas.renderMode != RenderMode.ScreenSpaceOverlay ||
                other.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize)
            {
                continue;
            }

            adaptedScalers.Add(other.GetInstanceID());
            other.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            other.matchWidthOrHeight = 1.0f;

            if (other.GetComponent<FullScreenPanelMarker>() == null)
            {
                other.referenceResolution /= OverlayTextBoost;
            }
        }
    }

    // =========================================================
    // CONSTRUCTION
    // =========================================================

    private void BuildCanvas()
    {
        GameObject canvasObject = new GameObject("MobileControlsCanvas",
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);

        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        // Above the assistant panel (400) and the exam strip (500), below the pause menu (600).
        canvas.sortingOrder = 550;

        scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = ReferenceResolution;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 1.0f;

        GameObject safe = new GameObject("SafeArea", typeof(RectTransform));
        safe.transform.SetParent(canvasObject.transform, false);
        safeRoot = safe.GetComponent<RectTransform>();
        safeRoot.pivot = Vector2.zero;
        safeRoot.anchorMin = Vector2.zero;
        safeRoot.anchorMax = Vector2.one;
        safeRoot.offsetMin = Vector2.zero;
        safeRoot.offsetMax = Vector2.zero;
    }

    private void BuildHud()
    {
        hudRoot = new GameObject("Hud", typeof(RectTransform));
        hudRoot.transform.SetParent(safeRoot, false);
        Stretch(hudRoot.GetComponent<RectTransform>());

        // --- left thumb -------------------------------------------------------------
        joystickBase = CreateImage("JoystickBase", hudRoot.transform, RingSprite(),
            new Vector2(0.0f, 0.0f), JoystickHome, Vector2.one * JoystickRadius * 2.0f, out joystickBaseImage);
        joystickKnob = CreateImage("JoystickKnob", joystickBase, CircleSprite(),
            new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * 120.0f, out joystickKnobImage);
        joystickBaseImage.raycastTarget = false;
        joystickKnobImage.raycastTarget = false;
        SetJoystickActive(false);

        sprintButton = CreateRoundButton("Run", hudRoot.transform, new Vector2(0.0f, 0.0f),
            new Vector2(110.0f, 480.0f), 104.0f, "RUN", 26.0f);
        sprintButton.Pressed = () => SetSprint(!AtomixInput.SprintToggled);

        MobileHudButton menuButton = CreateRoundButton("Menu", hudRoot.transform, new Vector2(0.0f, 1.0f),
            new Vector2(76.0f, -76.0f), 100.0f, string.Empty, 26.0f);
        AddMenuGlyph(menuButton.transform);
        menuButton.Released = OpenMenu;
        menuButton.Cancelled = DoNothing;

        // --- right thumb ------------------------------------------------------------
        Vector2 bottomRight = new Vector2(1.0f, 0.0f);

        aiButton = CreateRoundButton("Assistant", hudRoot.transform, bottomRight,
            new Vector2(-150.0f, 160.0f), 150.0f, "AI", 44.0f);
        aiButton.Pressed = OnAiPressed;
        aiButton.Released = OnAiReleased;
        aiButton.Cancelled = CancelAiHold;

        bookButton = CreateRoundButton("Book", hudRoot.transform, bottomRight,
            new Vector2(-345.0f, 110.0f), 116.0f, "BOOK", 26.0f);
        bookButton.Released = () => AtomixInput.Press(AtomixAction.Book);
        bookButton.Cancelled = DoNothing;

        MobileHudButton flyUp = CreateRoundButton("FlyUp", hudRoot.transform, bottomRight,
            new Vector2(-110.0f, 440.0f), 104.0f, "UP", 28.0f);
        BindHold(flyUp, AtomixAction.FlyUp);

        MobileHudButton flyDown = CreateRoundButton("FlyDown", hudRoot.transform, bottomRight,
            new Vector2(-110.0f, 316.0f), 104.0f, "DOWN", 24.0f);
        BindHold(flyDown, AtomixAction.FlyDown);

        // --- while holding something ------------------------------------------------
        heldCluster = new GameObject("HeldObjectControls", typeof(RectTransform));
        heldCluster.transform.SetParent(hudRoot.transform, false);
        Stretch(heldCluster.GetComponent<RectTransform>());

        BindHold(CreatePillButton("TurnLeft", heldCluster.transform, new Vector2(-575.0f, 510.0f), "TURN L"), AtomixAction.TurnLeft);
        BindHold(CreatePillButton("TurnRight", heldCluster.transform, new Vector2(-440.0f, 510.0f), "TURN R"), AtomixAction.TurnRight);
        BindHold(CreatePillButton("TiltLeft", heldCluster.transform, new Vector2(-575.0f, 400.0f), "TILT L"), AtomixAction.TiltLeft);
        BindHold(CreatePillButton("TiltRight", heldCluster.transform, new Vector2(-440.0f, 400.0f), "TILT R"), AtomixAction.TiltRight);

        MobileHudButton reset = CreatePillButton("ResetPose", heldCluster.transform, new Vector2(-575.0f, 290.0f), "RESET");
        reset.Released = () => AtomixInput.Press(AtomixAction.ResetHeldPose);
        reset.Cancelled = DoNothing;

        MobileHudButton drop = CreatePillButton("Drop", heldCluster.transform, new Vector2(-440.0f, 290.0f), "DROP");
        drop.IdleColour = new Color(0.45f, 0.14f, 0.12f, 0.70f);
        drop.ActiveColour = new Color(0.85f, 0.25f, 0.20f, 0.92f);
        drop.Released = () => AtomixInput.Press(AtomixAction.Drop);
        drop.Cancelled = DoNothing;

        heldCluster.SetActive(false);
    }

    private void BuildMenuSheet()
    {
        menuSheet = new GameObject("MenuSheet", typeof(RectTransform));
        menuSheet.transform.SetParent(canvas.transform, false);
        Stretch(menuSheet.GetComponent<RectTransform>());

        // The dimmed backdrop catches every touch, so nothing behind the menu can be grabbed by
        // accident, and tapping it closes the menu. A sibling of the panel rather than its
        // parent, so a tap on the panel's empty space does not bubble up to it and close it.
        GameObject backdrop = new GameObject("Backdrop", typeof(RectTransform), typeof(Image), typeof(Button));
        backdrop.transform.SetParent(menuSheet.transform, false);
        Stretch(backdrop.GetComponent<RectTransform>());
        backdrop.GetComponent<Image>().color = new Color(0.0f, 0.0f, 0.0f, 0.72f);
        backdrop.GetComponent<Button>().onClick.AddListener(CloseMenu);

        RectTransform panel = LabPanelBuilder.CreatePlate("Panel", menuSheet.transform, Vector2.zero,
            new Vector2(1000.0f, 820.0f), AtomixSettings.PanelColour);
        panel.GetComponent<Image>().raycastTarget = true;

        LabPanelBuilder.CreateText("Title", panel, new Vector2(0.0f, 350.0f), new Vector2(900.0f, 70.0f),
            "Menu", 44.0f, TextAlignmentOptions.Center, Color.white);

        GameObject grid = new GameObject("Items", typeof(RectTransform));
        grid.transform.SetParent(panel, false);
        menuGrid = grid.GetComponent<RectTransform>();
        menuGrid.anchoredPosition = new Vector2(0.0f, 10.0f);
        menuGrid.sizeDelta = new Vector2(940.0f, 560.0f);

        LabPanelBuilder.CreateButton("Close", panel, "Close", new Vector2(0.0f, -340.0f),
            new Vector2(300.0f, 84.0f), 30.0f, CloseMenu, new Color(0.25f, 0.27f, 0.32f, 1.0f));

        menuSheet.SetActive(false);
    }

    private static void BindHold(MobileHudButton button, AtomixAction action)
    {
        button.Pressed = () => AtomixInput.SetHeld(action, true);
        button.Released = () => AtomixInput.SetHeld(action, false);
    }

    private MobileHudButton CreateRoundButton(string objectName, Transform parent, Vector2 anchor,
                                              Vector2 position, float diameter, string label, float fontSize)
    {
        RectTransform rect = CreateImage(objectName, parent, CircleSprite(), anchor, position,
            Vector2.one * diameter, out Image image);
        image.raycastTarget = true;

        // A thin ring keeps the button legible against both the dark lab and the bright windows.
        CreateImage("Ring", rect, RingSprite(), new Vector2(0.5f, 0.5f), Vector2.zero,
            Vector2.one * diameter, out Image ring);
        ring.color = new Color(1.0f, 1.0f, 1.0f, 0.35f);
        ring.raycastTarget = false;

        if (!string.IsNullOrEmpty(label))
        {
            LabPanelBuilder.CreateText(objectName + "Label", rect, Vector2.zero,
                Vector2.one * diameter, label, fontSize, TextAlignmentOptions.Center, Color.white)
                .fontStyle = FontStyles.Bold;
        }

        MobileHudButton button = rect.gameObject.AddComponent<MobileHudButton>();
        image.color = button.IdleColour;
        return button;
    }

    private MobileHudButton CreatePillButton(string objectName, Transform parent, Vector2 position, string label)
    {
        RectTransform rect = CreateImage(objectName, parent, null, new Vector2(1.0f, 0.0f), position,
            new Vector2(124.0f, 92.0f), out Image image);
        image.raycastTarget = true;

        LabPanelBuilder.CreateText(objectName + "Label", rect, Vector2.zero, new Vector2(124.0f, 92.0f),
            label, 24.0f, TextAlignmentOptions.Center, Color.white).fontStyle = FontStyles.Bold;

        MobileHudButton button = rect.gameObject.AddComponent<MobileHudButton>();
        image.color = button.IdleColour;
        return button;
    }

    /// <summary>Three bars. The default font has no hamburger glyph, so it is drawn.</summary>
    private static void AddMenuGlyph(Transform parent)
    {
        for (int i = -1; i <= 1; i++)
        {
            CreateImage("Bar", parent, null, new Vector2(0.5f, 0.5f), new Vector2(0.0f, i * 16.0f),
                new Vector2(44.0f, 6.0f), out Image bar);
            bar.color = Color.white;
            bar.raycastTarget = false;
        }
    }

    private static RectTransform CreateImage(string objectName, Transform parent, Sprite sprite,
                                             Vector2 anchor, Vector2 position, Vector2 size, out Image image)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        image = go.GetComponent<Image>();
        image.sprite = sprite;
        return rect;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    // =========================================================
    // SPRITES
    // =========================================================

    private static Sprite CircleSprite()
    {
        if (circleSprite == null)
        {
            circleSprite = BuildDisc("AtomixTouchCircle", 0.0f);
        }
        return circleSprite;
    }

    private static Sprite RingSprite()
    {
        if (ringSprite == null)
        {
            ringSprite = BuildDisc("AtomixTouchRing", 0.06f);
        }
        return ringSprite;
    }

    /// <summary>
    /// An anti-aliased disc, or a ring when <paramref name="ringWidth"/> is above zero (as a
    /// fraction of the diameter). Generated rather than imported so the controls need no assets.
    /// </summary>
    private static Sprite BuildDisc(string spriteName, float ringWidth)
    {
        const int size = 256;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = spriteName;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        float radius = size * 0.5f;
        float inner = radius - ringWidth * size;
        Color32[] pixels = new Color32[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x + 0.5f - radius;
                float dy = y + 0.5f - radius;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);

                float alpha = Mathf.Clamp01(radius - 1.0f - distance + 0.5f);
                if (ringWidth > 0.0f)
                {
                    alpha *= Mathf.Clamp01(distance - inner + 0.5f);
                }

                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255.0f));
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100.0f);
    }
}
