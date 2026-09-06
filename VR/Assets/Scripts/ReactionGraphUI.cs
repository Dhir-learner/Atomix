using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// The scientific graph panel: a WorldSpace canvas built from code, in the same style as
/// <see cref="ExperimentHistoryUI"/> and ReactionLearningController, so it is clickable with the
/// existing crosshair interaction and needs no scene or prefab edits.
///
/// It opens by itself a few seconds after a successful experiment (see
/// <see cref="ReactionHistoryRecorder.Completed"/>), and can be reopened at any time with the
/// toggle key for the last reaction that was run.
/// </summary>
public class ReactionGraphUI : MonoBehaviour
{
    [Header("Input")]
    [Tooltip("Opens/closes the graphs. G is taken by the grip pose, Tab by the history panel.")]
    public KeyCode toggleKey = KeyCode.F;
    public KeyCode closeKey = KeyCode.Escape;

    [Header("Automatic display")]
    [Tooltip("Show the graphs automatically when an experiment succeeds.")]
    public bool showAfterSuccess = true;

    [Tooltip("Seconds to wait after success. The reaction scripts hold their own completion popup " +
             "for 10 s, so the default lets that close before the graphs cover it.")]
    public float delayAfterSuccess = 11.0f;

    [Header("Placement")]
    [Tooltip("Metres in front of the camera the panel appears.")]
    public float distanceFromCamera = 1.6f;

    [Header("Scenes")]
    [Tooltip("Graphs are only offered in these scenes.")]
    public string[] enabledScenes = { "LabScene" };

    private Canvas graphCanvas;
    private ReactionGraphRenderer graphRenderer;
    private TMP_Text titleText;
    private TMP_Text numbersText;
    private TMP_Text statusText;
    private Button askButton;
    private TMP_Text askLabel;

    private readonly List<Button> tabButtons = new List<Button>();

    private bool isOpen = false;
    private int currentReactionId = -1;
    private ReactionGraphType currentType = ReactionGraphType.EnergyProfile;
    private Coroutine pendingAutoShow;
    private float askCooldownUntil = 0.0f;

    private static readonly Color PanelColor = new Color(0.06f, 0.07f, 0.10f, 0.97f);
    private static readonly Color ButtonColor = new Color(0.16f, 0.30f, 0.50f, 1.0f);
    private static readonly Color ButtonActiveColor = new Color(0.24f, 0.52f, 0.78f, 1.0f);
    private static readonly Color AskColor = new Color(0.18f, 0.46f, 0.32f, 1.0f);
    private static readonly Color NeutralColor = new Color(0.78f, 0.82f, 0.88f, 1.0f);

    private const float CanvasWidth = 1280.0f;
    private const float CanvasHeight = 880.0f;

    private static readonly ReactionGraphType[] TabOrder =
    {
        ReactionGraphType.EnergyProfile,
        ReactionGraphType.EnthalpyLevels,
        ReactionGraphType.Entropy
    };

    private static string TabName(ReactionGraphType type)
    {
        switch (type)
        {
            case ReactionGraphType.EnergyProfile: return "Energy Profile";
            case ReactionGraphType.EnthalpyLevels: return "Exo / Endothermic";
            case ReactionGraphType.Entropy: return "Entropy";
            default: return type.ToString();
        }
    }

    // =========================================================
    // UNITY
    // =========================================================

    void OnEnable()
    {
        ReactionHistoryRecorder.Completed += HandleExperimentCompleted;
        SceneManager.activeSceneChanged += HandleSceneChanged;
    }

    void OnDisable()
    {
        ReactionHistoryRecorder.Completed -= HandleExperimentCompleted;
        SceneManager.activeSceneChanged -= HandleSceneChanged;
    }

    /// <summary>
    /// This panel lives on the DontDestroyOnLoad history object, so without this it would follow
    /// the player into the assistant scene still showing the last graph.
    /// </summary>
    private void HandleSceneChanged(Scene previous, Scene next)
    {
        if (isOpen)
        {
            Close();
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(toggleKey) && IsEnabledScene())
        {
            Toggle();
            return;
        }

        if (!isOpen)
        {
            return;
        }

        if (Input.GetKeyDown(closeKey))
        {
            Close();
            return;
        }

        // The assistant may finish connecting while the panel is already open, so keep the
        // Ask button in step with it rather than only refreshing when the tab changes.
        askRefreshTimer -= Time.unscaledDeltaTime;
        if (askRefreshTimer <= 0.0f)
        {
            askRefreshTimer = 0.5f;
            RefreshAskButton();
        }
    }

    private float askRefreshTimer = 0.0f;

    private bool IsEnabledScene()
    {
        if (enabledScenes == null || enabledScenes.Length == 0)
        {
            return true;
        }

        string active = SceneManager.GetActiveScene().name;
        for (int i = 0; i < enabledScenes.Length; i++)
        {
            if (enabledScenes[i] == active)
            {
                return true;
            }
        }
        return false;
    }

    // =========================================================
    // AUTOMATIC DISPLAY AFTER A SUCCESSFUL EXPERIMENT
    // =========================================================

    private void HandleExperimentCompleted(int reactionId, string reactionName,
                                           ExperimentOutcome outcome)
    {
        if (outcome != ExperimentOutcome.Success)
        {
            return;
        }

        // Remember it either way, so the toggle key can bring the graphs back later.
        currentReactionId = reactionId;

        if (!showAfterSuccess || !IsEnabledScene())
        {
            return;
        }

        if (ReactionGraphCatalog.Get(reactionId) == null)
        {
            return;
        }

        if (pendingAutoShow != null)
        {
            StopCoroutine(pendingAutoShow);
        }
        pendingAutoShow = StartCoroutine(ShowAfterDelay(reactionId));
    }

    private IEnumerator ShowAfterDelay(int reactionId)
    {
        // The reaction scripts show their own completion popup for ~10 s and play a guidance clip;
        // let that finish rather than covering it.
        yield return new WaitForSeconds(Mathf.Max(0.0f, delayAfterSuccess));

        pendingAutoShow = null;

        if (!isOpen && IsEnabledScene())
        {
            Show(reactionId);
        }
    }

    // =========================================================
    // OPEN / CLOSE
    // =========================================================

    public void Toggle()
    {
        if (isOpen)
        {
            Close();
        }
        else
        {
            Show(ResolveReactionId());
        }
    }

    /// <summary>
    /// Which reaction's graphs to show when opened by hand: whatever is running, else the last
    /// experiment that was recorded.
    /// </summary>
    private int ResolveReactionId()
    {
        if (currentReactionId > 0)
        {
            return currentReactionId;
        }

        int live = ExperimentContextProvider.CurrentReactionId;
        return live > 0 ? live : -1;
    }

    /// <summary>Opens the panel on the given reaction.</summary>
    public void Show(int reactionId)
    {
        EnsureUiBuilt();

        ReactionGraphData data = ReactionGraphCatalog.Get(reactionId);
        if (data == null)
        {
            // Nothing to draw. Say so rather than opening an empty panel.
            ShowUnavailable(reactionId);
            return;
        }

        currentReactionId = reactionId;
        isOpen = true;

        graphCanvas.gameObject.SetActive(true);
        graphRenderer.gameObject.SetActive(true);
        PositionInFrontOfCamera();
        SetGraphType(currentType);
    }

    private void ShowUnavailable(int reactionId)
    {
        isOpen = true;
        graphCanvas.gameObject.SetActive(true);
        PositionInFrontOfCamera();

        // Hide the plot rather than leaving the previous reaction's graph on screen.
        graphRenderer.gameObject.SetActive(false);

        titleText.text = reactionId > 0
            ? "No graph data for reaction " + reactionId
            : "No experiment has been completed yet";

        numbersText.text = string.Empty;
        SetStatus("Complete an experiment and its graphs will appear here.");
        RefreshAskButton();
    }

    public void Close()
    {
        isOpen = false;

        if (pendingAutoShow != null)
        {
            StopCoroutine(pendingAutoShow);
            pendingAutoShow = null;
        }

        if (graphCanvas != null)
        {
            graphCanvas.gameObject.SetActive(false);
        }
    }

    private void SetGraphType(ReactionGraphType type)
    {
        currentType = type;

        ReactionGraphData data = ReactionGraphCatalog.Get(currentReactionId);
        if (data == null)
        {
            return;
        }

        titleText.text = string.Format("Reaction {0}  -  {1}\n<size=70%>{2}</size>",
            data.reactionId, data.ResolvedTitle, data.reactionName);

        numbersText.text = data.NumbersLine();
        graphRenderer.Render(data, type);

        for (int i = 0; i < tabButtons.Count && i < TabOrder.Length; i++)
        {
            Image background = tabButtons[i].GetComponent<Image>();
            background.color = TabOrder[i] == type ? ButtonActiveColor : ButtonColor;
        }

        SetStatus(data.SpontaneitySummary());
        RefreshAskButton();
    }

    private void StepGraph(int direction)
    {
        int index = System.Array.IndexOf(TabOrder, currentType);
        if (index < 0)
        {
            index = 0;
        }

        index = (index + direction + TabOrder.Length) % TabOrder.Length;
        SetGraphType(TabOrder[index]);
    }

    // =========================================================
    // ASK THE AI
    // =========================================================

    private void RefreshAskButton()
    {
        if (askButton == null)
        {
            return;
        }

        bool available = InLabAssistantController.Instance != null &&
                         InLabAssistantController.Instance.CanAsk;

        askButton.interactable = available;
        askButton.GetComponent<Image>().color = available
            ? AskColor
            : new Color(AskColor.r * 0.5f, AskColor.g * 0.5f, AskColor.b * 0.5f, 1.0f);

        if (askLabel != null)
        {
            askLabel.color = available ? Color.white : NeutralColor;
        }
    }

    private void AskAiToExplain()
    {
        ReactionGraphData data = ReactionGraphCatalog.Get(currentReactionId);
        if (data == null)
        {
            return;
        }

        // Guard against a double crosshair click firing two requests at the assistant.
        if (Time.unscaledTime < askCooldownUntil)
        {
            return;
        }
        askCooldownUntil = Time.unscaledTime + 2.0f;

        InLabAssistantController assistant = InLabAssistantController.Instance;
        if (assistant == null || !assistant.CanAsk)
        {
            SetStatus("The lab assistant is not connected, so it cannot explain this right now. " +
                      "Check Resources/LabAssistantSettings.");
            RefreshAskButton();
            return;
        }

        string question = string.Format(
            "Can you explain the {0} graph for the {1} reaction I just did?",
            GraphQuestionName(currentType), data.ResolvedTitle);

        bool sent = assistant.AskAssistant(
            question,
            ExperimentContextProvider.BuildGraphContext(data, currentType));

        SetStatus(sent
            ? "Asked the lab assistant - listen for the answer in the assistant panel."
            : "The lab assistant is not available right now.");
    }

    private static string GraphQuestionName(ReactionGraphType type)
    {
        switch (type)
        {
            case ReactionGraphType.EnergyProfile: return "energy versus reaction progress";
            case ReactionGraphType.EnthalpyLevels: return "exothermic and endothermic";
            case ReactionGraphType.Entropy: return "entropy";
            default: return "reaction";
        }
    }

    private void SetStatus(string text)
    {
        if (statusText != null)
        {
            statusText.text = text;
        }
    }

    // =========================================================
    // UI CONSTRUCTION
    // =========================================================

    private void EnsureUiBuilt()
    {
        if (graphCanvas != null)
        {
            return;
        }

        GameObject canvasObject = new GameObject(
            "ReactionGraphCanvas",
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);

        graphCanvas = canvasObject.GetComponent<Canvas>();
        graphCanvas.renderMode = RenderMode.WorldSpace;
        graphCanvas.worldCamera = Camera.main;
        graphCanvas.sortingOrder = 540; // above the history panel

        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(CanvasWidth, CanvasHeight);
        canvasRect.localScale = Vector3.one * 0.001f;

        // Dim behind the panel, without swallowing crosshair clicks meant for the buttons.
        GameObject background = new GameObject("Background", typeof(RectTransform), typeof(Image));
        background.transform.SetParent(canvasObject.transform, false);
        RectTransform backgroundRect = background.GetComponent<RectTransform>();
        backgroundRect.anchorMin = Vector2.zero;
        backgroundRect.anchorMax = Vector2.one;
        backgroundRect.offsetMin = Vector2.zero;
        backgroundRect.offsetMax = Vector2.zero;
        Image backgroundImage = background.GetComponent<Image>();
        backgroundImage.color = new Color(0.0f, 0.0f, 0.0f, 0.75f);
        backgroundImage.raycastTarget = false;

        GameObject panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(canvasObject.transform, false);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.sizeDelta = new Vector2(CanvasWidth - 40.0f, CanvasHeight - 40.0f);
        Image panelImage = panel.GetComponent<Image>();
        panelImage.color = PanelColor;
        panelImage.raycastTarget = false;

        // --- Header --------------------------------------------------------------------
        // Kept clear of the close button on the right: 940 wide centred at -90 ends at x = 380.
        titleText = CreateText("Title", panel.transform,
            new Vector2(-90.0f, 378.0f), new Vector2(940.0f, 60.0f),
            30.0f, FontStyles.Bold, TextAlignmentOptions.Left);

        CreateButton("CloseButton", panel.transform, "Close  [Esc]",
            new Vector2(510.0f, 384.0f), new Vector2(180.0f, 46.0f), Close);

        // --- Tabs ----------------------------------------------------------------------
        BuildTabs(panel.transform);

        // --- Graph ---------------------------------------------------------------------
        graphRenderer = ReactionGraphRenderer.Create(panel.transform, new Vector2(980.0f, 520.0f));
        graphRenderer.GetComponent<RectTransform>().anchoredPosition = new Vector2(0.0f, 32.0f);

        // --- Numbers strip -------------------------------------------------------------
        numbersText = CreateText("Numbers", panel.transform,
            new Vector2(0.0f, -252.0f), new Vector2(1160.0f, 36.0f),
            22.0f, FontStyles.Bold, TextAlignmentOptions.Center);

        statusText = CreateText("Status", panel.transform,
            new Vector2(0.0f, -292.0f), new Vector2(1160.0f, 40.0f),
            19.0f, FontStyles.Normal, TextAlignmentOptions.Center);
        statusText.color = NeutralColor;

        // --- Footer buttons ------------------------------------------------------------
        // Four buttons, 230 + 230 + 280 + 220 wide with 20 between, spanning x -510 to 510.
        CreateButton("PrevButton", panel.transform, "< Previous graph",
            new Vector2(-395.0f, -350.0f), new Vector2(230.0f, 52.0f), () => StepGraph(-1));

        CreateButton("NextButton", panel.transform, "Next graph >",
            new Vector2(-145.0f, -350.0f), new Vector2(230.0f, 52.0f), () => StepGraph(1));

        askButton = CreateButton("AskButton", panel.transform, "Ask AI to Explain",
            new Vector2(130.0f, -350.0f), new Vector2(280.0f, 52.0f), AskAiToExplain);
        askButton.GetComponent<Image>().color = AskColor;
        askLabel = askButton.GetComponentInChildren<TMP_Text>();

        CreateButton("BackButton", panel.transform, "Back to Lab",
            new Vector2(400.0f, -350.0f), new Vector2(220.0f, 52.0f), Close);

        TMP_Text hint = CreateText("Hint", panel.transform,
            new Vector2(0.0f, -396.0f), new Vector2(1160.0f, 28.0f),
            17.0f, FontStyles.Italic, TextAlignmentOptions.Center);
        hint.text = "Press [" + toggleKey + "] any time to bring these graphs back.";
        hint.color = NeutralColor;
    }

    private void BuildTabs(Transform parent)
    {
        tabButtons.Clear();

        const float tabWidth = 300.0f;
        const float spacing = 16.0f;
        float totalWidth = TabOrder.Length * tabWidth + (TabOrder.Length - 1) * spacing;
        float startX = -totalWidth * 0.5f + tabWidth * 0.5f;

        for (int i = 0; i < TabOrder.Length; i++)
        {
            ReactionGraphType type = TabOrder[i];

            Button button = CreateButton("Tab_" + type, parent, TabName(type),
                new Vector2(startX + i * (tabWidth + spacing), 318.0f),
                new Vector2(tabWidth, 50.0f),
                () => SetGraphType(type));

            tabButtons.Add(button);
        }
    }

    private void PositionInFrontOfCamera()
    {
        if (graphCanvas == null)
        {
            return;
        }

        Camera camera = Camera.main;
        if (camera == null)
        {
            return;
        }

        graphCanvas.worldCamera = camera;
        graphCanvas.transform.position =
            camera.transform.position + camera.transform.forward * distanceFromCamera;
        graphCanvas.transform.rotation = camera.transform.rotation;
    }

    private TMP_Text CreateText(string objectName, Transform parent, Vector2 anchoredPosition,
                                Vector2 size, float fontSize, FontStyles fontStyle,
                                TextAlignmentOptions alignment)
    {
        GameObject textObject = new GameObject(objectName,
            typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);

        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0.5f, 0.5f);
        textRect.anchorMax = new Vector2(0.5f, 0.5f);
        textRect.pivot = new Vector2(0.5f, 0.5f);
        textRect.anchoredPosition = anchoredPosition;
        textRect.sizeDelta = size;

        TextMeshProUGUI textComponent = textObject.GetComponent<TextMeshProUGUI>();
        textComponent.fontSize = fontSize;
        textComponent.fontStyle = fontStyle;
        textComponent.alignment = alignment;
        textComponent.color = Color.white;
        textComponent.textWrappingMode = TextWrappingModes.Normal;
        textComponent.raycastTarget = false;

        return textComponent;
    }

    private Button CreateButton(string objectName, Transform parent, string label,
                                Vector2 anchoredPosition, Vector2 size,
                                UnityEngine.Events.UnityAction onClick)
    {
        GameObject buttonObject = new GameObject(objectName,
            typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);

        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
        buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
        buttonRect.pivot = new Vector2(0.5f, 0.5f);
        buttonRect.anchoredPosition = anchoredPosition;
        buttonRect.sizeDelta = size;

        buttonObject.GetComponent<Image>().color = ButtonColor;

        Button button = buttonObject.GetComponent<Button>();
        if (onClick != null)
        {
            button.onClick.AddListener(onClick);
        }

        TMP_Text buttonLabel = CreateText("Label", buttonObject.transform, Vector2.zero,
            size - new Vector2(10.0f, 8.0f), 21.0f, FontStyles.Bold,
            TextAlignmentOptions.Center);
        buttonLabel.text = label;

        return button;
    }
}
