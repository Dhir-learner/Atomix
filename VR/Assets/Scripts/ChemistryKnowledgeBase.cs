using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// The assistant's offline brain.
///
/// Until now every AI answer in Atomix came from Convai's REST endpoint, which means the four
/// features the project is built around - "why did my experiment go wrong", "what is happening
/// chemically", "explain this graph", "explain the molecular animation" - all failed together the
/// moment there was no internet, no microphone, an expired free-tier quota, or a firewall between
/// the lab PC and api.convai.com. In a college lab or a viva, that is most of the time.
///
/// This class answers the same questions from the data the game already holds: the live
/// <see cref="FreeHandReactionEngine"/> measurements, the thermodynamics in
/// <see cref="ReactionGraphCatalog"/>, the steps in <see cref="MolecularSceneCatalog"/>, and the
/// student's own attempt history. It is not a language model and does not pretend to be - it is a
/// grounded lookup that is always right about this bench, where a cloud model can only be
/// approximately right about chemistry in general.
///
/// Convai stays the primary path. This is what answers when Convai cannot.
/// </summary>
public static class ChemistryKnowledgeBase
{
    public enum Intent
    {
        WhyFailed,
        WhatHappened,
        Procedure,
        Quantities,
        Graphs,
        Molecular,
        Safety,
        Progress,
        Overview,
        Calculation
    }

    // =====================================================================================
    // PER-REACTION FACTS
    // =====================================================================================

    private class ReactionFacts
    {
        public int id;
        public string name;
        public string equation;
        public string type;
        public string whatYouDo;
        public string whatYouSee;
        public string keyConcept;
        public string commonMistake;
        public string safety;
    }

    private static Dictionary<int, ReactionFacts> facts;

    private static void BuildFacts()
    {
        facts = new Dictionary<int, ReactionFacts>();

        Add(1, "Sodium and water", "2Na + 2H2O -> 2NaOH + H2",
            "single displacement (redox)",
            "Pour the measured water into the Berzelius beaker first, then add the sodium.",
            "The sodium skates across the surface, hydrogen fizzes off, and the solution turns " +
            "pink if phenolphthalein is present - because sodium hydroxide is a strong base.",
            "Sodium has one loosely held outer electron. It gives that electron to water, which " +
            "releases hydrogen gas and leaves hydroxide ions behind. Losing electrons is " +
            "oxidation, so sodium is oxidised and hydrogen is reduced.",
            "Adding the sodium before the water, or using too much sodium for the water present. " +
            "With too little water the hydroxide never forms in solution and the reaction is " +
            "uncontrolled.",
            "In a real lab this is done behind a screen with a pea-sized piece. The hydrogen " +
            "released can ignite from the heat of the reaction itself.");

        Add(2, "Sulfuric acid and copper(II) oxide", "H2SO4 + CuO -> CuSO4 + H2O",
            "acid-base neutralisation of a basic oxide",
            "Pour the sulfuric acid into the beaker, then add the black copper(II) oxide powder " +
            "and warm it gently.",
            "The black powder disappears and the solution turns clear blue. That blue is the " +
            "hydrated copper(II) ion.",
            "Copper(II) oxide is a basic oxide, so it neutralises an acid to give a salt and " +
            "water. No electrons change hands: copper stays 2+ and sulfur stays 6+ throughout, " +
            "which is what makes this a neutralisation rather than a redox reaction.",
            "Not enough acid to dissolve all the oxide, so black powder is left sitting at the " +
            "bottom, or so much acid that unreacted acid is left in the product.",
            "Sulfuric acid is corrosive. Always add acid to water, never water to acid.");

        Add(3, "Hydrochloric acid and sodium bicarbonate", "HCl + NaHCO3 -> NaCl + H2O + CO2",
            "acid-carbonate reaction (endothermic)",
            "Pour the hydrochloric acid onto the sodium bicarbonate.",
            "Immediate vigorous fizzing as carbon dioxide escapes, and the beaker gets colder to " +
            "the touch.",
            "The acid protonates the bicarbonate to make carbonic acid, H2CO3, which is so " +
            "unstable it immediately falls apart into water and carbon dioxide. The fizzing you " +
            "see is that CO2 leaving the liquid.",
            "Using too little acid, so some bicarbonate never reacts and the fizzing stops early.",
            "Mild, but the CO2 displaces air - do not do this in a sealed container, which can " +
            "build up pressure.");

        Add(4, "Potassium and water", "2K + 2H2O -> 2KOH + H2",
            "single displacement (redox)",
            "Pour the measured water in first, then unscrew the potassium container and add the " +
            "potassium.",
            "A faster, more violent version of the sodium reaction, usually with a lilac flame as " +
            "the hydrogen ignites.",
            "Potassium is below sodium in group 1, so its outer electron is further from the " +
            "nucleus and even more loosely held. Reactivity increases down group 1 for exactly " +
            "that reason.",
            "Same as sodium: adding the metal before the water, or the wrong metal-to-water ratio.",
            "More dangerous than sodium. The hydrogen usually ignites on its own from the heat " +
            "released.");

        Add(5, "Aluminium and iodine", "2Al + 3I2 -> 2AlI3",
            "synthesis (redox)",
            "Mix the aluminium powder with the iodine, then add a single drop of water to start it.",
            "A dense purple cloud of iodine vapour and a lot of heat.",
            "Each aluminium atom gives up three electrons to become Al3+, and each iodine atom " +
            "takes one to become I-. The water is a catalyst - it is not consumed, it just lowers " +
            "the barrier enough for the reaction to start.",
            "Forgetting the water, so nothing happens at all, or adding too much of either " +
            "reagent so one is left over.",
            "Fume hood only. Iodine vapour is toxic and the reaction is violently exothermic.");

        Add(6, "Quicklime and water", "CaO + H2O -> Ca(OH)2",
            "synthesis (strongly exothermic)",
            "Add the water to the calcium oxide.",
            "The solid swells and steams, and the beaker becomes genuinely hot.",
            "The oxide ion in quicklime carries a full 2- charge, which makes it a very strong " +
            "base - strong enough to pull a proton straight off a water molecule. One water and " +
            "one oxide become two hydroxides.",
            "Adding too much water, which dilutes the product instead of slaking the lime.",
            "This is called slaking, and it can boil the water you add. Real slaking is done with " +
            "goggles and gloves.");

        Add(7, "Thermal decomposition of limestone", "CaCO3 -> CaO + CO2",
            "thermal decomposition (endothermic)",
            "Put the calcium carbonate in the test tube and heat it strongly with the burner.",
            "The solid stays put but carbon dioxide comes off; lime water in a second tube turns " +
            "milky if you collect it.",
            "This needs 185 kJ/mol of activation energy and absorbs 178 kJ/mol overall. Because " +
            "it is endothermic it only continues while you keep heating - take the flame away " +
            "and it stops.",
            "Not heating long enough or hot enough. This reaction needs about 840 degrees C.",
            "The tube gets extremely hot. Never point the open end at anyone.");

        Add(8, "Thermal decomposition of iron(II) sulfate", "2FeSO4 -> Fe2O3 + SO2 + SO3",
            "thermal decomposition (endothermic, redox)",
            "Heat the green iron(II) sulfate crystals strongly in the test tube, with the balloon " +
            "fitted before you start heating.",
            "The green crystals turn to a red-brown residue and choking fumes come off.",
            "Two things happen at once. Iron is oxidised from Fe2+ to Fe3+, and one sulfur is " +
            "reduced from 6+ to 4+ - which is why one sulfur leaves as SO2 and the other, " +
            "untouched, leaves as SO3. The red-brown residue is Fe2O3, the same compound as rust.",
            "Heating before the balloon is fitted, so the gases escape and the experiment cannot " +
            "be judged. This has the highest activation energy on the bench at 365 kJ/mol.",
            "SO2 and SO3 are toxic and corrosive. This is a fume-hood reaction.");
    }

    private static void Add(int id, string name, string equation, string type,
        string whatYouDo, string whatYouSee, string keyConcept, string commonMistake, string safety)
    {
        ReactionFacts f = new ReactionFacts();
        f.id = id;
        f.name = name;
        f.equation = equation;
        f.type = type;
        f.whatYouDo = whatYouDo;
        f.whatYouSee = whatYouSee;
        f.keyConcept = keyConcept;
        f.commonMistake = commonMistake;
        f.safety = safety;
        facts[id] = f;
    }

    private static ReactionFacts Facts(int reactionId)
    {
        if (facts == null)
        {
            BuildFacts();
        }

        ReactionFacts result;
        return facts.TryGetValue(reactionId, out result) ? result : null;
    }

    // =====================================================================================
    // INTENT
    // =====================================================================================

    /// <summary>
    /// Works out what the student is actually asking.
    ///
    /// Keyword scoring rather than first-match, and weighted rather than flat. Both matter:
    ///
    ///  - First-match fails on "why does the energy profile graph have a peak?", which contains a
    ///    failure word and a graph word, and answers the wrong question.
    ///  - Flat scoring fails on "can you explain the energy versus reaction progress graph", where
    ///    the generic verb "explain" outscored the specific noun "graph". Generic verbs are worth
    ///    1, topic words 3, and multi-word phrases 4, because a phrase can only have been typed on
    ///    purpose.
    /// </summary>
    public static Intent Classify(string question)
    {
        string q = (question ?? string.Empty).ToLowerInvariant();

        int failed =
            Score(q, 3, "wrong", "fail", "failed", "mistake", "error",
                        "didn't work", "did not work", "not working") +
            Score(q, 4, "why did it", "why did my", "what did i do wrong");

        int graphs =
            Score(q, 3, "graph", "enthalpy", "entropy", "exothermic", "endothermic",
                        "gibbs", "spontaneous", "activation") +
            Score(q, 4, "energy profile", "energy versus", "energy vs", "energy diagram",
                        "reaction progress", "delta h");

        int molecular =
            Score(q, 3, "molecular", "animation", "bond", "bonds", "electron", "electrons",
                        "atom", "atoms", "molecule", "orbital", "video") +
            Score(q, 4, "molecular level", "at the molecular");

        int procedure =
            Score(q, 4, "how do i", "how to", "what do i do", "steps", "procedure",
                        "instructions", "in what order");

        int quantities =
            Score(q, 3, "quantity", "amount", "tolerance", "proportion", "ratio", "measure") +
            Score(q, 4, "how much", "how many ml", "how many grams");

        int safety =
            Score(q, 3, "safe", "safety", "danger", "dangerous", "hazard", "toxic",
                        "explode", "explosion");

        int progress =
            Score(q, 4, "how am i doing", "my score", "my progress", "attempts",
                        "how many times", "my history", "my record");

        int happened =
            Score(q, 3, "what is happening", "what's happening", "what happened",
                        "chemistry", "chemically") +
            Score(q, 1, "explain", "why does", "how does", "what does", "tell me about");

        int calculation =
            Score(q, 3, "calculate", "calculation", "stoichiometr", "moles", "molar",
                        "work out", "challenge");

        int best = failed;
        Intent intent = Intent.WhyFailed;

        if (graphs > best) { best = graphs; intent = Intent.Graphs; }
        if (molecular > best) { best = molecular; intent = Intent.Molecular; }
        if (procedure > best) { best = procedure; intent = Intent.Procedure; }
        if (quantities > best) { best = quantities; intent = Intent.Quantities; }
        if (safety > best) { best = safety; intent = Intent.Safety; }
        if (progress > best) { best = progress; intent = Intent.Progress; }
        if (happened > best) { best = happened; intent = Intent.WhatHappened; }

        // Checked last and on a strict "greater than", so a question any older intent already
        // answered keeps going where it went before.
        if (calculation > best) { best = calculation; intent = Intent.Calculation; }

        return best == 0 ? Intent.Overview : intent;
    }

    private static int Score(string question, int weight, params string[] keywords)
    {
        int score = 0;
        for (int i = 0; i < keywords.Length; i++)
        {
            if (question.Contains(keywords[i]))
            {
                score += weight;
            }
        }
        return score;
    }

    // =====================================================================================
    // ANSWERING
    // =====================================================================================

    /// <summary>
    /// Answers from local data. Never returns null or empty: an assistant that silently says
    /// nothing is worse than one that says "here is what I can tell you about this reaction".
    /// </summary>
    public static string Answer(string question)
    {
        int reactionId = ExperimentContextProvider.CurrentReactionId;
        ReactionFacts f = Facts(reactionId);

        switch (Classify(question))
        {
            case Intent.WhyFailed: return AnswerWhyFailed(f);
            case Intent.Graphs: return AnswerGraphs(reactionId, f);
            case Intent.Molecular: return AnswerMolecular(reactionId, f);
            case Intent.Procedure: return AnswerProcedure(f);
            case Intent.Quantities: return AnswerQuantities(f);
            case Intent.Safety: return AnswerSafety(f);
            case Intent.Progress: return AnswerProgress(reactionId, f);
            case Intent.WhatHappened: return AnswerWhatHappened(f);
            case Intent.Calculation: return AnswerCalculation(f);
            default: return AnswerOverview(f);
        }
    }

    /// <summary>
    /// How to work out the amounts. During a stoichiometry challenge that is the whole point, so
    /// before the verdict it gives the method and the brief but never the numbers; after the
    /// verdict it walks through the real calculation.
    /// </summary>
    private static string AnswerCalculation(ReactionFacts f)
    {
        ReactionHistoryRecorder active = ReactionHistoryRecorder.Active;
        StoichiometryChallenge challenge = active != null ? active.Challenge : null;
        FreeHandReactionEngine engine = active != null ? active.Engine : null;

        if (challenge == null)
        {
            return AnswerQuantities(f);
        }

        if (engine != null && engine.IsResolved)
        {
            return (engine.HasSucceeded ? "You got it. " : string.Empty) + challenge.WorkedSolution;
        }

        return "Here is your brief:\n" + challenge.Brief + "\n\nThe method:\n" + challenge.Method +
               "\n\nI will not give you the numbers yet - that is the challenge. " +
               "Ask me again once it is judged and I will go through the working.";
    }

    private static string AnswerWhyFailed(ReactionFacts f)
    {
        ReactionHistoryRecorder active = ReactionHistoryRecorder.Active;
        FreeHandReactionEngine engine = active != null ? active.Engine : null;

        StringBuilder b = new StringBuilder();

        if (engine != null && engine.HasFailed)
        {
            // The engine already worked out the chemistry-accurate reason when it judged the
            // attempt. Restating it here keeps the assistant and the tooltip in agreement.
            b.Append(engine.GetFailureHeadline()).Append("\n\n");
            b.Append(DescribeMeasurements(engine)).Append("\n");

            switch (engine.LastResult)
            {
                case ReactionResult.FailOverdose:
                    b.Append("You went over on at least one reagent. Excess reagent does not make " +
                             "the reaction bigger - it means the other reagent runs out first and " +
                             "the leftover just sits there contaminating the product.\n\n");
                    break;
                case ReactionResult.FailUnderdose:
                    b.Append("You stopped short on at least one reagent. Below the required amount " +
                             "the limiting reagent runs out before the equation is satisfied, so " +
                             "the reaction is incomplete.\n\n");
                    break;
                case ReactionResult.FailWrongOrder:
                    b.Append("The reagents went in in the wrong order. Order matters here because " +
                             "the intermediate step never forms if the second reagent is not " +
                             "already waiting for it.\n\n");
                    break;
                default:
                    b.Append(engine.failureReason).Append("\n\n");
                    break;
            }

            // A failed challenge is usually a calculation that went wrong, not a pour.
            if (active.Challenge != null)
            {
                b.Append(active.Challenge.WorkedSolution).Append("\n\n");
            }
        }
        else if (engine != null && engine.HasSucceeded)
        {
            return "That attempt did not fail - it succeeded. " +
                   (f != null ? DescribeSuccess(f) : string.Empty);
        }
        else
        {
            ExperimentAttempt latest = ExperimentHistoryManager.Instance.GetMostRecentAttempt();
            if (latest == null)
            {
                return "You have not finished an experiment yet, so there is nothing to diagnose. " +
                       "Open the book with B, pick a reaction, and I will watch what you do.";
            }

            b.Append("Your last attempt at ").Append(latest.reactionName)
             .Append(" ended as ").Append(DescribeOutcome(latest.outcome)).Append(".\n\n");
        }

        if (f != null)
        {
            b.Append("The mistake students usually make here: ").Append(f.commonMistake).Append("\n\n");
            b.Append("To get it right: ").Append(f.whatYouDo);
        }

        return b.ToString().TrimEnd();
    }

    private static string DescribeMeasurements(FreeHandReactionEngine engine)
    {
        StringBuilder b = new StringBuilder("What you actually used:\n");
        List<string> substances = engine.Substances;

        for (int i = 0; i < substances.Count; i++)
        {
            string name = substances[i];
            float used = engine.GetCurrent(name);
            float target;

            if (!engine.targetQuantities.TryGetValue(name, out target))
            {
                continue;
            }

            string unit = engine.UnitFor(name);
            b.Append("  ").Append(name).Append(": ").Append(used.ToString("0.0")).Append(' ')
             .Append(unit).Append("  (needed ").Append(target.ToString("0.0")).Append(' ')
             .Append(unit);

            if (target > 0.0f)
            {
                float deviation = (used - target) / target * 100.0f;
                if (Mathf.Abs(deviation) >= 0.5f)
                {
                    b.Append(", ").Append(Mathf.Abs(deviation).ToString("0"))
                     .Append("% ").Append(deviation > 0.0f ? "over" : "under");
                }
            }

            b.Append(")\n");
        }

        return b.ToString();
    }

    private static string AnswerWhatHappened(ReactionFacts f)
    {
        if (f == null)
        {
            return "Pick a reaction from the book first and I can tell you exactly what is " +
                   "happening in it.";
        }

        return f.equation + "\n\nThis is a " + f.type + ".\n\n" +
               f.keyConcept + "\n\nWhat you should see: " + f.whatYouSee;
    }

    private static string AnswerProcedure(ReactionFacts f)
    {
        if (f == null)
        {
            return "Open the book with B, flip to the reaction you want, and choose PERFORM. " +
                   "Then I can talk you through that specific procedure.";
        }

        ReactionHistoryRecorder active = ReactionHistoryRecorder.Active;
        FreeHandReactionEngine engine = active != null ? active.Engine : null;

        StringBuilder b = new StringBuilder();
        b.Append(f.whatYouDo).Append("\n\n");

        if (engine != null && !engine.IsResolved)
        {
            string pending = engine.GetPendingSubstance();
            if (!string.IsNullOrEmpty(pending) && engine.ShowTargetsNow)
            {
                float target;
                engine.targetQuantities.TryGetValue(pending, out target);
                b.Append("Right now the next thing you need is ").Append(pending)
                 .Append(" - ").Append(target.ToString("0.0")).Append(' ')
                 .Append(engine.UnitFor(pending)).Append(".\n\n");
            }
            else if (!string.IsNullOrEmpty(pending))
            {
                // Expert or a challenge: the amount is the student's to know.
                b.Append("Right now the next thing you need is ").Append(pending)
                 .Append(" - how much is up to you at this level.\n\n");
            }
        }

        b.Append("What you should see: ").Append(f.whatYouSee);
        return b.ToString();
    }

    private static string AnswerQuantities(ReactionFacts f)
    {
        ReactionHistoryRecorder active = ReactionHistoryRecorder.Active;
        FreeHandReactionEngine engine = active != null ? active.Engine : null;

        if (engine == null)
        {
            return f == null
                ? "Start an experiment and I will tell you the exact amounts it needs."
                : "For " + f.name + " the equation is " + f.equation +
                  ". Start the experiment and I will give you the exact amounts and how close " +
                  "you currently are.";
        }

        if (!engine.ShowTargetsNow)
        {
            // Asking the assistant must not be a way round the hidden targets.
            StoichiometryChallenge challenge = active.Challenge;
            if (challenge != null)
            {
                return AnswerCalculation(f);
            }

            return "At the Expert level the amounts are yours to remember - I will not read them out " +
                   "while the experiment is running. Ask me again once it is judged, or switch this " +
                   "experiment to Standard in the book to see the targets.";
        }

        StringBuilder b = new StringBuilder("Here is what this reaction needs, and where you are:\n\n");
        List<string> substances = engine.Substances;

        for (int i = 0; i < substances.Count; i++)
        {
            string name = substances[i];
            float target;
            if (!engine.targetQuantities.TryGetValue(name, out target))
            {
                continue;
            }

            string unit = engine.UnitFor(name);
            b.Append("  ").Append(name).Append(": ").Append(target.ToString("0.0")).Append(' ')
             .Append(unit).Append("  (accepted ").Append(engine.MinAllowed(name).ToString("0.0"))
             .Append(" to ").Append(engine.MaxAllowed(name).ToString("0.0")).Append(' ').Append(unit)
             .Append(") - you have ").Append(engine.GetCurrent(name).ToString("0.0")).Append('\n');
        }

        b.Append("\nThe tolerance is ").Append(engine.EffectiveTolerancePercent.ToString("0.#"))
         .Append("%, the same margin a real lab would accept for this kind of preparation.");

        return b.ToString();
    }

    private static string AnswerGraphs(int reactionId, ReactionFacts f)
    {
        ReactionGraphData data = ReactionGraphCatalog.Get(reactionId);

        if (data == null)
        {
            return "Finish an experiment and press F to open the graphs - then I can walk you " +
                   "through the energy profile for it.";
        }

        bool exothermic = data.enthalpyChange < 0.0f;

        StringBuilder b = new StringBuilder();
        b.Append(data.ResolvedTitle).Append("\n\n");

        b.Append("Energy versus reaction progress: the curve climbs to a peak and then drops. That " +
                 "peak is the activation energy, ")
         .Append(data.activationEnergy.ToString("0")).Append(" kJ/mol - the hill the reactants " +
                 "have to get over before anything can happen. It is why this reaction ")
         .Append(data.activationEnergy > 100.0f
             ? "needs strong heating to start."
             : "starts easily once the reagents meet.")
         .Append("\n\n");

        b.Append("Where the curve ends compared with where it started is the enthalpy change, ")
         .Append(data.enthalpyChange.ToString("0.0")).Append(" kJ/mol. It is ")
         .Append(exothermic ? "negative" : "positive").Append(", so the products sit ")
         .Append(exothermic ? "lower" : "higher")
         .Append(" than the reactants and the reaction is ")
         .Append(exothermic ? "exothermic - it releases heat into the beaker."
                            : "endothermic - it absorbs heat from its surroundings, which is why " +
                              "the beaker feels cold or needs constant heating.")
         .Append("\n\n");

        b.Append("Entropy change is ").Append(data.entropyChange.ToString("0.0"))
         .Append(" J/(mol K). ")
         .Append(data.entropyChange > 0.0f
             ? "It is positive, meaning the products are more disordered than the reactants - " +
               "usually because a gas is produced."
             : "It is negative, meaning the products are more ordered than the reactants - " +
               "usually because a gas or free ions are being locked into a solid or solution.")
         .Append("\n\n");

        b.Append(data.SpontaneitySummary());

        if (f != null)
        {
            b.Append("\n\nIn the beaker this shows up as: ").Append(f.whatYouSee);
        }

        return b.ToString();
    }

    private static string AnswerMolecular(int reactionId, ReactionFacts f)
    {
        MolecularScene scene = MolecularSceneCatalog.Get(reactionId);

        if (scene == null)
        {
            return f == null
                ? "Pick a reaction and open the 3D view, and I will take you through it step by step."
                : f.keyConcept;
        }

        StringBuilder b = new StringBuilder();
        b.Append("Step by step, at the molecular level, for ").Append(scene.equation).Append(":\n\n");

        for (int i = 0; i < scene.stages.Count; i++)
        {
            b.Append(i + 1).Append(". ").Append(scene.stages[i].caption).Append(" - ")
             .Append(scene.stages[i].detail).Append("\n\n");
        }

        b.Append("In the 3D view, a bond turning red and thinning out is a bond breaking, a bond " +
                 "coming in green is a bond forming, and a yellow dot is an electron moving from " +
                 "one atom to another.");

        return b.ToString();
    }

    private static string AnswerSafety(ReactionFacts f)
    {
        if (f == null)
        {
            return "Nothing in here can hurt you - that is rather the point of a virtual lab. " +
                   "Pick a reaction and I will tell you what the real precautions would be.";
        }

        return "Real-lab safety for " + f.name + ":\n\n" + f.safety +
               "\n\nNone of that can hurt you in here, which is exactly why this is a good place " +
               "to get the procedure wrong first.";
    }

    private static string AnswerProgress(int reactionId, ReactionFacts f)
    {
        ExperimentHistoryManager history = ExperimentHistoryManager.Instance;
        List<ExperimentAttempt> all = history.GetHistory();

        if (all == null || all.Count == 0)
        {
            return "You have not recorded an attempt yet. Everything you do is logged - press Tab " +
                   "at any time to see your history.";
        }

        int successes = 0;
        int failures = 0;

        for (int i = 0; i < all.Count; i++)
        {
            if (all[i].outcome == ExperimentOutcome.Success)
            {
                successes++;
            }
            else if (all[i].outcome != ExperimentOutcome.InProgress &&
                     all[i].outcome != ExperimentOutcome.Abandoned)
            {
                failures++;
            }
        }

        StringBuilder b = new StringBuilder();
        b.Append("You have ").Append(all.Count).Append(" recorded attempt")
         .Append(all.Count == 1 ? "" : "s").Append(": ").Append(successes)
         .Append(" successful and ").Append(failures).Append(" failed.\n\n");

        if (f != null)
        {
            int here = history.GetAttemptCount(reactionId);
            b.Append("On ").Append(f.name).Append(" specifically you have had ").Append(here)
             .Append(" attempt").Append(here == 1 ? "" : "s").Append(".\n\n");
        }

        b.Append(successes == 0
            ? "Nothing wrong with that - failing an experiment and finding out why is the whole " +
              "reason this bench lets you choose your own quantities."
            : "Press Tab for the full history, including the exact quantities you used each time.");

        return b.ToString();
    }

    private static string AnswerOverview(ReactionFacts f)
    {
        if (f == null)
        {
            return "I am your lab assistant. I can tell you why an experiment failed, what is " +
                   "happening chemically, what the energy and entropy graphs mean, and what the " +
                   "molecular animation is showing. Pick a reaction from the book with B and ask " +
                   "me anything about it.";
        }

        return f.name + "  -  " + f.equation + "\n\n" +
               "This is a " + f.type + ". " + f.keyConcept + "\n\n" +
               "What to do: " + f.whatYouDo + "\n\n" +
               "Ask me why it failed, what the graphs mean, or what is happening to the bonds.";
    }

    private static string DescribeSuccess(ReactionFacts f)
    {
        return "You got " + f.equation + " right. " + f.keyConcept;
    }

    private static string DescribeOutcome(ExperimentOutcome outcome)
    {
        switch (outcome)
        {
            case ExperimentOutcome.Success: return "a success";
            case ExperimentOutcome.FailOverdose: return "a failure - too much of a reagent";
            case ExperimentOutcome.FailUnderdose: return "a failure - not enough of a reagent";
            case ExperimentOutcome.FailWrongOrder: return "a failure - wrong order";
            case ExperimentOutcome.FailTimeout: return "a failure - the timer ran out";
            case ExperimentOutcome.Abandoned: return "abandoned";
            default: return "still in progress";
        }
    }

    /// <summary>
    /// The procedure for a reaction, with no quantities in it - what the testing scene's paid hint
    /// buys.
    ///
    /// Deliberately `whatYouDo` and not `AnswerQuantities`: knowing the right amount is precisely
    /// what the testing scene exists to measure, so selling that would be selling the answer. This
    /// tells the student what to do and in what order, and leaves them to judge how much.
    /// </summary>
    public static string ProcedureHint(int reactionId)
    {
        ReactionFacts f = Facts(reactionId);

        if (f == null)
        {
            return "Work through the reagents in the order the equation is written.";
        }

        return f.whatYouDo + " (" + f.equation + ")";
    }

    /// <summary>
    /// A one-line greeting naming what the assistant can do, used when the panel first opens.
    /// </summary>
    public static string Greeting(bool cloudAvailable)
    {
        return cloudAvailable
            ? "Lab assistant ready. Hold V to speak, or press Enter to type a question."
            : "Lab assistant ready in offline mode - I answer from this lab's own chemistry data. " +
              "Press Enter to type a question, or hold V to speak once a connection is available.";
    }
}
