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

    // --- Live molecular animation -------------------------------------------------------
    // Reactions 1 and 8 never had an MP4 (videoClip: {fileID: 0} in the catalog asset), so the
    // LEARN screen used to dead-end on "Molecular explanation video is unavailable." Every
    // reaction now has an in-engine ball-and-stick animation instead, and the six that do have
    // an MP4 can switch to it as a second view.
    private MolecularAnimationRenderer molecular;
    private Button molecularButton;
    private TMP_Text molecularButtonLabel;
    private Button askAiButton;
    private bool molecularMode = false;
    private float askCooldownUntil = 0.0f;

    private VideoPlayer videoPlayer;
    private AudioSource videoAudioSource;
    private RenderTexture renderTexture;

    private LearningUiState currentState = LearningUiState.Hidden;
    private int pendingReactionId = -1;

    private bool waitingForPrepare = false;

    // --- Post-success playback (Task 5) -------------------------------------------------
    // When an experiment succeeds the same video panel is reused, but there is no book, no
    // FlipPages and nothing to "perform" - the student has just performed it. In this mode the
    // PERFORM button becomes CONTINUE and hands control back to whoever asked for the video.
    private bool postSuccessMode = false;
    private System.Action postSuccessContinue = null;

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

    /// <summary>
    /// Finds or builds the controller without a <see cref="FlipPages"/>, for the post-success
    /// playback path where the book is not involved. Reuses the book's instance when one already
    /// exists, so there is only ever one video player and one canvas.
    /// </summary>
    public static ReactionLearningController GetOrCreateStandalone()
    {
        if (instance != null)
        {
            return instance;
        }

        ReactionLearningController[] controllers =
            FindObjectsByType<ReactionLearningController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

        if (controllers != null && controllers.Length > 0)
        {
            instance = controllers[0];
            return instance;
        }

        GameObject controllerObject =
            new GameObject("ReactionLearningController");

        SceneManager.MoveGameObjectToScene(
            controllerObject,
            SceneManager.GetActiveScene()
        );

        instance = controllerObject.AddComponent<ReactionLearningController>();

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
        // A question is being typed into the assistant panel; every letter belongs to it.
        if (LabTextInput.IsCapturing)
        {
            return;
        }

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
        // The book flow needs FlipPages so PERFORM can start the experiment. The post-success
        // flow has nothing to perform, so it must not be blocked by a missing reference.
        if (flipPages == null && !postSuccessMode)
        {
            Debug.LogWarning(
                "ReactionLearningController: FlipPages reference missing."
            );

            return false;
        }

        if (videoCatalog == null)
        {
            videoCatalog =
                Resources.Load<ReactionLearningVideoCatalog>(
                    CatalogResourcePath
                );
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

        // Opening the book always leaves post-success playback behind.
        postSuccessMode = false;
        postSuccessContinue = null;

        pendingReactionId = reactionId;

        StopVideoPlayback(true);

        ShowChoiceUi();

        return true;
    }

    // =========================================================
    // POST-SUCCESS PLAYBACK (Task 5)
    // =========================================================

    /// <summary>True while the panel is showing a video because an experiment just succeeded.</summary>
    public bool IsShowingPostSuccess
    {
        get
        {
            return postSuccessMode &&
                   currentState != LearningUiState.Hidden;
        }
    }

    /// <summary>
    /// True whenever this panel is on screen for any reason - the book's LEARN/PERFORM choice, a
    /// video, or the post-success playback.
    ///
    /// The history and graph panels sit at the same 1.5 m in front of the camera, so they check
    /// this before opening: two world-space panels at the same depth just stack on top of each
    /// other and neither is readable.
    /// </summary>
    public static bool IsAnyPanelVisible
    {
        get
        {
            if (instance == null)
            {
                return false;
            }

            return instance.currentState != LearningUiState.Hidden &&
                   instance.learningCanvas != null &&
                   instance.learningCanvas.gameObject.activeInHierarchy;
        }
    }

    /// <summary>
    /// Plays the molecular visualisation straight after a successful experiment, skipping the
    /// LEARN/PERFORM choice. When the student presses CONTINUE (or Esc),
    /// <paramref name="onContinue"/> runs.
    /// </summary>
    /// <returns>
    /// True if the panel was shown - either with the video or with the "coming soon" placeholder
    /// for a reaction that has no clip. False only if the UI could not be built at all, in which
    /// case the caller should carry on without it.
    /// </returns>
    public bool TryShowAfterSuccess(int reactionId, System.Action onContinue)
    {
        if (reactionId < 1 || reactionId > 8)
        {
            return false;
        }

        postSuccessMode = true;
        postSuccessContinue = onContinue;

        if (!EnsureReady())
        {
            // Do not strand the caller waiting for a CONTINUE that can never come.
            postSuccessMode = false;
            postSuccessContinue = null;
            return false;
        }

        pendingReactionId = reactionId;

        StopVideoPlayback(true);

        ReactionLearningVideoCatalog.Entry entry;
        if (!TryGetPendingEntry(out entry))
        {
            postSuccessMode = false;
            postSuccessContinue = null;
            return false;
        }

        if (entry.videoClip == null && !MolecularSceneCatalog.Has(entry.reactionId))
        {
            // 5B: no clip for this reaction - show the placeholder rather than a broken player.
            ShowUnavailableUi(entry);
        }
        else
        {
            ShowVideoUi(entry);
        }

        return true;
    }

    /// <summary>Closes the post-success panel and runs the continuation exactly once.</summary>
    void FinishPostSuccess()
    {
        System.Action continuation = postSuccessContinue;

        postSuccessMode = false;
        postSuccessContinue = null;
        pendingReactionId = -1;

        StopVideoPlayback(true);

        SetUiActive(false);

        currentState =
            LearningUiState.Hidden;

        if (continuation != null)
        {
            continuation();
        }
    }

    /// <summary>
    /// Applies the post-success button labels. Called from every Show* path so the book flow's
    /// labels are always restored when the panel is reused for the book.
    /// </summary>
    void ApplyButtonLabels()
    {
        SetButtonLabel(performButton, postSuccessMode ? "CONTINUE" : "PERFORM");
        SetButtonLabel(videoPerformButton, postSuccessMode ? "CONTINUE" : "PERFORM");
        SetButtonLabel(backButton, "BACK");
        SetButtonLabel(videoBackButton, "BACK");
    }

    static void SetButtonLabel(Button button, string label)
    {
        if (button == null)
        {
            return;
        }

        TMP_Text text = button.GetComponentInChildren<TMP_Text>();
        if (text != null)
        {
            text.text = label;
        }
    }

    public void HandleBookClosed()
    {
        // A post-success video is not part of the book, so closing the book must not kill it -
        // that would drop the CONTINUE callback and the graphs would never appear.
        if (postSuccessMode)
        {
            return;
        }

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
                typeof(RawImage),
                typeof(AspectRatioFitter)
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

        AspectRatioFitter aspectFitter =
            videoObject.GetComponent<AspectRatioFitter>();
        
        aspectFitter.aspectMode =
            AspectRatioFitter.AspectMode.FitInParent;
        
        aspectFitter.aspectRatio = 16f / 9f; // default aspect ratio, will be updated if needed

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

        // Six buttons share one row rather than wrapping to a second: the panel is 720 tall and
        // a second row at y = -340 would hang off the bottom edge.
        const float ButtonRowY = -270f;
        Vector2 videoButtonSize = new Vector2(155f, 60f);

        playPauseButton =
            CreateButton(
                "PlayPauseButton",
                rootPanel.transform,
                "PAUSE",
                new Vector2(-407.5f, ButtonRowY),
                videoButtonSize
            );

        replayButton =
            CreateButton(
                "ReplayButton",
                rootPanel.transform,
                "REPLAY",
                new Vector2(-244.5f, ButtonRowY),
                videoButtonSize
            );

        molecularButton =
            CreateButton(
                "MolecularViewButton",
                rootPanel.transform,
                "3D VIEW",
                new Vector2(-81.5f, ButtonRowY),
                videoButtonSize
            );

        askAiButton =
            CreateButton(
                "AskAiButton",
                rootPanel.transform,
                "ASK AI",
                new Vector2(81.5f, ButtonRowY),
                videoButtonSize
            );

        videoPerformButton =
            CreateButton(
                "VideoPerformButton",
                rootPanel.transform,
                "PERFORM",
                new Vector2(244.5f, ButtonRowY),
                videoButtonSize
            );

        videoBackButton =
            CreateButton(
                "VideoBackButton",
                rootPanel.transform,
                "BACK",
                new Vector2(407.5f, ButtonRowY),
                videoButtonSize
            );

        playPauseLabel =
            playPauseButton.GetComponentInChildren<TMP_Text>();

        molecularButtonLabel =
            molecularButton.GetComponentInChildren<TMP_Text>();

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

        molecularButton.onClick.AddListener(
            ToggleMolecularView
        );

        askAiButton.onClick.AddListener(
            AskAboutWhatIsOnScreen
        );

        // Keep buttons above the video/background.
        learnButton.transform.SetAsLastSibling();
        performButton.transform.SetAsLastSibling();
        backButton.transform.SetAsLastSibling();

        playPauseButton.transform.SetAsLastSibling();
        replayButton.transform.SetAsLastSibling();
        molecularButton.transform.SetAsLastSibling();
        askAiButton.transform.SetAsLastSibling();
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

        molecularButton.gameObject.SetActive(false);

        askAiButton.gameObject.SetActive(false);

        videoPerformButton.gameObject.SetActive(false);

        videoBackButton.gameObject.SetActive(false);

        ApplyButtonLabels();

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

        if (entry.videoClip == null && !MolecularSceneCatalog.Has(entry.reactionId))
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

        // 5B: a reaction with no clip must never look broken. In the book flow this reads as
        // "not available yet, you can still perform it"; after a success it reads as a promise.
        bodyText.text = postSuccessMode
            ? "Molecular visualization coming soon.\n\n" +
              "This reaction does not have its molecular animation yet, but your result was " +
              "correct and has been recorded. Continue to see the scientific graphs."
            : "Molecular explanation video is currently unavailable.";

        learnButton.gameObject.SetActive(false);

        performButton.gameObject.SetActive(true);

        // Post-success has a single exit (CONTINUE); BACK would be a second name for it.
        backButton.gameObject.SetActive(!postSuccessMode);

        playPauseButton.gameObject.SetActive(false);

        replayButton.gameObject.SetActive(false);

        molecularButton.gameObject.SetActive(false);

        askAiButton.gameObject.SetActive(false);

        videoPerformButton.gameObject.SetActive(false);

        videoBackButton.gameObject.SetActive(false);

        ApplyButtonLabels();

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

        // The 3D view is offered only when this reaction has an animation authored for it, and
        // ASK AI only makes sense while an assistant - cloud or offline - can actually answer.
        molecularButton.gameObject.SetActive(
            MolecularSceneCatalog.Has(pendingReactionId));

        askAiButton.gameObject.SetActive(true);

        videoPerformButton.gameObject.SetActive(true);

        // Post-success shows PLAY/PAUSE, REPLAY and CONTINUE - BACK has nowhere to go.
        videoBackButton.gameObject.SetActive(!postSuccessMode);

        ApplyButtonLabels();

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
            // No MP4 for this reaction - reactions 1 and 8 have never had one. Rather than
            // dead-ending, render the molecular animation into the same display.
            if (TryStartMolecularAnimation())
            {
                return;
            }

            Debug.LogWarning(
                "ReactionLearningController: Null VideoClip."
            );

            bodyText.text =
                "Molecular explanation video is unavailable.";

            return;
        }

        StopMolecularAnimation();
        RefreshMolecularButtonLabel();

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
            
        if (source.texture != null)
        {
            AspectRatioFitter aspectFitter = videoDisplay.GetComponent<AspectRatioFitter>();
            if (aspectFitter != null)
            {
                aspectFitter.aspectRatio = (float)source.texture.width / (float)source.texture.height;
            }
        }

        source.Play();

        SetPlayPauseLabel("PAUSE");
    }

    void TogglePlayPause()
    {
        if (molecularMode)
        {
            if (molecular.IsPlaying)
            {
                molecular.Pause();
                SetPlayPauseLabel("PLAY");
            }
            else
            {
                molecular.Play();
                SetPlayPauseLabel("PAUSE");
            }

            return;
        }

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
        if (molecularMode)
        {
            molecular.Restart();
            SetPlayPauseLabel("PAUSE");
            return;
        }

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

        bodyText.text = postSuccessMode
            ? "Video finished. Replay it, or continue to the scientific graphs."
            : "Video finished. Replay it, perform the reaction, or go back.";

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
            bodyText.text = postSuccessMode
                ? "Unable to play this video, but your result was recorded. Continue to see the scientific graphs."
                : "Unable to play this video. You can still perform the reaction or go back.";

            SetPlayPauseLabel("PLAY");
        }
    }

    void StopVideoPlayback(bool clearDisplay)
    {
        waitingForPrepare = false;

        StopMolecularAnimation();

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
        // In post-success mode this same button reads CONTINUE - there is nothing to perform.
        if (postSuccessMode)
        {
            FinishPostSuccess();

            return;
        }

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
        // There is no LEARN/PERFORM choice to go back to after a successful experiment.
        if (postSuccessMode)
        {
            FinishPostSuccess();

            return;
        }

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
        if (postSuccessMode)
        {
            FinishPostSuccess();

            return;
        }

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
    // LIVE MOLECULAR ANIMATION
    // =========================================================

    /// <summary>
    /// Switches the display over to the in-engine ball-and-stick animation. Returns false when
    /// this reaction has no animation authored, so the caller can keep its old behaviour.
    /// </summary>
    bool TryStartMolecularAnimation()
    {
        if (!MolecularSceneCatalog.Has(pendingReactionId))
        {
            return false;
        }

        if (molecular == null)
        {
            molecular = gameObject.AddComponent<MolecularAnimationRenderer>();
        }

        if (!molecular.Prepare(pendingReactionId))
        {
            return false;
        }

        // The MP4 and the animation share one RawImage, so only one of them may be feeding it.
        if (videoPlayer != null)
        {
            videoPlayer.Stop();
        }

        if (videoAudioSource != null)
        {
            videoAudioSource.Stop();
        }

        waitingForPrepare = false;
        molecularMode = true;

        molecular.StageChanged -= HandleMolecularStageChanged;
        molecular.StageChanged += HandleMolecularStageChanged;

        videoDisplay.texture = molecular.OutputTexture;
        videoDisplay.enabled = true;

        molecular.Restart();

        SetPlayPauseLabel("PAUSE");
        RefreshMolecularButtonLabel();

        return true;
    }

    void StopMolecularAnimation()
    {
        if (molecular == null)
        {
            molecularMode = false;
            return;
        }

        molecular.StageChanged -= HandleMolecularStageChanged;
        molecular.Stop();
        molecularMode = false;
    }

    /// <summary>The 3D VIEW / VIDEO button: swaps between the recorded clip and the animation.</summary>
    void ToggleMolecularView()
    {
        if (currentState != LearningUiState.Video)
        {
            return;
        }

        ReactionLearningVideoCatalog.Entry entry;
        bool hasEntry = TryGetPendingEntry(out entry);

        if (molecularMode)
        {
            // Only worth going back if there is actually a clip to go back to.
            if (hasEntry && entry.videoClip != null)
            {
                PlayVideo(entry.videoClip);
                RefreshMolecularButtonLabel();
            }

            return;
        }

        TryStartMolecularAnimation();
    }

    void RefreshMolecularButtonLabel()
    {
        if (molecularButtonLabel == null)
        {
            return;
        }

        ReactionLearningVideoCatalog.Entry entry;
        bool hasClip = TryGetPendingEntry(out entry) && entry.videoClip != null;

        // With no clip there is nothing to toggle back to, so the button just names the view.
        molecularButtonLabel.text = molecularMode
            ? (hasClip ? "VIDEO" : "3D VIEW")
            : "3D VIEW";
    }

    void HandleMolecularStageChanged(int stageIndex, string caption, string detail)
    {
        if (currentState != LearningUiState.Video || !molecularMode)
        {
            return;
        }

        bodyText.text = string.IsNullOrEmpty(detail)
            ? caption
            : caption + "  -  " + detail;
    }

    // =========================================================
    // ASK THE ASSISTANT ABOUT WHAT IS ON SCREEN
    // =========================================================

    /// <summary>
    /// The ASK AI button. This is the missing half of the "watch, then ask" loop: the student can
    /// already ask about a failed experiment and about the graphs, but had no way to ask about the
    /// molecular step they were looking at.
    /// </summary>
    void AskAboutWhatIsOnScreen()
    {
        // A crosshair click can register twice; without this the assistant gets asked twice.
        if (Time.unscaledTime < askCooldownUntil)
        {
            return;
        }
        askCooldownUntil = Time.unscaledTime + 2.0f;

        InLabAssistantController assistant = InLabAssistantController.Instance;
        if (assistant == null || !assistant.CanAsk)
        {
            bodyText.text =
                "The lab assistant is not available right now. Check Resources/LabAssistantSettings.";
            return;
        }

        string question;
        string context;

        if (molecularMode && molecular != null && molecular.Scene != null)
        {
            MolecularScene scene = molecular.Scene;
            int step = Mathf.Clamp(molecular.CurrentStageIndex, 0, scene.stages.Count - 1);

            question = "In the molecular animation for " + scene.title +
                       ", step " + (step + 1) + " is \"" + scene.stages[step].caption +
                       "\". Can you explain what is happening to the atoms and electrons there?";

            context = ExperimentContextProvider.ContextHeader + "\n" +
                      "The student is watching the molecular animation for " + scene.equation +
                      ". Step " + (step + 1) + " of " + scene.stages.Count + ": " +
                      scene.stages[step].caption + " - " + scene.stages[step].detail;
        }
        else
        {
            ReactionLearningVideoCatalog.Entry entry;
            string name = TryGetPendingEntry(out entry)
                ? GetReactionTitle(entry)
                : "this reaction";

            question = "I just watched the molecular video for " + name +
                       ". Can you explain what happens to the bonds and electrons?";

            context = ExperimentContextProvider.ContextHeader + "\n" +
                      "The student has just watched the molecular explanation video for " + name + ".";
        }

        bool sent = assistant.AskAssistant(question, context);

        bodyText.text = sent
            ? "Asked the lab assistant - the answer appears in the assistant panel."
            : "The lab assistant could not take that question right now.";
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
