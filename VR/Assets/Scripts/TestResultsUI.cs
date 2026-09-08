using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// The report card shown when a testing run finishes.
///
/// Until now the testing scene ended with a single line of text - "You have finished all the
/// tasks!" - and the score strip. Everything needed for a proper debrief was already being
/// recorded by <see cref="ExperimentHistoryManager"/>; nothing was reading it back. This shows the
/// student what they actually did: every task attempted, whether it passed, what went wrong, the
/// score, and a grade - and offers to write it all out as a lab report.
///
/// It watches <see cref="Randomize.stopTesting"/> rather than requiring an edit to that script, so
/// the testing scene's own flow is untouched.
/// </summary>
public class TestResultsUI : MonoBehaviour
{
    [Header("Availability")]
    public string[] enabledScenes = { "TestingPhaseLab" };

    [Header("Placement")]
    public float distanceFromCamera = 1.6f;

    [Header("Input")]
    public KeyCode closeKey = KeyCode.Escape;

    private Canvas resultsCanvas;
    private RectTransform panel;
    private RectTransform rowRoot;
    private TMP_Text statusText;
    private bool isOpen;
    private bool shownForThisRun;

    private Randomize randomizer;
    private CountdownTimer countdown;
    private DateTime runStartedUtc = DateTime.MinValue;
    private bool playerWasEnabled;

    // Reference resolution for the overlay canvas. The layout below is expressed in these
    // units and the CanvasScaler maps them onto whatever the window actually is.
    private const float CanvasWidth = 1840.0f;
    private const float CanvasHeight = 1000.0f;

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
        // A new run starts the moment the testing scene loads.
        shownForThisRun = false;
        randomizer = null;
        countdown = null;
        runStartedUtc = DateTime.UtcNow;
        Close();
    }

    void Update()
    {
        // A question is being typed into the assistant panel; every letter belongs to it.
        if (LabTextInput.IsCapturing)
        {
            return;
        }

        if (!IsEnabledScene())
        {
            if (isOpen)
            {
                Close();
            }
            return;
        }

        if (isOpen)
        {
            if (Input.GetKeyDown(closeKey))
            {
                Close();
            }
            return;
        }

        if (shownForThisRun)
        {
            return;
        }

        if (randomizer == null)
        {
            randomizer = FindFirstObjectByType<Randomize>(FindObjectsInactive.Exclude);
            if (runStartedUtc == DateTime.MinValue)
            {
                runStartedUtc = DateTime.UtcNow;
            }
        }
        if (countdown == null)
        {
            countdown = FindFirstObjectByType<CountdownTimer>(FindObjectsInactive.Exclude);
        }

        if (randomizer != null && randomizer.stopTesting)
        {
            shownForThisRun = true;
            Open();
        }
    }

    private bool IsEnabledScene()
    {
        if (enabledScenes == null || enabledScenes.Length == 0)
        {
            return false;
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
    // OPEN / CLOSE
    // =========================================================

    public void Open()
    {
        LabPanelBuilder.CloseOtherPanels(this);

        EnsureUiBuilt();
        isOpen = true;
        resultsCanvas.gameObject.SetActive(true);
        Rebuild();

        // A report card is read, not aimed at - give the student their cursor back, and stop
        // them wandering off the bench while they read it.
        FirstPersonController.SetCursorLock(false);
        playerWasEnabled = LabPanelBuilder.SuspendPlayer();
    }

    public void Close()
    {
        if (isOpen)
        {
            LabPanelBuilder.ResumePlayer(playerWasEnabled);
            playerWasEnabled = false;
        }

        isOpen = false;
        if (resultsCanvas != null)
        {
            resultsCanvas.gameObject.SetActive(false);
        }
    }

    public bool IsOpen { get { return isOpen; } }

    // =========================================================
    // CONSTRUCTION
    // =========================================================

    private void EnsureUiBuilt()
    {
        if (resultsCanvas != null)
        {
            return;
        }

        // Screen Space Overlay, not a world-space plate.
        //
        // The first version used LabPanelBuilder.CreatePanelCanvas, which hangs the panel in the
        // world 1.6 m in front of the camera. At that distance the whole report card occupied
        // about a fifth of the screen and none of it was legible - the same "postage stamp in the
        // middle of the lab" problem the pause menu already solved by going full-screen. Overlay
        // also rasterises the text at screen pixels instead of scaling it down by 0.001 and back.
        CanvasScaler scaler;
        panel = LabPanelBuilder.CreateFullScreenCanvas(transform, "TestResultsCanvas",
            new Vector2(CanvasWidth, CanvasHeight), 560, 40.0f, out resultsCanvas, out scaler);

        LabPanelBuilder.CreateText("Title", panel, new Vector2(0.0f, CanvasHeight * 0.5f - 60.0f),
            new Vector2(1200.0f, 60.0f), "Testing Phase — Results", 44.0f,
            TextAlignmentOptions.Center, Color.white);

        rowRoot = LabPanelBuilder.CreatePlate("Rows", panel, new Vector2(0.0f, 20.0f),
            new Vector2(CanvasWidth - 140.0f, 620.0f), new Color(0.0f, 0.0f, 0.0f, 0.0f));

        statusText = LabPanelBuilder.CreateText("Status", panel,
            new Vector2(0.0f, -CanvasHeight * 0.5f + 118.0f), new Vector2(CanvasWidth - 160.0f, 56.0f),
            string.Empty, 21.0f, TextAlignmentOptions.Center, LabPanelBuilder.MutedTextColour);

        float buttonY = -CanvasHeight * 0.5f + 62.0f;

        // "Download" first and widest, because it is the one thing a student is most likely to
        // want from this screen and the old "Export lab report" wrote the whole history into a
        // hidden AppData folder rather than this run into somewhere findable.
        LabPanelBuilder.CreateButton("DownloadReport", panel, "Download this test report",
            new Vector2(-470.0f, buttonY), new Vector2(340.0f, 62.0f), 22.0f,
            () =>
            {
                if (statusText != null)
                {
                    statusText.text = ExamReportExporter.ExportRun();
                }
            });

        LabPanelBuilder.CreateButton("OpenFolder", panel, "Open folder",
            new Vector2(-170.0f, buttonY), new Vector2(220.0f, 62.0f), 22.0f,
            () =>
            {
                bool opened = ExamReportExporter.OpenReportFolder();
                if (statusText != null && !opened)
                {
                    statusText.text = "Could not open the reports folder.";
                }
            });

        LabPanelBuilder.CreateButton("ExportAll", panel, "Full history",
            new Vector2(60.0f, buttonY), new Vector2(200.0f, 62.0f), 22.0f,
            () => { if (statusText != null) { statusText.text = LabReportExporter.ExportAll(); } });

        // Retake clears the chosen task count so the setup screen asks again - the student may
        // well want a different length the second time round.
        LabPanelBuilder.CreateButton("Retry", panel, "Retake the test",
            new Vector2(305.0f, buttonY), new Vector2(250.0f, 62.0f), 22.0f,
            () =>
            {
                StaticData.taskCountValue = 0;
                SceneManager.LoadScene("TestingPhaseLab");
            });

        LabPanelBuilder.CreateButton("Menu", panel, "Main menu",
            new Vector2(545.0f, buttonY), new Vector2(190.0f, 62.0f), 22.0f,
            () =>
            {
                FirstPersonController.SetCursorLock(false);
                SceneManager.LoadScene("MainMenuScene");
            });
    }

    // =========================================================
    // CONTENT
    // =========================================================

    private void Rebuild()
    {
        if (rowRoot == null)
        {
            return;
        }

        LabPanelBuilder.ClearChildren(rowRoot);

        // Read from ExamSession rather than the experiment history. The history only ever saw
        // practical reactions - theory questions recorded nothing at all - so a theory-only run
        // used to reach this screen and report that nothing had happened.
        IReadOnlyList<ExamTaskRecord> runTasks = ExamSession.Tasks;
        AtomixCoinBank bank = AtomixCoinBank.Instance;

        float rowWidth = CanvasWidth - 140.0f;
        float y = 240.0f;

        int passed = ExamSession.Passed;
        int total = runTasks.Count;
        string grade = ExamSession.Grade;

        // --- headline -------------------------------------------------------------
        string gradeColour = ColorUtility.ToHtmlStringRGB(
            passed >= ExamSession.Failed ? AtomixSettings.SuccessColour : AtomixSettings.FailureColour);

        LabPanelBuilder.CreateText("Headline", rowRoot, new Vector2(0.0f, y + 66.0f),
            new Vector2(rowWidth, 48.0f),
            "<color=#FFD647>*</color> <b>" + ExamSession.RunCoins + "</b> coins     Passed <b>" +
            passed + "</b> of <b>" + total + "</b>     Grade <color=#" + gradeColour +
            "><b>" + grade + "</b></color>",
            36.0f, TextAlignmentOptions.Center, Color.white);

        // --- rank line ------------------------------------------------------------
        string rankLine = bank.RankName + "   -   " + bank.LifetimeEarned + " coins earned all-time";
        if (bank.CoinsToNextRank > 0)
        {
            rankLine += "   -   " + bank.CoinsToNextRank + " more to reach " + bank.NextRankName;
        }
        if (ExamSession.LongestStreak >= 2)
        {
            rankLine += "   -   best streak this run x" + ExamSession.LongestStreak;
        }

        LabPanelBuilder.CreateText("Rank", rowRoot, new Vector2(0.0f, y + 30.0f),
            new Vector2(rowWidth, 34.0f), rankLine, 23.0f, TextAlignmentOptions.Center,
            LabPanelBuilder.MutedTextColour);

        y -= 6.0f;

        if (total == 0)
        {
            LabPanelBuilder.CreateText("Empty", rowRoot, new Vector2(0.0f, y - 40.0f),
                new Vector2(rowWidth, 120.0f),
                "No tasks were completed in this run.",
                26.0f, TextAlignmentOptions.Center, LabPanelBuilder.MutedTextColour);
            return;
        }

        // --- one row per task ------------------------------------------------------
        float rowHeight = 50.0f;
        int shown = Mathf.Min(total, 10);

        for (int i = 0; i < shown; i++)
        {
            ExamTaskRecord task = runTasks[i];
            float rowY = y - i * (rowHeight + 6.0f);

            Color tint = task.Passed ? AtomixSettings.SuccessColour : AtomixSettings.FailureColour;

            LabPanelBuilder.CreatePlate("Row" + i, rowRoot, new Vector2(0.0f, rowY),
                new Vector2(rowWidth, rowHeight), LabPanelBuilder.RowColour);

            LabPanelBuilder.CreatePlate("Tick" + i, rowRoot,
                new Vector2(-rowWidth * 0.5f + 16.0f, rowY), new Vector2(8.0f, rowHeight), tint);

            LabPanelBuilder.CreateText("Kind" + i, rowRoot,
                new Vector2(-rowWidth * 0.5f + 110.0f, rowY), new Vector2(150.0f, rowHeight),
                task.kind == ExamTaskKind.Theory ? "Theory" : "Practical", 20.0f,
                TextAlignmentOptions.Left, LabPanelBuilder.MutedTextColour);

            LabPanelBuilder.CreateText("Name" + i, rowRoot,
                new Vector2(-rowWidth * 0.5f + 560.0f, rowY), new Vector2(700.0f, rowHeight),
                CleanName(task.title), 22.0f, TextAlignmentOptions.Left,
                AtomixSettings.BodyTextColour);

            LabPanelBuilder.CreateText("Outcome" + i, rowRoot,
                new Vector2(rowWidth * 0.5f - 320.0f, rowY), new Vector2(380.0f, rowHeight),
                task.OutcomeLabel, 21.0f, TextAlignmentOptions.Left, tint);

            LabPanelBuilder.CreateText("Coins" + i, rowRoot,
                new Vector2(rowWidth * 0.5f - 130.0f, rowY), new Vector2(110.0f, rowHeight),
                task.coinsEarned > 0 ? "+" + task.coinsEarned : "-", 21.0f,
                TextAlignmentOptions.Right,
                task.coinsEarned > 0 ? tint : LabPanelBuilder.MutedTextColour);

            LabPanelBuilder.CreateText("Time" + i, rowRoot,
                new Vector2(rowWidth * 0.5f - 35.0f, rowY), new Vector2(90.0f, rowHeight),
                task.secondsTaken.ToString("0") + "s", 21.0f,
                TextAlignmentOptions.Right, LabPanelBuilder.MutedTextColour);
        }

        // --- what to work on -------------------------------------------------------
        string advice = BuildExamAdvice(runTasks);
        if (!string.IsNullOrEmpty(advice))
        {
            float adviceY = y - shown * (rowHeight + 6.0f) - 34.0f;
            LabPanelBuilder.CreateText("Advice", rowRoot, new Vector2(0.0f, adviceY),
                new Vector2(rowWidth, 76.0f), advice, 22.0f, TextAlignmentOptions.Center,
                LabPanelBuilder.MutedTextColour);
        }

        if (statusText != null)
        {
            statusText.text = ExamSession.WasUnaided
                ? "Finished unaided - no coins were spent on help."
                : ExamSession.RunSpent + " coins were spent on help during this run.";
        }

        AwardAchievements(LabPerformanceSummary.FromHistory(CollectRunAttempts()));
    }

    /// <summary>
    /// Advice drawn from the exam record, which - unlike the history-based version - can see the
    /// theory questions and the tasks that timed out.
    /// </summary>
    private static string BuildExamAdvice(IReadOnlyList<ExamTaskRecord> tasks)
    {
        int timedOut = 0;
        int theoryWrong = 0;
        int overdose = 0;
        int underdose = 0;
        int wrongOrder = 0;

        for (int i = 0; i < tasks.Count; i++)
        {
            ExamTaskRecord t = tasks[i];
            if (t.Passed)
            {
                continue;
            }

            if (t.outcome == ExamTaskOutcome.TimedOut) { timedOut++; }

            if (t.kind == ExamTaskKind.Theory)
            {
                theoryWrong++;
                continue;
            }

            string detail = (t.detail ?? string.Empty).ToLowerInvariant();
            if (detail.Contains("too much")) { overdose++; }
            else if (detail.Contains("not enough")) { underdose++; }
            else if (detail.Contains("order")) { wrongOrder++; }
        }

        if (timedOut == 0 && theoryWrong == 0 && overdose == 0 && underdose == 0 && wrongOrder == 0)
        {
            return tasks.Count > 0
                ? "Nothing to correct - every task in this run was answered correctly."
                : string.Empty;
        }

        if (overdose >= underdose && overdose >= wrongOrder && overdose > 0)
        {
            return "Most common mistake: pouring past the mark. Watch the running total and stop early -\n" +
                   "the reading keeps climbing for a moment after you stop tipping.";
        }
        if (underdose >= wrongOrder && underdose > 0)
        {
            return "Most common mistake: stopping short. Give each reagent a moment to settle before\n" +
                   "you decide you are done.";
        }
        if (wrongOrder > 0)
        {
            return "Most common mistake: procedure order. Acid before oxide, water before metal,\n" +
                   "and both solids before any catalyst.";
        }
        if (timedOut > 0 && timedOut >= theoryWrong)
        {
            return timedOut + (timedOut == 1 ? " task ran out of time." : " tasks ran out of time.") +
                   "\nPress [F3] during a task to buy 30 more seconds with your coins.";
        }

        return theoryWrong + (theoryWrong == 1 ? " theory question was" : " theory questions were") +
               " answered incorrectly.\nAsk the lab assistant to explain the chemistry behind them.";
    }

    /// <summary>Attempts recorded since this run of the testing scene began.</summary>
    private List<ExperimentAttempt> CollectRunAttempts()
    {
        List<ExperimentAttempt> result = new List<ExperimentAttempt>();

        ExperimentHistoryManager manager = ExperimentHistoryManager.Instance;
        if (manager == null)
        {
            return result;
        }

        List<ExperimentAttempt> all = manager.GetHistory();
        if (all == null)
        {
            return result;
        }

        for (int i = all.Count - 1; i >= 0; i--)
        {
            ExperimentAttempt attempt = all[i];
            if (attempt == null || attempt.outcome == ExperimentOutcome.Abandoned)
            {
                continue;
            }

            // Timestamps are written in local time; compare on the same footing.
            if (attempt.Timestamp.ToUniversalTime() < runStartedUtc)
            {
                break;   // history is chronological, so everything older follows
            }

            result.Add(attempt);
        }

        result.Reverse();
        return result;
    }

    private static string CleanName(string reactionName)
    {
        if (string.IsNullOrEmpty(reactionName))
        {
            return "Experiment";
        }
        return reactionName.Replace(" [Test]", string.Empty);
    }

    private static string BuildAdvice(LabPerformanceSummary summary)
    {
        if (summary.failures == 0 && summary.successes > 0)
        {
            return "Nothing to correct — every experiment you completed was within tolerance.";
        }

        string worst = null;
        int worstCount = 0;
        foreach (KeyValuePair<string, int> pair in summary.mistakeCounts)
        {
            if (pair.Value > worstCount)
            {
                worstCount = pair.Value;
                worst = pair.Key;
            }
        }

        if (worst == null)
        {
            return string.Empty;
        }

        if (worst.Contains("too much"))
        {
            return "Most common mistake: pouring past the mark. Watch the running total and stop early —\n" +
                   "the reading keeps climbing for a moment after you stop tipping.";
        }
        if (worst.Contains("not enough"))
        {
            return "Most common mistake: stopping short. Give each reagent a moment to settle before\n" +
                   "you decide you are done.";
        }
        if (worst.Contains("order"))
        {
            return "Most common mistake: procedure order. Acid before oxide, water before metal,\n" +
                   "and both solids before any catalyst.";
        }
        return "Most common mistake: " + worst + ".";
    }

    private void AwardAchievements(LabPerformanceSummary summary)
    {
        AchievementSystem achievements = AchievementSystem.Instance;
        if (achievements == null)
        {
            return;
        }

        achievements.Unlock("exam_pass");
        if (summary.Grade == "A")
        {
            achievements.Unlock("exam_distinction");
        }
    }
}
