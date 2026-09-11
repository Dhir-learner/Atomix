using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

/// <summary>
/// "Make 10.0 g of CuSO4." A lab run where the student is given the amount of product and has to
/// work out the reagents - which is the calculation the whole bench is really teaching.
///
/// Built on the same hidden-target judging the testing scene uses (<see cref="ExamReactionRunner"/>
/// turns on <see cref="FreeHandReactionEngine.hideTargets"/> for exactly this purpose): the engine
/// still judges every reagent against a target, the target just is not on screen. Here the targets
/// are worked out from the requested mass with the atomic masses in <see cref="PeriodicTableData"/>.
///
/// <b>How each reagent's target is set</b>, from its <see cref="ChallengeRole"/>:
/// <list type="bullet">
/// <item><b>Reactant</b> - moles of product x (reagent coefficient / product coefficient), then
/// x molar mass for grams, or / concentration for a solution in ml. A solution's concentration is
/// whatever makes the recipe's own standard amounts exactly stoichiometric, and it is given to the
/// student in the brief, so their answer and the engine's agree to the decimal place.</item>
/// <item><b>Solvent</b> - kept at the recipe's own ratio to the basis reagent (e.g. 2.0 ml of water
/// per gram of quicklime), which the brief also states.</item>
/// <item><b>Fixed</b> - a catalyst; the standard amount whatever is asked for.</item>
/// </list>
///
/// Only experiments whose reagents are all poured or dropped can be set as a challenge: sodium and
/// potassium arrive as one fixed lump, and the two decompositions measure heating time, so there
/// is no amount for the student to calculate.
/// </summary>
public class StoichiometryChallenge
{
    /// <summary>The requested product never goes below this share of the recipe's standard yield...</summary>
    public const float MinYieldShare = 0.5f;

    /// <summary>...or above this, so every pour stays comfortably inside the vessels and the rates.</summary>
    public const float MaxYieldShare = 1.25f;

    /// <summary>Requested masses are rounded to this, in grams.</summary>
    public const float MassStep = 0.5f;

    private readonly ReactionDefinition definition;
    private readonly Dictionary<string, float> targets = new Dictionary<string, float>();
    private readonly Dictionary<string, float> concentrations = new Dictionary<string, float>();
    private readonly float productMolarMass;
    private readonly float productMoles;

    public int ReactionId { get { return definition.reactionId; } }
    public float ProductGrams { get; private set; }
    public string ProductFormula { get { return definition.productFormula; } }
    public string ProductName { get { return definition.productName; } }
    public ReactionDefinition Definition { get { return definition; } }

    private StoichiometryChallenge(ReactionDefinition definition, float grams, float productMolarMass)
    {
        this.definition = definition;
        this.productMolarMass = productMolarMass;
        ProductGrams = grams;
        productMoles = grams / productMolarMass;
    }

    // =========================================================
    // CREATION
    // =========================================================

    /// <summary>True when this experiment can be set as a challenge at all.</summary>
    public static bool IsEligible(ReactionDefinition definition)
    {
        return StandardYieldGrams(definition) > 0.0f;
    }

    /// <summary>
    /// Grams of product the recipe's own amounts make, or -1 when the experiment cannot be a
    /// challenge: not flagged for one, a reagent that is not poured or dropped, a formula that
    /// cannot be read, or a basis reagent that is not a reactant measured in grams.
    /// </summary>
    public static float StandardYieldGrams(ReactionDefinition definition)
    {
        if (definition == null || !definition.challengeEligible || definition.productCoefficient <= 0)
        {
            return -1.0f;
        }

        for (int i = 0; i < definition.reagents.Count; i++)
        {
            ReagentDefinition reagent = definition.reagents[i];
            if (reagent == null ||
                (reagent.addition != ReagentAddition.Pour && reagent.addition != ReagentAddition.Dropper))
            {
                return -1.0f;
            }

            if (reagent.challengeRole == ChallengeRole.Reactant &&
                (reagent.coefficient <= 0 || ChemicalFormula.MolarMass(reagent.formula) <= 0.0f ||
                 (reagent.unit != "g" && reagent.unit != "ml")))
            {
                return -1.0f;
            }
        }

        ReagentDefinition basis = definition.Reagent(definition.challengeBasisReagent);
        float productMass = ChemicalFormula.MolarMass(definition.productFormula);
        if (basis == null || basis.challengeRole != ChallengeRole.Reactant || basis.unit != "g" ||
            productMass <= 0.0f)
        {
            return -1.0f;
        }

        float basisMoles = basis.target / ChemicalFormula.MolarMass(basis.formula);
        return basisMoles * definition.productCoefficient / basis.coefficient * productMass;
    }

    /// <summary>A challenge for an exact mass of product. Null when the experiment is not eligible.</summary>
    public static StoichiometryChallenge Create(ReactionDefinition definition, float productGrams)
    {
        float standardYield = StandardYieldGrams(definition);
        float productMass = definition != null ? ChemicalFormula.MolarMass(definition.productFormula) : -1.0f;
        if (standardYield <= 0.0f || productMass <= 0.0f || productGrams <= 0.0f)
        {
            return null;
        }

        StoichiometryChallenge challenge = new StoichiometryChallenge(definition, productGrams, productMass);
        return challenge.WorkOutTargets() ? challenge : null;
    }

    /// <summary>
    /// A challenge for a random amount of product between <see cref="MinYieldShare"/> and
    /// <see cref="MaxYieldShare"/> of the standard yield, rounded to <see cref="MassStep"/>.
    /// </summary>
    public static StoichiometryChallenge CreateRandom(ReactionDefinition definition)
    {
        float standardYield = StandardYieldGrams(definition);
        if (standardYield <= 0.0f)
        {
            return null;
        }

        float low = Mathf.Ceil(standardYield * MinYieldShare / MassStep) * MassStep;
        float high = Mathf.Floor(standardYield * MaxYieldShare / MassStep) * MassStep;
        int steps = Mathf.Max(0, Mathf.RoundToInt((high - low) / MassStep));

        float grams = low + Random.Range(0, steps + 1) * MassStep;
        return Create(definition, grams);
    }

    private bool WorkOutTargets()
    {
        ReagentDefinition basis = definition.Reagent(definition.challengeBasisReagent);
        float basisStandardMoles = basis.target / ChemicalFormula.MolarMass(basis.formula);
        float standardProductMoles = basisStandardMoles * definition.productCoefficient / basis.coefficient;

        // Reactants first: a solvent's amount follows the basis reagent's.
        for (int i = 0; i < definition.reagents.Count; i++)
        {
            ReagentDefinition reagent = definition.reagents[i];
            if (reagent.challengeRole != ChallengeRole.Reactant)
            {
                continue;
            }

            float moles = MolesOf(reagent);
            if (reagent.unit == "g")
            {
                targets[reagent.name] = moles * ChemicalFormula.MolarMass(reagent.formula);
            }
            else
            {
                // mol/L that makes the recipe's own volume exactly right for its own yield.
                float standardMoles = standardProductMoles * reagent.coefficient / definition.productCoefficient;
                float concentration = standardMoles / (reagent.target / 1000.0f);
                concentrations[reagent.name] = concentration;
                targets[reagent.name] = moles / concentration * 1000.0f;
            }
        }

        float basisTarget;
        if (!targets.TryGetValue(basis.name, out basisTarget))
        {
            return false;
        }

        for (int i = 0; i < definition.reagents.Count; i++)
        {
            ReagentDefinition reagent = definition.reagents[i];
            if (reagent.challengeRole == ChallengeRole.Solvent)
            {
                targets[reagent.name] = SolventRatio(reagent) * basisTarget;
            }
            else if (reagent.challengeRole == ChallengeRole.Fixed)
            {
                targets[reagent.name] = reagent.target;
            }
        }

        foreach (float value in targets.Values)
        {
            if (value <= 0.0f || float.IsNaN(value) || float.IsInfinity(value))
            {
                return false;
            }
        }

        return true;
    }

    private float MolesOf(ReagentDefinition reagent)
    {
        return productMoles * reagent.coefficient / definition.productCoefficient;
    }

    /// <summary>Units of a solvent per gram of the basis reagent, from the recipe's own amounts.</summary>
    private float SolventRatio(ReagentDefinition solvent)
    {
        ReagentDefinition basis = definition.Reagent(definition.challengeBasisReagent);
        return solvent.target / basis.target;
    }

    // =========================================================
    // USE
    // =========================================================

    /// <summary>The amount the engine will judge this reagent against.</summary>
    public float TargetFor(string reagentName)
    {
        float value;
        return targets.TryGetValue(reagentName, out value) ? value : 0.0f;
    }

    /// <summary>Points every reagent's target at this challenge.</summary>
    public void ApplyTargets(FreeHandReactionEngine engine)
    {
        if (engine == null)
        {
            return;
        }

        foreach (KeyValuePair<string, float> pair in targets)
        {
            engine.SetTarget(pair.Key, pair.Value);
        }
    }

    /// <summary>True when two challenges ask for the same thing.</summary>
    public bool SameAs(StoichiometryChallenge other)
    {
        return other != null && other.ReactionId == ReactionId &&
               Mathf.Approximately(other.ProductGrams, ProductGrams);
    }

    /// <summary>"Make 10.0 g of CuSO4" - short enough for the HUD strip.</summary>
    public string Headline
    {
        get { return string.Format(CultureInfo.InvariantCulture, "Make {0:0.0} g of {1}", ProductGrams, ProductFormula); }
    }

    /// <summary>
    /// Everything the student needs, and nothing that gives the answer away: the product, the
    /// equation, the concentration of any solution, and the ratio for any solvent or catalyst.
    /// </summary>
    public string Brief
    {
        get
        {
            StringBuilder b = new StringBuilder();
            b.AppendFormat(CultureInfo.InvariantCulture, "Make {0:0.0} g of {1} ({2}).\n",
                ProductGrams, ProductName, ProductFormula);
            b.Append("Equation: ").Append(definition.equation).Append('\n');

            for (int i = 0; i < definition.reagents.Count; i++)
            {
                ReagentDefinition reagent = definition.reagents[i];
                float concentration;
                if (concentrations.TryGetValue(reagent.name, out concentration))
                {
                    b.AppendFormat(CultureInfo.InvariantCulture, "{0} is a {1:0.00} mol/L solution.\n",
                        reagent.name, concentration);
                }
                else if (reagent.challengeRole == ChallengeRole.Solvent)
                {
                    b.AppendFormat(CultureInfo.InvariantCulture, "Use {0:0.0} {1} of {2} per g of {3}.\n",
                        SolventRatio(reagent), reagent.unit, reagent.name, definition.challengeBasisReagent);
                }
                else if (reagent.challengeRole == ChallengeRole.Fixed)
                {
                    b.AppendFormat(CultureInfo.InvariantCulture, "{0} is a catalyst: always {1:0.0} {2}.\n",
                        reagent.name, reagent.target, reagent.unit);
                }
            }

            b.Append("Atomic masses are in the periodic table (P). The amounts are not shown - work them out.");
            return b.ToString();
        }
    }

    /// <summary>
    /// The given data in one line - "H2SO4 5.03 mol/L", "2.0 ml Water per g CaO" - so it stays in
    /// view on the bench, where the full brief from the book page does not.
    /// </summary>
    public string DataLine
    {
        get
        {
            StringBuilder b = new StringBuilder();
            for (int i = 0; i < definition.reagents.Count; i++)
            {
                ReagentDefinition reagent = definition.reagents[i];
                string part = null;

                float concentration;
                if (concentrations.TryGetValue(reagent.name, out concentration))
                {
                    part = string.Format(CultureInfo.InvariantCulture, "{0} {1:0.00} mol/L", reagent.name, concentration);
                }
                else if (reagent.challengeRole == ChallengeRole.Solvent)
                {
                    part = string.Format(CultureInfo.InvariantCulture, "{0:0.0} {1} {2} per g {3}",
                        SolventRatio(reagent), reagent.unit, reagent.name, definition.challengeBasisReagent);
                }
                else if (reagent.challengeRole == ChallengeRole.Fixed)
                {
                    part = string.Format(CultureInfo.InvariantCulture, "{0} {1:0.0} {2} (catalyst)",
                        reagent.name, reagent.target, reagent.unit);
                }

                if (part != null)
                {
                    if (b.Length > 0)
                    {
                        b.Append("   |   ");
                    }
                    b.Append(part);
                }
            }
            return b.ToString();
        }
    }

    /// <summary>The heading above the live amounts: what to make, and the data to make it with.</summary>
    public string TrackerTitle
    {
        get
        {
            string data = DataLine;
            return "[Challenge: " + Headline + "]" + (string.IsNullOrEmpty(data) ? string.Empty : "\n" + data);
        }
    }

    /// <summary>
    /// The calculation, step by step with the real numbers. Shown only once the attempt has a
    /// verdict - on the failure text, and by the lab assistant when asked.
    /// </summary>
    public string WorkedSolution
    {
        get
        {
            StringBuilder b = new StringBuilder("How to work it out:\n");
            b.AppendFormat(CultureInfo.InvariantCulture, "n({0}) = {1:0.0} g / {2:0.00} g/mol = {3:0.0000} mol\n",
                ProductFormula, ProductGrams, productMolarMass, productMoles);

            for (int i = 0; i < definition.reagents.Count; i++)
            {
                ReagentDefinition reagent = definition.reagents[i];
                float target = TargetFor(reagent.name);

                if (reagent.challengeRole == ChallengeRole.Reactant)
                {
                    float moles = MolesOf(reagent);
                    b.AppendFormat(CultureInfo.InvariantCulture, "{0}: {1}:{2} ratio -> {3:0.0000} mol",
                        reagent.name, reagent.coefficient, definition.productCoefficient, moles);

                    float concentration;
                    if (concentrations.TryGetValue(reagent.name, out concentration))
                    {
                        b.AppendFormat(CultureInfo.InvariantCulture, " / {0:0.00} mol/L = {1:0.0} ml\n",
                            concentration, target);
                    }
                    else
                    {
                        b.AppendFormat(CultureInfo.InvariantCulture, " x {0:0.00} g/mol = {1:0.00} g\n",
                            ChemicalFormula.MolarMass(reagent.formula), target);
                    }
                }
                else if (reagent.challengeRole == ChallengeRole.Solvent)
                {
                    b.AppendFormat(CultureInfo.InvariantCulture, "{0}: {1:0.0} {2}/g x {3:0.00} g {4} = {5:0.0} {2}\n",
                        reagent.name, SolventRatio(reagent), reagent.unit,
                        TargetFor(definition.challengeBasisReagent), definition.challengeBasisReagent, target);
                }
                else
                {
                    b.AppendFormat(CultureInfo.InvariantCulture, "{0}: catalyst, {1:0.0} {2}\n",
                        reagent.name, target, reagent.unit);
                }
            }

            return b.ToString().TrimEnd();
        }
    }

    /// <summary>The method without the numbers - what the assistant says before a verdict.</summary>
    public string Method
    {
        get
        {
            return "1. Moles of product = the mass asked for / the molar mass of " + ProductFormula + ".\n" +
                   "2. Use the coefficients in " + definition.equation + " to get the moles of each reactant.\n" +
                   "3. For a powder, moles x molar mass gives grams. For a solution, moles / concentration " +
                   "gives litres - multiply by 1000 for ml.\n" +
                   "4. Solvents and catalysts follow the amounts given in the brief.";
        }
    }
}
