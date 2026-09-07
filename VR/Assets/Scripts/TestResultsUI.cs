using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

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

    private const float CanvasWidth = 1240.0f;
    private const float CanvasHeight = 840.0f;

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
        LabPanelBuilder.FaceCamera(resultsCanvas, distanceFromCamera);
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

        panel = LabPanelBuilder.CreatePanelCanvas(transform, "TestResultsCanvas",
            new Vector2(CanvasWidth, CanvasHeight),
            new Vector2(CanvasWidth - 40.0f, CanvasHeight - 40.0f),
            560, out resultsCanvas);

        LabPanelBuilder.CreateText("Title", panel, new Vector2(0.0f, CanvasHeight * 0.5f - 60.0f),
            new Vector2(900.0f, 50.0f), "Testing Phase — Results", 36.0f,
            TextAlignmentOptions.Center, Color.white);

        rowRoot = LabPanelBuilder.CreatePlate("Rows", panel, new Vector2(0.0f, 20.0f),
            new Vector2(CanvasWidth - 140.0f, 520.0f), new Color(0.0f, 0.0f, 0.0f, 0.0f));

        statusText = LabPanelBuilder.CreateText("Status", panel,
            new Vector2(0.0f, -CanvasHeight * 0.5f + 118.0f), new Vector2(CanvasWidth - 160.0f, 56.0f),
            string.Empty, 18.0f, TextAlignmentOptions.Center, LabPanelBuilder.MutedTextColour);

        float buttonY = -CanvasHeight * 0.5f + 62.0f;
        LabPanelBuilder.CreateButton("Export", panel, "Export lab report",
            new Vector2(-230.0f, buttonY), new Vector2(280.0f, 52.0f), 20.0f,
            () => { if (statusText != null) { statusText.text = LabReportExporter.ExportAll(); } });

        LabPanelBuilder.CreateButton("Retry", panel, "Run the test again",
            new Vector2(70.0f, buttonY), new Vector2(280.0f, 52.0f), 20.0f,
            () => SceneManager.LoadScene("TestingPhaseLab"));

        LabPanelBuilder.CreateButton("Menu", panel, "Main menu",
            new Vector2(340.0f, buttonY), new Vector2(220.0f, 52.0f), 20.0f,
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

        List<ExperimentAttempt> runAttempts = CollectRunAttempts();
        LabPerformanceSummary summary = LabPerformanceSummary.FromHistory(runAttempts);

        float rowWidth = CanvasWidth - 140.0f;
        float y = 240.0f;

        // --- headline -------------------------------------------------------------
        string gradeColour = ColorUtility.ToHtmlStringRGB(
            summary.successes >= summary.failures ? AtomixSettings.SuccessColour : AtomixSettings.FailureColour);

        int score = countdown != null ? countdown.Score : 0;

        LabPanelBuilder.CreateText("Headline", rowRoot, new Vector2(0.0f, y + 46.0f),
            new Vector2(rowWidth, 48.0f),
            "Score <b>" + score + "</b>     Passed <b>" + summary.successes + "</b> of <b>" +
            (summary.successes + summary.failures) + "</b>     Grade <color=#" + gradeColour +
            "><b>" + summary.Grade + "</b></color>",
            28.0f, TextAlignmentOptions.Center, Color.white);

        y -= 6.0f;

        if (runAttempts.Count == 0)
        {
            LabPanelBuilder.CreateText("Empty", rowRoot, new Vector2(0.0f, y - 40.0f),
                new Vector2(rowWidth, 120.0f),
                "No graded experiments were recorded in this run.\n\n" +
                "Tasks that timed out before anything was poured are not scored.",
                20.0f, TextAlignmentOptions.Center, LabPanelBuilder.MutedTextColour);
            return;
        }

        // --- one row per attempt ---------------------------------------------------
        float rowHeight = 46.0f;
        for (int i = 0; i < runAttempts.Count && i < 10; i++)
        {
            ExperimentAttempt attempt = runAttempts[i];
            float rowY = y - i * (rowHeight + 6.0f);

            bool passed = attempt.outcome == ExperimentOutcome.Success;
            Color tint = passed ? AtomixSettings.SuccessColour : AtomixSettings.FailureColour;

            LabPanelBuilder.CreatePlate("Row" + i, rowRoot, new Vector2(0.0f, rowY),
                new Vector2(rowWidth, rowHeight), LabPanelBuilder.RowColour);

            LabPanelBuilder.CreatePlate("Tick" + i, rowRoot,
                new Vector2(-rowWidth * 0.5f + 16.0f, rowY), new Vector2(8.0f, rowHeight), tint);

            LabPanelBuilder.CreateText("Name" + i, rowRoot,
                new Vector2(-rowWidth * 0.5f + 340.0f, rowY), new Vector2(620.0f, rowHeight),
                CleanName(attempt.reactionName), 19.0f, TextAlignmentOptions.Left,
                AtomixSettings.BodyTextColour);

            LabPanelBuilder.CreateText("Outcome" + i, rowRoot,
                new Vector2(rowWidth * 0.5f - 300.0f, rowY), new Vector2(420.0f, rowHeight),
                LabReportExporter.Describe(attempt.outcome), 18.0f, TextAlignmentOptions.Left, tint);

            LabPanelBuilder.CreateText("Time" + i, rowRoot,
                new Vector2(rowWidth * 0.5f - 60.0f, rowY), new Vector2(100.0f, rowHeight),
                attempt.durationSeconds.ToString("0") + "s", 18.0f,
                TextAlignmentOptions.Right, LabPanelBuilder.MutedTextColour);
        }

        // --- what to work on -------------------------------------------------------
        string advice = BuildAdvice(summary);
        if (!string.IsNullOrEmpty(advice))
        {
            float adviceY = y - Mathf.Min(runAttempts.Count, 10) * (rowHeight + 6.0f) - 40.0f;
            LabPanelBuilder.CreateText("Advice", rowRoot, new Vector2(0.0f, adviceY),
                new Vector2(rowWidth, 70.0f), advice, 19.0f, TextAlignmentOptions.Center,
                LabPanelBuilder.MutedTextColour);
        }

        if (statusText != null)
        {
            statusText.text = "Every attempt above is already saved to your experiment history.";
        }

        AwardAchievements(summary);
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
