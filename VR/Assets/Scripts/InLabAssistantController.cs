using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// The lab assistant side panel: an always-available chemistry tutor in the lab scenes.
///
/// Answers come from Convai over its REST API (see <see cref="ConvaiAssistantBackend"/>), so there
/// is no SDK to import and no scene wiring - this controller creates itself before the first scene
/// and builds its own screen-space panel.
///
/// Credentials live in Assets/Resources/LabAssistantSettings.
/// </summary>
public class InLabAssistantController : MonoBehaviour
{
    [Header("Scenes")]
    [Tooltip("Scenes the assistant panel appears in.")]
    public string[] enabledScenes = { "LabScene", "LabAssistantScene" };

    [Tooltip("Scenes the assistant also appears as a visible character in. The main lab is kept " +
             "panel-only on purpose so nothing stands around the benches.")]
    public string[] characterScenes = { "LabAssistantScene" };

    [Header("Input")]
    [Tooltip("Hold to talk.")]
    public KeyCode pushToTalkKey = KeyCode.V;
    [Tooltip("Collapse / expand the side panel.")]
    public KeyCode minimizeKey = KeyCode.M;

    [Header("Panel")]
    public float panelWidth = 380.0f;
    public int maxChatLines = 30;
    [Tooltip("The newest messages always win: older text is dropped once the log exceeds this.")]
    public int maxChatCharacters = 1100;

    [Header("Context")]
    [Tooltip("Brief the assistant on the current experiment before the student speaks.")]
    public bool sendExperimentContext = true;

    // --- Provider ---------------------------------------------------------------------
    private LabAssistantSettings settings;
    private LabAssistantProvider activeProvider = LabAssistantProvider.Convai;
    private ConvaiAssistantBackend convai;
    private LabAssistantCharacter character;
    private string pendingQuestion = string.Empty;

    // --- UI ---------------------------------------------------------------------------
    private Canvas canvas;
    private GameObject expandedRoot;
    private GameObject collapsedRoot;
    private TMP_Text statusText;
    private TMP_Text chatText;
    private TMP_Text footerText;
    private Image micDot;

    private readonly List<string> chatLines = new List<string>();

    // --- State ------------------------------------------------------------------------
    private bool isActiveScene = false;
    private bool minimized = false;
    private bool pushToTalkHeld = false;
    private string lastContextDigest = string.Empty;

    private static readonly Color MicActiveColor = new Color(1.0f, 0.25f, 0.25f, 1.0f);
    private static readonly Color MicIdleColor = new Color(0.28f, 0.12f, 0.12f, 0.9f);
    private static readonly Color PanelColor = new Color(0.05f, 0.06f, 0.09f, 0.90f);
    private static readonly Color AccentColor = new Color(0.42f, 0.84f, 1.0f, 1.0f);
    private static readonly Color MutedColor = new Color(0.72f, 0.78f, 0.86f, 1.0f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (FindAnyObjectByType<InLabAssistantController>() != null)
        {
            return;
        }

        GameObject host = new GameObject("InLabAssistantController");
        host.AddComponent<InLabAssistantController>();
        DontDestroyOnLoad(host);
    }

    void OnEnable()
    {
        SceneManager.activeSceneChanged += HandleActiveSceneChanged;
    }

    void OnDisable()
    {
        SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
        StopPushToTalk();
    }

    void Start()
    {
        ApplyScene(SceneManager.GetActiveScene());
    }

    void HandleActiveSceneChanged(Scene previous, Scene next)
    {
        ApplyScene(next);
    }

    private void ApplyScene(Scene scene)
    {
        isActiveScene = IsEnabledScene(scene.name);

        if (!isActiveScene)
        {
            SetPanelVisible(false);
            StopPushToTalk();
            DespawnCharacter();
            TearDownConvai();
            return;
        }

        EnsureUiBuilt();
        SetPanelVisible(true);
        EnsureAssistantSession();
    }

    private bool IsEnabledScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName) || enabledScenes == null)
        {
            return false;
        }

        for (int i = 0; i < enabledScenes.Length; i++)
        {
            if (enabledScenes[i] == sceneName)
            {
                return true;
            }
        }
        return false;
    }

    void Update()
    {
        if (!isActiveScene)
        {
            return;
        }

        if (Input.GetKeyDown(minimizeKey))
        {
            SetMinimized(!minimized);
        }

        HandlePushToTalkInput();
        RefreshStatusUi();
    }

    // =========================================================
    // SESSION
    // =========================================================

    private void EnsureAssistantSession()
    {
        settings = LabAssistantSettings.Load();
        activeProvider = settings != null ? settings.ResolvedProvider : LabAssistantProvider.Convai;

        if (activeProvider == LabAssistantProvider.None)
        {
            AppendSystemLine("Assistant is turned off in Resources/LabAssistantSettings.");
            return;
        }

        if (convai != null)
        {
            return;
        }

        convai = gameObject.AddComponent<ConvaiAssistantBackend>();
        convai.Initialise(settings);
        convai.UserAsked += HandleUserAsked;
        convai.AssistantReplied += HandleAssistantReply;
        convai.Failed += HandleFailure;

        if (!convai.IsReady)
        {
            AppendSystemLine("Convai has no credentials yet.");
            AppendSystemLine("Add your API key and character ID to Resources/LabAssistantSettings.");
            return;
        }

        EnsureCharacter();
        AppendSystemLine("Lab assistant ready. Hold [" + pushToTalkKey + "] to ask a question.");
    }

    /// <summary>
    /// Gives the assistant a body in the scenes listed in <see cref="characterScenes"/>, and routes
    /// its voice through that body so replies come from the character rather than from nowhere.
    /// </summary>
    private void EnsureCharacter()
    {
        if (character != null || !IsCharacterScene(SceneManager.GetActiveScene().name))
        {
            return;
        }

        character = gameObject.AddComponent<LabAssistantCharacter>();
        if (character.Spawn(convai) && convai != null)
        {
            convai.SetVoiceAnchor(character.VoiceAnchor);
        }
    }

    private void DespawnCharacter()
    {
        if (character == null)
        {
            return;
        }

        if (convai != null)
        {
            convai.SetVoiceAnchor(null);
        }

        character.Despawn();
        Destroy(character);
        character = null;
    }

    private bool IsCharacterScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName) || characterScenes == null)
        {
            return false;
        }

        for (int i = 0; i < characterScenes.Length; i++)
        {
            if (characterScenes[i] == sceneName)
            {
                return true;
            }
        }
        return false;
    }

    private void TearDownConvai()
    {
        DespawnCharacter();

        if (convai == null)
        {
            return;
        }

        convai.UserAsked -= HandleUserAsked;
        convai.AssistantReplied -= HandleAssistantReply;
        convai.Failed -= HandleFailure;
        Destroy(convai);
        convai = null;
        pendingQuestion = string.Empty;
        lastContextDigest = string.Empty;
    }

    private void HandleUserAsked(string text)
    {
        pendingQuestion = text;
        AppendLine("<color=#7ADFFF><b>You:</b></color> " + text);
    }

    private void HandleAssistantReply(string text)
    {
        AppendLine("<color=#86F7A0><b>Assistant:</b></color> " + text);

        ExperimentHistoryManager manager = ExperimentHistoryManager.Instance;
        if (manager != null)
        {
            manager.LogAIInteraction(manager.ActiveAttemptId, pendingQuestion, text);
        }
        pendingQuestion = string.Empty;
    }

    private void HandleFailure(string message)
    {
        AppendSystemLine(message);
    }

    // =========================================================
    // PUSH TO TALK
    // =========================================================

    private void HandlePushToTalkInput()
    {
        if (Input.GetKeyDown(pushToTalkKey))
        {
            StartPushToTalk();
        }
        else if (Input.GetKeyUp(pushToTalkKey))
        {
            StopPushToTalk();
        }
    }

    private void StartPushToTalk()
    {
        if (pushToTalkHeld || convai == null || !convai.IsReady)
        {
            return;
        }

        pushToTalkHeld = true;
        convai.StartListening();
    }

    private void StopPushToTalk()
    {
        if (!pushToTalkHeld)
        {
            return;
        }

        pushToTalkHeld = false;

        if (convai != null)
        {
            convai.StopListeningAndSend(TakeFreshContext());
        }
    }

    private bool IsMicActive()
    {
        return convai != null && convai.IsRecording;
    }

    /// <summary>
    /// Returns the experiment briefing the first time the state changes, and an empty string after
    /// that, so the same numbers are not repeated on every follow-up question.
    /// </summary>
    private string TakeFreshContext()
    {
        if (!sendExperimentContext || !ExperimentContextProvider.HasContext)
        {
            return string.Empty;
        }

        string digest = ExperimentContextProvider.BuildDigest();
        if (digest == lastContextDigest)
        {
            return string.Empty;
        }

        lastContextDigest = digest;
        return ExperimentContextProvider.BuildContext();
    }

    // =========================================================
    // CHAT LOG
    // =========================================================

    private void AppendSystemLine(string text)
    {
        AppendLine("<color=#B9C3D0><i>" + text + "</i></color>");
    }

    private void AppendLine(string line)
    {
        chatLines.Add(line);

        int limit = Mathf.Max(4, maxChatLines);
        while (chatLines.Count > limit)
        {
            chatLines.RemoveAt(0);
        }

        RefreshChatUi();
    }

    /// <summary>
    /// Builds the log from the newest message backwards until the character budget runs out, so
    /// the most recent exchange is always the part that survives.
    /// </summary>
    private void RefreshChatUi()
    {
        if (chatText == null)
        {
            return;
        }

        int budget = Mathf.Max(200, maxChatCharacters);
        List<string> visible = new List<string>();
        int used = 0;

        for (int i = chatLines.Count - 1; i >= 0; i--)
        {
            string line = chatLines[i];
            if (used > 0 && used + line.Length > budget)
            {
                break;
            }
            visible.Insert(0, line);
            used += line.Length + 2;
        }

        chatText.text = string.Join("\n\n", visible.ToArray());
    }

    // =========================================================
    // UI
    // =========================================================

    private void EnsureUiBuilt()
    {
        if (canvas != null)
        {
            return;
        }

        GameObject canvasObject = new GameObject("InLabAssistantCanvas",
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);

        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 400; // under the history panel (520) and learning UI (500)

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920.0f, 1080.0f);
        scaler.matchWidthOrHeight = 1.0f;

        // Nothing in this panel is clickable - the cursor stays locked for the crosshair, so it
        // must never intercept a raycast meant for the lab.
        canvasObject.GetComponent<GraphicRaycaster>().enabled = false;

        BuildExpandedPanel(canvasObject.transform);
        BuildCollapsedTab(canvasObject.transform);
        SetMinimized(false);
    }

    private void BuildExpandedPanel(Transform parent)
    {
        expandedRoot = CreatePanelRoot("AssistantPanel", parent, panelWidth);

        // Title and status hug the top edge; the chat log fills everything between the status
        // block and the footer, so it can never be drawn underneath them.
        TMP_Text title = CreateStretchText("Title", expandedRoot.transform,
            new Vector2(16.0f, -54.0f), new Vector2(-46.0f, -16.0f),
            26.0f, FontStyles.Bold, TextAlignmentOptions.TopLeft, stretchVertically: false);
        title.text = "AI Lab Assistant";
        title.color = AccentColor;

        statusText = CreateStretchText("Status", expandedRoot.transform,
            new Vector2(16.0f, -112.0f), new Vector2(-16.0f, -58.0f),
            17.0f, FontStyles.Normal, TextAlignmentOptions.TopLeft, stretchVertically: false);
        statusText.color = MutedColor;

        // Mic indicator
        GameObject dot = new GameObject("MicDot", typeof(RectTransform), typeof(Image));
        dot.transform.SetParent(expandedRoot.transform, false);
        RectTransform dotRect = dot.GetComponent<RectTransform>();
        dotRect.anchorMin = new Vector2(1.0f, 1.0f);
        dotRect.anchorMax = new Vector2(1.0f, 1.0f);
        dotRect.pivot = new Vector2(1.0f, 1.0f);
        dotRect.anchoredPosition = new Vector2(-18.0f, -22.0f);
        dotRect.sizeDelta = new Vector2(18.0f, 18.0f);
        micDot = dot.GetComponent<Image>();
        micDot.color = MicIdleColor;
        micDot.raycastTarget = false;

        chatText = CreateStretchText("ChatLog", expandedRoot.transform,
            new Vector2(16.0f, 66.0f), new Vector2(-16.0f, -118.0f),
            17.0f, FontStyles.Normal, TextAlignmentOptions.BottomLeft, stretchVertically: true);
        chatText.color = Color.white;
        chatText.overflowMode = TextOverflowModes.Truncate;
        chatText.text = string.Empty;

        footerText = CreateStretchText("Footer", expandedRoot.transform,
            new Vector2(16.0f, 14.0f), new Vector2(-16.0f, 58.0f),
            16.0f, FontStyles.Normal, TextAlignmentOptions.BottomLeft, stretchVertically: false);
        footerText.color = MutedColor;
    }

    private void BuildCollapsedTab(Transform parent)
    {
        collapsedRoot = CreatePanelRoot("AssistantTab", parent, 210.0f);
        RectTransform rect = collapsedRoot.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1.0f, 1.0f);
        rect.anchorMax = new Vector2(1.0f, 1.0f);
        rect.pivot = new Vector2(1.0f, 1.0f);
        rect.anchoredPosition = new Vector2(-16.0f, -16.0f);
        rect.sizeDelta = new Vector2(210.0f, 44.0f);

        TMP_Text label = CreateStretchText("TabLabel", collapsedRoot.transform,
            new Vector2(10.0f, 8.0f), new Vector2(-10.0f, -8.0f),
            17.0f, FontStyles.Bold, TextAlignmentOptions.Center, stretchVertically: true);
        label.text = "Lab Assistant  [" + minimizeKey + "]";
        label.color = AccentColor;
    }

    private GameObject CreatePanelRoot(string objectName, Transform parent, float width)
    {
        GameObject root = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        root.transform.SetParent(parent, false);

        RectTransform rect = root.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1.0f, 0.0f);
        rect.anchorMax = new Vector2(1.0f, 1.0f);
        rect.pivot = new Vector2(1.0f, 0.5f);
        rect.anchoredPosition = new Vector2(-16.0f, 0.0f);
        rect.sizeDelta = new Vector2(width, -120.0f);

        Image image = root.GetComponent<Image>();
        image.color = PanelColor;
        image.raycastTarget = false;

        return root;
    }

    /// <summary>
    /// Creates a label that stretches to the panel width. When <paramref name="stretchVertically"/>
    /// is false the rect is pinned to the top edge, so offsets are measured down from there;
    /// when true it stretches between the given bottom and top insets.
    /// </summary>
    private TMP_Text CreateStretchText(string objectName, Transform parent,
                                       Vector2 offsetMin, Vector2 offsetMax,
                                       float fontSize, FontStyles style,
                                       TextAlignmentOptions alignment, bool stretchVertically)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);

        RectTransform rect = textObject.GetComponent<RectTransform>();
        if (stretchVertically)
        {
            rect.anchorMin = new Vector2(0.0f, 0.0f);
            rect.anchorMax = new Vector2(1.0f, 1.0f);
        }
        else
        {
            rect.anchorMin = new Vector2(0.0f, 1.0f);
            rect.anchorMax = new Vector2(1.0f, 1.0f);
        }
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.alignment = alignment;
        text.color = Color.white;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
        text.richText = true;

        return text;
    }

    private void SetPanelVisible(bool visible)
    {
        if (canvas != null)
        {
            canvas.gameObject.SetActive(visible);
        }
    }

    private void SetMinimized(bool value)
    {
        minimized = value;
        if (expandedRoot != null)
        {
            expandedRoot.SetActive(!minimized);
        }
        if (collapsedRoot != null)
        {
            collapsedRoot.SetActive(minimized);
        }
    }

    private void RefreshStatusUi()
    {
        if (statusText == null)
        {
            return;
        }

        string connection;
        if (activeProvider == LabAssistantProvider.None)
        {
            connection = "disabled";
        }
        else if (convai == null)
        {
            connection = "starting";
        }
        else if (!convai.IsReady)
        {
            connection = "not configured";
        }
        else
        {
            connection = convai.StatusDetail;
        }

        string experiment = ExperimentContextProvider.CurrentReactionName;
        statusText.text = string.IsNullOrEmpty(experiment)
            ? "Convai: " + connection
            : "Convai: " + connection + "\nExperiment: " + experiment;

        if (micDot != null)
        {
            micDot.color = IsMicActive() ? MicActiveColor : MicIdleColor;
        }

        if (footerText != null)
        {
            bool ready = convai != null && convai.IsReady;
            footerText.text = ready
                ? "Hold [" + pushToTalkKey + "] to talk    [" + minimizeKey + "] minimise"
                : "Assistant not available    [" + minimizeKey + "] minimise";
        }
    }
}
