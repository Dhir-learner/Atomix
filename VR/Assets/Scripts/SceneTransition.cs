using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Fades between scenes and puts something worth reading over the wait.
///
/// Every scene change in Atomix was a bare <c>SceneManager.LoadScene</c>. That call blocks the
/// main thread until the new scene is fully built, so pressing "Enter Laboratory" froze the game
/// on the last frame of the menu - no spinner, no fade, nothing moving - while Unity assembled a
/// 3.6 MB scene with 249 GameObjects. The window stops answering the OS during that, so on a
/// slower machine Windows greys it out and offers to close it. The single most common first
/// impression of this game was that it had crashed.
///
/// Loading asynchronously fixes the freeze; fading through black fixes the visual cut; and the
/// card in the middle turns the wait into the one moment a student is guaranteed to be looking at
/// the screen with nothing to do - so it carries a chemistry tip rather than a progress bar
/// nobody reads.
///
/// <b>Held to a floor of <see cref="MinimumSeconds"/>.</b> A transition that flashes past in
/// 200 ms on a fast machine reads as a glitch; the eye needs roughly a third of a second to
/// accept a cut as deliberate.
/// </summary>
[DisallowMultipleComponent]
public class SceneTransition : MonoBehaviour
{
    /// <summary>Shortest a transition is allowed to be, however fast the machine is.</summary>
    public const float MinimumSeconds = 0.85f;

    public float fadeOutSeconds = 0.28f;
    public float fadeInSeconds = 0.40f;

    private static SceneTransition instance;
    private static bool loading;

    /// <summary>
    /// When the in-flight load started. <see cref="loading"/> is a static guard against
    /// double-clicking "Retake the test" and queueing two loads - but if the coroutine ever dies
    /// between setting it and clearing it (a throw inside a scene's Awake would do it), the flag
    /// would stay set and every future scene change in the session would silently do nothing.
    /// After this long, a new request is allowed through regardless.
    /// </summary>
    private static float loadingStartedAt;

    private const float LoadingWatchdogSeconds = 30.0f;

    private Canvas canvas;
    private CanvasGroup group;
    private TMP_Text titleText;
    private TMP_Text tipText;
    private RectTransform barFill;

    /// <summary>True while a transition is running, so input handlers can stand down.</summary>
    public static bool IsLoading { get { return loading; } }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(this);
            return;
        }

        instance = this;
    }

    void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    /// <summary>
    /// Loads a scene behind a fade. Falls back to a direct load if the transition rig is not up
    /// yet, so this is a safe drop-in for every existing <c>SceneManager.LoadScene</c> call.
    /// </summary>
    public static void Load(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            return;
        }

        if (loading && Time.unscaledTime - loadingStartedAt < LoadingWatchdogSeconds)
        {
            return;      // double-clicking "Retake the test" must not queue two loads
        }

        if (instance == null || !instance.isActiveAndEnabled)
        {
            SceneManager.LoadScene(sceneName);
            return;
        }

        instance.StartCoroutine(instance.Run(sceneName));
    }

    /// <summary>Reloads the active scene. Used by the retry path and the exam retake.</summary>
    public static void Reload()
    {
        Load(SceneManager.GetActiveScene().name);
    }

    private IEnumerator Run(string sceneName)
    {
        loading = true;
        loadingStartedAt = Time.unscaledTime;

        EnsureBuilt();
        SetTip(sceneName);

        // The cursor has to come back before the next scene decides what to do with it: the menu
        // wants it free, the lab wants it locked, and being locked through a load leaves the
        // player unable to click "Play" if the load fails.
        FirstPersonController.SetCursorLock(false);
        AtomixAudio.Play(AtomixAudio.Cue.UiOpen, 0.7f);

        float startTime = Time.unscaledTime;

        yield return Fade(0.0f, 1.0f, fadeOutSeconds);

        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);
        if (operation == null)
        {
            // The scene is not in Build Settings. Say so loudly rather than hanging on black.
            Debug.LogError("[Atomix] Scene '" + sceneName + "' is not in Build Settings.");
            yield return Fade(1.0f, 0.0f, fadeInSeconds);
            loading = false;
            yield break;
        }

        operation.allowSceneActivation = false;

        // Unity stalls async loads at 0.9 until activation is allowed, so the bar is rescaled to
        // treat 0.9 as full. A bar that stops at 90% and sits there looks like a hung load.
        while (operation.progress < 0.9f)
        {
            SetProgress(operation.progress / 0.9f);
            yield return null;
        }

        SetProgress(1.0f);

        // Hold the floor, so a fast machine still gets a transition rather than a flicker.
        while (Time.unscaledTime - startTime < MinimumSeconds)
        {
            yield return null;
        }

        operation.allowSceneActivation = true;
        while (!operation.isDone)
        {
            yield return null;
        }

        // One frame for the incoming scene's Awake and Start, and for DesktopBootstrap to build
        // the rig, so the fade reveals a finished scene rather than one still assembling itself.
        yield return null;
        yield return null;

        yield return Fade(1.0f, 0.0f, fadeInSeconds);

        loading = false;
    }

    private IEnumerator Fade(float from, float to, float seconds)
    {
        if (group == null)
        {
            yield break;
        }

        canvas.gameObject.SetActive(true);
        group.alpha = from;

        float elapsed = 0.0f;
        while (elapsed < seconds)
        {
            elapsed += Time.unscaledDeltaTime;
            group.alpha = Mathf.Lerp(from, to, Mathf.SmoothStep(0.0f, 1.0f, elapsed / seconds));
            yield return null;
        }

        group.alpha = to;

        if (to <= 0.0f)
        {
            canvas.gameObject.SetActive(false);
        }
    }

    private void SetProgress(float value)
    {
        if (barFill != null)
        {
            barFill.anchorMax = new Vector2(Mathf.Clamp01(value), 1.0f);
        }
    }

    // =========================================================
    // CARD
    // =========================================================

    private void SetTip(string sceneName)
    {
        if (titleText != null)
        {
            titleText.text = TitleFor(sceneName);
        }

        if (tipText != null)
        {
            tipText.text = Tips[Random.Range(0, Tips.Length)];
        }

        SetProgress(0.0f);
    }

    private static string TitleFor(string sceneName)
    {
        switch (sceneName)
        {
            case "LabScene": return "Entering the laboratory";
            case "TestingPhaseLab": return "Preparing your test";
            case "LabAssistantScene": return "Finding your lab assistant";
            case "MainMenuScene": return "Returning to the main menu";
            default: return "Loading";
        }
    }

    /// <summary>
    /// What the student reads while they wait. Every one of these is a real control or a real
    /// rule of the free-hand engine, so the loading screen is the game's quietest tutorial.
    /// </summary>
    private static readonly string[] Tips =
    {
        "Press <b>B</b> to open the reaction book, then pick an experiment from its pages.",
        "Quantity matters. Pour too much and the reaction fails just as surely as pouring too little.",
        "Order matters too. Several experiments will not work if the reagents go in the wrong way round.",
        "Hold <b>V</b> to ask the lab assistant a question out loud, or press <b>Enter</b> to type one.",
        "Press <b>Y</b> after a failure and the assistant will explain the chemistry of what went wrong.",
        "<b>Tab</b> opens your experiment history - every attempt, every quantity, every mistake.",
        "<b>F</b> shows the energy, enthalpy and entropy graphs for the reaction you just completed.",
        "<b>P</b> opens an interactive periodic table you can use at any point in the lab.",
        "<b>F5</b> resets the bench so you can try the same experiment again without restarting.",
        "<b>L</b> cycles the measurement label above the glassware: full, compact, or off.",
        "Watch the molecular animation before you pour. Knowing what the bonds do makes the procedure obvious.",
        "Green in the readout at the top of the screen means that reagent is inside the accepted range.",
        "<b>F1</b> opens settings at any time - mouse speed, field of view, volume and accessibility.",
        "Failing is part of the method. The lab records why, so the next attempt is a better one."
    };

    private void EnsureBuilt()
    {
        if (canvas != null)
        {
            return;
        }

        GameObject canvasObject = new GameObject("SceneTransitionCanvas",
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
        canvasObject.transform.SetParent(transform, false);

        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9000;        // over absolutely everything, including the crosshair

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920.0f, 1080.0f);

        group = canvasObject.GetComponent<CanvasGroup>();
        group.alpha = 0.0f;
        group.interactable = false;

        // Blocks clicks while the screen is black, so a stray click cannot reach the menu button
        // underneath and start a second load.
        group.blocksRaycasts = true;

        // --- backdrop ---------------------------------------------------------------
        GameObject backdrop = new GameObject("Backdrop", typeof(RectTransform), typeof(Image));
        backdrop.transform.SetParent(canvasObject.transform, false);
        Stretch(backdrop.GetComponent<RectTransform>());
        backdrop.GetComponent<Image>().color = new Color(0.035f, 0.045f, 0.065f, 1.0f);

        // --- title ------------------------------------------------------------------
        titleText = MakeText(canvasObject.transform, "Title", new Vector2(0.0f, 60.0f),
            new Vector2(1400.0f, 70.0f), 46.0f, new Color(0.92f, 0.95f, 1.0f, 1.0f));
        titleText.fontStyle = FontStyles.Bold;

        // --- tip --------------------------------------------------------------------
        tipText = MakeText(canvasObject.transform, "Tip", new Vector2(0.0f, -30.0f),
            new Vector2(1180.0f, 130.0f), 26.0f, new Color(0.66f, 0.74f, 0.86f, 1.0f));
        tipText.textWrappingMode = TextWrappingModes.Normal;

        // --- progress bar -----------------------------------------------------------
        GameObject track = new GameObject("BarTrack", typeof(RectTransform), typeof(Image));
        track.transform.SetParent(canvasObject.transform, false);

        RectTransform trackRect = track.GetComponent<RectTransform>();
        trackRect.anchorMin = new Vector2(0.5f, 0.5f);
        trackRect.anchorMax = new Vector2(0.5f, 0.5f);
        trackRect.pivot = new Vector2(0.5f, 0.5f);
        trackRect.anchoredPosition = new Vector2(0.0f, -160.0f);
        trackRect.sizeDelta = new Vector2(560.0f, 5.0f);
        track.GetComponent<Image>().color = new Color(1.0f, 1.0f, 1.0f, 0.12f);

        GameObject fill = new GameObject("BarFill", typeof(RectTransform), typeof(Image));
        fill.transform.SetParent(track.transform, false);

        barFill = fill.GetComponent<RectTransform>();
        barFill.anchorMin = new Vector2(0.0f, 0.0f);
        barFill.anchorMax = new Vector2(0.0f, 1.0f);
        barFill.pivot = new Vector2(0.0f, 0.5f);
        barFill.offsetMin = Vector2.zero;
        barFill.offsetMax = Vector2.zero;
        fill.GetComponent<Image>().color = new Color(0.38f, 0.68f, 0.96f, 0.95f);

        canvasObject.SetActive(false);
    }

    private static TMP_Text MakeText(Transform parent, string name, Vector2 position,
                                     Vector2 size, float fontSize, Color colour)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        TMP_Text text = textObject.GetComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.alignment = TextAlignmentOptions.Center;
        text.color = colour;
        text.raycastTarget = false;
        text.richText = true;
        return text;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
