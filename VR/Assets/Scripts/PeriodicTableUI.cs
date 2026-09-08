using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// An interactive periodic table, opened with P.
///
/// The lab scene has a periodic table modelled on the wall that the student cannot read - it is a
/// texture. This is the real thing: all 118 elements, colour-coded by category, clickable for
/// detail, and with the elements involved in the experiment currently on the bench outlined so the
/// student can see where their reagents sit.
///
/// Built from code as a world-space canvas, exactly like the history and graph panels, so it works
/// with the existing crosshair interaction and needs no scene or prefab edits.
/// </summary>
public class PeriodicTableUI : MonoBehaviour
{
    [Header("Input")]
    public KeyCode toggleKey = KeyCode.P;
    public KeyCode closeKey = KeyCode.Escape;

    [Header("Placement")]
    [Tooltip("Metres in front of the camera the panel appears.")]
    public float distanceFromCamera = 1.6f;

    [Header("Availability")]
    [Tooltip("Scenes where P opens the table. The testing scene is excluded: the table names the " +
             "elements, which would help during an examination.")]
    public string[] blockedScenes = { "TestingPhaseLab", "MainMenuScene" };

    private Canvas tableCanvas;
    private RectTransform panel;
    private RectTransform gridRoot;
    private TMP_Text detailTitle;
    private TMP_Text detailBody;
    private TMP_Text highlightNote;
    private RectTransform detailSwatch;
    private bool isOpen;

    private readonly Dictionary<string, Image> cellImages = new Dictionary<string, Image>();
    private readonly HashSet<string> highlighted = new HashSet<string>();

    private const float CanvasWidth = 1560.0f;
    private const float CanvasHeight = 900.0f;
    private const float CellSize = 66.0f;
    private const float CellGap = 4.0f;
    /// <summary>Panel-space Y of the grid's centre; the detail card is placed relative to it.</summary>
    private const float GridCentreY = 40.0f;

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
            return;
        }

        if (isOpen && Input.GetKeyDown(closeKey))
        {
            Close();
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
        // The book, video and other panels sit at the same depth in front of the camera. Two
        // world-space panels there just stack and neither is readable.
        if (ReactionLearningController.IsAnyPanelVisible)
        {
            return;
        }

        LabPanelBuilder.CloseOtherPanels(this);

        EnsureUiBuilt();
        isOpen = true;
        tableCanvas.gameObject.SetActive(true);
        LabPanelBuilder.FaceCamera(tableCanvas, distanceFromCamera);
        RefreshHighlights();

        if (AchievementSystem.Instance != null)
        {
            AchievementSystem.Instance.Unlock("periodic_table");
        }
    }

    public void Close()
    {
        if (!isOpen)
        {
            return;
        }

        isOpen = false;
        if (tableCanvas != null)
        {
            tableCanvas.gameObject.SetActive(false);
        }
    }

    public bool IsOpen { get { return isOpen; } }

    // =========================================================
    // CONSTRUCTION
    // =========================================================

    private void EnsureUiBuilt()
    {
        if (tableCanvas != null)
        {
            return;
        }

        panel = LabPanelBuilder.CreatePanelCanvas(transform, "PeriodicTableCanvas",
            new Vector2(CanvasWidth, CanvasHeight),
            new Vector2(CanvasWidth - 40.0f, CanvasHeight - 40.0f),
            540, out tableCanvas);

        LabPanelBuilder.CreateText("Title", panel, new Vector2(0.0f, CanvasHeight * 0.5f - 56.0f),
            new Vector2(900.0f, 46.0f), "Periodic Table of the Elements", 34.0f,
            TextAlignmentOptions.Center, Color.white);

        LabPanelBuilder.CreateButton("Close", panel, "Close  [Esc]",
            new Vector2(CanvasWidth * 0.5f - 130.0f, CanvasHeight * 0.5f - 56.0f),
            new Vector2(190.0f, 46.0f), 22.0f, Close);

        gridRoot = LabPanelBuilder.CreatePlate("Grid", panel, new Vector2(0.0f, GridCentreY),
            new Vector2(18.0f * (CellSize + CellGap), 10.0f * (CellSize + CellGap)),
            new Color(0.0f, 0.0f, 0.0f, 0.0f));

        BuildGrid();
        BuildLegend();
        BuildDetailPane();

        ShowDetail(PeriodicTableData.BySymbol("H"));
    }

    private void BuildGrid()
    {
        // Grid origin: column 1 / row 1 at the top-left of the grid rect.
        float gridWidth = 18.0f * (CellSize + CellGap);
        float gridHeight = 10.0f * (CellSize + CellGap);
        float originX = -gridWidth * 0.5f + CellSize * 0.5f;
        float originY = gridHeight * 0.5f - CellSize * 0.5f;

        List<ChemicalElement> elements = PeriodicTableData.Elements;
        for (int i = 0; i < elements.Count; i++)
        {
            ChemicalElement element = elements[i];

            // Periods 8 and 9 are the lanthanide/actinide strip, dropped below with a gap.
            float rowIndex = element.period <= 7 ? element.period - 1 : element.period - 0.5f;
            float x = originX + (element.group - 1) * (CellSize + CellGap);
            float y = originY - rowIndex * (CellSize + CellGap);

            CreateCell(element, new Vector2(x, y));
        }

        // Markers in the main body pointing at the f-block strip.
        float laY = originY - 5.0f * (CellSize + CellGap);
        float acY = originY - 6.0f * (CellSize + CellGap);
        float markerX = originX + 2.0f * (CellSize + CellGap);
        LabPanelBuilder.CreateText("LaMarker", gridRoot, new Vector2(markerX, laY),
            new Vector2(CellSize, CellSize), "57-71", 16.0f, TextAlignmentOptions.Center,
            LabPanelBuilder.MutedTextColour);
        LabPanelBuilder.CreateText("AcMarker", gridRoot, new Vector2(markerX, acY),
            new Vector2(CellSize, CellSize), "89-103", 16.0f, TextAlignmentOptions.Center,
            LabPanelBuilder.MutedTextColour);
    }

    private void CreateCell(ChemicalElement element, Vector2 anchoredPosition)
    {
        GameObject cell = new GameObject("Cell_" + element.symbol,
            typeof(RectTransform), typeof(Image), typeof(Button));
        cell.transform.SetParent(gridRoot, false);

        RectTransform rect = cell.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(CellSize, CellSize);
        rect.anchoredPosition = anchoredPosition;

        Image image = cell.GetComponent<Image>();
        image.color = PeriodicTableData.CategoryColour(element.category);
        cellImages[element.symbol] = image;

        Button button = cell.GetComponent<Button>();
        button.targetGraphic = image;
        ChemicalElement captured = element;
        button.onClick.AddListener(() => ShowDetail(captured));

        LabPanelBuilder.CreateText("Z", cell.transform, new Vector2(0.0f, CellSize * 0.5f - 11.0f),
            new Vector2(CellSize, 16.0f), element.number.ToString(), 13.0f,
            TextAlignmentOptions.Center, new Color(1.0f, 1.0f, 1.0f, 0.85f));

        LabPanelBuilder.CreateText("Symbol", cell.transform, new Vector2(0.0f, -2.0f),
            new Vector2(CellSize, 30.0f), element.symbol, 24.0f,
            TextAlignmentOptions.Center, Color.white);

        LabPanelBuilder.CreateText("Mass", cell.transform, new Vector2(0.0f, -CellSize * 0.5f + 10.0f),
            new Vector2(CellSize, 14.0f), element.MassLabel, 10.5f,
            TextAlignmentOptions.Center, new Color(1.0f, 1.0f, 1.0f, 0.75f));
    }

    private void BuildLegend()
    {
        ElementCategory[] order =
        {
            ElementCategory.AlkaliMetal, ElementCategory.AlkalineEarth,
            ElementCategory.TransitionMetal, ElementCategory.PostTransitionMetal,
            ElementCategory.Metalloid, ElementCategory.ReactiveNonmetal,
            ElementCategory.NobleGas, ElementCategory.Lanthanide, ElementCategory.Actinide
        };

        float y = -CanvasHeight * 0.5f + 52.0f;
        float spacing = 168.0f;
        float startX = -(order.Length - 1) * spacing * 0.5f;

        for (int i = 0; i < order.Length; i++)
        {
            float x = startX + i * spacing;
            LabPanelBuilder.CreatePlate("Swatch" + i, panel, new Vector2(x - 68.0f, y),
                new Vector2(18.0f, 18.0f), PeriodicTableData.CategoryColour(order[i]));
            LabPanelBuilder.CreateText("Legend" + i, panel, new Vector2(x + 12.0f, y),
                new Vector2(150.0f, 22.0f), PeriodicTableData.CategoryName(order[i]), 14.0f,
                TextAlignmentOptions.Left, LabPanelBuilder.MutedTextColour);
        }
    }

    /// <summary>
    /// The element detail card.
    ///
    /// It used to sit at the top-left of the panel, which put it straight on top of Li, Be, Na and
    /// Mg - clicking an element hid four others. A periodic table has a large piece of genuinely
    /// empty space built into it: groups 3-12 of periods 1-3, the notch between the s- and p-blocks.
    /// The card lives there now, so it covers no element at any time. The coordinates below are
    /// derived from the grid rather than hand-tuned, so they follow if the cell size changes.
    /// </summary>
    private void BuildDetailPane()
    {
        float pitch = CellSize + CellGap;
        float gridWidth = 18.0f * pitch;
        float gridHeight = 10.0f * pitch;
        float originX = -gridWidth * 0.5f + CellSize * 0.5f;
        float originY = gridHeight * 0.5f - CellSize * 0.5f;

        // The empty notch: groups 3..12 across periods 1..3 (rows 0..2), in panel coordinates.
        float blockLeft = originX + 2.0f * pitch - CellSize * 0.5f;
        float blockRight = originX + 11.0f * pitch + CellSize * 0.5f;
        float blockTop = originY + CellSize * 0.5f + GridCentreY;
        float blockBottom = originY - 2.0f * pitch - CellSize * 0.5f + GridCentreY;

        float paneWidth = (blockRight - blockLeft) - 16.0f;
        float paneHeight = (blockTop - blockBottom) - 16.0f;
        float x = (blockLeft + blockRight) * 0.5f;
        float y = (blockTop + blockBottom) * 0.5f;

        LabPanelBuilder.CreatePlate("DetailPlate", panel, new Vector2(x, y),
            new Vector2(paneWidth, paneHeight), LabPanelBuilder.RowColour);

        float left = x - paneWidth * 0.5f;

        detailSwatch = LabPanelBuilder.CreatePlate("DetailSwatch", panel,
            new Vector2(left + 48.0f, y + 30.0f), new Vector2(56.0f, 56.0f), Color.white);

        detailTitle = LabPanelBuilder.CreateText("DetailTitle", panel,
            new Vector2(left + 265.0f, y + 58.0f), new Vector2(330.0f, 32.0f), "", 24.0f,
            TextAlignmentOptions.Left, Color.white);

        detailBody = LabPanelBuilder.CreateText("DetailBody", panel,
            new Vector2(left + 265.0f, y - 24.0f), new Vector2(330.0f, 108.0f), "", 17.0f,
            TextAlignmentOptions.TopLeft, AtomixSettings.BodyTextColour);

        // Created once and re-texted on each open; building it inside RefreshHighlights would
        // stack a fresh copy every time the panel was opened.
        highlightNote = LabPanelBuilder.CreateText("HighlightNote", panel,
            new Vector2(x + paneWidth * 0.5f - 160.0f, y - 24.0f), new Vector2(280.0f, 108.0f),
            "", 17.0f, TextAlignmentOptions.TopLeft, AtomixSettings.SuccessColour);
    }

    private void ShowDetail(ChemicalElement element)
    {
        if (element == null || detailTitle == null)
        {
            return;
        }

        detailTitle.text = element.symbol + "  –  " + element.name;
        detailBody.text =
            "Atomic number: " + element.number + "\n" +
            "Atomic weight: " + element.MassLabel + "\n" +
            "Group " + element.group + ", period " +
            (element.period <= 7 ? element.period.ToString() : (element.period == 8 ? "6" : "7")) + "\n" +
            element.CategoryLabel + (element.synthetic ? "  (no stable isotope)" : "");

        if (detailSwatch != null)
        {
            Image swatch = detailSwatch.GetComponent<Image>();
            if (swatch != null)
            {
                swatch.color = PeriodicTableData.CategoryColour(element.category);
            }
        }
    }

    // =========================================================
    // HIGHLIGHTING THE CURRENT EXPERIMENT
    // =========================================================

    /// <summary>
    /// Brightens the elements taking part in whatever experiment is on the bench. Read from
    /// <see cref="ReactionHistoryRecorder.Active"/>, which the reaction scripts already maintain,
    /// so nothing new needs wiring.
    /// </summary>
    private void RefreshHighlights()
    {
        foreach (KeyValuePair<string, Image> pair in cellImages)
        {
            ChemicalElement element = PeriodicTableData.BySymbol(pair.Key);
            if (pair.Value != null && element != null)
            {
                pair.Value.color = PeriodicTableData.CategoryColour(element.category);
            }
        }

        highlighted.Clear();
        if (highlightNote != null)
        {
            highlightNote.text = string.Empty;
        }

        ReactionHistoryRecorder active = ReactionHistoryRecorder.Active;
        if (active == null)
        {
            return;
        }

        string[] symbols = PeriodicTableData.ElementsForReaction(active.ReactionId);
        for (int i = 0; i < symbols.Length; i++)
        {
            highlighted.Add(symbols[i]);

            Image image;
            if (!cellImages.TryGetValue(symbols[i], out image) || image == null)
            {
                continue;
            }

            // Lift toward white rather than recolouring, so the category is still readable.
            image.color = Color.Lerp(image.color, Color.white, 0.55f);
        }

        if (symbols.Length > 0 && highlightNote != null)
        {
            highlightNote.color = AtomixSettings.SuccessColour;
            highlightNote.text = "<b>On the bench:</b> " + string.Join(", ", symbols);
        }
    }
}
