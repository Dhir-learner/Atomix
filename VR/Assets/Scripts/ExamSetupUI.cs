using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// The screen shown when the testing scene opens, asking how many tasks the run should be.
///
/// Before this, every run was the same length: <see cref="Randomize"/> stopped at a hard-coded
/// `reactionsDone.Count >= 10`, so a student who wanted a quick three-question check had to sit
/// through ten, and there was no way to say so. The count now comes from here.
///
/// It also holds the clock. <see cref="CountdownTimer"/> starts counting down the moment the scene
/// loads, so without this the first task would already be part-spent by the time the student had
/// finished reading the options.
///
/// Screen Space Overlay, like the pause menu and the report card - a world-space plate at 1.5 m is
/// a postage stamp in the middle of the lab.
/// </summary>
public class ExamSetupUI : MonoBehaviour
{
    [Header("Availability")]
    public string[] enabledScenes = { "TestingPhaseLab" };

    [Header("Choices")]
    [Tooltip("Task counts offered. Any that exceed what the chosen mode has are hidden.")]
    public int[] choices = { 3, 5, 8, 10 };

    private Canvas canvas;
    private RectTransform panel;
    private RectTransform choiceRoot;
    private TMP_Text summaryText;

    private CountdownTimer countdown;
    private bool isOpen;
    private bool shownForThisScene;
    private bool playerWasEnabled;
    private int selected;

    private const float ReferenceWidth = 1920.0f;
    private const float ReferenceHeight = 1080.0f;

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
        countdown = null;
        shownForThisScene = false;

        if (!IsEnabledScene(scene.name))
        {
            Close(false);
        }
    }

    void Update()
    {
        if (LabTextInput.IsCapturing)
        {
            return;
        }

        if (!IsEnabledScene(SceneManager.GetActiveScene().name))
        {
            return;
        }

        if (isOpen)
        {
            HoldTheClock();
            return;
        }

        if (shownForThisScene)
        {
            return;
        }

        // Wait for the scene's own timer to exist before opening, so the clock can be held.
        if (countdown == null)
        {
            countdown = FindFirstObjectByType<CountdownTimer>(FindObjectsInactive.Exclude);
            if (countdown == null)
            {
                return;
            }
        }

        shownForThisScene = true;
        Open();
    }

    /// <summary>
    /// Keeps the countdown parked while the student is choosing.
    ///
    /// Re-applied every frame rather than set once: CountdownTimer's own Update flips `continua`
    /// back on whenever its `reset` flag is raised, and Randomize raises that as it draws the
    /// first task.
    /// </summary>
    private void HoldTheClock()
    {
        if (countdown != null)
        {
            countdown.continua = false;
        }
    }

    // =========================================================
    // OPEN / CLOSE
    // =========================================================

    public void Open()
    {
        LabPanelBuilder.CloseOtherPanels(this);

        selected = Mathf.Clamp(StaticData.TaskCount, 1, StaticData.MaxTasksForMode);

        EnsureUiBuilt();
        isOpen = true;
        canvas.gameObject.SetActive(true);
        Rebuild();

        FirstPersonController.SetCursorLock(false);
        playerWasEnabled = LabPanelBuilder.SuspendPlayer();
    }

    /// <summary>Closes and gives the player back, for <see cref="LabPanelBuilder.CloseOtherPanels"/>.</summary>
    public void Close()
    {
        Close(true);
    }

    public void Close(bool restorePlayer)
    {
        if (isOpen && restorePlayer)
        {
            LabPanelBuilder.ResumePlayer(playerWasEnabled);
            FirstPersonController.SetCursorLock(true);
        }

        playerWasEnabled = false;
        isOpen = false;

        if (canvas != null)
        {
            canvas.gameObject.SetActive(false);
        }
    }

    public bool IsOpen { get { return isOpen; } }

    private void StartTest()
    {
        StaticData.taskCountValue = Mathf.Clamp(selected, 1, StaticData.MaxTasksForMode);

        Close(true);

        // Give the first task a full clock, since it has been held at zero speed until now.
        if (countdown != null)
        {
            countdown.reset = true;
        }
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

        CanvasScaler scaler;
        panel = LabPanelBuilder.CreateFullScreenCanvas(transform, "ExamSetupCanvas",
            new Vector2(ReferenceWidth, ReferenceHeight), 620, 260.0f, out canvas, out scaler);

        LabPanelBuilder.CreateText("Title", panel, new Vector2(0.0f, 190.0f),
            new Vector2(900.0f, 60.0f), "Testing Phase", 44.0f,
            TextAlignmentOptions.Center, Color.white);

        LabPanelBuilder.CreateText("Question", panel, new Vector2(0.0f, 128.0f),
            new Vector2(900.0f, 40.0f), "How many tasks should this test have?", 26.0f,
            TextAlignmentOptions.Center, AtomixSettings.BodyTextColour);

        summaryText = LabPanelBuilder.CreateText("Summary", panel, new Vector2(0.0f, -110.0f),
            new Vector2(900.0f, 80.0f), string.Empty, 22.0f,
            TextAlignmentOptions.Center, LabPanelBuilder.MutedTextColour);

        // The choice row is rebuilt on every Open, not built once here: which counts are on offer
        // depends on the task mix, and the student can change that in Settings and come back.
        choiceRoot = LabPanelBuilder.CreatePlate("Choices", panel, new Vector2(0.0f, 40.0f),
            new Vector2(1100.0f, 80.0f), new Color(0.0f, 0.0f, 0.0f, 0.0f));

        LabPanelBuilder.CreateButton("Start", panel, "Start test",
            new Vector2(-130.0f, -196.0f), new Vector2(240.0f, 62.0f), 24.0f, StartTest);

        LabPanelBuilder.CreateButton("Menu", panel, "Main menu",
            new Vector2(130.0f, -196.0f), new Vector2(240.0f, 62.0f), 24.0f,
            () =>
            {
                Close(false);
                FirstPersonController.SetCursorLock(false);
                SceneManager.LoadScene("MainMenuScene");
            });

        canvas.gameObject.SetActive(false);
    }

    private void BuildChoiceButtons()
    {
        if (choiceRoot == null)
        {
            return;
        }

        LabPanelBuilder.ClearChildren(choiceRoot);

        int max = StaticData.MaxTasksForMode;

        // Only offer counts the current mode can actually fill, plus "all of them".
        System.Collections.Generic.List<int> offered = new System.Collections.Generic.List<int>();
        for (int i = 0; i < choices.Length; i++)
        {
            if (choices[i] > 0 && choices[i] < max && !offered.Contains(choices[i]))
            {
                offered.Add(choices[i]);
            }
        }
        offered.Add(max);

        const float buttonWidth = 150.0f;
        const float gap = 18.0f;
        float total = offered.Count * buttonWidth + (offered.Count - 1) * gap;
        float x = -total * 0.5f + buttonWidth * 0.5f;

        for (int i = 0; i < offered.Count; i++)
        {
            int count = offered[i];
            string label = count == max ? "All " + max : count.ToString();

            // The chosen one is tinted, so the panel shows what will happen when Start is pressed
            // rather than leaving the student to remember which button they last clicked.
            Color? tint = count == selected
                ? (Color?)AtomixSettings.SuccessColour
                : null;

            LabPanelBuilder.CreateButton("Choice" + count, choiceRoot, label,
                new Vector2(x, 0.0f), new Vector2(buttonWidth, 70.0f), 26.0f,
                () => { selected = count; Rebuild(); }, tint);

            x += buttonWidth + gap;
        }
    }

    private void Rebuild()
    {
        BuildChoiceButtons();

        if (summaryText == null)
        {
            return;
        }

        string mode;
        switch (StaticData.includedTasksValue)
        {
            case 0: mode = "practical experiments only"; break;
            case 1: mode = "theory questions only"; break;
            default: mode = "practical experiments and theory questions"; break;
        }

        int count = Mathf.Clamp(selected, 1, StaticData.MaxTasksForMode);

        summaryText.text =
            "<b>" + count + "</b> task" + (count == 1 ? "" : "s") + "   -   " + mode +
            "\n60 seconds each   -   spend coins on help with [F2] [F3] [F4]" +
            "\n<size=19>Change the task mix in Settings from the main menu.</size>";
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
}
