using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// On-screen touch controls for phones and tablets. Every control presses the key or mouse button
/// the desktop scripts already read, through <see cref="LabInput"/>, so gameplay scripts have no
/// separate touch path.
///
/// Touches are hit-tested here rather than through uGUI Buttons: in the lab the EventSystem's input
/// module is switched off during gameplay (see <see cref="DesktopUIInput"/>), so a Button would never
/// hear a tap. Overlay panels that free the "cursor" (pause, test setup, results) hide these
/// controls and take ordinary uGUI taps instead.
/// </summary>
public class MobileControlsUI : MonoBehaviour
{
    private const string MainMenuSceneName = "MainMenuScene";

    [Tooltip("How fast dragging turns the view. Scaled to screen height, so it feels the same on every phone.")]
    public float lookSensitivity = 0.12f;

    [Tooltip("How far the joystick must be pushed, as a fraction of its radius, before walking starts.")]
    public float joystickDeadZone = 0.3f;

    private static readonly Color ControlColour = new Color(0.05f, 0.06f, 0.09f, 0.55f);
    private static readonly Color PressedColour = new Color(0.42f, 0.84f, 1.0f, 0.75f);
    private static readonly Color KnobColour = new Color(1.0f, 1.0f, 1.0f, 0.55f);

    private enum Kind { Tap, Hold, Joystick }

    private class Control
    {
        public RectTransform rect;
        public Image image;
        public TMP_Text label;
        public Kind kind;
        public KeyCode[] keys;
        public Func<bool> visible;
        public int fingerId = -1;
    }

    private static MobileControlsUI instance;

    private readonly List<Control> controls = new List<Control>();
    private Canvas canvas;
    private RectTransform safeRoot;
    private Rect appliedSafeArea;
    private Control joystick;
    private RectTransform joystickKnob;
    private Control useButton;
    private int lookFingerId = -1;

    private ObjectInteraction interaction;
    private bool examScene;
    private float nextSceneScan;

    /// <summary>Creates the controls once, on touch devices only.</summary>
    public static void Ensure()
    {
        if (!LabInput.IsTouch || instance != null)
        {
            return;
        }

        GameObject host = new GameObject("MobileControlsUI");
        DontDestroyOnLoad(host);
        instance = host.AddComponent<MobileControlsUI>();
    }

    void Awake()
    {
        // A tap anywhere would otherwise also read as a left click, and grab whatever the reticle
        // happens to point at while the student is only looking around.
        Input.simulateMouseWithTouches = false;
        Input.multiTouchEnabled = true;

        Build();
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (instance == this)
        {
            instance = null;
        }
    }

    void OnApplicationPause(bool paused)
    {
        if (paused)
        {
            ReleaseEverything();
        }
    }

    void OnApplicationFocus(bool focused)
    {
        if (!focused)
        {
            ReleaseEverything();
        }
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ReleaseEverything();
        interaction = null;
        examScene = false;
        nextSceneScan = 0.0f;
    }

    void Update()
    {
        bool show = SceneManager.GetActiveScene().name != MainMenuSceneName &&
                    FirstPersonController.IsCursorLocked &&
                    !LabTextInput.IsCapturing;

        if (canvas.enabled != show)
        {
            canvas.enabled = show;
            if (!show)
            {
                ReleaseEverything();
            }
        }

        if (!show)
        {
            return;
        }

        ScanSceneIfNeeded();
        ApplySafeArea();

        foreach (Control control in controls)
        {
            bool visible = control.visible == null || control.visible();
            if (control.rect.gameObject.activeSelf != visible)
            {
                control.rect.gameObject.SetActive(visible);
            }
            if (!visible && control.fingerId != -1)
            {
                EndControl(control);
            }
        }

        useButton.label.text = interaction != null && interaction.HeldObject != null ? "DROP" : "USE";

        for (int i = 0; i < Input.touchCount; i++)
        {
            HandleTouch(Input.GetTouch(i));
        }
    }

    // ------------------------------------------------------------------ touches

    void HandleTouch(Touch touch)
    {
        switch (touch.phase)
        {
            case TouchPhase.Began:
                Control hit = HitTest(touch.position);
                if (hit != null)
                {
                    hit.fingerId = touch.fingerId;
                    BeginControl(hit, touch.position);
                }
                else if (lookFingerId == -1)
                {
                    lookFingerId = touch.fingerId;
                }
                break;

            case TouchPhase.Moved:
            case TouchPhase.Stationary:
                if (touch.fingerId == lookFingerId && touch.phase == TouchPhase.Moved)
                {
                    float scale = lookSensitivity * 1080.0f / Mathf.Max(1, Screen.height);
                    LabInput.AddVirtualAxis("Mouse X", touch.deltaPosition.x * scale);
                    LabInput.AddVirtualAxis("Mouse Y", touch.deltaPosition.y * scale);
                }
                else if (joystick.fingerId == touch.fingerId)
                {
                    UpdateJoystick(touch.position);
                }
                break;

            case TouchPhase.Ended:
            case TouchPhase.Canceled:
                if (touch.fingerId == lookFingerId)
                {
                    lookFingerId = -1;
                }
                foreach (Control control in controls)
                {
                    if (control.fingerId == touch.fingerId)
                    {
                        EndControl(control);
                    }
                }
                break;
        }
    }

    Control HitTest(Vector2 screenPoint)
    {
        // Later controls are drawn on top, so test them first.
        for (int i = controls.Count - 1; i >= 0; i--)
        {
            Control control = controls[i];
            if (control.rect.gameObject.activeInHierarchy && control.fingerId == -1 &&
                RectTransformUtility.RectangleContainsScreenPoint(control.rect, screenPoint, null))
            {
                return control;
            }
        }
        return null;
    }

    void BeginControl(Control control, Vector2 screenPoint)
    {
        control.image.color = PressedColour;

        switch (control.kind)
        {
            case Kind.Tap:
                foreach (KeyCode key in control.keys)
                {
                    LabInput.VirtualTap(key);
                }
                break;
            case Kind.Hold:
                foreach (KeyCode key in control.keys)
                {
                    LabInput.VirtualPress(key);
                }
                break;
            case Kind.Joystick:
                UpdateJoystick(screenPoint);
                break;
        }
    }

    void EndControl(Control control)
    {
        control.fingerId = -1;
        control.image.color = ControlColour;

        if (control.kind == Kind.Hold)
        {
            foreach (KeyCode key in control.keys)
            {
                LabInput.VirtualRelease(key);
            }
        }
        else if (control.kind == Kind.Joystick)
        {
            joystickKnob.anchoredPosition = Vector2.zero;
            SetHeld(KeyCode.W, false);
            SetHeld(KeyCode.A, false);
            SetHeld(KeyCode.S, false);
            SetHeld(KeyCode.D, false);
        }
    }

    void UpdateJoystick(Vector2 screenPoint)
    {
        Vector2 local;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(joystick.rect, screenPoint, null, out local))
        {
            return;
        }

        float radius = joystick.rect.rect.width * 0.5f;
        Vector2 offset = Vector2.ClampMagnitude((local - joystick.rect.rect.center) / radius, 1.0f);
        joystickKnob.anchoredPosition = offset * radius;

        // FirstPersonController reads WASD as on/off, so the stick maps onto the same four keys.
        SetHeld(KeyCode.D, offset.x > joystickDeadZone);
        SetHeld(KeyCode.A, offset.x < -joystickDeadZone);
        SetHeld(KeyCode.W, offset.y > joystickDeadZone);
        SetHeld(KeyCode.S, offset.y < -joystickDeadZone);
    }

    static void SetHeld(KeyCode key, bool held)
    {
        if (held)
        {
            LabInput.VirtualPress(key);
        }
        else
        {
            LabInput.VirtualRelease(key);
        }
    }

    void ReleaseEverything()
    {
        lookFingerId = -1;
        foreach (Control control in controls)
        {
            if (control.fingerId != -1)
            {
                EndControl(control);
            }
        }
        LabInput.ReleaseAllVirtual();
    }

    // ------------------------------------------------------------------ scene state

    void ScanSceneIfNeeded()
    {
        if (interaction != null || Time.unscaledTime < nextSceneScan)
        {
            return;
        }

        // DesktopBootstrap adds these after the scene loads, so keep looking briefly.
        interaction = FindAnyObjectByType<ObjectInteraction>();
        examScene = FindAnyObjectByType<ExamCoinHud>() != null;
        nextSceneScan = Time.unscaledTime + 0.5f;
    }

    bool Holding()
    {
        return interaction != null && interaction.HeldObject != null;
    }

    void ApplySafeArea()
    {
        Rect safe = Screen.safeArea;
        if (safe == appliedSafeArea || Screen.width <= 0 || Screen.height <= 0)
        {
            return;
        }

        appliedSafeArea = safe;
        safeRoot.anchorMin = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
        safeRoot.anchorMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
    }

    // ------------------------------------------------------------------ layout

    void Build()
    {
        GameObject canvasObject = new GameObject("MobileControlsCanvas",
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        canvasObject.transform.SetParent(transform, false);

        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 350; // above the lab HUD, below the assistant panel (400) and overlays
        canvas.enabled = false;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920.0f, 1080.0f);
        scaler.matchWidthOrHeight = 1.0f;

        GameObject safeObject = new GameObject("SafeArea", typeof(RectTransform));
        safeObject.transform.SetParent(canvasObject.transform, false);
        safeRoot = safeObject.GetComponent<RectTransform>();
        safeRoot.offsetMin = Vector2.zero;
        safeRoot.offsetMax = Vector2.zero;

        Vector2 topLeft = new Vector2(0.0f, 1.0f);
        Vector2 bottomRight = new Vector2(1.0f, 0.0f);
        Vector2 small = new Vector2(150.0f, 72.0f);
        Func<bool> notExam = () => !examScene;
        Func<bool> onlyExam = () => examScene;
        Func<bool> notHolding = () => !Holding();
        Func<bool> holding = Holding;

        // Top row: panels.
        string[] topLabels = { "Menu", "Book", "History", "Graphs", "Table", "Label", "Retry" };
        KeyCode[] topKeys = { KeyCode.F1, KeyCode.B, KeyCode.Tab, KeyCode.F, KeyCode.P, KeyCode.L, KeyCode.F5 };
        for (int i = 0; i < topLabels.Length; i++)
        {
            Add(topLabels[i], topLeft, new Vector2(20.0f + i * 160.0f, -20.0f), small, Kind.Tap, null, topKeys[i]);
        }

        // Second row: the lab assistant, which the testing scene does not have.
        Add("Ask", topLeft, new Vector2(20.0f, -102.0f), small, Kind.Tap, notExam, KeyCode.Return);
        Add("Why?", topLeft, new Vector2(180.0f, -102.0f), small, Kind.Tap, notExam, KeyCode.Y);
        Add("Chat", topLeft, new Vector2(340.0f, -102.0f), small, Kind.Tap, notExam, KeyCode.M);

        // Second row in the testing scene: the coin shop.
        Add("Hint", topLeft, new Vector2(20.0f, -102.0f), small, Kind.Tap, onlyExam, KeyCode.F2);
        Add("+30s", topLeft, new Vector2(180.0f, -102.0f), small, Kind.Tap, onlyExam, KeyCode.F3);
        Add("Skip", topLeft, new Vector2(340.0f, -102.0f), small, Kind.Tap, onlyExam, KeyCode.F4);

        // Bottom left: walking.
        joystick = Add(string.Empty, Vector2.zero, new Vector2(60.0f, 60.0f), new Vector2(300.0f, 300.0f),
            Kind.Joystick, null);
        joystick.image.color = ControlColour;
        joystickKnob = LabPanelBuilder.CreatePlate("Knob", joystick.rect, Vector2.zero,
            new Vector2(120.0f, 120.0f), KnobColour);

        // Bottom right: acting on the world. USE grabs, activates and drops.
        useButton = Add("USE", bottomRight, new Vector2(-60.0f, 60.0f), new Vector2(220.0f, 220.0f),
            Kind.Tap, null, KeyCode.Mouse0);
        useButton.label.fontSize = 40.0f;

        Vector2 key = new Vector2(110.0f, 100.0f);
        Add("Up", bottomRight, new Vector2(-300.0f, 170.0f), key, Kind.Hold, notHolding, KeyCode.Space);
        Add("Down", bottomRight, new Vector2(-300.0f, 60.0f), key, Kind.Hold, notHolding, KeyCode.LeftControl);

        // While holding glassware: the pose keys. Tilt and roll are held, because how far and how
        // long the vessel is tipped is what sets the pour.
        Add("Reset", bottomRight, new Vector2(-300.0f, 170.0f), key, Kind.Tap, holding, KeyCode.T);
        Add("Roll R", bottomRight, new Vector2(-420.0f, 170.0f), key, Kind.Hold, holding, KeyCode.C);
        Add("Roll L", bottomRight, new Vector2(-420.0f, 60.0f), key, Kind.Hold, holding, KeyCode.C, KeyCode.LeftShift);
        Add("Tilt +", bottomRight, new Vector2(-540.0f, 170.0f), key, Kind.Hold, holding, KeyCode.Z);
        Add("Tilt -", bottomRight, new Vector2(-540.0f, 60.0f), key, Kind.Hold, holding, KeyCode.X);
        Add("Turn R", bottomRight, new Vector2(-660.0f, 170.0f), key, Kind.Hold, holding, KeyCode.E);
        Add("Turn L", bottomRight, new Vector2(-660.0f, 60.0f), key, Kind.Hold, holding, KeyCode.Q);
    }

    Control Add(string text, Vector2 corner, Vector2 position, Vector2 size, Kind kind,
                Func<bool> visible, params KeyCode[] keys)
    {
        RectTransform rect = LabPanelBuilder.CreatePlate(text.Length > 0 ? text : "Joystick", safeRoot,
            Vector2.zero, size, ControlColour);
        rect.anchorMin = corner;
        rect.anchorMax = corner;
        rect.pivot = corner;
        rect.anchoredPosition = position;

        Control control = new Control
        {
            rect = rect,
            image = rect.GetComponent<Image>(),
            kind = kind,
            keys = keys,
            visible = visible
        };

        if (text.Length > 0)
        {
            control.label = LabPanelBuilder.CreateText(text + "Label", rect, Vector2.zero, size, text, 28.0f,
                TextAlignmentOptions.Center, Color.white);
        }

        controls.Add(control);
        return control;
    }
}
