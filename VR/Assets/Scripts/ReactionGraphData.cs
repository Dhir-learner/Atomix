using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Whether the reaction releases heat or absorbs it.</summary>
public enum ReactionThermalType
{
    Exothermic,
    Endothermic
}

/// <summary>
/// Which of the three scientific graphs is being shown.
/// </summary>
public enum ReactionGraphType
{
    /// <summary>Energy vs reaction progress - the classic activation-energy curve.</summary>
    EnergyProfile,

    /// <summary>Reactant/product enthalpy levels with the dH arrow between them.</summary>
    EnthalpyLevels,

    /// <summary>Entropy change, as a signed bar against a reference scale.</summary>
    Entropy
}

/// <summary>
/// The thermodynamic and kinetic data behind one reaction's graphs.
///
/// Every dH and dS in the shipped catalog is derived by Hess's law from standard
/// formation enthalpies and absolute entropies at 298.15 K, so the numbers match a
/// textbook rather than being invented for the visuals. Ea values come from kinetics
/// literature for the corresponding real process.
/// </summary>
[CreateAssetMenu(fileName = "ReactionGraphData", menuName = "Atomix/Reaction Graph Data")]
public class ReactionGraphData : ScriptableObject
{
    /// <summary>Standard temperature the tabulated values refer to.</summary>
    public const float StandardTemperatureKelvin = 298.15f;

    [Header("Identity")]
    [Min(1)]
    public int reactionId = 1;

    [Tooltip("Balanced equation, e.g. \"CaCO3 -> CaO + CO2\".")]
    public string reactionName = string.Empty;

    [Tooltip("Friendly heading shown above the graph.")]
    public string displayTitle = string.Empty;

    [Header("Thermodynamics")]
    public ReactionThermalType reactionType = ReactionThermalType.Exothermic;

    [Tooltip("Activation energy Ea in kJ/mol. Always positive, and never below the enthalpy change.")]
    public float activationEnergy = 50.0f;

    [Tooltip("Enthalpy change dH in kJ/mol for the equation as written. Negative = exothermic.")]
    public float enthalpyChange = -50.0f;

    [Tooltip("Entropy change dS in J/(mol K) for the equation as written.")]
    public float entropyChange = 0.0f;

    [Header("Energy profile")]
    [Tooltip("Normalised curve: x = 0 (reactants) to 1 (products), y = energy in kJ/mol " +
             "relative to the reactants. Left empty, a curve is synthesised from Ea and dH.")]
    public List<Vector2> energyProfilePoints = new List<Vector2>();

    [Header("Narration")]
    [TextArea(4, 14)]
    [Tooltip("What the AI assistant should say when asked to explain these graphs.")]
    public string graphExplanationText = string.Empty;

    // =========================================================
    // DERIVED CHEMISTRY
    // =========================================================

    /// <summary>True when the reaction gives out heat (dH negative).</summary>
    public bool IsExothermic
    {
        get { return enthalpyChange < 0.0f; }
    }

    /// <summary>
    /// The classification actually used for display. Prefers the sign of dH over the serialized
    /// enum so a mistyped Inspector value can never colour a graph the wrong way round.
    /// </summary>
    public ReactionThermalType EffectiveType
    {
        get
        {
            return IsExothermic
                ? ReactionThermalType.Exothermic
                : ReactionThermalType.Endothermic;
        }
    }

    /// <summary>
    /// Barrier for the reverse reaction, in kJ/mol: Ea(reverse) = Ea(forward) - dH.
    /// </summary>
    public float ReverseActivationEnergy
    {
        get { return activationEnergy - enthalpyChange; }
    }

    /// <summary>Gibbs free energy in kJ/mol at the given temperature. dS is converted from J to kJ.</summary>
    public float GibbsFreeEnergyAt(float kelvin)
    {
        return enthalpyChange - kelvin * entropyChange / 1000.0f;
    }

    /// <summary>Gibbs free energy in kJ/mol at 298.15 K.</summary>
    public float StandardGibbsFreeEnergy
    {
        get { return GibbsFreeEnergyAt(StandardTemperatureKelvin); }
    }

    /// <summary>Spontaneous under standard conditions when dG is negative.</summary>
    public bool IsSpontaneousAtRoomTemperature
    {
        get { return StandardGibbsFreeEnergy < 0.0f; }
    }

    /// <summary>
    /// Temperature in kelvin where dG changes sign (dH / dS), or 0 when dS is ~0 and the
    /// sign never flips.
    /// </summary>
    public float CrossoverTemperatureKelvin
    {
        get
        {
            if (Mathf.Abs(entropyChange) < 0.01f)
            {
                return 0.0f;
            }
            return enthalpyChange * 1000.0f / entropyChange;
        }
    }

    /// <summary>
    /// One sentence on when this reaction is thermodynamically favourable, from the signs of
    /// dH and dS. This is the whole point of showing entropy next to enthalpy.
    ///
    /// The crossover temperature is only quoted where it means something. Extrapolating dH/dS
    /// assumes both stay constant with temperature and that no phase changes intervene, which for
    /// the strongly exothermic reactions here lands the crossover thousands of degrees above the
    /// point where the substances still exist - a number that would be worse than useless to a
    /// student.
    /// </summary>
    public string SpontaneitySummary()
    {
        bool exothermic = enthalpyChange < 0.0f;
        bool entropyRises = entropyChange > 0.0f;
        float crossoverC = CrossoverTemperatureKelvin - 273.15f;

        if (exothermic && entropyRises)
        {
            return "Spontaneous at every temperature - energy is released and disorder increases.";
        }

        if (!exothermic && !entropyRises)
        {
            return "Never spontaneous - it costs energy and reduces disorder at the same time.";
        }

        if (exothermic)
        {
            // dH < 0, dS < 0: the enthalpy term wins while T is low, and here that covers every
            // temperature the reaction can actually be run at.
            return "Spontaneous at room temperature. Only at high temperature would the entropy " +
                   "penalty win - which is what drives the reverse reaction when you heat the product.";
        }

        // dH > 0, dS > 0: the entropy term has to be big enough, so it usually needs heat.
        if (crossoverC <= 25.0f)
        {
            return "Spontaneous at room temperature - the gain in disorder is big enough to pay " +
                   "the energy cost without any heating at all.";
        }

        return string.Format(
            "Spontaneous above about {0:F0} C - it needs heat, because only then does the rise in " +
            "disorder outweigh the energy cost.",
            crossoverC);
    }

    /// <summary>
    /// Energy in kJ/mol at a normalised reaction progress of 0 to 1.
    /// Uses the authored curve when there is one and a synthesised curve otherwise, so a graph
    /// still draws correctly if the points were never filled in.
    /// </summary>
    public float SampleEnergy(float progress)
    {
        progress = Mathf.Clamp01(progress);

        if (energyProfilePoints == null || energyProfilePoints.Count < 2)
        {
            return SynthesiseEnergy(progress);
        }

        // The points are authored in ascending x, but do not assume it.
        for (int i = 0; i < energyProfilePoints.Count - 1; i++)
        {
            Vector2 a = energyProfilePoints[i];
            Vector2 b = energyProfilePoints[i + 1];

            if (progress >= a.x && progress <= b.x && b.x > a.x)
            {
                float t = (progress - a.x) / (b.x - a.x);
                return Mathf.Lerp(a.y, b.y, t);
            }
        }

        return progress <= energyProfilePoints[0].x
            ? energyProfilePoints[0].y
            : energyProfilePoints[energyProfilePoints.Count - 1].y;
    }

    /// <summary>
    /// A plausible curve from Ea and dH alone: a smoothstep from reactants to products with a
    /// Gaussian barrier on top, placed early for exothermic and late for endothermic reactions
    /// after Hammond's postulate. Used only when no points were authored.
    /// </summary>
    public float SynthesiseEnergy(float progress)
    {
        return RawSynthesise(progress, SynthesisedBumpHeight);
    }

    // Cached so the bisection below runs once per data object rather than per sample.
    [NonSerialized] private bool bumpHeightSolved = false;
    [NonSerialized] private float bumpHeight = 0.0f;
    [NonSerialized] private float bumpHeightSolvedFor = 0.0f;

    private float SynthesisedBumpHeight
    {
        get
        {
            // Re-solve if Ea or dH were edited in the Inspector since the last solve.
            float signature = activationEnergy * 1000.0f + enthalpyChange;
            if (bumpHeightSolved && Mathf.Approximately(signature, bumpHeightSolvedFor))
            {
                return bumpHeight;
            }

            bumpHeight = SolveBumpHeight();
            bumpHeightSolvedFor = signature;
            bumpHeightSolved = true;
            return bumpHeight;
        }
    }

    /// <summary>
    /// The Gaussian is sitting on a sloping baseline, so its height is not simply Ea - the peak
    /// drifts off centre once dH is large. Bisect on the height until the curve's maximum lands
    /// on Ea, which keeps a synthesised curve consistent with the authored points.
    /// </summary>
    private float SolveBumpHeight()
    {
        float low = 0.0f;
        float high = Mathf.Abs(activationEnergy) + Mathf.Abs(enthalpyChange) * 4.0f + 100.0f;

        for (int iteration = 0; iteration < 60; iteration++)
        {
            float mid = (low + high) * 0.5f;

            float maximum = float.NegativeInfinity;
            for (int i = 0; i <= 100; i++)
            {
                maximum = Mathf.Max(maximum, RawSynthesise(i / 100.0f, mid));
            }

            if (maximum < activationEnergy)
            {
                low = mid;
            }
            else
            {
                high = mid;
            }
        }

        return (low + high) * 0.5f;
    }

    private float RawSynthesise(float progress, float height)
    {
        float peakX = IsExothermic ? 0.42f : 0.58f;

        float t = Mathf.Clamp01((progress - (peakX - 0.28f)) / 0.56f);
        float step = t * t * (3.0f - 2.0f * t);

        float bump = Mathf.Exp(-Mathf.Pow((progress - peakX) / 0.15f, 2.0f));

        return enthalpyChange * step + height * bump;
    }

    /// <summary>Highest point of the curve in kJ/mol - the transition state.</summary>
    public float PeakEnergy
    {
        get
        {
            float peak = float.NegativeInfinity;
            for (int i = 0; i <= 100; i++)
            {
                peak = Mathf.Max(peak, SampleEnergy(i / 100.0f));
            }
            return peak;
        }
    }

    /// <summary>Reaction progress 0-1 at which the curve peaks - where the transition state sits.</summary>
    public float PeakProgress
    {
        get
        {
            float peak = float.NegativeInfinity;
            float peakAt = 0.5f;
            for (int i = 0; i <= 100; i++)
            {
                float x = i / 100.0f;
                float y = SampleEnergy(x);
                if (y > peak)
                {
                    peak = y;
                    peakAt = x;
                }
            }
            return peakAt;
        }
    }

    /// <summary>Title for the panel heading, falling back through the other name fields.</summary>
    public string ResolvedTitle
    {
        get
        {
            if (!string.IsNullOrEmpty(displayTitle))
            {
                return displayTitle;
            }
            if (!string.IsNullOrEmpty(reactionName))
            {
                return reactionName;
            }
            return "Reaction " + reactionId;
        }
    }

    /// <summary>
    /// Compact one-line summary of the three headline numbers, used in the UI strip and in the
    /// briefing handed to the AI.
    /// </summary>
    public string NumbersLine()
    {
        return string.Format(
            "dH = {0:+0.0;-0.0} kJ/mol   |   Ea = {1:F0} kJ/mol   |   dS = {2:+0.0;-0.0} J/(mol K)   |   dG(25 C) = {3:+0.0;-0.0} kJ/mol",
            enthalpyChange, activationEnergy, entropyChange, StandardGibbsFreeEnergy);
    }

    /// <summary>
    /// Fills in anything an authored asset left blank, and repairs values that are physically
    /// impossible. Called after the catalog loads so a half-filled Inspector entry still draws.
    /// </summary>
    public void NormaliseInPlace()
    {
        if (activationEnergy <= 0.0f)
        {
            // A barrier of zero would draw as a flat line; fall back to something drawable.
            activationEnergy = Mathf.Max(5.0f, Mathf.Abs(enthalpyChange) * 0.25f);
        }

        // The transition state cannot sit below the products.
        if (activationEnergy < enthalpyChange)
        {
            activationEnergy = enthalpyChange * 1.05f;
        }

        reactionType = EffectiveType;

        if (energyProfilePoints == null)
        {
            energyProfilePoints = new List<Vector2>();
        }
    }

    /// <summary>Builds a data object in code - used by the catalog's built-in fallback.</summary>
    public static ReactionGraphData Create(
        int reactionId,
        string reactionName,
        string displayTitle,
        float activationEnergy,
        float enthalpyChange,
        float entropyChange,
        string graphExplanationText,
        IEnumerable<Vector2> points)
    {
        ReactionGraphData data = CreateInstance<ReactionGraphData>();
        data.name = "ReactionGraphData_" + reactionId;
        data.reactionId = reactionId;
        data.reactionName = reactionName;
        data.displayTitle = displayTitle;
        data.activationEnergy = activationEnergy;
        data.enthalpyChange = enthalpyChange;
        data.entropyChange = entropyChange;
        data.graphExplanationText = graphExplanationText;
        data.reactionType = enthalpyChange < 0.0f
            ? ReactionThermalType.Exothermic
            : ReactionThermalType.Endothermic;

        data.energyProfilePoints = new List<Vector2>();
        if (points != null)
        {
            data.energyProfilePoints.AddRange(points);
        }

        data.NormaliseInPlace();
        return data;
    }
}
