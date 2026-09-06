using System.Collections.Generic;
using Inworld;
using Inworld.Entities;
using Inworld.Interactions;
using Inworld.Packet;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Brings the Inworld lab assistant into the main lab as an always-available side panel, instead
/// of it only existing in the separate LabAssistantScene.
///
/// It boots the SDK itself: no scene or prefab edits are needed. The Inworld session data lives in
/// Assets/Resources/InLabAssistantGameData.asset (a copy of the chemist game data), the controller
/// and a voice-only character are built from code, and the connection state machine is pumped the
/// same way Inworld's own ConnectButton does it.
///
/// LabAssistantScene is deliberately left alone - this controller disables itself there and tears
/// down anything it created, so the standalone assistant scene keeps working exactly as before.
/// </summary>
public class InLabAssistantController : MonoBehaviour
{
    public const string GameDataResourceName = "InLabAssistantGameData";
    public const string ControllerPrefabResourceName = "InLabInworldController";
    private const string LabAssistantSceneName = "LabAssistantScene";

    [Header("Scenes")]
    [Tooltip("Scenes the in-lab side panel appears in. LabAssistantScene is always excluded.")]
    public string[] enabledScenes = { "LabScene" };

    [Header("Input")]
    [Tooltip("Hold to talk. Matches the key used in LabAssistantScene.")]
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

    // --- Inworld runtime objects we own (and may therefore destroy) --------------------
    private GameObject spawnedControllerObject;
    private GameObject spawnedCharacterObject;
    private InworldCharacter assistantCharacter;
    private bool inworldOwnedByUs = false;

    // --- UI ---------------------------------------------------------------------------
    private Canvas canvas;
    private GameObject expandedRoot;
    private GameObject collapsedRoot;
    private TMP_Text statusText;
    private TMP_Text chatText;
    private TMP_Text footerText;
    private Image micDot;

    private readonly List<string> chatLines = new List<string>();
    private string pendingAssistantLine = string.Empty;
    private bool assistantLineOpen = false;

    // --- State ------------------------------------------------------------------------
    private bool isActiveScene = false;
    private bool minimized = false;
    private bool pushToTalkHeld = false;
    private bool micRecording = false;
    private bool audioEventsBound = false;
    private bool packetsBound = false;
    private string lastContextDigest = string.Empty;
    private float nextConnectionPump = 0.0f;
    private bool statusEventsBound = false;
    private bool reportedProblem = false;
    private int connectionAttempts = 0;
    private const int maxConnectionAttempts = 3;

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
        UnbindAudioEvents();
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
            // Leaving the lab: hide the panel and give up the session so LabAssistantScene (or the
            // main menu) is never fighting a second InworldController.
            SetPanelVisible(false);
            StopPushToTalk();
            UnbindAudioEvents();
            TearDownOwnedInworld();
            return;
        }

        EnsureUiBuilt();
        SetPanelVisible(true);
        EnsureInworldSession();
    }

    private bool IsEnabledScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName) || sceneName == LabAssistantSceneName)
        {
            return false;
        }

        if (enabledScenes == null || enabledScenes.Length == 0)
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

        PumpConnection();
        EnsurePushToTalkMode();
        BindAudioEvents();
        BindCharacterPackets();
        HandlePushToTalkInput();
        RefreshStatusUi();
    }

    // =========================================================
    // INWORLD SESSION
    // =========================================================

    /// <summary>
    /// Builds a controller and a voice-only character if the scene has none. The avatar mesh is
    /// deliberately not spawned - the student gets the panel, the subtitles and the voice.
    /// </summary>
    private void EnsureInworldSession()
    {
        if (InworldController.Instance != null)
        {
            EnsureAssistantCharacter();
            return;
        }

        InworldGameData gameData = Resources.Load<InworldGameData>(GameDataResourceName);
        if (gameData == null)
        {
            AppendSystemLine("Assistant unavailable: Resources/" + GameDataResourceName + " is missing.");
            return;
        }

        // The controller MUST come from the prefab, not AddComponent. InworldClient keeps its
        // server URLs in an InworldServerConfig ScriptableObject held in a [SerializeField]
        // reference, and AudioCapture relies on serialized UnityEvents. A component added at
        // runtime gets none of that, and the SDK then null-references deep inside
        // InworldClient._GetAccessToken.
        GameObject controllerPrefab = Resources.Load<GameObject>(ControllerPrefabResourceName);
        if (controllerPrefab == null)
        {
            AppendSystemLine("Assistant unavailable: Resources/" + ControllerPrefabResourceName + " is missing.");
            return;
        }

        GameObject controllerObject = Instantiate(controllerPrefab);
        controllerObject.name = "InworldController (In-Lab)";

        InworldController controller = controllerObject.GetComponent<InworldController>();
        if (controller == null)
        {
            AppendSystemLine("Assistant unavailable: the controller prefab has no InworldController.");
            Destroy(controllerObject);
            return;
        }

        spawnedControllerObject = controllerObject;
        inworldOwnedByUs = true;

        // LoadData rather than the GameData property: the property setter calls
        // AssetDatabase.SaveAssets() inside UNITY_EDITOR, which we do not want during play.
        controller.LoadData(gameData);

        SpawnAssistantCharacter(controllerObject.transform, gameData);
        BindStatusEvents();
        AppendSystemLine("Connecting to your lab assistant...");
    }

    private void SpawnAssistantCharacter(Transform parent, InworldGameData gameData)
    {
        if (gameData.characters == null || gameData.characters.Count == 0)
        {
            AppendSystemLine("Assistant unavailable: the Inworld game data lists no characters.");
            return;
        }

        GameObject characterObject = new GameObject("LabAssistant (Voice Only)");
        // Built while INACTIVE on purpose. InworldInteraction.Awake sets enabled = false, and on a
        // freshly added component that flips OnDisable, which calls StopCoroutine on a routine that
        // has not started yet and throws. Adding the components before the object is ever enabled
        // means Awake runs once, in order, with enabled already false.
        characterObject.SetActive(false);
        characterObject.transform.SetParent(parent, false);

        // Audio interaction first: InworldCharacter requires an InworldInteraction, and the audio
        // subclass satisfies that while also giving us spoken replies.
        characterObject.AddComponent<AudioSource>();
        InworldAudioInteraction interaction = characterObject.AddComponent<InworldAudioInteraction>();
        interaction.enabled = false;

        InworldCharacter character = characterObject.AddComponent<InworldCharacter>();
        character.Data = gameData.characters[0];

        characterObject.SetActive(true);

        spawnedCharacterObject = characterObject;
        assistantCharacter = character;
    }

    private void EnsureAssistantCharacter()
    {
        if (assistantCharacter != null)
        {
            return;
        }

        InworldCharacter[] characters =
            FindObjectsByType<InworldCharacter>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (characters != null && characters.Length > 0)
        {
            assistantCharacter = characters[0];
        }
    }

    /// <summary>
    /// Walks the session from Idle to Connected. Mirrors Inworld's own ConnectButton: nothing in
    /// the SDK advances these steps on its own.
    /// </summary>
    private void PumpConnection()
    {
        if (InworldController.Instance == null || Time.unscaledTime < nextConnectionPump)
        {
            return;
        }

        nextConnectionPump = Time.unscaledTime + 1.0f;
        BindStatusEvents();

        switch (InworldController.Status)
        {
            case InworldConnectionStatus.Idle:
                // Only retry from Idle a limited number of times: if the account itself is
                // rejecting the session, hammering it every second helps nobody.
                if (connectionAttempts < maxConnectionAttempts)
                {
                    connectionAttempts++;
                    InworldController.Instance.Reconnect();
                }
                break;
            case InworldConnectionStatus.Initialized:
                InworldController.Client.StartSession();
                break;
            case InworldConnectionStatus.Connected:
                SelectAssistantCharacter();
                break;
            case InworldConnectionStatus.Error:
            case InworldConnectionStatus.Exhausted:
                ReportConnectionProblem();
                break;
        }
    }

    private void BindStatusEvents()
    {
        if (statusEventsBound || InworldController.Client == null)
        {
            return;
        }

        InworldController.Client.OnStatusChanged += HandleStatusChanged;
        statusEventsBound = true;
    }

    private void UnbindStatusEvents()
    {
        if (!statusEventsBound)
        {
            return;
        }

        if (InworldController.Client != null)
        {
            InworldController.Client.OnStatusChanged -= HandleStatusChanged;
        }
        statusEventsBound = false;
    }

    private void HandleStatusChanged(InworldConnectionStatus status)
    {
        if (status == InworldConnectionStatus.Connected)
        {
            reportedProblem = false;
            AppendSystemLine("Assistant connected. Hold [" + pushToTalkKey + "] to ask a question.");
            return;
        }

        if (status == InworldConnectionStatus.Error || status == InworldConnectionStatus.Exhausted)
        {
            ReportConnectionProblem();
        }
    }

    /// <summary>
    /// Puts the server's own words in front of the student instead of leaving the panel stuck on
    /// "Connecting...". A rejection here is an Inworld account/session problem, not a lab problem.
    /// </summary>
    private void ReportConnectionProblem()
    {
        if (reportedProblem)
        {
            return;
        }
        reportedProblem = true;

        string detail = InworldController.Client != null ? InworldController.Client.ErrorMessage : null;
        if (string.IsNullOrEmpty(detail))
        {
            detail = "the Inworld session was refused";
        }

        AppendSystemLine("Assistant unavailable: " + detail);
        AppendSystemLine("This is an Inworld account/session problem, not a lab problem. " +
                         "Check the workspace, scene and API key in Inworld Studio.");
    }

    private void SelectAssistantCharacter()
    {
        CharacterHandler handler = InworldController.CharacterHandler;
        if (handler == null || handler.CurrentCharacter != null)
        {
            return;
        }

        List<InworldCharacter> live = handler.CurrentCharacters;
        if (live != null && live.Count > 0)
        {
            handler.CurrentCharacter = live[0];
            assistantCharacter = live[0];
        }
    }

    private void TearDownOwnedInworld()
    {
        if (!inworldOwnedByUs)
        {
            return;
        }

        UnbindCharacterPackets();
        UnbindStatusEvents();

        if (InworldController.Instance != null)
        {
            InworldController.Instance.Disconnect();
        }

        if (spawnedCharacterObject != null)
        {
            Destroy(spawnedCharacterObject);
        }
        if (spawnedControllerObject != null)
        {
            Destroy(spawnedControllerObject);
        }

        spawnedCharacterObject = null;
        spawnedControllerObject = null;
        assistantCharacter = null;
        inworldOwnedByUs = false;
        lastContextDigest = string.Empty;
        reportedProblem = false;
        connectionAttempts = 0;
    }

    // =========================================================
    // PUSH TO TALK  (same behaviour as LabAssistantPushToTalkUI)
    // =========================================================

    private void EnsurePushToTalkMode()
    {
        if (InworldController.CharacterHandler != null)
        {
            InworldController.CharacterHandler.ManualAudioHandling = true;
        }

        AudioCapture audio = InworldController.Audio;
        if (audio != null)
        {
            audio.AutoPush = false;
            audio.IsBlocked = false;
        }
    }

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
        if (pushToTalkHeld || InworldController.Instance == null)
        {
            return;
        }

        if (InworldController.Status != InworldConnectionStatus.Connected)
        {
            return;
        }

        SendExperimentContextIfChanged();

        pushToTalkHeld = true;
        InworldController.Instance.StartAudio();
    }

    private void StopPushToTalk()
    {
        if (!pushToTalkHeld)
        {
            return;
        }

        pushToTalkHeld = false;

        if (InworldController.Instance == null)
        {
            return;
        }

        if (InworldController.Status == InworldConnectionStatus.Connected)
        {
            InworldController.Instance.PushAudio();
        }
        else
        {
            InworldController.Instance.StopAudio();
        }
    }

    private void BindAudioEvents()
    {
        if (audioEventsBound)
        {
            return;
        }

        AudioCapture audio = InworldController.Audio;
        if (audio == null || audio.OnRecordingStart == null || audio.OnRecordingEnd == null)
        {
            return; // AudioCapture has not finished initialising its serialized events yet.
        }

        audio.OnRecordingStart.AddListener(OnRecordingStart);
        audio.OnRecordingEnd.AddListener(OnRecordingEnd);
        audioEventsBound = true;
    }

    private void UnbindAudioEvents()
    {
        if (!audioEventsBound)
        {
            return;
        }

        AudioCapture audio = InworldController.Audio;
        if (audio != null && audio.OnRecordingStart != null && audio.OnRecordingEnd != null)
        {
            audio.OnRecordingStart.RemoveListener(OnRecordingStart);
            audio.OnRecordingEnd.RemoveListener(OnRecordingEnd);
        }

        audioEventsBound = false;
    }

    private void OnRecordingStart()
    {
        micRecording = true;
    }

    private void OnRecordingEnd()
    {
        micRecording = false;
    }

    private bool IsMicActive()
    {
        AudioCapture audio = InworldController.Audio;
        bool capturing = audio != null && audio.IsCapturing && !audio.IsBlocked;
        return pushToTalkHeld || micRecording || capturing;
    }

    // =========================================================
    // EXPERIMENT CONTEXT
    // =========================================================

    /// <summary>
    /// Sends the current experiment state as a narrative action just before the student speaks.
    /// A narrative action is stage direction rather than a player utterance, so it steers the
    /// answer without appearing as if the student said it.
    /// </summary>
    private void SendExperimentContextIfChanged()
    {
        if (!sendExperimentContext || InworldController.Instance == null)
        {
            return;
        }

        if (!ExperimentContextProvider.HasContext)
        {
            return;
        }

        string digest = ExperimentContextProvider.BuildDigest();
        if (digest == lastContextDigest)
        {
            return;
        }

        lastContextDigest = digest;
        InworldController.Instance.SendNarrativeAction(ExperimentContextProvider.BuildContext());
    }

    // =========================================================
    // SUBTITLES  (3C: history logging is handled by LabAssistantHistoryBridge)
    // =========================================================

    private void BindCharacterPackets()
    {
        if (packetsBound)
        {
            return;
        }

        EnsureAssistantCharacter();
        if (assistantCharacter == null || assistantCharacter.Event == null ||
            assistantCharacter.Event.onPacketReceived == null)
        {
            return;
        }

        assistantCharacter.Event.onPacketReceived.AddListener(HandlePacket);
        packetsBound = true;
    }

    private void UnbindCharacterPackets()
    {
        if (!packetsBound)
        {
            return;
        }

        if (assistantCharacter != null && assistantCharacter.Event != null &&
            assistantCharacter.Event.onPacketReceived != null)
        {
            assistantCharacter.Event.onPacketReceived.RemoveListener(HandlePacket);
        }

        packetsBound = false;
    }

    private void HandlePacket(InworldPacket packet)
    {
        TextPacket textPacket = packet as TextPacket;
        if (textPacket == null || textPacket.text == null)
        {
            return;
        }

        string body = textPacket.text.text;
        if (string.IsNullOrEmpty(body) || string.IsNullOrWhiteSpace(body))
        {
            return;
        }

        body = body.Trim();

        if (packet.Source == SourceType.PLAYER)
        {
            if (!textPacket.text.final)
            {
                return;
            }
            CloseAssistantLine();
            AppendLine("<color=#7ADFFF><b>You:</b></color> " + body);
            return;
        }

        if (packet.Source == SourceType.AGENT)
        {
            // Replies stream in as chunks - keep rewriting the same line so it reads as one answer.
            pendingAssistantLine = pendingAssistantLine.Length == 0 ? body : pendingAssistantLine + " " + body;

            if (!assistantLineOpen)
            {
                assistantLineOpen = true;
                chatLines.Add(string.Empty);
            }

            chatLines[chatLines.Count - 1] = "<color=#86F7A0><b>Assistant:</b></color> " + pendingAssistantLine;
            TrimChat();
            RefreshChatUi();
        }
    }

    private void CloseAssistantLine()
    {
        assistantLineOpen = false;
        pendingAssistantLine = string.Empty;
    }

    private void AppendSystemLine(string text)
    {
        AppendLine("<color=#B9C3D0><i>" + text + "</i></color>");
    }

    private void AppendLine(string line)
    {
        chatLines.Add(line);
        TrimChat();
        RefreshChatUi();
    }

    private void TrimChat()
    {
        int limit = Mathf.Max(4, maxChatLines);
        while (chatLines.Count > limit)
        {
            chatLines.RemoveAt(0);
        }
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

        string connection = InworldController.Instance == null
            ? "offline"
            : InworldController.Status.ToString().ToLower();

        string experiment = ExperimentContextProvider.CurrentReactionName;
        statusText.text = string.IsNullOrEmpty(experiment)
            ? "Session: " + connection
            : "Session: " + connection + "\nExperiment: " + experiment;

        if (micDot != null)
        {
            micDot.color = IsMicActive() ? MicActiveColor : MicIdleColor;
        }

        if (footerText != null)
        {
            bool connected = InworldController.Instance != null &&
                             InworldController.Status == InworldConnectionStatus.Connected;
            footerText.text = connected
                ? "Hold [" + pushToTalkKey + "] to talk    [" + minimizeKey + "] minimise"
                : "Waiting for the assistant to connect...    [" + minimizeKey + "] minimise";
        }
    }
}
