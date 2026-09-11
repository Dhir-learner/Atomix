using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// In-lab review panel for <see cref="ExperimentHistoryManager"/>. Built entirely from code as a
/// WorldSpace canvas, matching how ReactionLearningController builds its UI, so it needs no scene
/// or prefab edits and is clickable with the existing crosshair interaction.
///
/// Toggle with Tab (H is already taken by the controls help overlay). Click a row to expand it
/// and read the steps, quantities and AI questions for that attempt.
/// </summary>
public class ExperimentHistoryUI : MonoBehaviour
{
    [Header("Input")]
    public KeyCode toggleKey = KeyCode.Tab;
    public KeyCode closeKey = KeyCode.Escape;
    public float scrollSpeed = 900.0f;

    [Header("Placement")]
    [Tooltip("Metres in front of the camera the panel appears.")]
    public float distanceFromCamera = 1.5f;

    [Header("Availability")]
    [Tooltip("Scenes where the panel cannot be opened. The testing scene is excluded because past " +
             "attempts would otherwise be an answer key while the student is being examined.")]
    public string[] blockedScenes = { "TestingPhaseLab" };

    private Canvas historyCanvas;
    private RectTransform listContent;
    private ScrollRect scrollRect;
    private TMP_Text headerText;
    private TMP_Text footerText;
    private RectTransform filterRow;

    private bool isOpen = false;
    private int filterReactionId = -1;         // -1 = show everything
    private string expandedAttemptId = string.Empty;
    private bool listDirty = true;

    private static readonly Color PanelColor = new Color(0.06f, 0.07f, 0.10f, 0.96f);
    private static readonly Color RowColor = new Color(0.13f, 0.15f, 0.20f, 1.0f);
    private static readonly Color RowSelectedColor = new Color(0.16f, 0.30f, 0.50f, 1.0f);
    private static readonly Color ButtonColor = new Color(0.16f, 0.30f, 0.50f, 1.0f);
    private static readonly Color ButtonActiveColor = new Color(0.24f, 0.52f, 0.78f, 1.0f);
    private static readonly Color SuccessColor = new Color(0.45f, 0.92f, 0.55f, 1.0f);
    private static readonly Color FailColor = new Color(1.0f, 0.51f, 0.42f, 1.0f);
    private static readonly Color NeutralColor = new Color(0.78f, 0.82f, 0.88f, 1.0f);

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

        if (!isOpen)
        {
            return;
        }

        if (Input.GetKeyDown(closeKey))
        {
            Close();
            return;
        }

        HandleScrollInput();

        if (listDirty)
        {
            RebuildList();
        }
    }

    bool IsBlockedScene()
    {
        if (blockedScenes == null || blockedScenes.Length == 0)
        {
            return false;
        }

        string active = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
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
        // The book / video panel sits at the same distance in front of the camera. Two
        // world-space panels at the same depth just stack and neither is readable, so the one
        // already on screen wins.
        if (ReactionLearningController.IsAnyPanelVisible)
        {
            return;
        }

        // Closes the graph panel, and now the periodic table and pause menu too.
        LabPanelBuilder.CloseOtherPanels(this);

        EnsureUiBuilt();
        isOpen = true;
        listDirty = true;
        historyCanvas.gameObject.SetActive(true);
        PositionInFrontOfCamera();
        RebuildList();
    }

    public void Close()
    {
        isOpen = false;
        if (historyCanvas != null)
        {
            historyCanvas.gameObject.SetActive(false);
        }
    }

    private void HandleScrollInput()
    {
        if (scrollRect == null || listContent == null)
        {
            return;
        }

        // The OS cursor stays locked for crosshair interaction, so the ScrollRect never receives
        // a pointer and cannot scroll itself - drive it by hand instead.
        float scrollablePixels = Mathf.Max(1.0f, listContent.rect.height - scrollRect.viewport.rect.height);
        float pixels = 0.0f;

        pixels += Input.mouseScrollDelta.y * 120.0f;   // one wheel notch
        if (Input.GetKey(KeyCode.UpArrow))
        {
            pixels += scrollSpeed * Time.unscaledDeltaTime;
        }
        if (Input.GetKey(KeyCode.DownArrow))
        {
            pixels -= scrollSpeed * Time.unscaledDeltaTime;
        }

        if (Mathf.Approximately(pixels, 0.0f))
        {
            return;
        }

        scrollRect.verticalNormalizedPosition =
            Mathf.Clamp01(scrollRect.verticalNormalizedPosition + pixels / scrollablePixels);
    }

    // =========================================================
    // UI CONSTRUCTION
    // =========================================================

    private void EnsureUiBuilt()
    {
        if (historyCanvas != null)
        {
            return;
        }

        GameObject canvasObject = new GameObject(
            "ExperimentHistoryCanvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));

        canvasObject.transform.SetParent(transform, false);

        historyCanvas = canvasObject.GetComponent<Canvas>();
        historyCanvas.renderMode = RenderMode.WorldSpace;
        historyCanvas.worldCamera = Camera.main;
        historyCanvas.sortingOrder = 520; // just above the learning UI

        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(1200.0f, 780.0f);
        canvasRect.localScale = Vector3.one * 0.001f;

        // Dim behind the panel. Must not swallow crosshair clicks meant for the rows.
        GameObject background = new GameObject("Background", typeof(RectTransform), typeof(Image));
        background.transform.SetParent(canvasObject.transform, false);
        RectTransform backgroundRect = background.GetComponent<RectTransform>();
        backgroundRect.anchorMin = Vector2.zero;
        backgroundRect.anchorMax = Vector2.one;
        backgroundRect.offsetMin = Vector2.zero;
        backgroundRect.offsetMax = Vector2.zero;
        Image backgroundImage = background.GetComponent<Image>();
        backgroundImage.color = new Color(0.0f, 0.0f, 0.0f, 0.72f);
        backgroundImage.raycastTarget = false;

        GameObject panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(canvasObject.transform, false);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.sizeDelta = new Vector2(1160.0f, 740.0f);
        panel.GetComponent<Image>().color = PanelColor;
        panel.GetComponent<Image>().raycastTarget = false;

        headerText = CreateText("Header", panel.transform,
            new Vector2(0.0f, 330.0f), new Vector2(1100.0f, 60.0f),
            34.0f, FontStyles.Bold, TextAlignmentOptions.Left);
        headerText.text = "Experiment History";

        TMP_Text hint = CreateText("Hint", panel.transform,
            new Vector2(0.0f, 292.0f), new Vector2(1100.0f, 34.0f),
            20.0f, FontStyles.Normal, TextAlignmentOptions.Left);
        hint.text = "Click a row to expand it.  Mouse wheel / arrow keys scroll.  " +
                    toggleKey + " or " + closeKey + " closes.";
        hint.color = NeutralColor;

        BuildFilterRow(panel.transform);
        BuildScrollView(panel.transform);

        footerText = CreateText("Footer", panel.transform,
            new Vector2(0.0f, -338.0f), new Vector2(1100.0f, 34.0f),
            17.0f, FontStyles.Normal, TextAlignmentOptions.Left);
        footerText.color = NeutralColor;

        CreateButton("CloseButton", panel.transform, "Close",
            new Vector2(500.0f, 330.0f), new Vector2(140.0f, 48.0f), Close);

        CreateButton("ClearButton", panel.transform, "Clear History",
            new Vector2(478.0f, -338.0f), new Vector2(184.0f, 42.0f), () =>
            {
                ExperimentHistoryManager.Instance.ClearHistory();
                expandedAttemptId = string.Empty;
                listDirty = true;
            });
    }

    private void BuildFilterRow(Transform parent)
    {
        GameObject row = new GameObject("FilterRow", typeof(RectTransform));
        row.transform.SetParent(parent, false);

        filterRow = row.GetComponent<RectTransform>();
        filterRow.anchorMin = new Vector2(0.5f, 0.5f);
        filterRow.anchorMax = new Vector2(0.5f, 0.5f);
        filterRow.pivot = new Vector2(0.5f, 0.5f);
        filterRow.anchoredPosition = new Vector2(0.0f, 246.0f);
        filterRow.sizeDelta = new Vector2(1100.0f, 46.0f);

        HorizontalLayoutGroup layout = row.AddComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.spacing = 8.0f;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
    }

    private void BuildScrollView(Transform parent)
    {
        GameObject viewport = new GameObject("Viewport",
            typeof(RectTransform), typeof(Image), typeof(Mask), typeof(ScrollRect));
        viewport.transform.SetParent(parent, false);

        RectTransform viewportRect = viewport.GetComponent<RectTransform>();
        viewportRect.anchorMin = new Vector2(0.5f, 0.5f);
        viewportRect.anchorMax = new Vector2(0.5f, 0.5f);
        viewportRect.pivot = new Vector2(0.5f, 0.5f);
        viewportRect.anchoredPosition = new Vector2(0.0f, -42.0f);
        viewportRect.sizeDelta = new Vector2(1100.0f, 520.0f);

        Image viewportImage = viewport.GetComponent<Image>();
        viewportImage.color = new Color(0.09f, 0.10f, 0.14f, 1.0f);
        viewportImage.raycastTarget = false;
        viewport.GetComponent<Mask>().showMaskGraphic = true;

        GameObject content = new GameObject("Content", typeof(RectTransform));
        content.transform.SetParent(viewport.transform, false);

        listContent = content.GetComponent<RectTransform>();
        listContent.anchorMin = new Vector2(0.0f, 1.0f);
        listContent.anchorMax = new Vector2(1.0f, 1.0f);
        listContent.pivot = new Vector2(0.5f, 1.0f);
        listContent.anchoredPosition = Vector2.zero;
        listContent.sizeDelta = new Vector2(0.0f, 0.0f);

        VerticalLayoutGroup layout = content.AddComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.spacing = 6.0f;
        layout.padding = new RectOffset(10, 10, 10, 10);
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        layout.childControlWidth = true;
        layout.childControlHeight = true;

        ContentSizeFitter fitter = content.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        scrollRect = viewport.GetComponent<ScrollRect>();
        scrollRect.content = listContent;
        scrollRect.viewport = viewportRect;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 40.0f;
    }

    // =========================================================
    // LIST POPULATION
    // =========================================================

    private void RebuildList()
    {
        listDirty = false;

        if (listContent == null)
        {
            return;
        }

        ClearChildren(listContent);

        ExperimentHistoryManager manager = ExperimentHistoryManager.Instance;
        RebuildFilterRow(manager);

        List<ExperimentAttempt> attempts = filterReactionId < 0
            ? manager.GetHistory()
            : manager.GetHistoryForReaction(filterReactionId);

        attempts.Reverse(); // newest first

        int successCount = 0;
        for (int i = 0; i < attempts.Count; i++)
        {
            if (attempts[i].outcome == ExperimentOutcome.Success)
            {
                successCount++;
            }
        }

        headerText.text = "Experiment History  -  " + attempts.Count + " attempt" +
                          (attempts.Count == 1 ? "" : "s") + ", " + successCount + " successful";

        if (footerText != null)
        {
            footerText.text = "Saved to: " + ExperimentHistoryManager.SaveFilePath;
        }

        if (attempts.Count == 0)
        {
            TMP_Text empty = CreateText("Empty", listContent,
                Vector2.zero, new Vector2(1050.0f, 60.0f),
                24.0f, FontStyles.Italic, TextAlignmentOptions.Center);
            empty.text = "No experiments recorded yet. Perform an experiment and it will appear here.";
            empty.color = NeutralColor;
            AddLayoutHeight(empty.gameObject, 60.0f);
            return;
        }

        for (int i = 0; i < attempts.Count; i++)
        {
            CreateAttemptRow(attempts[i], attempts.Count - i);
        }
    }

    private void RebuildFilterRow(ExperimentHistoryManager manager)
    {
        if (filterRow == null)
        {
            return;
        }

        ClearChildren(filterRow);

        CreateFilterButton("All", -1);

        List<int> reactionIds = manager.GetRecordedReactionIds();
        for (int i = 0; i < reactionIds.Count; i++)
        {
            int reactionId = reactionIds[i];
            CreateFilterButton("R" + reactionId, reactionId);
        }
    }

    private void CreateFilterButton(string label, int reactionId)
    {
        Button button = CreateButton("Filter_" + label, filterRow, label,
            Vector2.zero, new Vector2(reactionId < 0 ? 90.0f : 70.0f, 40.0f),
            () =>
            {
                filterReactionId = reactionId;
                expandedAttemptId = string.Empty;
                listDirty = true;
            });

        Image image = button.GetComponent<Image>();
        image.color = filterReactionId == reactionId ? ButtonActiveColor : ButtonColor;

        LayoutElement element = button.gameObject.AddComponent<LayoutElement>();
        element.preferredWidth = reactionId < 0 ? 90.0f : 70.0f;
        element.preferredHeight = 40.0f;
    }

    private void CreateAttemptRow(ExperimentAttempt attempt, int displayNumber)
    {
        bool expanded = attempt.attemptId == expandedAttemptId;

        GameObject row = new GameObject("Attempt_" + displayNumber,
            typeof(RectTransform), typeof(Image), typeof(Button));
        row.transform.SetParent(listContent, false);

        Image rowImage = row.GetComponent<Image>();
        rowImage.color = expanded ? RowSelectedColor : RowColor;

        string capturedId = attempt.attemptId;
        row.GetComponent<Button>().onClick.AddListener(() =>
        {
            expandedAttemptId = (expandedAttemptId == capturedId) ? string.Empty : capturedId;
            listDirty = true;
        });

        VerticalLayoutGroup rowLayout = row.AddComponent<VerticalLayoutGroup>();
        rowLayout.padding = new RectOffset(14, 14, 10, 10);
        rowLayout.spacing = 4.0f;
        rowLayout.childForceExpandWidth = true;
        rowLayout.childForceExpandHeight = false;
        rowLayout.childControlWidth = true;
        rowLayout.childControlHeight = true;

        ContentSizeFitter rowFitter = row.AddComponent<ContentSizeFitter>();
        rowFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        rowFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        // Title on the left, the star rating in a fixed-width column on the right.
        GameObject titleLine = new GameObject("TitleLine", typeof(RectTransform));
        titleLine.transform.SetParent(row.transform, false);
        HorizontalLayoutGroup titleLayout = titleLine.AddComponent<HorizontalLayoutGroup>();
        titleLayout.spacing = 12.0f;
        titleLayout.childAlignment = TextAnchor.MiddleLeft;
        titleLayout.childControlWidth = true;
        titleLayout.childControlHeight = true;
        titleLayout.childForceExpandWidth = false;
        titleLayout.childForceExpandHeight = true;
        AddLayoutHeight(titleLine, 32.0f);

        TMP_Text title = CreateText("Title", titleLine.transform, Vector2.zero,
            new Vector2(1020.0f, 34.0f), 24.0f, FontStyles.Bold, TextAlignmentOptions.Left);
        title.text = string.Format("#{0}  {1}   -   {2}", displayNumber, attempt.reactionName, attempt.OutcomeLabel);
        title.color = attempt.outcome == ExperimentOutcome.Success ? SuccessColor
                    : attempt.IsFailure ? FailColor
                    : NeutralColor;
        title.textWrappingMode = TextWrappingModes.NoWrap;
        title.overflowMode = TextOverflowModes.Ellipsis;
        LayoutElement titleElement = title.gameObject.AddComponent<LayoutElement>();
        titleElement.minWidth = 0.0f;
        titleElement.preferredWidth = 1.0f;     // take whatever the stars leave, never push them out
        titleElement.flexibleWidth = 1.0f;

        float accuracy;
        int stars;
        bool rated = ExperimentScoring.TryGetScore(attempt, out accuracy, out stars);
        CreateStarColumn(titleLine.transform, rated, stars);

        TMP_Text summary = CreateText("Summary", row.transform, Vector2.zero,
            new Vector2(1020.0f, 28.0f), 19.0f, FontStyles.Normal, TextAlignmentOptions.Left);
        summary.text = BuildSummaryLine(attempt);
        summary.color = NeutralColor;
        summary.textWrappingMode = TextWrappingModes.NoWrap;
        summary.overflowMode = TextOverflowModes.Ellipsis;
        AddLayoutHeight(summary.gameObject, 26.0f);

        if (!expanded)
        {
            return;
        }

        TMP_Text detail = CreateText("Detail", row.transform, Vector2.zero,
            new Vector2(1020.0f, 100.0f), 19.0f, FontStyles.Normal, TextAlignmentOptions.TopLeft);
        detail.text = BuildDetailText(attempt);
        detail.color = Color.white;

        // Let the text tell the layout how tall it needs to be.
        detail.ForceMeshUpdate();
        float preferredHeight = detail.preferredHeight + 8.0f;
        AddLayoutHeight(detail.gameObject, preferredHeight);
    }

    // =========================================================
    // STARS
    // =========================================================

    private const float StarSize = 26.0f;
    private const float StarSpacing = 4.0f;
    private static readonly Color EarnedStarColor = new Color(1.0f, 0.84f, 0.30f, 1.0f);
    private static readonly Color EmptyStarColor = new Color(1.0f, 1.0f, 1.0f, 0.16f);

    /// <summary>
    /// Three stars, gold for each one earned. The column is always the same width - empty for an
    /// attempt with no verdict to rate - so the ratings line up down the whole list.
    /// </summary>
    private void CreateStarColumn(Transform parent, bool rated, int stars)
    {
        GameObject column = new GameObject("Stars", typeof(RectTransform));
        column.transform.SetParent(parent, false);

        LayoutElement columnElement = column.AddComponent<LayoutElement>();
        float width = ExperimentScoring.MaxStars * StarSize + (ExperimentScoring.MaxStars - 1) * StarSpacing;
        columnElement.minWidth = width;
        columnElement.preferredWidth = width;
        columnElement.flexibleWidth = 0.0f;

        HorizontalLayoutGroup layout = column.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = StarSpacing;
        layout.childAlignment = TextAnchor.MiddleRight;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        if (!rated)
        {
            return;
        }

        Sprite sprite = StarSprite.Get();
        for (int i = 0; i < ExperimentScoring.MaxStars; i++)
        {
            GameObject star = new GameObject("Star" + (i + 1), typeof(RectTransform), typeof(Image));
            star.transform.SetParent(column.transform, false);
            star.GetComponent<RectTransform>().sizeDelta = new Vector2(StarSize, StarSize);

            Image image = star.GetComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.color = i < stars ? EarnedStarColor : EmptyStarColor;
            image.raycastTarget = false;     // the row behind it is the button
        }
    }

    private string BuildSummaryLine(ExperimentAttempt attempt)
    {
        StringBuilder builder = new StringBuilder();
        builder.Append(attempt.Timestamp.ToString("dd MMM yyyy HH:mm"));
        builder.Append("   |   ");
        builder.Append(attempt.ModeLabel);

        float accuracy;
        int stars;
        if (attempt.outcome == ExperimentOutcome.Success &&
            ExperimentScoring.TryGetScore(attempt, out accuracy, out stars))
        {
            builder.AppendFormat("   |   accuracy {0:0}%", accuracy);
        }

        builder.Append("   |   ");
        builder.AppendFormat("{0:F1}s", attempt.durationSeconds);
        builder.Append("   |   ");
        builder.AppendFormat("{0} step{1}", attempt.steps.Count, attempt.steps.Count == 1 ? "" : "s");

        if (attempt.aiInteractions.Count > 0)
        {
            builder.AppendFormat("   |   {0} AI question{1}",
                attempt.aiInteractions.Count, attempt.aiInteractions.Count == 1 ? "" : "s");
        }

        return builder.ToString();
    }

    private string BuildDetailText(ExperimentAttempt attempt)
    {
        StringBuilder builder = new StringBuilder();

        float accuracy;
        int stars;
        if (ExperimentScoring.TryGetScore(attempt, out accuracy, out stars))
        {
            builder.AppendLine("<b>Score</b>");
            builder.AppendFormat("   {0} of {1} stars   -   accuracy {2:0}%   -   {3}\n",
                stars, ExperimentScoring.MaxStars, Mathf.Max(0.0f, accuracy), attempt.ModeLabel);
            builder.AppendLine("   Accuracy is your least precise reagent, against the Standard margin for error.");
            builder.AppendLine();
        }

        if (attempt.quantitiesUsed.Count > 0)
        {
            builder.AppendLine("<b>Quantities</b>");
            foreach (KeyValuePair<string, float> pair in attempt.quantitiesUsed)
            {
                float target;
                if (attempt.targetQuantities.TryGetValue(pair.Key, out target))
                {
                    builder.AppendFormat("   {0}: {1:F1}  (target {2:F1})\n", pair.Key, pair.Value, target);
                }
                else
                {
                    builder.AppendFormat("   {0}: {1:F1}\n", pair.Key, pair.Value);
                }
            }
            builder.AppendLine();
        }

        builder.AppendLine("<b>Steps</b>");
        if (attempt.steps.Count == 0)
        {
            builder.AppendLine("   (nothing recorded)");
        }
        else
        {
            for (int i = 0; i < attempt.steps.Count; i++)
            {
                ExperimentStep step = attempt.steps[i];
                string marker = step.wasCorrect ? "OK  " : "!!  ";
                builder.AppendFormat("   {0:F1}s  {1}{2}\n", step.timestamp, marker, step.action);
            }
        }

        if (attempt.aiInteractions.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("<b>AI Lab Assistant</b>");
            for (int i = 0; i < attempt.aiInteractions.Count; i++)
            {
                AIInteraction interaction = attempt.aiInteractions[i];
                if (!string.IsNullOrEmpty(interaction.userQuestion))
                {
                    builder.AppendFormat("   You: {0}\n", interaction.userQuestion);
                }
                if (!string.IsNullOrEmpty(interaction.aiResponse))
                {
                    builder.AppendFormat("   AI:  {0}\n", interaction.aiResponse);
                }
            }
        }

        return builder.ToString();
    }

    /// <summary>Marks the list for a rebuild, e.g. after a new attempt finishes.</summary>
    public void MarkDirty()
    {
        listDirty = true;
    }

    // =========================================================
    // HELPERS
    // =========================================================

    /// <summary>
    /// Destroy() only takes effect at the end of the frame, so the old rows would still be
    /// counted by the layout group while the new ones are added. Unparent first so they leave
    /// the layout immediately.
    /// </summary>
    private static void ClearChildren(RectTransform parent)
    {
        if (parent == null)
        {
            return;
        }

        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            Transform child = parent.GetChild(i);
            child.SetParent(null, false);
            Destroy(child.gameObject);
        }
    }

    private void PositionInFrontOfCamera()
    {
        if (historyCanvas == null)
        {
            return;
        }

        Camera camera = Camera.main;
        if (camera == null)
        {
            return;
        }

        historyCanvas.worldCamera = camera;
        historyCanvas.transform.position =
            camera.transform.position + camera.transform.forward * distanceFromCamera;
        historyCanvas.transform.rotation = camera.transform.rotation;
    }

    private static void AddLayoutHeight(GameObject target, float height)
    {
        LayoutElement element = target.GetComponent<LayoutElement>();
        if (element == null)
        {
            element = target.AddComponent<LayoutElement>();
        }
        element.preferredHeight = height;
        element.minHeight = height;
    }

    private TMP_Text CreateText(string objectName, Transform parent, Vector2 anchoredPosition,
                                Vector2 size, float fontSize, FontStyles fontStyle,
                                TextAlignmentOptions alignment)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);

        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0.5f, 0.5f);
        textRect.anchorMax = new Vector2(0.5f, 0.5f);
        textRect.pivot = new Vector2(0.5f, 0.5f);
        textRect.anchoredPosition = anchoredPosition;
        textRect.sizeDelta = size;

        TextMeshProUGUI textComponent = textObject.GetComponent<TextMeshProUGUI>();
        textComponent.fontSize = fontSize;
        textComponent.fontStyle = fontStyle;
        textComponent.alignment = alignment;
        textComponent.color = Color.white;
        textComponent.textWrappingMode = TextWrappingModes.Normal;
        textComponent.raycastTarget = false;

        return textComponent;
    }

    private Button CreateButton(string objectName, Transform parent, string label,
                                Vector2 anchoredPosition, Vector2 size,
                                UnityEngine.Events.UnityAction onClick)
    {
        GameObject buttonObject = new GameObject(objectName,
            typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);

        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
        buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
        buttonRect.pivot = new Vector2(0.5f, 0.5f);
        buttonRect.anchoredPosition = anchoredPosition;
        buttonRect.sizeDelta = size;

        buttonObject.GetComponent<Image>().color = ButtonColor;

        Button button = buttonObject.GetComponent<Button>();
        if (onClick != null)
        {
            button.onClick.AddListener(onClick);
        }

        TMP_Text buttonLabel = CreateText("Label", buttonObject.transform, Vector2.zero,
            size - new Vector2(8.0f, 8.0f), 20.0f, FontStyles.Bold, TextAlignmentOptions.Center);
        buttonLabel.text = label;

        return button;
    }
}
