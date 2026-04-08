using System;
using Inworld;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Desktop push-to-talk controls for LabAssistantScene.
/// Creates a small UI button and a top-left red mic indicator.
/// </summary>
public class LabAssistantPushToTalkUI : MonoBehaviour
{
    private const string LabAssistantSceneName = "LabAssistantScene";

    [Header("Push To Talk")]
    [Tooltip("Hold this key to talk in LabAssistantScene")]
    public KeyCode pushToTalkKey = KeyCode.V;

    [Tooltip("Require an active Inworld connection before starting mic capture")]
    public bool requireConnectedSession = true;

    [Header("UI")]
    [Tooltip("Color of the top-left dot while mic is active")]
    public Color activeDotColor = new Color(1f, 0.2f, 0.2f, 1f);

    [Tooltip("Color of the top-left dot while mic is inactive")]
    public Color inactiveDotColor = new Color(0.25f, 0.1f, 0.1f, 0.9f);

    private Image micDot;
    private Text micStatusLabel;
    private Text pushToTalkButtonText;
    private Image pushToTalkButtonImage;
    private bool isPushToTalkPressed;
    private bool isRecordingEventActive;
    private bool audioEventsBound;
    private bool forceRefreshVisual;

    void Start()
    {
        CreateUi();
        EnsurePushToTalkMode();
        TryBindAudioEvents();
        RefreshVisualState();
    }

    void Update()
    {
        if (SceneManager.GetActiveScene().name != LabAssistantSceneName)
        {
            return;
        }

        EnsurePushToTalkMode();
        TryBindAudioEvents();
        HandleKeyboardPushToTalk();
        RefreshVisualState();
    }

    void OnDisable()
    {
        StopPushToTalk();
        UnbindAudioEvents();
    }

    void EnsurePushToTalkMode()
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

    void HandleKeyboardPushToTalk()
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

    void StartPushToTalk()
    {
        if (isPushToTalkPressed)
        {
            return;
        }

        if (!CanStartPushToTalk())
        {
            return;
        }

        isPushToTalkPressed = true;
        InworldController.Instance.StartAudio();
        forceRefreshVisual = true;
    }

    void StopPushToTalk()
    {
        if (!isPushToTalkPressed)
        {
            return;
        }

        isPushToTalkPressed = false;
        if (InworldController.Instance != null)
        {
            if (InworldController.Status == InworldConnectionStatus.Connected)
            {
                InworldController.Instance.PushAudio();
            }
            else
            {
                InworldController.Instance.StopAudio();
            }
        }

        forceRefreshVisual = true;
    }

    bool CanStartPushToTalk()
    {
        if (InworldController.Instance == null)
        {
            return false;
        }

        if (!requireConnectedSession)
        {
            return true;
        }

        return InworldController.Status == InworldConnectionStatus.Connected;
    }

    void TryBindAudioEvents()
    {
        if (audioEventsBound)
        {
            return;
        }

        AudioCapture audio = InworldController.Audio;
        if (audio == null)
        {
            return;
        }

        audio.OnRecordingStart.AddListener(OnRecordingStart);
        audio.OnRecordingEnd.AddListener(OnRecordingEnd);
        audioEventsBound = true;
        forceRefreshVisual = true;
    }

    void UnbindAudioEvents()
    {
        if (!audioEventsBound)
        {
            return;
        }

        AudioCapture audio = InworldController.Audio;
        if (audio != null)
        {
            audio.OnRecordingStart.RemoveListener(OnRecordingStart);
            audio.OnRecordingEnd.RemoveListener(OnRecordingEnd);
        }

        audioEventsBound = false;
    }

    void OnRecordingStart()
    {
        isRecordingEventActive = true;
        forceRefreshVisual = true;
    }

    void OnRecordingEnd()
    {
        isRecordingEventActive = false;
        forceRefreshVisual = true;
    }

    bool IsMicActive()
    {
        AudioCapture audio = InworldController.Audio;
        bool isCapturing = audio != null && audio.IsCapturing && !audio.IsBlocked;
        return isPushToTalkPressed || isRecordingEventActive || isCapturing;
    }

    void RefreshVisualState()
    {
        if (micDot == null || micStatusLabel == null || pushToTalkButtonText == null || pushToTalkButtonImage == null)
        {
            return;
        }

        bool micActive = IsMicActive();
        if (!forceRefreshVisual && !micActive && !isPushToTalkPressed && !isRecordingEventActive)
        {
            return;
        }

        micDot.color = micActive ? activeDotColor : inactiveDotColor;
        micStatusLabel.text = micActive ? "MIC ON" : "MIC OFF";

        if (micActive)
        {
            pushToTalkButtonImage.color = new Color(0.75f, 0.2f, 0.2f, 0.95f);
            pushToTalkButtonText.text = "Talking...";
        }
        else
        {
            pushToTalkButtonImage.color = new Color(0.08f, 0.08f, 0.08f, 0.85f);
            pushToTalkButtonText.text = $"Hold to Talk ({pushToTalkKey})";
        }

        forceRefreshVisual = false;
    }

    void CreateUi()
    {
        Canvas canvas = GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            gameObject.AddComponent<GraphicRaycaster>();
        }

        GameObject indicatorRoot = new GameObject("MicIndicator");
        indicatorRoot.transform.SetParent(transform, false);
        RectTransform indicatorRect = indicatorRoot.AddComponent<RectTransform>();
        indicatorRect.anchorMin = new Vector2(0f, 1f);
        indicatorRect.anchorMax = new Vector2(0f, 1f);
        indicatorRect.pivot = new Vector2(0f, 1f);
        indicatorRect.anchoredPosition = new Vector2(14f, -14f);
        indicatorRect.sizeDelta = new Vector2(140f, 28f);

        GameObject dotObject = new GameObject("Dot");
        dotObject.transform.SetParent(indicatorRoot.transform, false);
        RectTransform dotRect = dotObject.AddComponent<RectTransform>();
        dotRect.anchorMin = new Vector2(0f, 0.5f);
        dotRect.anchorMax = new Vector2(0f, 0.5f);
        dotRect.pivot = new Vector2(0f, 0.5f);
        dotRect.anchoredPosition = new Vector2(0f, 0f);
        dotRect.sizeDelta = new Vector2(14f, 14f);
        micDot = dotObject.AddComponent<Image>();
        micDot.color = inactiveDotColor;

        GameObject statusTextObject = new GameObject("StatusText");
        statusTextObject.transform.SetParent(indicatorRoot.transform, false);
        RectTransform statusRect = statusTextObject.AddComponent<RectTransform>();
        statusRect.anchorMin = new Vector2(0f, 0f);
        statusRect.anchorMax = new Vector2(1f, 1f);
        statusRect.offsetMin = new Vector2(20f, 0f);
        statusRect.offsetMax = new Vector2(0f, 0f);
        micStatusLabel = statusTextObject.AddComponent<Text>();
        micStatusLabel.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        micStatusLabel.fontSize = 16;
        micStatusLabel.fontStyle = FontStyle.Bold;
        micStatusLabel.alignment = TextAnchor.MiddleLeft;
        micStatusLabel.color = Color.white;
        micStatusLabel.text = "MIC OFF";

        GameObject buttonObject = new GameObject("PushToTalkButton");
        buttonObject.transform.SetParent(transform, false);
        RectTransform buttonRect = buttonObject.AddComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0f, 1f);
        buttonRect.anchorMax = new Vector2(0f, 1f);
        buttonRect.pivot = new Vector2(0f, 1f);
        buttonRect.anchoredPosition = new Vector2(14f, -48f);
        buttonRect.sizeDelta = new Vector2(220f, 42f);

        pushToTalkButtonImage = buttonObject.AddComponent<Image>();
        pushToTalkButtonImage.color = new Color(0.08f, 0.08f, 0.08f, 0.85f);

        Button button = buttonObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 1f, 1f, 0.96f);
        colors.pressedColor = new Color(0.9f, 0.9f, 0.9f, 0.96f);
        button.colors = colors;

        HoldPointerButton holdPointerButton = buttonObject.AddComponent<HoldPointerButton>();
        holdPointerButton.OnPressed += StartPushToTalk;
        holdPointerButton.OnReleased += StopPushToTalk;

        GameObject buttonTextObject = new GameObject("Label");
        buttonTextObject.transform.SetParent(buttonObject.transform, false);
        RectTransform buttonTextRect = buttonTextObject.AddComponent<RectTransform>();
        buttonTextRect.anchorMin = Vector2.zero;
        buttonTextRect.anchorMax = Vector2.one;
        buttonTextRect.offsetMin = new Vector2(10f, 6f);
        buttonTextRect.offsetMax = new Vector2(-10f, -6f);

        pushToTalkButtonText = buttonTextObject.AddComponent<Text>();
        pushToTalkButtonText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        pushToTalkButtonText.fontSize = 16;
        pushToTalkButtonText.alignment = TextAnchor.MiddleCenter;
        pushToTalkButtonText.color = Color.white;
        pushToTalkButtonText.text = $"Hold to Talk ({pushToTalkKey})";
    }
}

/// <summary>
/// Emits press/release events while a UI element is held.
/// </summary>
public class HoldPointerButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    public event Action OnPressed;
    public event Action OnReleased;

    private bool isPressed;

    public void OnPointerDown(PointerEventData eventData)
    {
        if (isPressed)
        {
            return;
        }

        isPressed = true;
        OnPressed?.Invoke();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        Release();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        Release();
    }

    void OnDisable()
    {
        Release();
    }

    void Release()
    {
        if (!isPressed)
        {
            return;
        }

        isPressed = false;
        OnReleased?.Invoke();
    }
}
