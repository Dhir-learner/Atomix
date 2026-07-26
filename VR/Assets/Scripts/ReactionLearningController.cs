using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

public class ReactionLearningController : MonoBehaviour
{
    const string CatalogResourcePath = "ReactionLearningVideoCatalog";

    enum LearningUiState
    {
        Hidden,
        Choice,
        Video,
        Unavailable
    }

    [SerializeField] private ReactionLearningVideoCatalog videoCatalog;
    [SerializeField] private FlipPages flipPages;

    // Dedicated desktop UI
    private Canvas learningCanvas;
    private GameObject rootPanel;

    private TMP_Text titleText;
    private TMP_Text bodyText;
    private RawImage videoDisplay;

    private Button learnButton;
    private Button performButton;
    private Button backButton;

    private Button playPauseButton;
    private Button replayButton;
    private Button videoPerformButton;
    private Button videoBackButton;

    private TMP_Text playPauseLabel;

    private VideoPlayer videoPlayer;
    private AudioSource videoAudioSource;
    private RenderTexture renderTexture;

    private LearningUiState currentState = LearningUiState.Hidden;
    private int pendingReactionId = -1;

    private bool waitingForPrepare = false;

    // Singleton instance to prevent state bugs with multiple instances
    private static ReactionLearningController instance;

    // =========================================================
    // CREATION / LOOKUP
    // =========================================================

    public static ReactionLearningController GetOrCreate(FlipPages sourceFlipPages)
    {
        if (sourceFlipPages == null)
        {
            return null;
        }

        if (instance != null)
        {
            instance.Initialize(sourceFlipPages);
            return instance;
        }

        Scene sourceScene = sourceFlipPages.gameObject.scene;

        ReactionLearningController[] controllers =
            FindObjectsByType<ReactionLearningController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

        if (controllers != null && controllers.Length > 0)
        {
            instance = controllers[0];
            instance.Initialize(sourceFlipPages);
            return instance;
        }

        GameObject controllerObject =
            new GameObject("ReactionLearningController");

        SceneManager.MoveGameObjectToScene(
            controllerObject,
            sourceScene
        );

        instance = controllerObject.AddComponent<ReactionLearningController>();
        instance.Initialize(sourceFlipPages);

        return instance;
    }

    public static void NotifyBookClosed(Scene scene)
    {
        ReactionLearningController[] controllers =
            FindObjectsByType<ReactionLearningController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

        foreach (ReactionLearningController controller in controllers)
        {
            if (controller != null &&
                controller.gameObject.scene == scene)
            {
                controller.HandleBookClosed();
            }
        }
    }

    // =========================================================
    // UNITY
    // =========================================================

    void Awake()
    {
        if (videoCatalog == null)
        {
            videoCatalog =
                Resources.Load<ReactionLearningVideoCatalog>(
                    CatalogResourcePath
                );
        }
    }

    void Update()
    {
        if (currentState == LearningUiState.Hidden)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            HandleBackAction();
        }

        if (currentState == LearningUiState.Video &&
            Input.GetKeyDown(KeyCode.Space))
        {
            TogglePlayPause();
        }
    }

    void OnDestroy()
    {
        if (videoPlayer != null)
        {
            videoPlayer.prepareCompleted -= OnVideoPrepared;
            videoPlayer.loopPointReached -= OnVideoFinished;
            videoPlayer.errorReceived -= OnVideoError;
        }

        if (renderTexture != null)
        {
            renderTexture.Release();
            Destroy(renderTexture);
            renderTexture = null;
        }
    }

    // =========================================================
    // INITIALIZATION
    // =========================================================

    void Initialize(FlipPages sourceFlipPages)
    {
        if (sourceFlipPages == null)
        {
            return;
        }

        flipPages = sourceFlipPages;

        if (videoCatalog == null)
        {
            videoCatalog =
                Resources.Load<ReactionLearningVideoCatalog>(
                    CatalogResourcePath
                );
        }
    }

    bool EnsureReady()
    {
        if (flipPages == null)
        {
            Debug.LogWarning(
                "ReactionLearningController: FlipPages reference missing."
            );

            return false;
        }

        if (rootPanel == null || learningCanvas == null)
        {
            BuildUi();
        }

        return rootPanel != null &&
               learningCanvas != null;
    }

    // =========================================================
    // REQUEST REACTION
    // =========================================================

    public bool TryRequestReaction(int reactionId)
    {
        if (reactionId < 1 || reactionId > 8)
        {
            Debug.LogWarning(
                $"ReactionLearningController: Invalid reaction ID {reactionId}."
            );

            return false;
        }

        if (!EnsureReady())
        {
            return false;
        }

        pendingReactionId = reactionId;

        StopVideoPlayback(true);

        ShowChoiceUi();

        return true;
    }

    public void HandleBookClosed()
    {
        StopVideoPlayback(true);

        pendingReactionId = -1;

        SetUiActive(false);

        currentState = LearningUiState.Hidden;
    }

    // =========================================================
    // BUILD DESKTOP CANVAS
    // =========================================================

    void BuildUi()
    {
        // -----------------------------------------------------
        // Canvas
        // -----------------------------------------------------

        GameObject canvasObject =
            new GameObject(
                "ReactionLearningCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster)
            );

        canvasObject.transform.SetParent(transform, false);

        learningCanvas = canvasObject.GetComponent<Canvas>();

        // Match existing desktop interaction by using WorldSpace and crosshair
        learningCanvas.renderMode =
            RenderMode.WorldSpace;
            
        learningCanvas.worldCamera = Camera.main;

        learningCanvas.sortingOrder = 500;

        // Position it in front of the camera when building, but we will update it in ShowChoiceUi
        CanvasScaler scaler =
            canvasObject.GetComponent<CanvasScaler>();

        // -----------------------------------------------------
        // Full screen background
        // -----------------------------------------------------

        GameObject background =
            new GameObject(
                "Background",
                typeof(RectTransform),
                typeof(Image)
            );

        background.transform.SetParent(
            canvasObject.transform,
            false
        );

        RectTransform backgroundRect =
            background.GetComponent<RectTransform>();

        backgroundRect.anchorMin = Vector2.zero;
        backgroundRect.anchorMax = Vector2.one;

        backgroundRect.offsetMin = Vector2.zero;
        backgroundRect.offsetMax = Vector2.zero;

        Image backgroundImage =
            background.GetComponent<Image>();

        backgroundImage.color =
            new Color(0f, 0f, 0f, 0.65f);

        // Background should not steal button clicks.
        backgroundImage.raycastTarget = false;

        // -----------------------------------------------------
        // Main panel
        // -----------------------------------------------------

        rootPanel =
            new GameObject(
                "ReactionLearningPanel",
                typeof(RectTransform),
                typeof(Image)
            );

        rootPanel.transform.SetParent(
            canvasObject.transform,
            false
        );

        RectTransform panelRect =
            rootPanel.GetComponent<RectTransform>();

        panelRect.anchorMin =
            new Vector2(0.5f, 0.5f);

        panelRect.anchorMax =
            new Vector2(0.5f, 0.5f);

        panelRect.pivot =
            new Vector2(0.5f, 0.5f);

        panelRect.anchoredPosition =
            Vector2.zero;

        panelRect.sizeDelta =
            new Vector2(1000f, 720f);

        Image panelImage =
            rootPanel.GetComponent<Image>();

        panelImage.color =
            new Color(
                0.06f,
                0.07f,
                0.10f,
                0.98f
            );

        // -----------------------------------------------------
        // Title
        // -----------------------------------------------------

        titleText =
            CreateText(
                "ReactionTitle",
                rootPanel.transform,
                new Vector2(0f, 290f),
                new Vector2(900f, 80f),
                38f,
                FontStyles.Bold,
                TextAlignmentOptions.Center
            );

        // -----------------------------------------------------
        // Body / status
        // -----------------------------------------------------

        bodyText =
            CreateText(
                "ReactionBody",
                rootPanel.transform,
                new Vector2(0f, 210f),
                new Vector2(900f, 80f),
                27f,
                FontStyles.Normal,
                TextAlignmentOptions.Center
            );

        // -----------------------------------------------------
        // Video display
        // -----------------------------------------------------

        GameObject videoObject =
            new GameObject(
                "ReactionVideoDisplay",
                typeof(RectTransform),
                typeof(RawImage)
            );

        videoObject.transform.SetParent(
            rootPanel.transform,
            false
        );

        RectTransform videoRect =
            videoObject.GetComponent<RectTransform>();

        videoRect.anchorMin =
            new Vector2(0.5f, 0.5f);

        videoRect.anchorMax =
            new Vector2(0.5f, 0.5f);

        videoRect.pivot =
            new Vector2(0.5f, 0.5f);

        videoRect.anchoredPosition =
            new Vector2(0f, 20f);

        videoRect.sizeDelta =
            new Vector2(800f, 400f);

        videoDisplay =
            videoObject.GetComponent<RawImage>();

        videoDisplay.color = Color.white;

        // Critical: video must not intercept button clicks.
        videoDisplay.raycastTarget = false;

        videoDisplay.enabled = false;

        EnsureVideoComponents(videoObject);

        // -----------------------------------------------------
        // Choice buttons
        // -----------------------------------------------------

        learnButton =
            CreateButton(
                "LearnButton",
                rootPanel.transform,
                "LEARN",
                new Vector2(-190f, -170f),
                new Vector2(260f, 65f)
            );

        performButton =
            CreateButton(
                "PerformButton",
                rootPanel.transform,
                "PERFORM",
                new Vector2(190f, -170f),
                new Vector2(260f, 65f)
            );

        backButton =
            CreateButton(
                "BackButton",
                rootPanel.transform,
                "BACK",
                new Vector2(0f, -270f),
                new Vector2(220f, 60f)
            );

        // -----------------------------------------------------
        // Video buttons
        // -----------------------------------------------------

        playPauseButton =
            CreateButton(
                "PlayPauseButton",
                rootPanel.transform,
                "PAUSE",
                new Vector2(-330f, -270f),
                new Vector2(180f, 60f)
            );

        replayButton =
            CreateButton(
                "ReplayButton",
                rootPanel.transform,
                "REPLAY",
                new Vector2(-110f, -270f),
                new Vector2(180f, 60f)
            );

        videoPerformButton =
            CreateButton(
                "VideoPerformButton",
                rootPanel.transform,
                "PERFORM",
                new Vector2(130f, -270f),
                new Vector2(220f, 60f)
            );

        videoBackButton =
            CreateButton(
                "VideoBackButton",
                rootPanel.transform,
                "BACK",
                new Vector2(350f, -270f),
                new Vector2(180f, 60f)
            );

        playPauseLabel =
            playPauseButton.GetComponentInChildren<TMP_Text>();

        // -----------------------------------------------------
        // Events
        // -----------------------------------------------------

        learnButton.onClick.AddListener(OnLearnClicked);

        performButton.onClick.AddListener(OnPerformClicked);

        backButton.onClick.AddListener(OnBackClicked);

        playPauseButton.onClick.AddListener(
            TogglePlayPause
        );

        replayButton.onClick.AddListener(
            OnReplayClicked
        );

        videoPerformButton.onClick.AddListener(
            OnPerformClicked
        );

        videoBackButton.onClick.AddListener(
            ReturnToChoiceFromVideo
        );

        // Keep buttons above the video/background.
        learnButton.transform.SetAsLastSibling();
        performButton.transform.SetAsLastSibling();
        backButton.transform.SetAsLastSibling();

        playPauseButton.transform.SetAsLastSibling();
        replayButton.transform.SetAsLastSibling();
        videoPerformButton.transform.SetAsLastSibling();
        videoBackButton.transform.SetAsLastSibling();

        SetUiActive(false);
    }

    // =========================================================
    // VIDEO COMPONENTS
    // =========================================================

    void EnsureVideoComponents(GameObject videoObject)
    {
        videoPlayer =
            videoObject.GetComponent<VideoPlayer>();

        if (videoPlayer == null)
        {
            videoPlayer =
                videoObject.AddComponent<VideoPlayer>();
        }

        videoAudioSource =
            videoObject.GetComponent<AudioSource>();

        if (videoAudioSource == null)
        {
            videoAudioSource =
                videoObject.AddComponent<AudioSource>();
        }

        videoAudioSource.playOnAwake = false;
        videoAudioSource.loop = false;

        renderTexture =
            new RenderTexture(
                1280,
                720,
                0,
                RenderTextureFormat.ARGB32
            );

        renderTexture.name =
            "ReactionLearningVideoRenderTexture";

        renderTexture.Create();

        videoPlayer.playOnAwake = false;
        videoPlayer.isLooping = false;

        videoPlayer.renderMode =
            VideoRenderMode.RenderTexture;

        videoPlayer.targetTexture =
            renderTexture;

        videoPlayer.audioOutputMode =
            VideoAudioOutputMode.AudioSource;

        videoPlayer.controlledAudioTrackCount = 1;

        videoPlayer.EnableAudioTrack(
            0,
            true
        );

        videoPlayer.SetTargetAudioSource(
            0,
            videoAudioSource
        );

        videoPlayer.prepareCompleted +=
            OnVideoPrepared;

        videoPlayer.loopPointReached +=
            OnVideoFinished;

        videoPlayer.errorReceived +=
            OnVideoError;
    }

    // =========================================================
    // UI HELPERS
    // =========================================================

    TMP_Text CreateText(
        string objectName,
        Transform parent,
        Vector2 anchoredPosition,
        Vector2 size,
        float fontSize,
        FontStyles fontStyle,
        TextAlignmentOptions alignment)
    {
        GameObject textObject =
            new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(TextMeshProUGUI)
            );

        textObject.transform.SetParent(
            parent,
            false
        );

        RectTransform textRect =
            textObject.GetComponent<RectTransform>();

        textRect.anchorMin =
            new Vector2(0.5f, 0.5f);

        textRect.anchorMax =
            new Vector2(0.5f, 0.5f);

        textRect.pivot =
            new Vector2(0.5f, 0.5f);

        textRect.anchoredPosition =
            anchoredPosition;

        textRect.sizeDelta =
            size;

        TextMeshProUGUI textComponent =
            textObject.GetComponent<TextMeshProUGUI>();

        textComponent.fontSize =
            fontSize;

        textComponent.fontStyle =
            fontStyle;

        textComponent.alignment =
            alignment;

        textComponent.color =
            Color.white;

        textComponent.enableWordWrapping =
            true;

        textComponent.raycastTarget =
            false;

        return textComponent;
    }

    Button CreateButton(
        string objectName,
        Transform parent,
        string label,
        Vector2 anchoredPosition,
        Vector2 size)
    {
        GameObject buttonObject =
            new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(Image),
                typeof(Button)
            );

        buttonObject.transform.SetParent(
            parent,
            false
        );

        RectTransform buttonRect =
            buttonObject.GetComponent<RectTransform>();

        buttonRect.anchorMin =
            new Vector2(0.5f, 0.5f);

        buttonRect.anchorMax =
            new Vector2(0.5f, 0.5f);

        buttonRect.pivot =
            new Vector2(0.5f, 0.5f);

        buttonRect.anchoredPosition =
            anchoredPosition;

        buttonRect.sizeDelta =
            size;

        Image buttonImage =
            buttonObject.GetComponent<Image>();

        buttonImage.color =
            new Color(
                0.16f,
                0.30f,
                0.50f,
                1f
            );

        Button button =
            buttonObject.GetComponent<Button>();

        TMP_Text buttonLabel =
            CreateText(
                "Label",
                buttonObject.transform,
                Vector2.zero,
                size - new Vector2(8f, 8f),
                24f,
                FontStyles.Bold,
                TextAlignmentOptions.Center
            );

        buttonLabel.text =
            label;

        return button;
    }

    // =========================================================
    // CHOICE UI
    // =========================================================

    void ShowChoiceUi()
    {
        if (!TryGetPendingEntry(
                out ReactionLearningVideoCatalog.Entry entry))
        {
            return;
        }

        PositionUiInFrontOfCamera();
        SetUiActive(true);

        currentState =
            LearningUiState.Choice;

        SetVideoVisibility(false);

        titleText.text =
            GetReactionTitle(entry);

        bodyText.text =
            "What would you like to do?";

        learnButton.gameObject.SetActive(true);

        performButton.gameObject.SetActive(true);

        backButton.gameObject.SetActive(true);

        playPauseButton.gameObject.SetActive(false);

        replayButton.gameObject.SetActive(false);

        videoPerformButton.gameObject.SetActive(false);

        videoBackButton.gameObject.SetActive(false);

        SetCursorForUi();
    }

    // =========================================================
    // LEARN
    // =========================================================

    void OnLearnClicked()
    {
        if (!TryGetPendingEntry(
                out ReactionLearningVideoCatalog.Entry entry))
        {
            Debug.LogWarning(
                "ReactionLearningController: Learn requested with no pending reaction."
            );

            return;
        }

        if (entry.videoClip == null)
        {
            ShowUnavailableUi(entry);

            return;
        }

        ShowVideoUi(entry);
    }

    // =========================================================
    // MISSING VIDEO
    // =========================================================

    void ShowUnavailableUi(
        ReactionLearningVideoCatalog.Entry entry)
    {
        PositionUiInFrontOfCamera();
        SetUiActive(true);

        currentState =
            LearningUiState.Unavailable;

        SetVideoVisibility(false);

        titleText.text =
            GetReactionTitle(entry);

        bodyText.text =
            "Molecular explanation video is currently unavailable.";

        learnButton.gameObject.SetActive(false);

        performButton.gameObject.SetActive(true);

        backButton.gameObject.SetActive(true);

        playPauseButton.gameObject.SetActive(false);

        replayButton.gameObject.SetActive(false);

        videoPerformButton.gameObject.SetActive(false);

        videoBackButton.gameObject.SetActive(false);

        SetCursorForUi();
    }

    // =========================================================
    // VIDEO UI
    // =========================================================

    void ShowVideoUi(
        ReactionLearningVideoCatalog.Entry entry)
    {
        PositionUiInFrontOfCamera();
        SetUiActive(true);

        currentState =
            LearningUiState.Video;

        SetVideoVisibility(true);

        titleText.text =
            GetReactionTitle(entry);

        bodyText.text =
            "Loading molecular reaction video...";

        learnButton.gameObject.SetActive(false);

        performButton.gameObject.SetActive(false);

        backButton.gameObject.SetActive(false);

        playPauseButton.gameObject.SetActive(true);

        replayButton.gameObject.SetActive(true);

        videoPerformButton.gameObject.SetActive(true);

        videoBackButton.gameObject.SetActive(true);

        PlayVideo(entry.videoClip);

        SetCursorForUi();
    }

    // =========================================================
    // VIDEO PLAYBACK
    // =========================================================

    void PlayVideo(VideoClip clip)
    {
        if (videoPlayer == null)
        {
            Debug.LogWarning(
                "ReactionLearningController: VideoPlayer missing."
            );

            bodyText.text =
                "Unable to initialize video player.";

            return;
        }

        if (clip == null)
        {
            Debug.LogWarning(
                "ReactionLearningController: Null VideoClip."
            );

            bodyText.text =
                "Molecular explanation video is unavailable.";

            return;
        }

        videoPlayer.Stop();

        waitingForPrepare = true;

        videoPlayer.clip = clip;

        videoPlayer.targetTexture =
            renderTexture;

        videoDisplay.texture =
            renderTexture;

        SetPlayPauseLabel("LOADING");

        videoPlayer.Prepare();
    }

    void OnVideoPrepared(VideoPlayer source)
    {
        if (!waitingForPrepare)
        {
            return;
        }

        if (currentState != LearningUiState.Video)
        {
            return;
        }

        waitingForPrepare = false;

        bodyText.text =
            "Molecular reaction explanation";

        source.Play();

        SetPlayPauseLabel("PAUSE");
    }

    void TogglePlayPause()
    {
        if (currentState != LearningUiState.Video ||
            videoPlayer == null ||
            videoPlayer.clip == null ||
            waitingForPrepare)
        {
            return;
        }

        if (videoPlayer.isPlaying)
        {
            videoPlayer.Pause();

            SetPlayPauseLabel("PLAY");
        }
        else
        {
            videoPlayer.Play();

            SetPlayPauseLabel("PAUSE");
        }
    }

    void OnReplayClicked()
    {
        if (currentState != LearningUiState.Video ||
            videoPlayer == null ||
            videoPlayer.clip == null)
        {
            return;
        }

        if (!videoPlayer.isPrepared)
        {
            bodyText.text =
                "Loading molecular reaction video...";

            waitingForPrepare = true;

            SetPlayPauseLabel("LOADING");

            videoPlayer.Prepare();

            return;
        }

        videoPlayer.time = 0;

        videoPlayer.Play();

        bodyText.text =
            "Molecular reaction explanation";

        SetPlayPauseLabel("PAUSE");
    }

    void OnVideoFinished(VideoPlayer source)
    {
        if (currentState != LearningUiState.Video)
        {
            return;
        }

        bodyText.text =
            "Video finished. Replay it, perform the reaction, or go back.";

        SetPlayPauseLabel("PLAY");
    }

    void OnVideoError(
        VideoPlayer source,
        string message)
    {
        waitingForPrepare = false;

        Debug.LogWarning(
            $"ReactionLearningController: Video playback error: {message}"
        );

        if (currentState ==
            LearningUiState.Video)
        {
            bodyText.text =
                "Unable to play this video. You can still perform the reaction or go back.";

            SetPlayPauseLabel("PLAY");
        }
    }

    void StopVideoPlayback(bool clearDisplay)
    {
        waitingForPrepare = false;

        if (videoPlayer != null)
        {
            videoPlayer.Stop();

            videoPlayer.clip = null;
        }

        if (videoAudioSource != null)
        {
            videoAudioSource.Stop();
        }

        if (clearDisplay &&
            videoDisplay != null)
        {
            videoDisplay.texture = null;
        }
    }

    // =========================================================
    // PERFORM
    // =========================================================

    void OnPerformClicked()
    {
        if (pendingReactionId < 1 ||
            pendingReactionId > 8)
        {
            Debug.LogWarning(
                "ReactionLearningController: Perform requested with no pending reaction."
            );

            return;
        }

        int reactionToPerform =
            pendingReactionId;

        pendingReactionId = -1;

        StopVideoPlayback(true);

        SetUiActive(false);

        currentState =
            LearningUiState.Hidden;

        if (flipPages != null)
        {
            // IMPORTANT:
            // Preserve original Atomix reaction flow.
            flipPages.StartReactionExperiment(
                reactionToPerform
            );
        }
        else
        {
            Debug.LogError(
                "ReactionLearningController: FlipPages missing. Cannot start reaction."
            );
        }
    }

    // =========================================================
    // BACK
    // =========================================================

    void OnBackClicked()
    {
        BackToReactionSelection();
    }

    void ReturnToChoiceFromVideo()
    {
        if (!TryGetPendingEntry(out _))
        {
            BackToReactionSelection();

            return;
        }

        StopVideoPlayback(true);

        ShowChoiceUi();
    }

    void BackToReactionSelection()
    {
        pendingReactionId = -1;

        StopVideoPlayback(true);

        SetUiActive(false);

        currentState =
            LearningUiState.Hidden;

        // Book remains open.
        SetCursorForUi();
    }

    void HandleBackAction()
    {
        switch (currentState)
        {
            case LearningUiState.Video:

                ReturnToChoiceFromVideo();

                break;

            case LearningUiState.Choice:

            case LearningUiState.Unavailable:

                BackToReactionSelection();

                break;
        }
    }

    // =========================================================
    // DATA
    // =========================================================

    bool TryGetPendingEntry(
        out ReactionLearningVideoCatalog.Entry entry)
    {
        if (pendingReactionId < 1 ||
            pendingReactionId > 8)
        {
            entry = null;

            return false;
        }

        if (videoCatalog != null &&
            videoCatalog.TryGetEntry(
                pendingReactionId,
                out entry
            ))
        {
            return true;
        }

        // Fail-safe entry.
        // Allows Perform even if catalog loading fails.

        entry =
            new ReactionLearningVideoCatalog.Entry
            {
                reactionId =
                    pendingReactionId,

                reactionName =
                    $"Reaction {pendingReactionId}"
            };

        return true;
    }

    string GetReactionTitle(
        ReactionLearningVideoCatalog.Entry entry)
    {
        if (entry == null)
        {
            return "Reaction";
        }

        if (!string.IsNullOrWhiteSpace(
                entry.displayTitle))
        {
            return entry.displayTitle;
        }

        if (!string.IsNullOrWhiteSpace(
                entry.reactionName))
        {
            return entry.reactionName;
        }

        return $"Reaction {entry.reactionId}";
    }

    // =========================================================
    // UI STATE
    // =========================================================

    void SetVideoVisibility(bool visible)
    {
        if (videoDisplay != null)
        {
            videoDisplay.enabled =
                visible;
        }
    }

    void SetUiActive(bool active)
    {
        if (learningCanvas != null)
        {
            learningCanvas.gameObject.SetActive(
                active
            );
        }
    }

    void SetPlayPauseLabel(string text)
    {
        if (playPauseLabel != null)
        {
            playPauseLabel.text =
                text;
        }
    }

    void SetCursorForUi()
    {
        // DO NOT unlock the OS cursor. Keep it locked so crosshair interaction works.
    }
    
    void PositionUiInFrontOfCamera()
    {
        if (learningCanvas == null) return;
        
        Camera cam = Camera.main;
        if (cam != null)
        {
            Transform camTransform = cam.transform;
            learningCanvas.transform.position = camTransform.position + camTransform.forward * 1.5f;
            learningCanvas.transform.rotation = camTransform.rotation;
            
            RectTransform canvasRect = learningCanvas.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(1000f, 720f);
            canvasRect.localScale = Vector3.one * 0.001f;
        }
    }
}

// using TMPro;
// using UnityEngine;
// using UnityEngine.SceneManagement;
// using UnityEngine.UI;
// using UnityEngine.Video;

// public class ReactionLearningController : MonoBehaviour
// {
//     const string CatalogResourcePath = "ReactionLearningVideoCatalog";

//     enum LearningUiState
//     {
//         Hidden,
//         Choice,
//         Video,
//         Unavailable
//     }

//     [SerializeField] private ReactionLearningVideoCatalog videoCatalog;
//     [SerializeField] private FlipPages flipPages;
//     [SerializeField] private Canvas bookCanvas;

//     private GameObject rootPanel;
//     private TMP_Text titleText;
//     private TMP_Text bodyText;
//     private RawImage videoDisplay;

//     private Button learnButton;
//     private Button performButton;
//     private Button backButton;
//     private Button playPauseButton;
//     private Button replayButton;
//     private Button videoPerformButton;
//     private Button videoBackButton;

//     private TMP_Text playPauseLabel;
//     private VideoPlayer videoPlayer;
//     private AudioSource videoAudioSource;
//     private RenderTexture renderTexture;

//     private LearningUiState currentState = LearningUiState.Hidden;
//     private int pendingReactionId = -1;

//     public static ReactionLearningController GetOrCreate(FlipPages sourceFlipPages)
//     {
//         if (sourceFlipPages == null)
//         {
//             return null;
//         }

//         Scene sourceScene = sourceFlipPages.gameObject.scene;
//         ReactionLearningController[] controllers = FindObjectsByType<ReactionLearningController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
//         for (int i = 0; i < controllers.Length; i++)
//         {
//             ReactionLearningController controller = controllers[i];
//             if (controller != null && controller.gameObject.scene == sourceScene)
//             {
//                 controller.Initialize(sourceFlipPages);
//                 return controller;
//             }
//         }

//         GameObject controllerObject = new GameObject("ReactionLearningController");
//         SceneManager.MoveGameObjectToScene(controllerObject, sourceScene);
//         ReactionLearningController createdController = controllerObject.AddComponent<ReactionLearningController>();
//         createdController.Initialize(sourceFlipPages);
//         return createdController;
//     }

//     public static void NotifyBookClosed(Scene scene)
//     {
//         ReactionLearningController[] controllers = FindObjectsByType<ReactionLearningController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
//         for (int i = 0; i < controllers.Length; i++)
//         {
//             ReactionLearningController controller = controllers[i];
//             if (controller != null && controller.gameObject.scene == scene)
//             {
//                 controller.HandleBookClosed();
//             }
//         }
//     }

//     void Awake()
//     {
//         if (videoCatalog == null)
//         {
//             videoCatalog = Resources.Load<ReactionLearningVideoCatalog>(CatalogResourcePath);
//         }
//     }

//     void Update()
//     {
//         if (currentState == LearningUiState.Hidden || rootPanel == null || !rootPanel.activeInHierarchy)
//         {
//             return;
//         }

//         if (Input.GetKeyDown(KeyCode.Escape))
//         {
//             HandleBackAction();
//         }

//         if (currentState == LearningUiState.Video && Input.GetKeyDown(KeyCode.Space))
//         {
//             TogglePlayPause();
//         }
//     }

//     void OnDestroy()
//     {
//         if (renderTexture != null)
//         {
//             renderTexture.Release();
//             Destroy(renderTexture);
//             renderTexture = null;
//         }
//     }

//     public bool TryRequestReaction(int reactionId)
//     {
//         if (reactionId < 1 || reactionId > 8)
//         {
//             Debug.LogWarning($"ReactionLearningController: Invalid reaction ID requested: {reactionId}");
//             return false;
//         }

//         if (!EnsureReady())
//         {
//             return false;
//         }

//         pendingReactionId = reactionId;
//         StopVideoPlayback(clearDisplay: true);
//         ShowChoiceUi();
//         return true;
//     }

//     public void HandleBookClosed()
//     {
//         StopVideoPlayback(clearDisplay: true);
//         pendingReactionId = -1;
//         SetUiActive(false);
//         currentState = LearningUiState.Hidden;
//     }

//     void Initialize(FlipPages sourceFlipPages)
//     {
//         if (sourceFlipPages == null)
//         {
//             return;
//         }

//         flipPages = sourceFlipPages;
//         if (bookCanvas == null && flipPages.bookCanvas != null)
//         {
//             bookCanvas = flipPages.bookCanvas.GetComponent<Canvas>();
//         }

//         if (videoCatalog == null)
//         {
//             videoCatalog = Resources.Load<ReactionLearningVideoCatalog>(CatalogResourcePath);
//         }
//     }

//     bool EnsureReady()
//     {
//         if (flipPages == null)
//         {
//             Debug.LogWarning("ReactionLearningController: FlipPages reference is missing.");
//             return false;
//         }

//         if (bookCanvas == null && flipPages.bookCanvas != null)
//         {
//             bookCanvas = flipPages.bookCanvas.GetComponent<Canvas>();
//         }

//         if (bookCanvas == null)
//         {
//             Debug.LogWarning("ReactionLearningController: Book canvas is missing.");
//             return false;
//         }

//         if (rootPanel == null)
//         {
//             BuildUi();
//         }

//         return rootPanel != null;
//     }

//     void BuildUi()
//     {
//         rootPanel = new GameObject("ReactionLearningPanel", typeof(RectTransform), typeof(Image));
//         rootPanel.transform.SetParent(bookCanvas.transform, false);

//         RectTransform panelRect = rootPanel.GetComponent<RectTransform>();
//         panelRect.anchorMin = new Vector2(0.5f, 0.5f);
//         panelRect.anchorMax = new Vector2(0.5f, 0.5f);
//         panelRect.pivot = new Vector2(0.5f, 0.5f);
//         panelRect.anchoredPosition = Vector2.zero;
//         panelRect.sizeDelta = new Vector2(680f, 500f);

//         Image panelImage = rootPanel.GetComponent<Image>();
//         panelImage.color = new Color(0.06f, 0.07f, 0.1f, 0.94f);

//         titleText = CreateText("ReactionTitle", rootPanel.transform, new Vector2(0f, 200f), new Vector2(620f, 70f), 34f, FontStyles.Bold, TextAlignmentOptions.Center);
//         bodyText = CreateText("ReactionBody", rootPanel.transform, new Vector2(0f, 112f), new Vector2(620f, 120f), 28f, FontStyles.Normal, TextAlignmentOptions.Center);

//         GameObject videoObject = new GameObject("ReactionVideoDisplay", typeof(RectTransform), typeof(RawImage));
//         videoObject.transform.SetParent(rootPanel.transform, false);
//         RectTransform videoRect = videoObject.GetComponent<RectTransform>();
//         videoRect.anchorMin = new Vector2(0.5f, 0.5f);
//         videoRect.anchorMax = new Vector2(0.5f, 0.5f);
//         videoRect.pivot = new Vector2(0.5f, 0.5f);
//         videoRect.anchoredPosition = new Vector2(0f, 35f);
//         videoRect.sizeDelta = new Vector2(580f, 240f);
//         videoDisplay = videoObject.GetComponent<RawImage>();
//         videoDisplay.color = Color.white;
//         videoDisplay.enabled = false;

//         EnsureVideoComponents(videoObject);

//         learnButton = CreateButton("LearnButton", rootPanel.transform, "LEARN", new Vector2(-150f, -150f));
//         performButton = CreateButton("PerformButton", rootPanel.transform, "PERFORM", new Vector2(150f, -150f));
//         backButton = CreateButton("BackButton", rootPanel.transform, "BACK", new Vector2(0f, -220f));

//         playPauseButton = CreateButton("PlayPauseButton", rootPanel.transform, "PAUSE", new Vector2(-230f, -150f));
//         replayButton = CreateButton("ReplayButton", rootPanel.transform, "REPLAY", new Vector2(-70f, -150f));
//         videoPerformButton = CreateButton("VideoPerformButton", rootPanel.transform, "PERFORM", new Vector2(90f, -150f));
//         videoBackButton = CreateButton("VideoBackButton", rootPanel.transform, "BACK", new Vector2(250f, -150f));

//         playPauseLabel = playPauseButton.GetComponentInChildren<TMP_Text>();

//         learnButton.onClick.AddListener(OnLearnClicked);
//         performButton.onClick.AddListener(OnPerformClicked);
//         backButton.onClick.AddListener(OnBackClicked);
//         playPauseButton.onClick.AddListener(TogglePlayPause);
//         replayButton.onClick.AddListener(OnReplayClicked);
//         videoPerformButton.onClick.AddListener(OnPerformClicked);
//         videoBackButton.onClick.AddListener(ReturnToChoiceFromVideo);

//         SetUiActive(false);
//     }

//     void EnsureVideoComponents(GameObject videoObject)
//     {
//         videoPlayer = videoObject.GetComponent<VideoPlayer>();
//         if (videoPlayer == null)
//         {
//             videoPlayer = videoObject.AddComponent<VideoPlayer>();
//         }

//         videoAudioSource = videoObject.GetComponent<AudioSource>();
//         if (videoAudioSource == null)
//         {
//             videoAudioSource = videoObject.AddComponent<AudioSource>();
//         }

//         videoAudioSource.playOnAwake = false;
//         videoAudioSource.loop = false;

//         renderTexture = new RenderTexture(1280, 720, 0);
//         renderTexture.name = "ReactionLearningVideoRenderTexture";
//         renderTexture.Create();

//         videoPlayer.playOnAwake = false;
//         videoPlayer.isLooping = false;
//         videoPlayer.renderMode = VideoRenderMode.RenderTexture;
//         videoPlayer.targetTexture = renderTexture;
//         videoPlayer.audioOutputMode = VideoAudioOutputMode.AudioSource;
//         videoPlayer.controlledAudioTrackCount = 1;
//         videoPlayer.EnableAudioTrack(0, true);
//         videoPlayer.SetTargetAudioSource(0, videoAudioSource);
//         videoPlayer.loopPointReached += OnVideoFinished;
//         videoPlayer.errorReceived += OnVideoError;
//     }

//     TMP_Text CreateText(string objectName, Transform parent, Vector2 anchoredPosition, Vector2 size, float fontSize, FontStyles fontStyle, TextAlignmentOptions alignment)
//     {
//         GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
//         textObject.transform.SetParent(parent, false);

//         RectTransform textRect = textObject.GetComponent<RectTransform>();
//         textRect.anchorMin = new Vector2(0.5f, 0.5f);
//         textRect.anchorMax = new Vector2(0.5f, 0.5f);
//         textRect.pivot = new Vector2(0.5f, 0.5f);
//         textRect.anchoredPosition = anchoredPosition;
//         textRect.sizeDelta = size;

//         TextMeshProUGUI textComponent = textObject.GetComponent<TextMeshProUGUI>();
//         textComponent.fontSize = fontSize;
//         textComponent.fontStyle = fontStyle;
//         textComponent.alignment = alignment;
//         textComponent.color = Color.white;
//         textComponent.enableWordWrapping = true;
//         return textComponent;
//     }

//     Button CreateButton(string objectName, Transform parent, string label, Vector2 anchoredPosition)
//     {
//         GameObject buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
//         buttonObject.transform.SetParent(parent, false);

//         RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
//         buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
//         buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
//         buttonRect.pivot = new Vector2(0.5f, 0.5f);
//         buttonRect.anchoredPosition = anchoredPosition;
//         buttonRect.sizeDelta = new Vector2(140f, 46f);

//         Image buttonImage = buttonObject.GetComponent<Image>();
//         buttonImage.color = new Color(0.16f, 0.26f, 0.41f, 0.95f);

//         Button button = buttonObject.GetComponent<Button>();

//         TMP_Text buttonLabel = CreateText("Label", buttonObject.transform, Vector2.zero, new Vector2(136f, 42f), 24f, FontStyles.Bold, TextAlignmentOptions.Center);
//         buttonLabel.text = label;

//         return button;
//     }

//     void OnLearnClicked()
//     {
//         if (!TryGetPendingEntry(out ReactionLearningVideoCatalog.Entry entry))
//         {
//             Debug.LogWarning("ReactionLearningController: Learn requested with no pending reaction.");
//             return;
//         }

//         if (entry.videoClip == null)
//         {
//             ShowUnavailableUi(entry);
//             return;
//         }

//         ShowVideoUi(entry);
//     }

//     void OnPerformClicked()
//     {
//         if (pendingReactionId < 1 || pendingReactionId > 8)
//         {
//             Debug.LogWarning("ReactionLearningController: Perform requested with no pending reaction.");
//             return;
//         }

//         int reactionToPerform = pendingReactionId;
//         pendingReactionId = -1;

//         StopVideoPlayback(clearDisplay: true);
//         SetUiActive(false);
//         currentState = LearningUiState.Hidden;

//         if (flipPages != null)
//         {
//             flipPages.StartReactionExperiment(reactionToPerform);
//         }
//     }

//     void OnBackClicked()
//     {
//         BackToReactionSelection();
//     }

//     void ReturnToChoiceFromVideo()
//     {
//         if (!TryGetPendingEntry(out _))
//         {
//             BackToReactionSelection();
//             return;
//         }

//         StopVideoPlayback(clearDisplay: true);
//         ShowChoiceUi();
//     }

//     void BackToReactionSelection()
//     {
//         pendingReactionId = -1;
//         StopVideoPlayback(clearDisplay: true);
//         SetUiActive(false);
//         currentState = LearningUiState.Hidden;
//     }

//     void TogglePlayPause()
//     {
//         if (currentState != LearningUiState.Video || videoPlayer == null || videoPlayer.clip == null)
//         {
//             return;
//         }

//         if (videoPlayer.isPlaying)
//         {
//             videoPlayer.Pause();
//             SetPlayPauseLabel("PLAY");
//         }
//         else
//         {
//             videoPlayer.Play();
//             SetPlayPauseLabel("PAUSE");
//         }
//     }

//     void OnReplayClicked()
//     {
//         if (currentState != LearningUiState.Video || videoPlayer == null || videoPlayer.clip == null)
//         {
//             return;
//         }

//         videoPlayer.time = 0;
//         videoPlayer.Play();
//         SetPlayPauseLabel("PAUSE");
//     }

//     void ShowChoiceUi()
//     {
//         if (!TryGetPendingEntry(out ReactionLearningVideoCatalog.Entry entry))
//         {
//             return;
//         }

//         SetUiActive(true);
//         currentState = LearningUiState.Choice;
//         SetVideoVisibility(false);

//         titleText.text = GetReactionTitle(entry);
//         bodyText.text = "What would you like to do?";

//         learnButton.gameObject.SetActive(true);
//         performButton.gameObject.SetActive(true);
//         backButton.gameObject.SetActive(true);

//         playPauseButton.gameObject.SetActive(false);
//         replayButton.gameObject.SetActive(false);
//         videoPerformButton.gameObject.SetActive(false);
//         videoBackButton.gameObject.SetActive(false);

//         SetCursorForUi();
//     }

//     void ShowUnavailableUi(ReactionLearningVideoCatalog.Entry entry)
//     {
//         SetUiActive(true);
//         currentState = LearningUiState.Unavailable;
//         SetVideoVisibility(false);

//         titleText.text = GetReactionTitle(entry);
//         bodyText.text = "Molecular explanation video is currently unavailable.";

//         learnButton.gameObject.SetActive(false);
//         performButton.gameObject.SetActive(true);
//         backButton.gameObject.SetActive(true);

//         playPauseButton.gameObject.SetActive(false);
//         replayButton.gameObject.SetActive(false);
//         videoPerformButton.gameObject.SetActive(false);
//         videoBackButton.gameObject.SetActive(false);

//         SetCursorForUi();
//     }

//     void ShowVideoUi(ReactionLearningVideoCatalog.Entry entry)
//     {
//         SetUiActive(true);
//         currentState = LearningUiState.Video;
//         SetVideoVisibility(true);

//         titleText.text = GetReactionTitle(entry);
//         bodyText.text = "Molecular reaction learning video";

//         learnButton.gameObject.SetActive(false);
//         performButton.gameObject.SetActive(false);
//         backButton.gameObject.SetActive(false);

//         playPauseButton.gameObject.SetActive(true);
//         replayButton.gameObject.SetActive(true);
//         videoPerformButton.gameObject.SetActive(true);
//         videoBackButton.gameObject.SetActive(true);

//         PlayVideo(entry.videoClip);
//         SetCursorForUi();
//     }

//     void PlayVideo(VideoClip clip)
//     {
//         if (videoPlayer == null)
//         {
//             Debug.LogWarning("ReactionLearningController: VideoPlayer is missing.");
//             return;
//         }

//         if (clip == null)
//         {
//             Debug.LogWarning("ReactionLearningController: Tried to play a null clip.");
//             return;
//         }

//         videoPlayer.Stop();
//         videoPlayer.clip = clip;
//         videoPlayer.targetTexture = renderTexture;
//         videoDisplay.texture = renderTexture;
//         videoPlayer.Prepare();
//         videoPlayer.Play();
//         SetPlayPauseLabel("PAUSE");
//     }

//     void StopVideoPlayback(bool clearDisplay)
//     {
//         if (videoPlayer != null)
//         {
//             videoPlayer.Stop();
//             videoPlayer.clip = null;
//         }

//         if (videoAudioSource != null)
//         {
//             videoAudioSource.Stop();
//         }

//         if (clearDisplay && videoDisplay != null)
//         {
//             videoDisplay.texture = null;
//         }
//     }

//     void SetVideoVisibility(bool visible)
//     {
//         if (videoDisplay != null)
//         {
//             videoDisplay.enabled = visible;
//         }
//     }

//     void SetUiActive(bool isActive)
//     {
//         if (rootPanel != null)
//         {
//             rootPanel.SetActive(isActive);
//         }
//     }

//     void SetPlayPauseLabel(string text)
//     {
//         if (playPauseLabel != null)
//         {
//             playPauseLabel.text = text;
//         }
//     }

//     void HandleBackAction()
//     {
//         switch (currentState)
//         {
//             case LearningUiState.Video:
//                 ReturnToChoiceFromVideo();
//                 break;
//             case LearningUiState.Choice:
//             case LearningUiState.Unavailable:
//                 BackToReactionSelection();
//                 break;
//         }
//     }

//     bool TryGetPendingEntry(out ReactionLearningVideoCatalog.Entry entry)
//     {
//         if (pendingReactionId < 1 || pendingReactionId > 8)
//         {
//             entry = null;
//             return false;
//         }

//         if (videoCatalog != null && videoCatalog.TryGetEntry(pendingReactionId, out entry))
//         {
//             return true;
//         }

//         entry = new ReactionLearningVideoCatalog.Entry
//         {
//             reactionId = pendingReactionId,
//             reactionName = $"Reaction {pendingReactionId}"
//         };
//         return true;
//     }

//     string GetReactionTitle(ReactionLearningVideoCatalog.Entry entry)
//     {
//         if (entry == null)
//         {
//             return "Reaction";
//         }

//         if (!string.IsNullOrWhiteSpace(entry.displayTitle))
//         {
//             return entry.displayTitle;
//         }

//         if (!string.IsNullOrWhiteSpace(entry.reactionName))
//         {
//             return entry.reactionName;
//         }

//         return $"Reaction {entry.reactionId}";
//     }

//     void SetCursorForUi()
//     {
//         FirstPersonController.SetCursorLock(false);
//     }

//     void OnVideoFinished(VideoPlayer source)
//     {
//         if (currentState != LearningUiState.Video)
//         {
//             return;
//         }

//         bodyText.text = "Video finished. You can replay, perform the reaction, or go back.";
//         SetPlayPauseLabel("PLAY");
//     }

//     void OnVideoError(VideoPlayer source, string message)
//     {
//         Debug.LogWarning($"ReactionLearningController: Video playback error: {message}");
//         if (currentState == LearningUiState.Video)
//         {
//             bodyText.text = "Unable to play this video right now.";
//             SetPlayPauseLabel("PLAY");
//         }
//     }
// }
