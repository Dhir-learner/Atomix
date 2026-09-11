using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Draws the three scientific graphs for a reaction into a runtime <see cref="Texture2D"/> shown
/// through a <see cref="RawImage"/>, with TextMeshPro labels laid over it for the axis numbers and
/// annotations.
///
/// The texture carries everything geometric - axes, grid, the energy curve, level lines, arrows,
/// bars - and the text is real TMP so it stays crisp on a world-space canvas instead of being
/// blitted in at a fixed resolution.
///
/// Nothing here needs a scene or prefab: call <see cref="Create"/> and then <see cref="Render"/>.
/// </summary>
public class ReactionGraphRenderer : MonoBehaviour
{
    // --- Palette ----------------------------------------------------------------------
    private static readonly Color32 Background = new Color32(15, 18, 28, 255);
    private static readonly Color32 PlotBackground = new Color32(21, 26, 38, 255);
    private static readonly Color32 GridColor = new Color32(45, 54, 74, 255);
    private static readonly Color32 AxisColor = new Color32(150, 165, 190, 255);
    private static readonly Color32 GuideColor = new Color32(120, 134, 160, 255);

    /// <summary>Exothermic reactions are drawn in red, endothermic in blue.</summary>
    public static readonly Color32 ExothermicColor = new Color32(255, 106, 84, 255);
    public static readonly Color32 EndothermicColor = new Color32(86, 168, 255, 255);

    private static readonly Color32 ActivationColor = new Color32(255, 205, 92, 255);
    private static readonly Color32 EntropyUpColor = new Color32(112, 226, 146, 255);
    private static readonly Color32 EntropyDownColor = new Color32(255, 168, 92, 255);
    private static readonly Color TextColor = new Color(0.90f, 0.93f, 0.98f, 1.0f);
    private static readonly Color MutedTextColor = new Color(0.66f, 0.72f, 0.82f, 1.0f);

    [Header("Texture")]
    [Tooltip("Pixel size of the generated graph texture.")]
    public int textureWidth = 980;
    public int textureHeight = 520;

    [Header("Labels")]
    public float axisLabelSize = 18.0f;
    public float annotationSize = 19.0f;
    public float titleSize = 22.0f;

    private RawImage image;
    private Texture2D texture;
    private GraphPainter painter;
    private RectTransform selfRect;

    private readonly List<TMP_Text> labelPool = new List<TMP_Text>();
    private int labelsUsed = 0;

    // Plot area inside the texture, in pixels.
    private int plotLeft, plotRight, plotBottom, plotTop;

    /// <summary>The data currently drawn, or null.</summary>
    public ReactionGraphData CurrentData { get; private set; }

    /// <summary>The graph type currently drawn.</summary>
    public ReactionGraphType CurrentType { get; private set; }

    // =========================================================
    // CREATION
    // =========================================================

    /// <summary>Builds a renderer as a child of <paramref name="parent"/> filling the given size.</summary>
    public static ReactionGraphRenderer Create(Transform parent, Vector2 size)
    {
        GameObject host = new GameObject("ReactionGraph",
            typeof(RectTransform), typeof(RawImage), typeof(ReactionGraphRenderer));
        host.transform.SetParent(parent, false);

        RectTransform rect = host.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = size;

        ReactionGraphRenderer renderer = host.GetComponent<ReactionGraphRenderer>();
        renderer.textureWidth = Mathf.Clamp(Mathf.RoundToInt(size.x), 256, 2048);
        renderer.textureHeight = Mathf.Clamp(Mathf.RoundToInt(size.y), 256, 2048);
        return renderer;
    }

    void Awake()
    {
        selfRect = GetComponent<RectTransform>();
        image = GetComponent<RawImage>();
        if (image != null)
        {
            // The graph must never swallow a crosshair click meant for the buttons around it.
            image.raycastTarget = false;
        }
    }

    void OnDestroy()
    {
        if (texture != null)
        {
            Destroy(texture);
            texture = null;
        }
    }

    private void EnsureTexture()
    {
        if (texture != null &&
            texture.width == textureWidth &&
            texture.height == textureHeight)
        {
            return;
        }

        if (texture != null)
        {
            Destroy(texture);
        }

        texture = new Texture2D(textureWidth, textureHeight, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;

        painter = new GraphPainter(texture);

        if (image == null)
        {
            image = GetComponent<RawImage>();
        }
        if (image != null)
        {
            image.texture = texture;
        }
    }

    // =========================================================
    // RENDER
    // =========================================================

    /// <summary>Draws one graph. Safe to call every time the tab changes.</summary>
    public void Render(ReactionGraphData data, ReactionGraphType type)
    {
        if (data == null)
        {
            return;
        }

        CurrentData = data;
        CurrentType = type;

        EnsureTexture();
        BeginLabels();

        painter.Clear(Background);

        switch (type)
        {
            case ReactionGraphType.EnergyProfile:
                DrawEnergyProfile(data);
                break;

            case ReactionGraphType.EnthalpyLevels:
                DrawEnthalpyLevels(data);
                break;

            case ReactionGraphType.Entropy:
                DrawEntropy(data);
                break;
        }

        painter.Apply();
        EndLabels();
    }

    private Color32 TypeColor(ReactionGraphData data)
    {
        return data.IsExothermic ? ExothermicColor : EndothermicColor;
    }

    // =========================================================
    // GRAPH 1 - ENERGY VS REACTION PROGRESS
    // =========================================================

    private void DrawEnergyProfile(ReactionGraphData data)
    {
        SetPlotArea(96, 34, 62, 64);

        // --- Vertical range ------------------------------------------------------------
        float peak = data.PeakEnergy;
        float lowest = Mathf.Min(0.0f, data.enthalpyChange);
        float span = Mathf.Max(1.0f, peak - lowest);

        float yMin = lowest - span * 0.16f;
        float yMax = peak + span * 0.16f;

        DrawPlotFrame();
        DrawYAxisTicks(yMin, yMax);
        DrawXAxisProgressTicks();

        Color32 curveColor = TypeColor(data);

        // --- Reactant and product level guides -----------------------------------------
        int reactantY = MapY(0.0f, yMin, yMax);
        int productY = MapY(data.enthalpyChange, yMin, yMax);

        painter.DashedLine(plotLeft, reactantY, plotRight, reactantY, 1.6f, GuideColor, 9, 7);
        painter.DashedLine(plotLeft, productY, plotRight, productY, 1.6f, GuideColor, 9, 7);

        // --- The curve -----------------------------------------------------------------
        const int Samples = 260;
        List<Vector2> curve = new List<Vector2>(Samples);
        for (int i = 0; i < Samples; i++)
        {
            float progress = i / (Samples - 1.0f);
            float energy = data.SampleEnergy(progress);
            curve.Add(new Vector2(MapX(progress), MapY(energy, yMin, yMax)));
        }

        // A soft tint under the curve, down to whichever level is lower.
        int fillBase = Mathf.Min(reactantY, productY);
        painter.FillUnderCurve(curve, fillBase, curveColor, 0.16f);
        painter.Polyline(curve, 3.4f, curveColor);

        // --- Transition state ----------------------------------------------------------
        float peakProgress = data.PeakProgress;
        int peakX = MapX(peakProgress);
        int peakY = MapY(peak, yMin, yMax);

        painter.DashedLine(peakX, plotBottom, peakX, peakY, 1.4f, GuideColor, 7, 6);
        painter.FilledCircle(peakX, peakY, 6.0f, ActivationColor);

        AddLabel("Transition state", peakX, peakY + 16, 0.5f, 0.0f,
            axisLabelSize, ActivationColor, TextAlignmentOptions.Bottom);

        // --- Ea arrow ------------------------------------------------------------------
        // Drawn to the left of the peak so it cannot sit on top of the curve.
        int eaX = Mathf.RoundToInt(Mathf.Lerp(plotLeft, peakX, 0.55f));
        painter.DoubleArrow(eaX, reactantY, eaX, peakY, 2.2f, ActivationColor, 9.0f);

        string eaText = string.Format("Ea = {0:F0} kJ/mol", data.activationEnergy);

        // When Ea is small next to dH the arrow is only a few pixels tall, and a label beside it
        // would land on the curve or on the "Reactants" caption. Lift it clear instead.
        if (Mathf.Abs(peakY - reactantY) < 70)
        {
            AddLabel(eaText, eaX,
                Mathf.Min(plotTop - 26, Mathf.Max(reactantY, peakY) + 40),
                0.5f, 0.0f, annotationSize, ActivationColor, TextAlignmentOptions.Bottom);
        }
        else
        {
            AddLabelBeside(eaText, eaX, (reactantY + peakY) / 2, 10,
                annotationSize, ActivationColor);
        }

        // --- dH arrow ------------------------------------------------------------------
        int dhX = Mathf.RoundToInt(Mathf.Lerp(peakX, plotRight, 0.45f));
        painter.DoubleArrow(dhX, reactantY, dhX, productY, 2.2f, curveColor, 9.0f);

        string dhWord = data.IsExothermic ? "released" : "absorbed";
        AddLabelBeside(
            string.Format("dH = {0:+0.0;-0.0} kJ/mol\n({1:F0} kJ/mol {2})",
                data.enthalpyChange, Mathf.Abs(data.enthalpyChange), dhWord),
            dhX, (reactantY + productY) / 2, 10, annotationSize, curveColor);

        // --- Level captions ------------------------------------------------------------
        AddLabel("Reactants", plotLeft + 8, reactantY + 8, 0.0f, 0.0f,
            axisLabelSize, MutedTextColor, TextAlignmentOptions.BottomLeft);

        AddLabel("Products", plotRight - 8, productY + 8, 1.0f, 0.0f,
            axisLabelSize, MutedTextColor, TextAlignmentOptions.BottomRight);

        // --- Axis titles ---------------------------------------------------------------
        AddLabel("Reaction Progress", (plotLeft + plotRight) / 2, 16, 0.5f, 0.0f,
            annotationSize, TextColor, TextAlignmentOptions.Bottom);

        AddRotatedLabel("Energy (kJ/mol)", 24, (plotBottom + plotTop) / 2,
            annotationSize, TextColor);

        AddLabel(data.IsExothermic
                ? "Exothermic - the products sit below the reactants"
                : "Endothermic - the products sit above the reactants",
            (plotLeft + plotRight) / 2, textureHeight - 12, 0.5f, 1.0f,
            titleSize, new Color(curveColor.r / 255.0f, curveColor.g / 255.0f, curveColor.b / 255.0f, 1.0f),
            TextAlignmentOptions.Top);
    }

    // =========================================================
    // GRAPH 2 - EXOTHERMIC / ENDOTHERMIC LEVELS
    // =========================================================

    private void DrawEnthalpyLevels(ReactionGraphData data)
    {
        SetPlotArea(96, 34, 62, 64);

        float lowest = Mathf.Min(0.0f, data.enthalpyChange);
        float highest = Mathf.Max(0.0f, data.enthalpyChange);
        float span = Mathf.Max(1.0f, highest - lowest);

        float yMin = lowest - span * 0.30f;
        float yMax = highest + span * 0.30f;

        DrawPlotFrame();
        DrawYAxisTicks(yMin, yMax);

        Color32 color = TypeColor(data);

        int reactantY = MapY(0.0f, yMin, yMax);
        int productY = MapY(data.enthalpyChange, yMin, yMax);

        int barLeft = plotLeft + 60;
        int barRight = plotRight - 60;
        int barWidth = (barRight - barLeft);

        int leftBarEnd = barLeft + Mathf.RoundToInt(barWidth * 0.34f);
        int rightBarStart = barLeft + Mathf.RoundToInt(barWidth * 0.66f);

        // Reactant level (always the zero reference) and product level.
        painter.ThickLine(barLeft, reactantY, leftBarEnd, reactantY, 6.0f, GuideColor);
        painter.ThickLine(rightBarStart, productY, barRight, productY, 6.0f, color);

        // A faint connector so the eye follows one level to the other.
        painter.DashedLine(leftBarEnd, reactantY, rightBarStart, reactantY, 1.6f, GridColor, 8, 8);
        painter.DashedLine(leftBarEnd, productY, rightBarStart, productY, 1.6f, GridColor, 8, 8);

        AddLabel("Reactants\n" + ShortFormula(data, true),
            (barLeft + leftBarEnd) / 2, reactantY + 12, 0.5f, 0.0f,
            annotationSize, MutedTextColor, TextAlignmentOptions.Bottom);

        AddLabel("Products\n" + ShortFormula(data, false),
            (rightBarStart + barRight) / 2, productY - 12, 0.5f, 1.0f,
            annotationSize,
            new Color(color.r / 255.0f, color.g / 255.0f, color.b / 255.0f, 1.0f),
            TextAlignmentOptions.Top);

        // The dH arrow points the way the energy actually moves.
        int arrowX = (leftBarEnd + rightBarStart) / 2;
        painter.Arrow(arrowX, reactantY, arrowX, productY, 3.0f, color, 13.0f);

        AddLabel(string.Format("dH = {0:+0.0;-0.0} kJ/mol", data.enthalpyChange),
            arrowX + 14, (reactantY + productY) / 2, 0.0f, 0.5f,
            annotationSize + 2.0f,
            new Color(color.r / 255.0f, color.g / 255.0f, color.b / 255.0f, 1.0f),
            TextAlignmentOptions.Left);

        // Headline classification.
        string headline = data.IsExothermic ? "EXOTHERMIC" : "ENDOTHERMIC";
        string subtitle = data.IsExothermic
            ? string.Format("{0:F1} kJ/mol of heat flows OUT of the reaction, into the surroundings.\n" +
                            "The flask and its contents get hotter.", Mathf.Abs(data.enthalpyChange))
            : string.Format("{0:F1} kJ/mol of heat must flow IN from the surroundings.\n" +
                            "The flask and its contents get colder unless you keep heating.",
                            Mathf.Abs(data.enthalpyChange));

        AddLabel(headline, (plotLeft + plotRight) / 2, textureHeight - 10, 0.5f, 1.0f,
            titleSize + 8.0f,
            new Color(color.r / 255.0f, color.g / 255.0f, color.b / 255.0f, 1.0f),
            TextAlignmentOptions.Top);

        AddLabel(subtitle, (plotLeft + plotRight) / 2, 14, 0.5f, 0.0f,
            axisLabelSize + 1.0f, MutedTextColor, TextAlignmentOptions.Bottom);

        AddRotatedLabel("Enthalpy (kJ/mol)", 24, (plotBottom + plotTop) / 2,
            annotationSize, TextColor);
    }

    // =========================================================
    // GRAPH 3 - ENTROPY
    // =========================================================

    private void DrawEntropy(ReactionGraphData data)
    {
        // Full-width layout: a big signed bar on top, the comparison strip underneath.
        SetPlotArea(96, 34, 252, 92);

        // Scale against the largest magnitude in the whole catalog so the bars are comparable.
        float scale = LargestEntropyMagnitude();
        float axisMax = Mathf.Max(50.0f, scale * 1.12f);

        DrawPlotFrame();

        int zeroX = MapXValue(0.0f, -axisMax, axisMax);
        int barY = (plotBottom + plotTop) / 2;
        int barHalf = Mathf.Max(14, (plotTop - plotBottom) / 6);

        // Zero line and a light grid so the magnitude is readable.
        DrawEntropyAxisTicks(-axisMax, axisMax);
        painter.ThickLine(zeroX, plotBottom, zeroX, plotTop, 2.0f, AxisColor);

        Color32 barColor = data.entropyChange >= 0.0f ? EntropyUpColor : EntropyDownColor;
        int valueX = MapXValue(data.entropyChange, -axisMax, axisMax);

        painter.FillRect(Mathf.Min(zeroX, valueX), barY - barHalf,
                         Mathf.Max(zeroX, valueX), barY + barHalf, barColor);

        bool positive = data.entropyChange >= 0.0f;

        // Both captions are centred over the bar's own span rather than hung off its end. The
        // largest dS in the catalog reaches almost to the plot edge, and a label beside it there
        // would run off the texture.
        int barCentre = (zeroX + valueX) / 2;

        // Value and verdict are one two-line label above the bar, which leaves the space below it
        // free for the ordered/disordered axis captions.
        AddLabel(string.Format("dS = {0:+0.0;-0.0} J/(mol K)\n{1}",
                data.entropyChange,
                positive ? "Disorder INCREASES" : "Disorder DECREASES"),
            barCentre, barY + barHalf + 10, 0.5f, 0.0f,
            annotationSize + 3.0f,
            new Color(barColor.r / 255.0f, barColor.g / 255.0f, barColor.b / 255.0f, 1.0f),
            TextAlignmentOptions.Bottom);

        AddLabel("more ordered  <---", zeroX - 12, plotBottom + 8, 1.0f, 0.0f,
            axisLabelSize, MutedTextColor, TextAlignmentOptions.BottomRight);
        AddLabel("--->  more disordered", zeroX + 12, plotBottom + 8, 0.0f, 0.0f,
            axisLabelSize, MutedTextColor, TextAlignmentOptions.BottomLeft);

        AddLabel("Entropy change", (plotLeft + plotRight) / 2, textureHeight - 10, 0.5f, 1.0f,
            titleSize + 4.0f, TextColor, TextAlignmentOptions.Top);

        DrawEntropyComparisonStrip(data, axisMax);

        // Gibbs working sits along the bottom - this is where entropy earns its place. Split
        // over two lines because one line of it is wider than the texture.
        // The plain-English verdict is not repeated here: ReactionGraphUI already shows
        // SpontaneitySummary() on the status line directly under the graph.
        string gibbs = string.Format(
            "dG at 25 C  =  dH - T dS  =  {0:+0.0;-0.0}  -  298 x ({1:+0.0;-0.0} / 1000)  =  {2:+0.0;-0.0} kJ/mol\n{3}",
            data.enthalpyChange, data.entropyChange, data.StandardGibbsFreeEnergy,
            data.IsSpontaneousAtRoomTemperature
                ? "dG is negative, so the reaction is spontaneous at room temperature."
                : "dG is positive, so the reaction is not spontaneous at room temperature.");

        AddWideLabel(gibbs, textureWidth / 2, 16, axisLabelSize + 1.0f, TextColor);
    }

    /// <summary>
    /// A row of small bars, one per reaction, so this reaction's entropy change can be read
    /// against the other seven rather than in isolation.
    /// </summary>
    private void DrawEntropyComparisonStrip(ReactionGraphData current, float axisMax)
    {
        ReactionGraphCatalog catalog = ReactionGraphCatalog.Shared;
        if (catalog == null)
        {
            return;
        }

        int stripTop = plotBottom - 20;
        int stripBottom = 84;
        if (stripTop - stripBottom < 40)
        {
            return;
        }

        // Hangs below the plot frame, not on it.
        AddLabel("All eight reactions, same scale", plotLeft, stripTop, 0.0f, 1.0f,
            axisLabelSize, MutedTextColor, TextAlignmentOptions.TopLeft);

        IReadOnlyList<ReactionGraphData> all = catalog.Entries;
        int count = all.Count;
        if (count == 0)
        {
            return;
        }

        int zeroX = MapXValue(0.0f, -axisMax, axisMax);
        painter.DashedLine(zeroX, stripBottom, zeroX, stripTop - 18, 1.4f, GridColor, 6, 6);

        int rowHeight = (stripTop - 20 - stripBottom) / Mathf.Max(1, count);
        int barHeight = Mathf.Max(4, rowHeight - 4);

        for (int i = 0; i < count; i++)
        {
            ReactionGraphData entry = all[i];
            if (entry == null)
            {
                continue;
            }

            int rowCentre = stripTop - 22 - i * rowHeight - rowHeight / 2;
            int endX = MapXValue(entry.entropyChange, -axisMax, axisMax);

            bool isCurrent = entry.reactionId == current.reactionId;
            Color32 baseColor = entry.entropyChange >= 0.0f ? EntropyUpColor : EntropyDownColor;
            Color32 rowColor = isCurrent
                ? baseColor
                : new Color32(baseColor.r, baseColor.g, baseColor.b, 90);

            painter.FillRect(Mathf.Min(zeroX, endX), rowCentre - barHeight / 2,
                             Mathf.Max(zeroX, endX), rowCentre + barHeight / 2, rowColor);

            AddLabel("R" + entry.reactionId, plotLeft - 10, rowCentre, 1.0f, 0.5f,
                axisLabelSize - 2.0f,
                isCurrent ? TextColor : MutedTextColor,
                TextAlignmentOptions.Right);
        }
    }

    private float LargestEntropyMagnitude()
    {
        float largest = 1.0f;
        ReactionGraphCatalog catalog = ReactionGraphCatalog.Shared;
        if (catalog == null)
        {
            return 400.0f;
        }

        IReadOnlyList<ReactionGraphData> all = catalog.Entries;
        for (int i = 0; i < all.Count; i++)
        {
            if (all[i] != null)
            {
                largest = Mathf.Max(largest, Mathf.Abs(all[i].entropyChange));
            }
        }
        return largest;
    }

    // =========================================================
    // PLOT SCAFFOLDING
    // =========================================================

    private void SetPlotArea(int left, int right, int bottom, int top)
    {
        plotLeft = left;
        plotRight = textureWidth - right;
        plotBottom = bottom;
        plotTop = textureHeight - top;
    }

    private void DrawPlotFrame()
    {
        painter.FillRect(plotLeft, plotBottom, plotRight, plotTop, PlotBackground);
        painter.ThickLine(plotLeft, plotBottom, plotRight, plotBottom, 2.0f, AxisColor);
        painter.ThickLine(plotLeft, plotBottom, plotLeft, plotTop, 2.0f, AxisColor);
    }

    private int MapX(float progress)
    {
        return Mathf.RoundToInt(Mathf.Lerp(plotLeft, plotRight, Mathf.Clamp01(progress)));
    }

    private int MapXValue(float value, float min, float max)
    {
        float t = Mathf.InverseLerp(min, max, value);
        return Mathf.RoundToInt(Mathf.Lerp(plotLeft, plotRight, t));
    }

    private int MapY(float value, float min, float max)
    {
        float t = Mathf.InverseLerp(min, max, value);
        return Mathf.RoundToInt(Mathf.Lerp(plotBottom, plotTop, t));
    }

    private void DrawYAxisTicks(float yMin, float yMax)
    {
        float step = NiceStep(yMax - yMin, 6);
        float first = Mathf.Ceil(yMin / step) * step;

        for (float value = first; value <= yMax + step * 0.01f; value += step)
        {
            int y = MapY(value, yMin, yMax);
            if (y < plotBottom || y > plotTop)
            {
                continue;
            }

            painter.ThickLine(plotLeft, y, plotRight, y, 1.0f, GridColor);
            painter.ThickLine(plotLeft - 6, y, plotLeft, y, 1.6f, AxisColor);

            AddLabel(FormatTick(value, step), plotLeft - 12, y, 1.0f, 0.5f,
                axisLabelSize, MutedTextColor, TextAlignmentOptions.Right);
        }
    }

    private void DrawEntropyAxisTicks(float min, float max)
    {
        float step = NiceStep(max - min, 8);
        float first = Mathf.Ceil(min / step) * step;

        for (float value = first; value <= max + step * 0.01f; value += step)
        {
            int x = MapXValue(value, min, max);
            if (x < plotLeft || x > plotRight)
            {
                continue;
            }

            painter.ThickLine(x, plotBottom, x, plotTop, 1.0f, GridColor);

            // Pull the outermost ticks inward so a centred label cannot hang off the texture.
            string tick = FormatTick(value, step);
            float half = EstimateTextWidth(tick, axisLabelSize - 2.0f) * 0.5f;
            float pivotX = 0.5f;
            if (x - half < 2.0f)
            {
                pivotX = 0.0f;
            }
            else if (x + half > textureWidth - 2.0f)
            {
                pivotX = 1.0f;
            }

            AddLabel(tick, x, plotTop + 6, pivotX, 0.0f,
                axisLabelSize - 2.0f, MutedTextColor, TextAlignmentOptions.Bottom);
        }

        AddLabel("J/(mol K)", plotRight, plotTop + 26, 1.0f, 0.0f,
            axisLabelSize - 1.0f, MutedTextColor, TextAlignmentOptions.BottomRight);
    }

    private void DrawXAxisProgressTicks()
    {
        for (int i = 0; i <= 5; i++)
        {
            float progress = i / 5.0f;
            int x = MapX(progress);

            painter.ThickLine(x, plotBottom, x, plotBottom - 6, 1.6f, AxisColor);
            if (i > 0)
            {
                painter.ThickLine(x, plotBottom, x, plotTop, 1.0f, GridColor);
            }

            AddLabel(progress.ToString("0.0"), x, plotBottom - 10, 0.5f, 1.0f,
                axisLabelSize - 2.0f, MutedTextColor, TextAlignmentOptions.Top);
        }
    }

    /// <summary>Rounds a range to a readable tick spacing from the 1 / 2 / 2.5 / 5 family.</summary>
    private static float NiceStep(float range, int targetTicks)
    {
        if (range <= 0.0f || targetTicks <= 0)
        {
            return 1.0f;
        }

        float rough = range / targetTicks;
        float magnitude = Mathf.Pow(10.0f, Mathf.Floor(Mathf.Log10(rough)));
        float normalised = rough / magnitude;

        float nice;
        if (normalised <= 1.0f) nice = 1.0f;
        else if (normalised <= 2.0f) nice = 2.0f;
        else if (normalised <= 2.5f) nice = 2.5f;
        else if (normalised <= 5.0f) nice = 5.0f;
        else nice = 10.0f;

        return nice * magnitude;
    }

    private static string FormatTick(float value, float step)
    {
        // Show a decimal only when the step is fine enough to need one.
        return step < 1.0f ? value.ToString("0.0") : value.ToString("0");
    }

    /// <summary>Reactant or product side of the equation, for the level diagram captions.</summary>
    private static string ShortFormula(ReactionGraphData data, bool reactants)
    {
        string equation = data.reactionName;
        if (string.IsNullOrEmpty(equation))
        {
            return string.Empty;
        }

        int arrow = equation.IndexOf("->");
        if (arrow < 0)
        {
            return equation;
        }

        return reactants
            ? equation.Substring(0, arrow).Trim()
            : equation.Substring(arrow + 2).Trim();
    }

    // =========================================================
    // LABEL POOL
    // =========================================================

    private void BeginLabels()
    {
        labelsUsed = 0;
    }

    private void EndLabels()
    {
        for (int i = labelsUsed; i < labelPool.Count; i++)
        {
            labelPool[i].gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Places a TMP label over the texture. Coordinates are texture pixels with the origin at the
    /// bottom-left; the pivot says which corner of the label sits on that point.
    /// </summary>
    private TMP_Text AddLabel(string text, int pixelX, int pixelY, float pivotX, float pivotY,
                              float fontSize, Color color, TextAlignmentOptions alignment)
    {
        TMP_Text label = TakeLabel();

        label.text = text;
        label.fontSize = fontSize;
        label.color = color;
        label.alignment = alignment;

        // Reset, because a pooled label may have been used as a wide wrapping caption last time.
        label.textWrappingMode = TextWrappingModes.NoWrap;

        RectTransform rect = label.rectTransform;
        rect.localRotation = Quaternion.identity;
        rect.pivot = new Vector2(pivotX, pivotY);
        rect.sizeDelta = new Vector2(360.0f, 90.0f);
        rect.anchoredPosition = PixelToLocal(pixelX, pixelY);

        return label;
    }

    /// <summary>
    /// Places a label to the right of a point, or to the left when the right-hand side would run
    /// off the texture. Several of these annotations sit near the plot edge - dH for the strongly
    /// endothermic reactions, dS for FeSO4 - and would otherwise be clipped.
    /// </summary>
    private void AddLabelBeside(string text, int pixelX, int pixelY, int offset,
                                float fontSize, Color32 color)
    {
        Color tint = new Color(color.r / 255.0f, color.g / 255.0f, color.b / 255.0f, 1.0f);
        float estimated = EstimateTextWidth(text, fontSize);

        bool fitsRight = pixelX + offset + estimated <= textureWidth - 6;

        if (fitsRight)
        {
            AddLabel(text, pixelX + offset, pixelY, 0.0f, 0.5f,
                fontSize, tint, TextAlignmentOptions.Left);
        }
        else
        {
            AddLabel(text, pixelX - offset, pixelY, 1.0f, 0.5f,
                fontSize, tint, TextAlignmentOptions.Right);
        }
    }

    /// <summary>
    /// Rough width of a rendered string in pixels. TMP could measure it exactly, but only after a
    /// layout pass, and this only has to be good enough to decide which side of a point to use.
    /// </summary>
    private static float EstimateTextWidth(string text, float fontSize)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0.0f;
        }

        int longest = 0;
        int current = 0;
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                longest = Mathf.Max(longest, current);
                current = 0;
                continue;
            }
            current++;
        }
        longest = Mathf.Max(longest, current);

        // ~0.55 em per character averaged over mixed-case text with digits.
        return longest * fontSize * 0.55f;
    }

    /// <summary>
    /// A centred caption spanning the full texture width, with wrapping switched on. The other
    /// labels are NoWrap so short annotations never break mid-number; this one carries a whole
    /// sentence and has to be allowed to fold.
    /// </summary>
    private void AddWideLabel(string text, int pixelX, int pixelY, float fontSize, Color color)
    {
        TMP_Text label = AddLabel(text, pixelX, pixelY, 0.5f, 0.0f, fontSize, color,
            TextAlignmentOptions.Bottom);

        label.textWrappingMode = TextWrappingModes.Normal;

        float width = textureWidth - 40.0f;
        if (selfRect != null)
        {
            // The rect is in local units, which may differ from texture pixels.
            width *= selfRect.rect.size.x / textureWidth;
        }
        label.rectTransform.sizeDelta = new Vector2(width, 120.0f);
    }

    /// <summary>Same, but turned on its side for the y-axis title.</summary>
    private void AddRotatedLabel(string text, int pixelX, int pixelY, float fontSize, Color color)
    {
        TMP_Text label = AddLabel(text, pixelX, pixelY, 0.5f, 0.5f, fontSize, color,
            TextAlignmentOptions.Center);
        label.rectTransform.localRotation = Quaternion.Euler(0.0f, 0.0f, 90.0f);
    }

    /// <summary>
    /// Texture pixels to the RawImage's local space. The RectTransform is centred, and the texture
    /// is stretched across it, so this also handles a texture that is not 1:1 with the rect.
    /// </summary>
    private Vector2 PixelToLocal(int pixelX, int pixelY)
    {
        if (selfRect == null)
        {
            selfRect = GetComponent<RectTransform>();
        }

        Vector2 size = selfRect != null ? selfRect.rect.size
                                        : new Vector2(textureWidth, textureHeight);

        float x = (pixelX / (float)textureWidth - 0.5f) * size.x;
        float y = (pixelY / (float)textureHeight - 0.5f) * size.y;
        return new Vector2(x, y);
    }

    private TMP_Text TakeLabel()
    {
        if (labelsUsed < labelPool.Count)
        {
            TMP_Text existing = labelPool[labelsUsed];
            existing.gameObject.SetActive(true);
            labelsUsed++;
            return existing;
        }

        GameObject host = new GameObject("Label" + labelPool.Count,
            typeof(RectTransform), typeof(TextMeshProUGUI));
        host.transform.SetParent(transform, false);

        RectTransform rect = host.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);

        TextMeshProUGUI label = host.GetComponent<TextMeshProUGUI>();
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Overflow;
        label.raycastTarget = false;

        labelPool.Add(label);
        labelsUsed++;
        return label;
    }
}

/// <summary>
/// Minimal software rasteriser over a <see cref="Texture2D"/>: filled rectangles, anti-aliased
/// lines and polylines, dashed lines, arrows and circles. Everything is drawn into a Color32
/// buffer and pushed once, so a whole graph costs a single texture upload.
///
/// Pixel origin is bottom-left, matching Unity's texture convention.
/// </summary>
public class GraphPainter
{
    private readonly Texture2D target;
    private readonly Color32[] pixels;
    private readonly int width;
    private readonly int height;

    public GraphPainter(Texture2D texture)
    {
        target = texture;
        width = texture.width;
        height = texture.height;
        pixels = new Color32[width * height];
    }

    public void Clear(Color32 color)
    {
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = color;
        }
    }

    public void Apply()
    {
        target.SetPixels32(pixels);
        target.Apply(false);
    }

    // --- Primitives -------------------------------------------------------------------

    private void Blend(int x, int y, Color32 color, float alpha)
    {
        if (x < 0 || y < 0 || x >= width || y >= height || alpha <= 0.0f)
        {
            return;
        }

        alpha *= color.a / 255.0f;
        if (alpha <= 0.0f)
        {
            return;
        }
        if (alpha > 1.0f)
        {
            alpha = 1.0f;
        }

        int index = y * width + x;
        Color32 destination = pixels[index];

        pixels[index] = new Color32(
            (byte)(destination.r + (color.r - destination.r) * alpha),
            (byte)(destination.g + (color.g - destination.g) * alpha),
            (byte)(destination.b + (color.b - destination.b) * alpha),
            255);
    }

    public void FillRect(int x0, int y0, int x1, int y1, Color32 color)
    {
        int left = Mathf.Max(0, Mathf.Min(x0, x1));
        int right = Mathf.Min(width - 1, Mathf.Max(x0, x1));
        int bottom = Mathf.Max(0, Mathf.Min(y0, y1));
        int top = Mathf.Min(height - 1, Mathf.Max(y0, y1));

        for (int y = bottom; y <= top; y++)
        {
            for (int x = left; x <= right; x++)
            {
                Blend(x, y, color, 1.0f);
            }
        }
    }

    /// <summary>Anti-aliased line of the given thickness, by distance-to-segment coverage.</summary>
    public void ThickLine(float x0, float y0, float x1, float y1, float thickness, Color32 color)
    {
        float half = Mathf.Max(0.5f, thickness * 0.5f);

        int left = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(x0, x1) - half - 1.0f));
        int right = Mathf.Min(width - 1, Mathf.CeilToInt(Mathf.Max(x0, x1) + half + 1.0f));
        int bottom = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(y0, y1) - half - 1.0f));
        int top = Mathf.Min(height - 1, Mathf.CeilToInt(Mathf.Max(y0, y1) + half + 1.0f));

        float dx = x1 - x0;
        float dy = y1 - y0;
        float lengthSquared = dx * dx + dy * dy;

        for (int y = bottom; y <= top; y++)
        {
            for (int x = left; x <= right; x++)
            {
                float px = x + 0.5f - x0;
                float py = y + 0.5f - y0;

                float t = lengthSquared > 0.0001f
                    ? Mathf.Clamp01((px * dx + py * dy) / lengthSquared)
                    : 0.0f;

                float ox = px - dx * t;
                float oy = py - dy * t;
                float distance = Mathf.Sqrt(ox * ox + oy * oy);

                // Coverage falls off over one pixel at the edge, which is the anti-aliasing.
                float alpha = Mathf.Clamp01(half + 0.5f - distance);
                Blend(x, y, color, alpha);
            }
        }
    }

    public void Polyline(List<Vector2> points, float thickness, Color32 color)
    {
        if (points == null || points.Count < 2)
        {
            return;
        }

        for (int i = 0; i < points.Count - 1; i++)
        {
            ThickLine(points[i].x, points[i].y, points[i + 1].x, points[i + 1].y, thickness, color);
        }
    }

    public void DashedLine(float x0, float y0, float x1, float y1, float thickness,
                           Color32 color, int dash, int gap)
    {
        float dx = x1 - x0;
        float dy = y1 - y0;
        float length = Mathf.Sqrt(dx * dx + dy * dy);
        if (length < 0.5f)
        {
            return;
        }

        float ux = dx / length;
        float uy = dy / length;
        float step = Mathf.Max(1.0f, dash + gap);

        for (float travelled = 0.0f; travelled < length; travelled += step)
        {
            float end = Mathf.Min(length, travelled + dash);
            ThickLine(x0 + ux * travelled, y0 + uy * travelled,
                      x0 + ux * end, y0 + uy * end, thickness, color);
        }
    }

    public void FilledCircle(float cx, float cy, float radius, Color32 color)
    {
        int left = Mathf.Max(0, Mathf.FloorToInt(cx - radius - 1.0f));
        int right = Mathf.Min(width - 1, Mathf.CeilToInt(cx + radius + 1.0f));
        int bottom = Mathf.Max(0, Mathf.FloorToInt(cy - radius - 1.0f));
        int top = Mathf.Min(height - 1, Mathf.CeilToInt(cy + radius + 1.0f));

        for (int y = bottom; y <= top; y++)
        {
            for (int x = left; x <= right; x++)
            {
                float ox = x + 0.5f - cx;
                float oy = y + 0.5f - cy;
                float distance = Mathf.Sqrt(ox * ox + oy * oy);
                Blend(x, y, color, Mathf.Clamp01(radius + 0.5f - distance));
            }
        }
    }

    /// <summary>Solid triangle, used for arrowheads.</summary>
    public void FillTriangle(Vector2 a, Vector2 b, Vector2 c, Color32 color)
    {
        int left = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, Mathf.Min(b.x, c.x))));
        int right = Mathf.Min(width - 1, Mathf.CeilToInt(Mathf.Max(a.x, Mathf.Max(b.x, c.x))));
        int bottom = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, Mathf.Min(b.y, c.y))));
        int top = Mathf.Min(height - 1, Mathf.CeilToInt(Mathf.Max(a.y, Mathf.Max(b.y, c.y))));

        float area = Edge(a, b, c);
        if (Mathf.Abs(area) < 0.0001f)
        {
            return;
        }

        for (int y = bottom; y <= top; y++)
        {
            for (int x = left; x <= right; x++)
            {
                Vector2 p = new Vector2(x + 0.5f, y + 0.5f);

                float w0 = Edge(b, c, p) / area;
                float w1 = Edge(c, a, p) / area;
                float w2 = Edge(a, b, p) / area;

                if (w0 >= 0.0f && w1 >= 0.0f && w2 >= 0.0f)
                {
                    Blend(x, y, color, 1.0f);
                }
            }
        }
    }

    private static float Edge(Vector2 a, Vector2 b, Vector2 p)
    {
        return (b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x);
    }

    /// <summary>Line with an arrowhead at the far end.</summary>
    public void Arrow(float x0, float y0, float x1, float y1, float thickness,
                      Color32 color, float headSize)
    {
        float dx = x1 - x0;
        float dy = y1 - y0;
        float length = Mathf.Sqrt(dx * dx + dy * dy);
        if (length < 1.0f)
        {
            return;
        }

        float ux = dx / length;
        float uy = dy / length;

        // Stop the shaft where the head begins so the tip stays sharp.
        ThickLine(x0, y0, x1 - ux * headSize, y1 - uy * headSize, thickness, color);
        DrawHead(x1, y1, ux, uy, headSize, color);
    }

    /// <summary>Line with an arrowhead at both ends - the usual way Ea and dH are annotated.</summary>
    public void DoubleArrow(float x0, float y0, float x1, float y1, float thickness,
                            Color32 color, float headSize)
    {
        float dx = x1 - x0;
        float dy = y1 - y0;
        float length = Mathf.Sqrt(dx * dx + dy * dy);
        if (length < 1.0f)
        {
            return;
        }

        float ux = dx / length;
        float uy = dy / length;

        ThickLine(x0 + ux * headSize, y0 + uy * headSize,
                  x1 - ux * headSize, y1 - uy * headSize, thickness, color);

        DrawHead(x1, y1, ux, uy, headSize, color);
        DrawHead(x0, y0, -ux, -uy, headSize, color);
    }

    private void DrawHead(float tipX, float tipY, float ux, float uy, float size, Color32 color)
    {
        // Perpendicular to the direction of travel.
        float px = -uy;
        float py = ux;

        Vector2 tip = new Vector2(tipX, tipY);
        Vector2 baseCentre = new Vector2(tipX - ux * size, tipY - uy * size);
        Vector2 left = baseCentre + new Vector2(px, py) * size * 0.5f;
        Vector2 right = baseCentre - new Vector2(px, py) * size * 0.5f;

        FillTriangle(tip, left, right, color);
    }

    /// <summary>
    /// Translucent tint between a curve and a baseline, which makes the energy profile read as a
    /// shape rather than a bare stroke.
    /// </summary>
    public void FillUnderCurve(List<Vector2> curve, int baseline, Color32 color, float alpha)
    {
        if (curve == null || curve.Count < 2)
        {
            return;
        }

        Color32 tint = new Color32(color.r, color.g, color.b,
            (byte)Mathf.Clamp(Mathf.RoundToInt(alpha * 255.0f), 0, 255));

        // Resolve the curve to one height per column first. Filling per segment instead would
        // blend the shared column at every joint twice, which shows up as vertical banding.
        int[] columnY = new int[width];
        bool[] columnSet = new bool[width];

        for (int i = 0; i < curve.Count - 1; i++)
        {
            Vector2 a = curve[i];
            Vector2 b = curve[i + 1];

            int xStart = Mathf.Clamp(Mathf.RoundToInt(Mathf.Min(a.x, b.x)), 0, width - 1);
            int xEnd = Mathf.Clamp(Mathf.RoundToInt(Mathf.Max(a.x, b.x)), 0, width - 1);

            for (int x = xStart; x <= xEnd; x++)
            {
                float t = Mathf.Approximately(b.x, a.x)
                    ? 0.0f
                    : Mathf.Clamp01((x - a.x) / (b.x - a.x));

                columnY[x] = Mathf.RoundToInt(Mathf.Lerp(a.y, b.y, t));
                columnSet[x] = true;
            }
        }

        for (int x = 0; x < width; x++)
        {
            if (!columnSet[x])
            {
                continue;
            }

            int low = Mathf.Max(0, Mathf.Min(baseline, columnY[x]));
            int high = Mathf.Min(height - 1, Mathf.Max(baseline, columnY[x]));

            for (int y = low; y <= high; y++)
            {
                Blend(x, y, tint, 1.0f);
            }
        }
    }
}
