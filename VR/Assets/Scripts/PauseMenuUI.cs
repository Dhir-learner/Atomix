using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// The pause / settings menu, opened with F1.
///
/// Atomix had no way to change anything at runtime: mouse sensitivity, field of view and volume
/// were fixed at whatever the scripts shipped with, and the only way back to the main menu was the
/// exit sign in the corner of the lab. For a teaching tool used on whatever machine a classroom
/// happens to have, that is the first thing a student needs and the last thing they were given.
///
/// Three tabs - Settings, Achievements, Controls - drawn on a <b>full-screen overlay</b> canvas
/// rather than a world-space plate. The other panels float 1.5 m in front of the camera, which is
/// right for something you read beside the glassware, but a settings screen is a lot of small
/// controls read head-on: at that distance it has to shrink to fit the field of view, which left it
/// a postage stamp in the middle of the lab. Overlay also rasterises text at screen pixels instead
/// of scaling it down by 0.001 and back up, so it stays crisp at any resolution.
///
/// It deliberately does not touch <c>Time.timeScale</c>. Several reaction scripts sequence their
/// effects with <c>DateTime.Now</c>, which ignores the time scale, so freezing time would desync
/// them. The testing countdown is paused directly instead, which is the only clock that would be
/// unfair to leave running.
/// </summary>
public class PauseMenuUI : MonoBehaviour
{
    private enum Tab { Settings, Achievements, Controls }

    [Header("Input")]
    public KeyCode toggleKey = KeyCode.F1;

    [Header("Layout")]
    [Tooltip("Design resolution the layout is authored against; the canvas scales to the window.")]
    public Vector2 referenceResolution = new Vector2(1920.0f, 1080.0f);

    [Tooltip("Pixels of clear space between the panel and the edge of the screen.")]
    public float screenMargin = 70.0f;

    [Header("Availability")]
    [Tooltip("Scenes where F1 does nothing. Empty by default - the menu is useful everywhere, and " +
             "a student whose mouse is too fast needs to fix it before entering the lab, not after.")]
    public string[] blockedScenes = new string[0];

    private Canvas menuCanvas;
    private CanvasScaler menuScaler;
    private RectTransform panel;
    private RectTransform bodyRoot;
    private TMP_Text statusText;
    private Button quitToMenuButton;
    private Button restartButton;
    private readonly List<Button> tabButtons = new List<Button>();
    private Tab activeTab = Tab.Settings;
    private bool isOpen;

    private bool restoreCursorLock;
    private bool playerWasEnabled;
    private CountdownTimer pausedCountdown;
    private bool countdownWasRunning;

    // Layout bands in the 1920x1080 design space, measured from the panel centre. Kept as named
    // constants because the previous version put the status line 2 px from the Reset button.
    private const float TitleY = 400.0f;
    private const float TabsY = 300.0f;
    private const float BodyHeight = 640.0f;
    /// <summary>Top of the usable content band, safely below the tab row.</summary>
    private const float BodyTop = 262.0f;
    /// <summary>Heading centre to the first row centre beneath it.</summary>
    private const float HeaderStep = 42.0f;
    /// <summary>Row centre to row centre inside a section.</summary>
    private const float RowStep = 52.0f;
    /// <summary>Last row of a section to the next section heading.</summary>
    private const float SectionStep = 46.0f;
    private const float StatusY = -352.0f;
    private const float FooterY = -424.0f;
    private const float RowWidth = 1100.0f;

    /// <summary>
    /// The settings tab is two columns. It was one, and it fitted - right up until the audio
    /// buses, the comfort options and the display controls were added, at which point a single
    /// column ran off the bottom of the screen. Splitting it is also simply a better settings
    /// screen: controls and comfort on the left, everything about the machine on the right.
    /// </summary>
    private const float ColumnWidth = 740.0f;
    private const float ColumnOffset = 395.0f;

    void Update()
    {
        // A question is being typed into the assistant panel; every letter belongs to it.
        if (LabTextInput.IsCapturing)
        {
            return;
        }

        if (IsBlockedScene())
        {
            if (isOpen)
            {
                Close();
            }
            return;
        }

        if (Input.GetKeyDown(toggleKey))
        {
            Toggle();
        }
    }

    private bool IsBlockedScene()
    {
        if (blockedScenes == null || blockedScenes.Length == 0)
        {
            return false;
        }

        string active = SceneManager.GetActiveScene().name;
        for (int i = 0; i < blockedScenes.Length; i++)
        {
            if (blockedScenes[i] == active)
            {
                return true;
            }
        }
        return false;
    }

    public void Toggle()
    {
        if (isOpen)
        {
            Close();
        }
        else
        {
            Open();
        }
    }

    public void Open()
    {
        if (ReactionLearningController.IsAnyPanelVisible)
        {
            return;
        }

        LabPanelBuilder.CloseOtherPanels(this);

        EnsureUiBuilt();
        isOpen = true;
        menuCanvas.gameObject.SetActive(true);
        ApplyScale();

        // A settings menu is a lot of small controls, so it gets a real mouse pointer rather than
        // the crosshair. DesktopUIInput enables the UI input module exactly when the cursor is
        // unlocked, so the buttons become clickable the moment we release it.
        restoreCursorLock = FirstPersonController.IsCursorLocked;
        FirstPersonController.SetCursorLock(false);

        // ...and freeze the player, because movement is otherwise still allowed while unlocked.
        playerWasEnabled = LabPanelBuilder.SuspendPlayer();

        // Nothing to go back to when the menu already is the main menu.
        if (quitToMenuButton != null)
        {
            quitToMenuButton.gameObject.SetActive(
                SceneManager.GetActiveScene().name != "MainMenuScene");
        }

        if (restartButton != null)
        {
            LabRetryController retry = GetComponent<LabRetryController>();
            restartButton.gameObject.SetActive(retry != null && retry.CanRestart);
        }

        PauseExamCountdown();
        Rebuild();
    }

    public void Close()
    {
        if (!isOpen)
        {
            return;
        }

        isOpen = false;
        if (menuCanvas != null)
        {
            menuCanvas.gameObject.SetActive(false);
        }

        ResumeExamCountdown();
        LabPanelBuilder.ResumePlayer(playerWasEnabled);
        playerWasEnabled = false;
        FirstPersonController.SetCursorLock(restoreCursorLock);
    }

    public bool IsOpen { get { return isOpen; } }

    private void ApplyScale()
    {
        if (menuScaler != null)
        {
            menuScaler.scaleFactor = AtomixSettings.UiScale;
        }
    }

    /// <summary>
    /// Stops the testing-phase clock while the menu is up. Only the countdown is paused, not the
    /// whole game, because the reaction scripts time their visuals off the wall clock.
    /// </summary>
    private void PauseExamCountdown()
    {
        pausedCountdown = FindFirstObjectByType<CountdownTimer>(FindObjectsInactive.Exclude);
        if (pausedCountdown != null)
        {
            countdownWasRunning = pausedCountdown.continua;
            pausedCountdown.continua = false;
        }
    }

    private void ResumeExamCountdown()
    {
        if (pausedCountdown != null && countdownWasRunning)
        {
            pausedCountdown.continua = true;
        }
        pausedCountdown = null;
        countdownWasRunning = false;
    }

    // =========================================================
    // CONSTRUCTION
    // =========================================================

    private void EnsureUiBuilt()
    {
        if (menuCanvas != null)
        {
            return;
        }

        panel = LabPanelBuilder.CreateFullScreenCanvas(transform, "PauseMenuCanvas",
            referenceResolution, 600, screenMargin, out menuCanvas, out menuScaler);

        LabPanelBuilder.CreateText("Title", panel, new Vector2(0.0f, TitleY),
            new Vector2(900.0f, 56.0f), "Atomix", 42.0f, TextAlignmentOptions.Center, Color.white);

        LabPanelBuilder.CreateText("Subtitle", panel, new Vector2(0.0f, TitleY - 48.0f),
            new Vector2(1200.0f, 28.0f), "Paused — press F1 or Resume to go back", 20.0f,
            TextAlignmentOptions.Center, LabPanelBuilder.MutedTextColour);

        tabButtons.Clear();
        tabButtons.Add(LabPanelBuilder.CreateButton("TabSettings", panel, "Settings",
            new Vector2(-250.0f, TabsY), new Vector2(230.0f, 52.0f), 22.0f,
            () => { activeTab = Tab.Settings; Rebuild(); }));
        tabButtons.Add(LabPanelBuilder.CreateButton("TabAchievements", panel, "Achievements",
            new Vector2(0.0f, TabsY), new Vector2(230.0f, 52.0f), 22.0f,
            () => { activeTab = Tab.Achievements; Rebuild(); }));
        tabButtons.Add(LabPanelBuilder.CreateButton("TabControls", panel, "Controls",
            new Vector2(250.0f, TabsY), new Vector2(230.0f, 52.0f), 22.0f,
            () => { activeTab = Tab.Controls; Rebuild(); }));

        bodyRoot = LabPanelBuilder.CreatePlate("Body", panel, Vector2.zero,
            new Vector2(1600.0f, BodyHeight), new Color(0.0f, 0.0f, 0.0f, 0.0f));

        statusText = LabPanelBuilder.CreateText("Status", panel, new Vector2(0.0f, StatusY),
            new Vector2(1500.0f, 52.0f), string.Empty, 19.0f,
            TextAlignmentOptions.Center, LabPanelBuilder.MutedTextColour);

        LabPanelBuilder.CreateButton("Resume", panel, "Resume  [F1]",
            new Vector2(-660.0f, FooterY), new Vector2(280.0f, 58.0f), 22.0f, Close);

        // A failed experiment used to be a dead end. This is the same thing F5 does, put where
        // someone who does not know the key will still find it.
        restartButton = LabPanelBuilder.CreateButton("RestartExperiment", panel, "Retry experiment  [F5]",
            new Vector2(-330.0f, FooterY), new Vector2(340.0f, 58.0f), 22.0f,
            () =>
            {
                LabRetryController retry = GetComponent<LabRetryController>();
                if (retry != null && retry.CanRestart)
                {
                    Close();
                    retry.RestartCurrentExperiment();
                }
                else if (statusText != null)
                {
                    statusText.text = "No experiment on the bench to retry - choose one from the book first.";
                }
            });

        LabPanelBuilder.CreateButton("ExportReport", panel, "Export lab report",
            new Vector2(20.0f, FooterY), new Vector2(320.0f, 58.0f), 22.0f,
            () => { if (statusText != null) { statusText.text = LabReportExporter.ExportAll(); } });

        // The welcome card shows once per machine and then never again, which makes it the one
        // piece of guidance in the game a student cannot get back. Now they can.
        LabPanelBuilder.CreateButton("ShowIntro", panel, "Show introduction",
            new Vector2(650.0f, FooterY), new Vector2(280.0f, 58.0f), 22.0f,
            () =>
            {
                Close();
                LabOnboarding.ShowAgain();
            });

        quitToMenuButton = LabPanelBuilder.CreateButton("QuitToMenu", panel, "Main menu",
            new Vector2(350.0f, FooterY), new Vector2(280.0f, 58.0f), 22.0f,
            () =>
            {
                Close();
                FirstPersonController.SetCursorLock(false);
                SceneTransition.Load("MainMenuScene");
            });
    }

    private void Rebuild()
    {
        if (bodyRoot == null)
        {
            return;
        }

        LabPanelBuilder.ClearChildren(bodyRoot);
        HighlightActiveTab();

        switch (activeTab)
        {
            case Tab.Settings: BuildSettingsTab(); break;
            case Tab.Achievements: BuildAchievementsTab(); break;
            case Tab.Controls: BuildControlsTab(); break;
        }
    }

    private void HighlightActiveTab()
    {
        for (int i = 0; i < tabButtons.Count; i++)
        {
            if (tabButtons[i] == null)
            {
                continue;
            }

            Image image = tabButtons[i].GetComponent<Image>();
            if (image != null)
            {
                image.color = i == (int)activeTab
                    ? LabPanelBuilder.ButtonActiveColour
                    : LabPanelBuilder.ButtonColour;
            }
        }
    }

    // =========================================================
    // TABS
    // =========================================================

    private void BuildSettingsTab()
    {
        const float labelSize = 22.0f;
        const float headingSize = 19.0f;

        BuildControlsColumn(-ColumnOffset, labelSize, headingSize);
        BuildMachineColumn(ColumnOffset, labelSize, headingSize);

        if (statusText != null && string.IsNullOrEmpty(statusText.text))
        {
            statusText.text =
                "Walking motion covers the head bob and the sprint zoom, and ships off.  " +
                "Laboratory ambience ships off too.  " +
                "Colour-blind safe swaps the green and red result pair for blue and orange.";
        }
    }

    /// <summary>Left column: how the player controls the game, and how it accommodates them.</summary>
    private void BuildControlsColumn(float x, float labelSize, float headingSize)
    {
        float y = BodyTop - 18.0f;

        LabPanelBuilder.CreateSectionHeading(bodyRoot, "Controls", new Vector2(x, y), ColumnWidth, headingSize);
        y -= HeaderStep;

        LabPanelBuilder.CreateStepperRow(bodyRoot, "Mouse sensitivity", new Vector2(x, y), ColumnWidth, labelSize,
            () => AtomixSettings.MouseSensitivity.ToString("0.00"),
            () => AtomixSettings.MouseSensitivity -= 0.25f,
            () => AtomixSettings.MouseSensitivity += 0.25f);
        y -= RowStep;

        LabPanelBuilder.CreateToggleRow(bodyRoot, "Invert vertical look", new Vector2(x, y), ColumnWidth, labelSize,
            () => AtomixSettings.InvertLook,
            value => AtomixSettings.InvertLook = value);
        y -= RowStep;

        LabPanelBuilder.CreateStepperRow(bodyRoot, "Movement speed", new Vector2(x, y), ColumnWidth, labelSize,
            () => AtomixSettings.MoveSpeed.ToString("0.0") + " m/s",
            () => AtomixSettings.MoveSpeed -= 0.2f,
            () => AtomixSettings.MoveSpeed += 0.2f);
        y -= SectionStep;

        LabPanelBuilder.CreateSectionHeading(bodyRoot, "Accessibility and comfort", new Vector2(x, y),
            ColumnWidth, headingSize);
        y -= HeaderStep;

        // Both of these go all the way to zero. Motion sensitivity is common enough in a
        // classroom that "reduce" is not a good enough answer.
        LabPanelBuilder.CreateStepperRow(bodyRoot, "Camera shake", new Vector2(x, y), ColumnWidth, labelSize,
            () => AtomixSettings.ScreenShake <= 0.0f
                ? "Off"
                : Mathf.RoundToInt(AtomixSettings.ScreenShake * 100.0f) + "%",
            () => AtomixSettings.ScreenShake -= 0.25f,
            () => AtomixSettings.ScreenShake += 0.25f);
        y -= RowStep;

        // One control for the bob and the sprint lens zoom together. Someone turning this down
        // is doing it because the movement is making them feel ill, and finding two separate
        // switches for that is a bad answer.
        LabPanelBuilder.CreateStepperRow(bodyRoot, "Walking motion", new Vector2(x, y), ColumnWidth, labelSize,
            () => AtomixSettings.HeadBob <= 0.0f
                ? "Off"
                : Mathf.RoundToInt(AtomixSettings.HeadBob * 100.0f) + "%",
            () => AtomixSettings.HeadBob -= 0.25f,
            () => AtomixSettings.HeadBob += 0.25f);
        y -= RowStep;

        LabPanelBuilder.CreateToggleRow(bodyRoot, "High-contrast panels", new Vector2(x, y), ColumnWidth, labelSize,
            () => AtomixSettings.HighContrastUi,
            value => AtomixSettings.HighContrastUi = value);
        y -= RowStep;

        LabPanelBuilder.CreateToggleRow(bodyRoot, "Colour-blind safe results", new Vector2(x, y), ColumnWidth, labelSize,
            () => AtomixSettings.ColourBlindSafe,
            value => AtomixSettings.ColourBlindSafe = value);
        y -= RowStep;

        LabPanelBuilder.CreateToggleRow(bodyRoot, "Show crosshair", new Vector2(x, y), ColumnWidth, labelSize,
            () => AtomixSettings.ShowCrosshair,
            value => AtomixSettings.ShowCrosshair = value);
        y -= RowStep;

        LabPanelBuilder.CreateStepperRow(bodyRoot, "Menu size", new Vector2(x, y), ColumnWidth, labelSize,
            () => Mathf.RoundToInt(AtomixSettings.UiScale * 100.0f) + "%",
            () => { AtomixSettings.UiScale -= 0.1f; ApplyScale(); },
            () => { AtomixSettings.UiScale += 0.1f; ApplyScale(); });
    }

    /// <summary>Right column: everything about the machine this is running on.</summary>
    private void BuildMachineColumn(float x, float labelSize, float headingSize)
    {
        float y = BodyTop - 18.0f;

        LabPanelBuilder.CreateSectionHeading(bodyRoot, "Display", new Vector2(x, y), ColumnWidth, headingSize);
        y -= HeaderStep;

        LabPanelBuilder.CreateStepperRow(bodyRoot, "Field of view", new Vector2(x, y), ColumnWidth, labelSize,
            () => AtomixSettings.FieldOfView.ToString("0") + " deg",
            () => AtomixSettings.FieldOfView -= 5.0f,
            () => AtomixSettings.FieldOfView += 5.0f);
        y -= RowStep;

        LabPanelBuilder.CreateStepperRow(bodyRoot, "Graphics quality", new Vector2(x, y), ColumnWidth, labelSize,
            QualityLabel, () => StepQuality(-1), () => StepQuality(1));
        y -= RowStep;

        LabPanelBuilder.CreateToggleRow(bodyRoot, "Visual effects", new Vector2(x, y), ColumnWidth, labelSize,
            () => AtomixSettings.PostFx,
            value => { AtomixSettings.PostFx = value; AtomixPostFx.Refresh(); });
        y -= RowStep;

        LabPanelBuilder.CreateToggleRow(bodyRoot, "Fullscreen", new Vector2(x, y), ColumnWidth, labelSize,
            () => AtomixSettings.Fullscreen,
            value => AtomixSettings.Fullscreen = value);
        y -= RowStep;

        LabPanelBuilder.CreateStepperRow(bodyRoot, "Frame rate limit", new Vector2(x, y), ColumnWidth, labelSize,
            () => AtomixSettings.FrameCap <= 0 ? "Unlimited" : AtomixSettings.FrameCap + " fps",
            () => StepFrameCap(-1), () => StepFrameCap(1));
        y -= SectionStep;

        LabPanelBuilder.CreateSectionHeading(bodyRoot, "Sound", new Vector2(x, y), ColumnWidth, headingSize);
        y -= HeaderStep;

        LabPanelBuilder.CreateStepperRow(bodyRoot, "Master volume", new Vector2(x, y), ColumnWidth, labelSize,
            () => Percent(AtomixSettings.MasterVolume),
            () => AtomixSettings.MasterVolume -= 0.1f,
            () => AtomixSettings.MasterVolume += 0.1f);
        y -= RowStep;

        LabPanelBuilder.CreateStepperRow(bodyRoot, "Interaction sounds", new Vector2(x, y), ColumnWidth, labelSize,
            () => Percent(AtomixSettings.SfxVolume),
            () => AtomixSettings.SfxVolume -= 0.1f,
            () => AtomixSettings.SfxVolume += 0.1f);
        y -= RowStep;

        LabPanelBuilder.CreateStepperRow(bodyRoot, "Laboratory ambience", new Vector2(x, y), ColumnWidth, labelSize,
            () => Percent(AtomixSettings.AmbienceVolume),
            () => AtomixSettings.AmbienceVolume -= 0.1f,
            () => AtomixSettings.AmbienceVolume += 0.1f);
        y -= RowStep;

        LabPanelBuilder.CreateStepperRow(bodyRoot, "Menu music", new Vector2(x, y), ColumnWidth, labelSize,
            () => Percent(AtomixSettings.MusicVolume),
            () => AtomixSettings.MusicVolume -= 0.1f,
            () => AtomixSettings.MusicVolume += 0.1f);
    }

    private static string Percent(float value)
    {
        return value <= 0.0f ? "Off" : Mathf.RoundToInt(value * 100.0f) + "%";
    }

    private static string QualityLabel()
    {
        int level = AtomixSettings.QualityLevel;
        string[] names = QualitySettings.names;

        if (level < 0 || level >= names.Length)
        {
            // Nothing has been chosen yet, so report what the project is actually running at.
            int current = QualitySettings.GetQualityLevel();
            return current >= 0 && current < names.Length ? names[current] : "Default";
        }

        return names[level];
    }

    private static void StepQuality(int direction)
    {
        string[] names = QualitySettings.names;
        if (names.Length == 0)
        {
            return;
        }

        int level = AtomixSettings.QualityLevel;
        if (level < 0 || level >= names.Length)
        {
            level = QualitySettings.GetQualityLevel();
        }

        AtomixSettings.QualityLevel = Mathf.Clamp(level + direction, 0, names.Length - 1);

        // The stack decides for itself what it can afford at the new level, so it has to be told.
        AtomixPostFx.Refresh();
    }

    private static void StepFrameCap(int direction)
    {
        int[] choices = AtomixSettings.FrameCapChoices;
        int index = 0;
        for (int i = 0; i < choices.Length; i++)
        {
            if (choices[i] == AtomixSettings.FrameCap)
            {
                index = i;
                break;
            }
        }

        AtomixSettings.FrameCap = choices[Mathf.Clamp(index + direction, 0, choices.Length - 1)];
    }

    private void BuildAchievementsTab()
    {
        AchievementSystem achievements = AchievementSystem.Instance;
        if (achievements == null)
        {
            LabPanelBuilder.CreateText("NoAchievements", bodyRoot, Vector2.zero,
                new Vector2(RowWidth, 80.0f), "Progress tracking is not running.", 24.0f,
                TextAlignmentOptions.Center, LabPanelBuilder.MutedTextColour);
            return;
        }

        IList<Achievement> catalogue = achievements.Catalogue;
        const float listWidth = 1500.0f;
        float half = listWidth * 0.5f;

        // Sits inside the body band, which starts below the tab row. The first version put this
        // at y = 292, straight through the Achievements tab button.
        LabPanelBuilder.CreateText("AchHeader", bodyRoot, new Vector2(0.0f, BodyTop - 20.0f),
            new Vector2(listWidth, 40.0f),
            achievements.EarnedCount + " of " + catalogue.Count + " unlocked", 28.0f,
            TextAlignmentOptions.Center, Color.white);

        // The coin wallet lives in the testing scene, but its totals are lifetime, so this is the
        // one place outside a test where a student can see where their rank stands.
        AtomixCoinBank bank = AtomixCoinBank.Instance;

        string walletLine =
            "<color=#FFD647>*</color> <b>" + bank.Balance + "</b> coins to spend   -   " +
            bank.LifetimeEarned + " earned all-time   -   rank <b>" + bank.RankName + "</b>";

        if (bank.CoinsToNextRank > 0)
        {
            walletLine += "   -   " + bank.CoinsToNextRank + " more to reach " + bank.NextRankName;
        }

        LabPanelBuilder.CreateText("Wallet", bodyRoot, new Vector2(0.0f, BodyTop - 56.0f),
            new Vector2(listWidth, 32.0f), walletLine, 22.0f,
            TextAlignmentOptions.Center, LabPanelBuilder.MutedTextColour);

        LabPanelBuilder.CreateText("WalletStats", bodyRoot, new Vector2(0.0f, BodyTop - 86.0f),
            new Vector2(listWidth, 32.0f),
            bank.RunsCompleted + " tests completed   -   best run " + bank.BestRun +
            " coins   -   best streak x" + bank.BestStreak + "   -   " +
            bank.DistinctReactionsCleared + " of 8 experiments cleared in a test",
            20.0f, TextAlignmentOptions.Center, LabPanelBuilder.MutedTextColour);

        float y = BodyTop - 126.0f;

        // 40 px rows, unless that would run the list past the bottom of the body band and into
        // the status line - which thirteen of them already did.
        float available = y - (-BodyHeight * 0.5f) + 20.0f;
        float rowHeight = Mathf.Min(40.0f, available / Mathf.Max(1, catalogue.Count));

        for (int i = 0; i < catalogue.Count; i++)
        {
            Achievement achievement = catalogue[i];
            bool earned = achievements.IsEarned(achievement.id);
            float rowY = y - i * rowHeight;

            string description = (achievement.hidden && !earned)
                ? "Hidden - keep going."
                : achievement.description;

            // Alternating plate so the eye can track a row across the full width.
            if (i % 2 == 0)
            {
                LabPanelBuilder.CreatePlate("AchRow" + i, bodyRoot, new Vector2(0.0f, rowY),
                    new Vector2(listWidth, rowHeight - 4.0f),
                    new Color(1.0f, 1.0f, 1.0f, 0.035f));
            }

            LabPanelBuilder.CreateStatusPip(bodyRoot, new Vector2(-half + 40.0f, rowY),
                earned, AtomixSettings.SuccessColour);

            LabPanelBuilder.CreateText("AchTitle" + i, bodyRoot,
                new Vector2(-half + 280.0f, rowY), new Vector2(400.0f, rowHeight),
                achievement.title, 23.0f, TextAlignmentOptions.Left,
                earned ? Color.white : LabPanelBuilder.MutedTextColour);

            LabPanelBuilder.CreateText("AchDesc" + i, bodyRoot,
                new Vector2(-half + 990.0f, rowY), new Vector2(960.0f, rowHeight),
                description, 20.0f, TextAlignmentOptions.Left,
                earned ? AtomixSettings.BodyTextColour : LabPanelBuilder.MutedTextColour);
        }
    }

    private void BuildControlsTab()
    {
        string[,] rows =
        {
            { "W A S D", "Move" },
            { "Mouse", "Look around" },
            { "Space / Ctrl", "Move up / down" },
            { "Left Shift", "Sprint" },
            { "Left click", "Grab, release or activate" },
            { "R", "Release held object" },
            { "Q / E", "Rotate held object left or right" },
            { "Z / X", "Tilt held object - steeper pours faster" },
            { "C", "Roll held object (Shift to reverse) - also pours" },
            { "T", "Reset held object pose" },
            { "Mouse wheel", "Spin held object / move a panel" },
            { "B", "Open or close the reaction book" },
            { "1 - 8", "Jump straight to an experiment" },
            { "F5", "Reset the bench and retry the experiment" },
            { "Tab", "Experiment history" },
            { "F", "Scientific graphs" },
            { "P", "Periodic table" },
            { "L", "Cycle the measurement label" },
            { "V", "Hold to talk to the lab assistant" },
            { "Enter", "Type a question - no microphone needed" },
            { "Y", "Ask why the last experiment failed" },
            { "M", "Minimise the assistant panel" },
            { "F2 / F3 / F4", "Testing scene: hint, +30 seconds, skip" },
            { "F1", "This menu" },
            { "Escape", "Unlock cursor / close a panel" }
        };

        // Two columns, so the text can be read at a comfortable size instead of being squeezed
        // into 21 single-file rows.
        int total = rows.GetLength(0);
        int perColumn = (total + 1) / 2;
        const float rowHeight = 46.0f;
        const float columnWidth = 800.0f;
        float[] columnCentre = { -430.0f, 450.0f };
        float top = BodyTop - 24.0f;

        for (int i = 0; i < total; i++)
        {
            int column = i < perColumn ? 0 : 1;
            int indexInColumn = i < perColumn ? i : i - perColumn;
            float rowY = top - indexInColumn * rowHeight;
            float centre = columnCentre[column];

            if (indexInColumn % 2 == 0)
            {
                LabPanelBuilder.CreatePlate("KeyRow" + i, bodyRoot, new Vector2(centre, rowY),
                    new Vector2(columnWidth, rowHeight - 4.0f),
                    new Color(1.0f, 1.0f, 1.0f, 0.035f));
            }

            // Key names are short, so their column gives width to the descriptions: at 430 px the
            // longer ones ("Roll held object (Shift to reverse) - also pours") wrapped onto a
            // second line taller than the row and ran into the next.
            LabPanelBuilder.CreateText("KeyName" + i, bodyRoot,
                new Vector2(centre - columnWidth * 0.5f + 145.0f, rowY),
                new Vector2(250.0f, rowHeight), rows[i, 0], 23.0f,
                TextAlignmentOptions.Left, Color.white);

            LabPanelBuilder.CreateText("KeyDesc" + i, bodyRoot,
                new Vector2(centre - columnWidth * 0.5f + 540.0f, rowY),
                new Vector2(520.0f, rowHeight), rows[i, 1], 21.0f,
                TextAlignmentOptions.Left, AtomixSettings.BodyTextColour);
        }
    }
}
