using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shared construction helpers for the runtime world-space panels.
///
/// <see cref="ExperimentHistoryUI"/>, <see cref="ReactionGraphUI"/> and
/// <see cref="ReactionLearningController"/> each grew their own copy of "make a world-space canvas
/// 1.5 m in front of the camera, add a dark plate, add some text, add a button". The panels added
/// here use this instead, so a fourth, fifth and sixth copy did not appear - and so a change to
/// the house style lands everywhere at once.
///
/// Deliberately left as a static helper rather than a base class: the panels have nothing else in
/// common, and Unity components do not inherit comfortably.
/// </summary>
public static class LabPanelBuilder
{
    /// <summary>World-space canvases are built at pixel sizes and scaled down by this.</summary>
    public const float WorldScale = 0.001f;

    public static Color RowColour { get { return new Color(0.13f, 0.15f, 0.20f, 1.0f); } }
    public static Color RowSelectedColour { get { return new Color(0.16f, 0.30f, 0.50f, 1.0f); } }
    public static Color ButtonColour { get { return new Color(0.16f, 0.30f, 0.50f, 1.0f); } }
    public static Color ButtonActiveColour { get { return new Color(0.24f, 0.52f, 0.78f, 1.0f); } }
    public static Color MutedTextColour { get { return new Color(0.62f, 0.68f, 0.78f, 1.0f); } }

    /// <summary>
    /// Builds the canvas + dim backdrop + panel plate every panel starts with.
    /// Returns the panel plate; the canvas is the returned transform's grandparent.
    /// </summary>
    public static RectTransform CreatePanelCanvas(Transform owner, string canvasName,
                                                  Vector2 canvasSize, Vector2 panelSize,
                                                  int sortingOrder, out Canvas canvas)
    {
        GameObject canvasObject = new GameObject(
            canvasName,
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));

        canvasObject.transform.SetParent(owner, false);

        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = Camera.main;
        canvas.sortingOrder = sortingOrder;

        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        canvasRect.sizeDelta = canvasSize;
        canvasRect.localScale = Vector3.one * WorldScale;

        // Dims the lab behind the panel. Must not swallow crosshair clicks meant for the buttons.
        GameObject backdrop = new GameObject("Backdrop", typeof(RectTransform), typeof(Image));
        backdrop.transform.SetParent(canvasObject.transform, false);
        RectTransform backdropRect = backdrop.GetComponent<RectTransform>();
        backdropRect.anchorMin = Vector2.zero;
        backdropRect.anchorMax = Vector2.one;
        backdropRect.offsetMin = Vector2.zero;
        backdropRect.offsetMax = Vector2.zero;
        Image backdropImage = backdrop.GetComponent<Image>();
        backdropImage.color = new Color(0.0f, 0.0f, 0.0f, 0.72f);
        backdropImage.raycastTarget = false;

        GameObject panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(canvasObject.transform, false);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = panelSize;
        panelRect.anchoredPosition = Vector2.zero;
        Image panelImage = panel.GetComponent<Image>();
        panelImage.color = AtomixSettings.PanelColour;
        panelImage.raycastTarget = false;

        return panelRect;
    }

    /// <summary>
    /// Builds a genuine full-screen panel: a Screen Space Overlay canvas that scales with the
    /// window, rather than a world-space plate floating at a fixed distance.
    ///
    /// Used for the pause menu, which is a lot of small controls read head-on. A world-space panel
    /// has to be small enough to fit the field of view at 1.5 m, which left it as a postage stamp
    /// in the middle of the lab. Overlay also renders crisp at any resolution, because the text is
    /// rasterised at screen pixels instead of being scaled down by 0.001 and back up again.
    ///
    /// Returns the panel plate, stretched to the canvas minus <paramref name="margin"/>.
    /// </summary>
    public static RectTransform CreateFullScreenCanvas(Transform owner, string canvasName,
                                                       Vector2 referenceResolution, int sortingOrder,
                                                       float margin, out Canvas canvas,
                                                       out CanvasScaler scaler)
    {
        GameObject canvasObject = new GameObject(
            canvasName,
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));

        canvasObject.transform.SetParent(owner, false);

        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = referenceResolution;
        // Split the difference so the layout survives both 16:9 and wider desktop aspect ratios.
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        scaler.scaleFactor = AtomixSettings.UiScale;

        GameObject backdrop = new GameObject("Backdrop", typeof(RectTransform), typeof(Image));
        backdrop.transform.SetParent(canvasObject.transform, false);
        RectTransform backdropRect = backdrop.GetComponent<RectTransform>();
        backdropRect.anchorMin = Vector2.zero;
        backdropRect.anchorMax = Vector2.one;
        backdropRect.offsetMin = Vector2.zero;
        backdropRect.offsetMax = Vector2.zero;
        Image backdropImage = backdrop.GetComponent<Image>();
        backdropImage.color = new Color(0.0f, 0.0f, 0.0f, 0.80f);
        // Full-screen and opaque: this one DOES absorb clicks, so a stray click behind the menu
        // cannot grab a beaker you cannot see.
        backdropImage.raycastTarget = true;

        GameObject panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(canvasObject.transform, false);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = new Vector2(margin, margin);
        panelRect.offsetMax = new Vector2(-margin, -margin);
        Image panelImage = panel.GetComponent<Image>();
        panelImage.color = AtomixSettings.PanelColour;
        panelImage.raycastTarget = false;

        return panelRect;
    }

    /// <summary>Places a world-space panel squarely in front of the player's eyes.</summary>
    public static void FaceCamera(Canvas canvas, float distance)
    {
        if (canvas == null)
        {
            return;
        }

        Camera camera = Camera.main;
        if (camera == null)
        {
            return;
        }

        canvas.worldCamera = camera;
        canvas.transform.position = camera.transform.position + camera.transform.forward * distance;
        canvas.transform.rotation = camera.transform.rotation;
        canvas.transform.localScale = Vector3.one * WorldScale * AtomixSettings.UiScale;
    }

    public static TMP_Text CreateText(string objectName, Transform parent, Vector2 anchoredPosition,
                                      Vector2 size, string content, float fontSize,
                                      TextAlignmentOptions alignment, Color colour)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPosition;

        TMP_Text text = textObject.GetComponent<TextMeshProUGUI>();
        text.text = content;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = colour;
        text.raycastTarget = false;
        text.richText = true;
        return text;
    }

    /// <summary>
    /// A crosshair-clickable button. The label is a child text with raycasting off, so
    /// <see cref="ObjectInteraction.TryUiInteraction"/> resolves the click to the Button itself.
    /// </summary>
    public static Button CreateButton(string objectName, Transform parent, string label,
                                      Vector2 anchoredPosition, Vector2 size, float fontSize,
                                      Action onClick, Color? colourOverride = null)
    {
        GameObject buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPosition;

        Image image = buttonObject.GetComponent<Image>();
        image.color = colourOverride.HasValue ? colourOverride.Value : ButtonColour;

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        if (onClick != null)
        {
            button.onClick.AddListener(() => onClick());
        }

        CreateText(objectName + "Label", buttonObject.transform, Vector2.zero, size, label,
            fontSize, TextAlignmentOptions.Center, Color.white);

        return button;
    }

    /// <summary>A flat coloured plate, used for rows, headers and separators.</summary>
    public static RectTransform CreatePlate(string objectName, Transform parent,
                                            Vector2 anchoredPosition, Vector2 size, Color colour)
    {
        GameObject plate = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        plate.transform.SetParent(parent, false);

        RectTransform rect = plate.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPosition;

        Image image = plate.GetComponent<Image>();
        image.color = colour;
        image.raycastTarget = false;
        return rect;
    }

    /// <summary>Height of one settings row, and the plate drawn behind it.</summary>
    public const float SettingRowHeight = 48.0f;

    /// <summary>
    /// Only glyphs in the default TextMeshPro atlas may be used in built UI. That atlas covers
    /// Latin-1 and General Punctuation and nothing else, so the arrows and check marks the first
    /// version used (U+25C0, U+25B6, U+2714) rendered as empty boxes on screen. Plain ASCII here,
    /// and a coloured plate instead of a tick, so nothing can turn into tofu.
    /// </summary>
    private const string StepDownGlyph = "-";
    private const string StepUpGlyph = "+";

    /// <summary>
    /// A settings row: backing plate, left-aligned label, right-aligned value, and two nudge
    /// buttons. The label and value sit at fixed offsets from the row edges, so every row in a
    /// column lines up regardless of how long its label is.
    ///
    /// Steppers rather than a uGUI Slider because a Slider needs a pointer <em>drag</em>, and the
    /// crosshair never produces one.
    /// </summary>
    public static TMP_Text CreateStepperRow(Transform parent, string label, Vector2 anchoredPosition,
                                            float rowWidth, float fontSize,
                                            Func<string> readValue, Action stepDown, Action stepUp)
    {
        float half = rowWidth * 0.5f;
        CreatePlate(label + "Row", parent, anchoredPosition,
            new Vector2(rowWidth, SettingRowHeight), RowColour);

        CreateText(label + "Label", parent,
            new Vector2(anchoredPosition.x - half + 300.0f, anchoredPosition.y),
            new Vector2(560.0f, SettingRowHeight), label, fontSize,
            TextAlignmentOptions.Left, AtomixSettings.BodyTextColour);

        TMP_Text value = CreateText(label + "Value", parent,
            new Vector2(anchoredPosition.x + half - 250.0f, anchoredPosition.y),
            new Vector2(240.0f, SettingRowHeight), readValue(), fontSize,
            TextAlignmentOptions.Right, Color.white);

        CreateButton(label + "Down", parent, StepDownGlyph,
            new Vector2(anchoredPosition.x + half - 96.0f, anchoredPosition.y),
            new Vector2(52.0f, SettingRowHeight - 10.0f), fontSize + 4.0f,
            () => { stepDown(); value.text = readValue(); });

        CreateButton(label + "Up", parent, StepUpGlyph,
            new Vector2(anchoredPosition.x + half - 38.0f, anchoredPosition.y),
            new Vector2(52.0f, SettingRowHeight - 10.0f), fontSize + 4.0f,
            () => { stepUp(); value.text = readValue(); });

        return value;
    }

    /// <summary>A settings row whose control is a single ON/OFF button.</summary>
    public static TMP_Text CreateToggleRow(Transform parent, string label, Vector2 anchoredPosition,
                                           float rowWidth, float fontSize,
                                           Func<bool> read, Action<bool> write)
    {
        float half = rowWidth * 0.5f;
        CreatePlate(label + "Row", parent, anchoredPosition,
            new Vector2(rowWidth, SettingRowHeight), RowColour);

        CreateText(label + "Label", parent,
            new Vector2(anchoredPosition.x - half + 300.0f, anchoredPosition.y),
            new Vector2(560.0f, SettingRowHeight), label, fontSize,
            TextAlignmentOptions.Left, AtomixSettings.BodyTextColour);

        Button button = CreateButton(label + "Toggle", parent, read() ? "ON" : "OFF",
            new Vector2(anchoredPosition.x + half - 74.0f, anchoredPosition.y),
            new Vector2(128.0f, SettingRowHeight - 10.0f), fontSize, null);

        TMP_Text state = button.GetComponentInChildren<TMP_Text>();
        Image image = button.GetComponent<Image>();

        button.onClick.AddListener(() =>
        {
            write(!read());
            if (state != null)
            {
                state.text = read() ? "ON" : "OFF";
            }
            if (image != null)
            {
                image.color = read() ? ButtonActiveColour : ButtonColour;
            }
        });

        if (image != null)
        {
            image.color = read() ? ButtonActiveColour : ButtonColour;
        }

        return state;
    }

    /// <summary>A small caps heading that groups the rows beneath it.</summary>
    public static void CreateSectionHeading(Transform parent, string heading,
                                            Vector2 anchoredPosition, float rowWidth, float fontSize)
    {
        CreateText(heading + "Heading", parent,
            new Vector2(anchoredPosition.x - rowWidth * 0.5f + 300.0f, anchoredPosition.y),
            new Vector2(560.0f, 30.0f), heading.ToUpperInvariant(), fontSize,
            TextAlignmentOptions.Left, ButtonActiveColour);
    }

    /// <summary>
    /// A filled or hollow status pip. Used where a check mark would be the obvious choice but is
    /// not available in the font atlas.
    /// </summary>
    public static void CreateStatusPip(Transform parent, Vector2 anchoredPosition, bool filled,
                                       Color filledColour)
    {
        CreatePlate("PipFrame", parent, anchoredPosition, new Vector2(22.0f, 22.0f),
            filled ? filledColour : new Color(0.28f, 0.32f, 0.40f, 1.0f));

        if (!filled)
        {
            // Hollow centre, so an unearned pip reads as an outline rather than a dim block.
            CreatePlate("PipHole", parent, anchoredPosition, new Vector2(14.0f, 14.0f),
                AtomixSettings.PanelColour);
        }
    }

    /// <summary>
    /// Closes every other runtime panel before opening a new one.
    ///
    /// All of them sit at the same depth in front of the camera, so two open at once just stack
    /// and neither is readable. They all live on the one persistent object, so closing the
    /// siblings is enough - and each Close() is idempotent.
    /// </summary>
    public static void CloseOtherPanels(Component opening)
    {
        if (opening == null)
        {
            return;
        }

        GameObject host = opening.gameObject;

        ExperimentHistoryUI history = host.GetComponent<ExperimentHistoryUI>();
        if (history != null && !ReferenceEquals(history, opening))
        {
            history.Close();
        }

        ReactionGraphUI graphs = host.GetComponent<ReactionGraphUI>();
        if (graphs != null && !ReferenceEquals(graphs, opening))
        {
            graphs.Close();
        }

        PeriodicTableUI table = host.GetComponent<PeriodicTableUI>();
        if (table != null && !ReferenceEquals(table, opening))
        {
            table.Close();
        }

        PauseMenuUI pause = host.GetComponent<PauseMenuUI>();
        if (pause != null && !ReferenceEquals(pause, opening))
        {
            pause.Close();
        }

        // The two testing-scene panels belong here too. Both suspend the player while they are
        // up, and two overlapping suspends can hand the controller back in the wrong order and
        // leave the student unable to move.
        ExamSetupUI setup = host.GetComponent<ExamSetupUI>();
        if (setup != null && !ReferenceEquals(setup, opening))
        {
            setup.Close();
        }

        TestResultsUI results = host.GetComponent<TestResultsUI>();
        if (results != null && !ReferenceEquals(results, opening))
        {
            results.Close();
        }
    }

    /// <summary>
    /// Freezes the desktop player while a full-screen panel is up, and reports whether the
    /// controller had been enabled so the caller can put it back exactly as it was.
    /// Disabling the component stops both WASD and mouse-look in one step.
    /// </summary>
    public static bool SuspendPlayer()
    {
        FirstPersonController controller =
            UnityEngine.Object.FindFirstObjectByType<FirstPersonController>(FindObjectsInactive.Exclude);
        if (controller == null || !controller.enabled)
        {
            return false;
        }

        controller.enabled = false;
        return true;
    }

    public static void ResumePlayer(bool wasEnabled)
    {
        if (!wasEnabled)
        {
            return;
        }

        FirstPersonController controller =
            UnityEngine.Object.FindFirstObjectByType<FirstPersonController>(FindObjectsInactive.Include);
        if (controller != null)
        {
            controller.enabled = true;
        }
    }

    /// <summary>Removes every child of a transform immediately, so a rebuild cannot double up.</summary>
    public static void ClearChildren(Transform parent)
    {
        if (parent == null)
        {
            return;
        }

        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            Transform child = parent.GetChild(i);
            // Destroy() is deferred to end of frame, so unparent first or the rebuilt content
            // briefly coexists with the old inside a layout group.
            child.SetParent(null, false);
            UnityEngine.Object.Destroy(child.gameObject);
        }
    }
}
