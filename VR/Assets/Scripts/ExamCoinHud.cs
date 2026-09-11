using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// The coin strip and help shop in the testing scene.
///
/// The testing scene already had a score number on a canvas, but it did nothing except count up
/// and reset. This puts the wallet on screen - run coins, balance, rank - and lets the student
/// spend what they have earned, which is the point of earning it.
///
/// Three purchases, all priced so that a good run pays for roughly one of them:
///
///   F2   Hint          40 coins   what this reaction needs, from the offline knowledge base
///   F3   +30 seconds   60 coins   more time on the current task
///   F4   Skip task    100 coins   give up on this one and move on
///
/// Every purchase is recorded against the task it was bought on, so the exported report shows
/// which answers were unaided - a run finished without buying anything earns a bonus and says so.
///
/// Keys rather than buttons because the testing scene keeps the cursor locked for the crosshair,
/// exactly as the rest of the lab's runtime panels do. F2-F4 were checked against every KeyCode in
/// the project before use.
/// </summary>
public class ExamCoinHud : MonoBehaviour
{
    [Header("Availability")]
    public string[] enabledScenes = { "TestingPhaseLab" };

    [Header("Input")]
    public KeyCode hintKey = KeyCode.F2;
    public KeyCode extraTimeKey = KeyCode.F3;
    public KeyCode skipKey = KeyCode.F4;

    [Header("Behaviour")]
    [Tooltip("Seconds a purchase message or coin award stays on screen.")]
    public float messageSeconds = 5.0f;

    private Canvas canvas;
    private TMP_Text walletText;
    private TMP_Text shopText;
    private TMP_Text messageText;

    private Randomize randomizer;
    private CountdownTimer countdown;

    private int lastTaskCount;
    private float messageUntil;

    private static readonly Color CoinColour = new Color(1.00f, 0.84f, 0.28f, 1.0f);
    private static readonly Color MutedColour = new Color(0.70f, 0.76f, 0.84f, 1.0f);
    private static readonly Color GoodColour = new Color(0.42f, 0.92f, 0.55f, 1.0f);
    private static readonly Color BadColour = new Color(1.00f, 0.48f, 0.42f, 1.0f);

    void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        randomizer = null;
        countdown = null;
        lastTaskCount = 0;
        messageUntil = 0.0f;
        SetVisible(IsEnabledScene(scene.name));
    }

    void Update()
    {
        // A question is being typed into the assistant panel; every letter belongs to it.
        if (LabTextInput.IsCapturing)
        {
            return;
        }

        bool active = IsEnabledScene(SceneManager.GetActiveScene().name);
        SetVisible(active);

        if (!active)
        {
            return;
        }

        EnsureUiBuilt();
        EnsureReferences();
        AnnounceNewAwards();
        HandlePurchaseInput();
        RefreshUi();
    }

    private void EnsureReferences()
    {
        if (randomizer == null)
        {
            randomizer = FindFirstObjectByType<Randomize>(FindObjectsInactive.Exclude);
        }
        if (countdown == null)
        {
            countdown = FindFirstObjectByType<CountdownTimer>(FindObjectsInactive.Exclude);
        }
    }

    /// <summary>
    /// Says what a task just paid, including any bonus. Coins that appear without explanation are
    /// just a number going up; naming the reason is what makes the streak worth chasing.
    /// </summary>
    private void AnnounceNewAwards()
    {
        int count = ExamSession.Tasks.Count;
        if (count <= lastTaskCount)
        {
            lastTaskCount = count;
            return;
        }

        ExamTaskRecord latest = ExamSession.Tasks[count - 1];
        lastTaskCount = count;

        if (!latest.Passed)
        {
            ShowMessage("No coins - " + latest.OutcomeLabel.ToLowerInvariant(), BadColour);
            return;
        }

        string reason = "+" + latest.coinsEarned + " coins";

        if (latest.secondsRemaining >= 45.0f)
        {
            reason += "  (finished with " + Mathf.FloorToInt(latest.secondsRemaining) + "s to spare)";
        }

        if (ExamSession.CurrentStreak == 3)
        {
            reason += "  +streak x3 bonus";
        }
        else if (ExamSession.CurrentStreak == 5)
        {
            reason += "  +streak x5 bonus";
        }

        ShowMessage(reason, GoodColour);
    }

    private void HandlePurchaseInput()
    {
        if (Input.GetKeyDown(hintKey))
        {
            BuyHint();
        }
        else if (Input.GetKeyDown(extraTimeKey))
        {
            BuyExtraTime();
        }
        else if (Input.GetKeyDown(skipKey))
        {
            BuySkip();
        }
    }

    private bool TakePayment(int price, string label)
    {
        if (!AtomixCoinBank.Instance.Spend(price))
        {
            ShowMessage("Not enough coins - " + label + " costs " + price +
                        ", you have " + AtomixCoinBank.Instance.Balance + ".", BadColour);
            return false;
        }

        ExamSession.RecordHelpPurchase(label, price);
        return true;
    }

    private void BuyHint()
    {
        int taskId = randomizer != null ? randomizer.currentReactionInTestPhase : -1;

        // Once the run is over the last task's number is still here, but there is nothing left
        // to use a hint on.
        if (randomizer != null && randomizer.stopTesting)
        {
            ShowMessage("The test is over - there is no task left to use a hint on.", MutedColour);
            return;
        }

        if (taskId < 1 || taskId > 8)
        {
            // A hint for a multiple-choice question would just be the answer.
            ShowMessage("Hints are only available on practical experiments.", MutedColour);
            return;
        }

        if (!TakePayment(AtomixCoinBank.PriceHint, "hint"))
        {
            return;
        }

        // Deliberately the procedure, not the quantities: knowing the right amount is the thing
        // the testing scene exists to measure, and selling it would sell the answer.
        string hint = ChemistryKnowledgeBase.ProcedureHint(taskId);
        ShowMessage("HINT: " + hint, CoinColour);
    }

    private void BuyExtraTime()
    {
        if (countdown == null || !countdown.continua)
        {
            ShowMessage("There is no running task to add time to.", MutedColour);
            return;
        }

        if (!TakePayment(AtomixCoinBank.PriceExtraTime, "+30 seconds"))
        {
            return;
        }

        countdown.AddTime(AtomixCoinBank.ExtraTimeSeconds);
        ShowMessage("+30 seconds added to this task.", CoinColour);
    }

    private void BuySkip()
    {
        if (randomizer == null || randomizer.stopTesting)
        {
            return;
        }

        if (!TakePayment(AtomixCoinBank.PriceSkip, "skip"))
        {
            return;
        }

        int taskId = randomizer.currentReactionInTestPhase;

        ExamSession.RecordTask(
            taskId >= 9 ? ExamTaskKind.Theory : ExamTaskKind.Practical,
            taskId,
            ExamSession.TitleForTask(taskId),
            string.Empty,
            "Skipped by spending coins",
            ExamTaskOutcome.Skipped,
            0.0f,
            countdown != null ? countdown.SecondsOnTask : 0.0f);

        if (countdown != null)
        {
            countdown.continua = false;
            countdown.wasScored = true;
        }

        randomizer.generateNewReaction = true;
        ShowMessage("Task skipped.", CoinColour);
    }

    private void ShowMessage(string text, Color colour)
    {
        if (messageText == null)
        {
            return;
        }

        messageText.text = text;
        messageText.color = colour;
        messageUntil = Time.unscaledTime + messageSeconds;
    }

    // =====================================================================================
    // UI
    // =====================================================================================

    private void RefreshUi()
    {
        AtomixCoinBank bank = AtomixCoinBank.Instance;

        if (walletText != null)
        {
            string streak = ExamSession.CurrentStreak >= 2
                ? "   <color=#6BF08C>streak x" + ExamSession.CurrentStreak + "</color>"
                : string.Empty;

            // The scene has its own coin pill showing the run total, so this leads with the
            // balance - the number that decides whether you can afford anything below.
            walletText.text =
                "<color=#FFD647>*</color> <b>" + bank.Balance + "</b> to spend" +
                "   -   " + bank.RankName + streak;
        }

        if (shopText != null)
        {
            shopText.text =
                Price(hintKey, "Hint", AtomixCoinBank.PriceHint) + "   " +
                Price(extraTimeKey, "+30s", AtomixCoinBank.PriceExtraTime) + "   " +
                Price(skipKey, "Skip", AtomixCoinBank.PriceSkip);
        }

        if (messageText != null && Time.unscaledTime > messageUntil && messageText.text.Length > 0)
        {
            messageText.text = string.Empty;
        }
    }

    /// <summary>Greys out an option that cannot currently be afforded, rather than hiding it.</summary>
    private string Price(KeyCode key, string label, int cost)
    {
        bool affordable = AtomixCoinBank.Instance.CanAfford(cost);
        string colour = affordable ? "#DCE4EE" : "#5A6270";

        return "<color=" + colour + ">[" + key + "] " + label + " " + cost + "</color>";
    }

    private void SetVisible(bool visible)
    {
        if (canvas != null && canvas.gameObject.activeSelf != visible)
        {
            canvas.gameObject.SetActive(visible);
        }
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

    private void EnsureUiBuilt()
    {
        if (canvas != null)
        {
            return;
        }

        GameObject canvasObject = new GameObject("ExamCoinHud",
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);

        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        // Above the scene's own task canvases, below the end-of-test report card.
        canvas.sortingOrder = 500;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920.0f, 1080.0f);

        // The strip must never eat a crosshair click meant for a beaker.
        canvasObject.GetComponent<GraphicRaycaster>().enabled = false;

        // Bottom-left, not top-centre.
        //
        // The first version of this strip was anchored top-centre at full width, which put it
        // straight through the testing scene's own authored canvases: the reaction title
        // ("2FeSO4 -> Fe2O3 + SO2 + SO3 [Test]"), the free-hand tracker line ("Heating 0.0 s")
        // and the scene's coin pill all live in that band. Four separate texts ended up drawn on
        // top of one another and none of them was readable.
        //
        // The bottom-left corner is the one region no authored canvas in this scene uses.
        GameObject plate = new GameObject("Plate", typeof(RectTransform), typeof(Image));
        plate.transform.SetParent(canvasObject.transform, false);
        RectTransform plateRect = plate.GetComponent<RectTransform>();
        plateRect.anchorMin = new Vector2(0.0f, 0.0f);
        plateRect.anchorMax = new Vector2(0.0f, 0.0f);
        plateRect.pivot = new Vector2(0.0f, 0.0f);
        plateRect.anchoredPosition = new Vector2(22.0f, 22.0f);

        // Wide enough for a full wallet line - balance, rank and streak - at 20 px. At 470 the
        // longer rank names auto-shrank the line to 14 px.
        plateRect.sizeDelta = new Vector2(540.0f, 80.0f);

        Image plateImage = plate.GetComponent<Image>();
        plateImage.color = new Color(0.04f, 0.05f, 0.08f, 0.78f);
        plateImage.raycastTarget = false;

        walletText = CreateText("Wallet", plate.transform, new Vector2(14.0f, -10.0f),
            new Vector2(512.0f, 30.0f), 20.0f, Color.white);
        shopText = CreateText("Shop", plate.transform, new Vector2(14.0f, -44.0f),
            new Vector2(512.0f, 26.0f), 17.0f, MutedColour);

        // The message sits directly above the plate, in the same column, so a purchase or an
        // award reads as coming from it rather than appearing at random on the screen.
        messageText = CreateText("Message", canvasObject.transform, Vector2.zero,
            new Vector2(680.0f, 76.0f), 20.0f, CoinColour);
        RectTransform messageRect = messageText.rectTransform;
        messageRect.anchorMin = new Vector2(0.0f, 0.0f);
        messageRect.anchorMax = new Vector2(0.0f, 0.0f);
        messageRect.pivot = new Vector2(0.0f, 0.0f);
        messageRect.anchoredPosition = new Vector2(22.0f, 112.0f);
        messageText.alignment = TextAlignmentOptions.BottomLeft;
        messageText.text = string.Empty;
    }

    private TMP_Text CreateText(string name, Transform parent, Vector2 position,
                                Vector2 size, float fontSize, Color colour)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.0f, 1.0f);
        rect.anchorMax = new Vector2(0.0f, 1.0f);
        rect.pivot = new Vector2(0.0f, 1.0f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.color = colour;
        text.alignment = TextAlignmentOptions.Left;

        // The strip is narrow and the rank names vary in length; without this a long line
        // silently wraps behind the plate instead of shrinking to fit it.
        // Allowed to shrink a little, never below what can be read at a glance mid-experiment.
        text.enableAutoSizing = true;
        text.fontSizeMax = fontSize;
        text.fontSizeMin = Mathf.Max(fontSize * 0.85f, 15.0f);
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
        text.richText = true;
        return text;
    }
}
