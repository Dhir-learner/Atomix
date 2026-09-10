using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// The first thirty seconds in the laboratory.
///
/// A student who pressed "Enter Laboratory" arrived standing in a room full of glassware with no
/// statement of what they were supposed to do. <see cref="ControlsHelpUI"/> has always been there
/// on <b>H</b>, and it is thorough - fifty key bindings across seven headings - but a reference
/// card is not an introduction. Nothing said <i>open the book, pick an experiment, measure
/// carefully, the amounts are what is being judged</i>, which is the entire game.
///
/// So this is four sentences, once, on the first visit. It teaches the loop rather than the
/// controls, points at <b>H</b> for the rest, and gets out of the way.
///
/// <b>It never blocks.</b> The card does not pause the game, does not take the cursor, does not
/// need dismissing before the student can move, and disappears on its own. Onboarding that has to
/// be clicked through is onboarding that gets clicked through without being read; a student who
/// already knows what to do can simply start walking and the card fades out behind them.
///
/// Shown once per machine. It can always be brought back from the pause menu's Controls tab, and
/// the objective strip underneath it stays for the whole first session, because "you have not
/// chosen an experiment yet" is the single most common way to be stuck in this game.
/// </summary>
[DisallowMultipleComponent]
public class LabOnboarding : MonoBehaviour
{
    private const string SeenKey = "atomix.onboarding.seen";

    [Tooltip("Scenes that get the introduction card.")]
    public string[] scenes = { "LabScene" };

    [Tooltip("Seconds the card stays up before it fades on its own.")]
    public float cardSeconds = 16.0f;

    [Tooltip("Seconds the card takes to fade out.")]
    public float fadeSeconds = 1.2f;

    private Canvas canvas;
    private CanvasGroup cardGroup;
    private RectTransform card;
    private TMP_Text objectiveText;
    private RectTransform objectiveStrip;
    private GameObject objectivePlate;

    private float remaining;
    private bool dismissed;

    /// <summary>The card is armed but held until the loading fade has finished.</summary>
    private float armDelay;

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
        Consider(SceneManager.GetActiveScene().name);
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Hide();
        Consider(scene.name);
    }

    private void Consider(string sceneName)
    {
        if (!IsTargetScene(sceneName))
        {
            return;
        }

        // The objective strip runs every visit; the card only on the first.
        EnsureBuilt();
        if (objectiveStrip != null)
        {
            objectiveStrip.gameObject.SetActive(true);
        }

        if (PlayerPrefs.GetInt(SeenKey, 0) != 0)
        {
            return;
        }

        PlayerPrefs.SetInt(SeenKey, 1);
        PlayerPrefs.Save();
        Show();
    }

    private bool IsTargetScene(string sceneName)
    {
        if (scenes == null)
        {
            return false;
        }

        for (int i = 0; i < scenes.Length; i++)
        {
            if (scenes[i] == sceneName)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>Brings the card back. Wired to the pause menu so it is never lost for good.</summary>
    public static void ShowAgain()
    {
        LabOnboarding onboarding = FindFirstObjectByType<LabOnboarding>(FindObjectsInactive.Include);
        if (onboarding != null)
        {
            onboarding.EnsureBuilt();
            onboarding.Show();
        }
    }

    private void Show()
    {
        EnsureBuilt();
        if (cardGroup == null)
        {
            return;
        }

        dismissed = false;
        remaining = cardSeconds;

        // Held back until the loading fade is off the screen, so the card is not revealed
        // half-faded underneath it.
        armDelay = SceneTransition.IsLoading ? 1.0f : 0.15f;

        cardGroup.alpha = 0.0f;
        card.gameObject.SetActive(true);
    }

    private void Hide()
    {
        dismissed = true;
        remaining = 0.0f;

        if (card != null)
        {
            card.gameObject.SetActive(false);
        }

        if (objectiveStrip != null)
        {
            objectiveStrip.gameObject.SetActive(false);
        }
    }

    void Update()
    {
        UpdateCard();
        UpdateObjective();
    }

    private void UpdateCard()
    {
        if (dismissed || cardGroup == null || !card.gameObject.activeSelf)
        {
            return;
        }

        if (armDelay > 0.0f)
        {
            armDelay -= Time.unscaledDeltaTime;
            return;
        }

        remaining -= Time.unscaledDeltaTime;

        // Any deliberate action means the student has started playing and does not need this.
        // Movement keys are excluded on purpose - looking around while reading is normal.
        if (!LabTextInput.IsCapturing &&
            (Input.GetKeyDown(KeyCode.B) || Input.GetKeyDown(KeyCode.H) ||
             Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(0)))
        {
            remaining = Mathf.Min(remaining, fadeSeconds);
        }

        if (remaining <= 0.0f)
        {
            card.gameObject.SetActive(false);
            dismissed = true;
            return;
        }

        // Fade in over the first quarter second, hold, then fade out over the last stretch.
        float age = cardSeconds - remaining;
        float fadeIn = Mathf.Clamp01(age / 0.35f);
        float fadeOut = Mathf.Clamp01(remaining / fadeSeconds);
        cardGroup.alpha = Mathf.Min(fadeIn, fadeOut);
    }

    /// <summary>
    /// One line at the bottom of the screen saying what to do next. It reads the live experiment
    /// state, so it stops being an instruction as soon as the instruction has been followed.
    /// </summary>
    private void UpdateObjective()
    {
        if (objectiveStrip == null || !objectiveStrip.gameObject.activeSelf || objectiveText == null)
        {
            return;
        }

        string line = CurrentObjective();
        bool wanted = !string.IsNullOrEmpty(line);

        // The plate is what is shown and hidden - the text always stays active, so its own
        // activeSelf cannot be used to decide anything.
        if (objectivePlate != null && objectivePlate.activeSelf != wanted)
        {
            objectivePlate.SetActive(wanted);
        }

        if (!wanted)
        {
            lastObjective = null;
            return;
        }

        // Assigning TMP_Text.text re-lays-out the mesh, so only write when it actually changed.
        if (line != lastObjective)
        {
            lastObjective = line;
            objectiveText.text = line;
        }
    }

    private string lastObjective;

    private string CurrentObjective()
    {
        ReactionHistoryRecorder selected = ReactionHistoryRecorder.Selected;

        if (selected == null)
        {
            return "Press <b>B</b> to open the reaction book and choose an experiment.";
        }

        FreeHandReactionEngine engine = selected.Engine;
        if (engine == null)
        {
            // Reaction 1 tracks its own quantities and has no engine to read.
            return string.Empty;
        }

        if (engine.HasSucceeded)
        {
            return "Experiment complete. <b>F</b> for the science behind it, <b>B</b> to pick another.";
        }

        if (engine.HasFailed)
        {
            return "<b>F5</b> resets the bench to try again   -   <b>Y</b> asks the assistant what went wrong.";
        }

        string pending = engine.GetPendingSubstance();
        if (!string.IsNullOrEmpty(pending))
        {
            return engine.hideTargets
                ? "Add the <b>" + pending + "</b>. You are being marked on the amount, so measure it yourself."
                : "Add the <b>" + pending + "</b>   -   watch the readout at the top of the screen for the amount.";
        }

        return string.Empty;
    }

    // =========================================================
    // BUILD
    // =========================================================

    private void EnsureBuilt()
    {
        if (canvas != null)
        {
            return;
        }

        GameObject canvasObject = new GameObject("LabOnboardingCanvas",
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        canvasObject.transform.SetParent(transform, false);

        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        // Below the HUD toast (560) so an achievement can still be read over the card, and below
        // the loading screen (9000) so a scene change covers it.
        canvas.sortingOrder = 520;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920.0f, 1080.0f);

        BuildCard(canvasObject.transform);
        BuildObjective(canvasObject.transform);
    }

    private void BuildCard(Transform parent)
    {
        GameObject plate = new GameObject("WelcomeCard",
            typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        plate.transform.SetParent(parent, false);

        card = plate.GetComponent<RectTransform>();
        card.anchorMin = new Vector2(0.5f, 0.5f);
        card.anchorMax = new Vector2(0.5f, 0.5f);
        card.pivot = new Vector2(0.5f, 0.5f);
        card.anchoredPosition = new Vector2(0.0f, 110.0f);
        card.sizeDelta = new Vector2(880.0f, 340.0f);

        cardGroup = plate.GetComponent<CanvasGroup>();
        cardGroup.interactable = false;
        cardGroup.blocksRaycasts = false;      // the student can click straight through it

        Image plateImage = plate.GetComponent<Image>();
        plateImage.color = AtomixSettings.PanelColour;
        plateImage.raycastTarget = false;

        TMP_Text title = MakeText(plate.transform, "Title", new Vector2(0.0f, 124.0f),
            new Vector2(800.0f, 46.0f), 34.0f, new Color(0.92f, 0.95f, 1.0f, 1.0f));
        title.text = "Welcome to the laboratory";
        title.fontStyle = FontStyles.Bold;

        TMP_Text body = MakeText(plate.transform, "Body", new Vector2(0.0f, -18.0f),
            new Vector2(790.0f, 210.0f), 24.0f, AtomixSettings.BodyTextColour);
        body.alignment = TextAlignmentOptions.TopLeft;
        body.textWrappingMode = TextWrappingModes.Normal;
        body.lineSpacing = 12.0f;
        body.text =
            "You are a chemistry student, and this bench is yours.\n" +
            "\n" +
            "<b>1.</b>  Press <b>B</b> to open the reaction book and choose one of eight experiments.\n" +
            "<b>2.</b>  Pick up the glassware with <b>left click</b>, tilt it with <b>Z</b> and <b>X</b> to pour.\n" +
            "<b>3.</b>  <b>The amount is what is being judged.</b> Too much fails as surely as too little - " +
            "watch the readout at the top of the screen.\n" +
            "<b>4.</b>  When it goes wrong, press <b>Y</b> and the lab assistant will explain the chemistry.";

        TMP_Text footer = MakeText(plate.transform, "Footer", new Vector2(0.0f, -146.0f),
            new Vector2(800.0f, 32.0f), 20.0f, LabPanelBuilder.MutedTextColour);
        footer.text = "<b>H</b> for the full controls   -   <b>F1</b> for settings";

        plate.SetActive(false);
    }

    private void BuildObjective(Transform parent)
    {
        GameObject strip = new GameObject("ObjectiveStrip", typeof(RectTransform));
        strip.transform.SetParent(parent, false);

        objectiveStrip = strip.GetComponent<RectTransform>();
        objectiveStrip.anchorMin = new Vector2(0.5f, 0.0f);
        objectiveStrip.anchorMax = new Vector2(0.5f, 0.0f);
        objectiveStrip.pivot = new Vector2(0.5f, 0.0f);

        // Above the toast band at y=150, so an achievement notice does not sit on top of it.
        objectiveStrip.anchoredPosition = new Vector2(0.0f, 262.0f);
        objectiveStrip.sizeDelta = new Vector2(1000.0f, 40.0f);

        GameObject plate = new GameObject("Plate", typeof(RectTransform), typeof(Image));
        plate.transform.SetParent(strip.transform, false);

        RectTransform plateRect = plate.GetComponent<RectTransform>();
        plateRect.anchorMin = Vector2.zero;
        plateRect.anchorMax = Vector2.one;
        plateRect.offsetMin = Vector2.zero;
        plateRect.offsetMax = Vector2.zero;

        Image plateImage = plate.GetComponent<Image>();
        plateImage.color = new Color(0.04f, 0.05f, 0.08f, 0.62f);
        plateImage.raycastTarget = false;
        objectivePlate = plate;

        objectiveText = MakeText(plate.transform, "Text", Vector2.zero,
            new Vector2(980.0f, 36.0f), 22.0f, new Color(0.80f, 0.87f, 0.96f, 1.0f));

        strip.SetActive(false);
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
}
