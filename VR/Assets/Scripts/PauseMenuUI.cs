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

    void Update()
    {
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

        quitToMenuButton = LabPanelBuilder.CreateButton("QuitToMenu", panel, "Main menu",
            new Vector2(350.0f, FooterY), new Vector2(280.0f, 58.0f), 22.0f,
            () =>
            {
                Close();
                FirstPersonController.SetCursorLock(false);
                SceneManager.LoadScene("MainMenuScene");
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
        const float labelSize = 25.0f;
        const float headingSize = 20.0f;
        float y = BodyTop - 18.0f;

        LabPanelBuilder.CreateSectionHeading(bodyRoot, "Controls", new Vector2(0.0f, y), RowWidth, headingSize);
        y -= HeaderStep;

        LabPanelBuilder.CreateStepperRow(bodyRoot, "Mouse sensitivity", new Vector2(0.0f, y), RowWidth, labelSize,
            () => AtomixSettings.MouseSensitivity.ToString("0.00"),
            () => AtomixSettings.MouseSensitivity -= 0.25f,
            () => AtomixSettings.MouseSensitivity += 0.25f);
        y -= RowStep;

        LabPanelBuilder.CreateToggleRow(bodyRoot, "Invert vertical look", new Vector2(0.0f, y), RowWidth, labelSize,
            () => AtomixSettings.InvertLook,
            value => AtomixSettings.InvertLook = value);
        y -= RowStep;

        LabPanelBuilder.CreateStepperRow(bodyRoot, "Movement speed", new Vector2(0.0f, y), RowWidth, labelSize,
            () => AtomixSettings.MoveSpeed.ToString("0.0") + " m/s",
            () => AtomixSettings.MoveSpeed -= 0.2f,
            () => AtomixSettings.MoveSpeed += 0.2f);
        y -= SectionStep;

        LabPanelBuilder.CreateSectionHeading(bodyRoot, "Display and sound", new Vector2(0.0f, y), RowWidth, headingSize);
        y -= HeaderStep;

        LabPanelBuilder.CreateStepperRow(bodyRoot, "Field of view", new Vector2(0.0f, y), RowWidth, labelSize,
            () => AtomixSettings.FieldOfView.ToString("0") + " deg",
            () => AtomixSettings.FieldOfView -= 5.0f,
            () => AtomixSettings.FieldOfView += 5.0f);
        y -= RowStep;

        LabPanelBuilder.CreateStepperRow(bodyRoot, "Volume", new Vector2(0.0f, y), RowWidth, labelSize,
            () => Mathf.RoundToInt(AtomixSettings.MasterVolume * 100.0f) + "%",
            () => AtomixSettings.MasterVolume -= 0.1f,
            () => AtomixSettings.MasterVolume += 0.1f);
        y -= RowStep;

        LabPanelBuilder.CreateStepperRow(bodyRoot, "Menu size", new Vector2(0.0f, y), RowWidth, labelSize,
            () => Mathf.RoundToInt(AtomixSettings.UiScale * 100.0f) + "%",
            () => { AtomixSettings.UiScale -= 0.1f; ApplyScale(); },
            () => { AtomixSettings.UiScale += 0.1f; ApplyScale(); });
        y -= SectionStep;

        LabPanelBuilder.CreateSectionHeading(bodyRoot, "Accessibility", new Vector2(0.0f, y), RowWidth, headingSize);
        y -= HeaderStep;

        LabPanelBuilder.CreateToggleRow(bodyRoot, "High-contrast panels", new Vector2(0.0f, y), RowWidth, labelSize,
            () => AtomixSettings.HighContrastUi,
            value => AtomixSettings.HighContrastUi = value);
        y -= RowStep;

        LabPanelBuilder.CreateToggleRow(bodyRoot, "Colour-blind safe results", new Vector2(0.0f, y), RowWidth, labelSize,
            () => AtomixSettings.ColourBlindSafe,
            value => AtomixSettings.ColourBlindSafe = value);
        y -= RowStep;

        LabPanelBuilder.CreateToggleRow(bodyRoot, "Show crosshair", new Vector2(0.0f, y), RowWidth, labelSize,
            () => AtomixSettings.ShowCrosshair,
            value => AtomixSettings.ShowCrosshair = value);

        if (statusText != null && string.IsNullOrEmpty(statusText.text))
        {
            statusText.text = "Colour-blind safe swaps the green and red result pair for blue and orange.";
        }
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

        float y = BodyTop - 68.0f;
        const float rowHeight = 40.0f;

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
            { "Z / X", "Tilt held object forward or back" },
            { "C", "Roll held object (Shift to reverse)" },
            { "T", "Reset held object pose" },
            { "B", "Open or close the reaction book" },
            { "1 - 8", "Jump straight to an experiment" },
            { "F5", "Reset the bench and retry the experiment" },
            { "Tab", "Experiment history" },
            { "F", "Scientific graphs" },
            { "P", "Periodic table" },
            { "L", "Cycle the measurement label" },
            { "H", "Controls help overlay" },
            { "V", "Hold to talk to the lab assistant" },
            { "M", "Minimise the assistant panel" },
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

            LabPanelBuilder.CreateText("KeyName" + i, bodyRoot,
                new Vector2(centre - columnWidth * 0.5f + 170.0f, rowY),
                new Vector2(300.0f, rowHeight), rows[i, 0], 23.0f,
                TextAlignmentOptions.Left, Color.white);

            LabPanelBuilder.CreateText("KeyDesc" + i, bodyRoot,
                new Vector2(centre - columnWidth * 0.5f + 580.0f, rowY),
                new Vector2(430.0f, rowHeight), rows[i, 1], 21.0f,
                TextAlignmentOptions.Left, AtomixSettings.BodyTextColour);
        }
    }
}
