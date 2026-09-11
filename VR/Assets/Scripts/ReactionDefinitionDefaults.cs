using UnityEngine;

/// <summary>
/// The built-in copy of every recipe in <c>Resources/ReactionDefinitions</c>, used only when an
/// asset is missing or fails <see cref="ReactionDefinition.IsValid"/>. Edit the assets, not this:
/// both are generated from one table, and this exists so that a damaged data file degrades to
/// the shipped recipe instead of to an experiment with nothing in it.
/// </summary>
public static class ReactionDefinitionDefaults
{
    public static ReactionDefinition Create(int reactionId)
    {
        switch (reactionId)
        {
            case 1: return Reaction1();
            case 2: return Reaction2();
            case 3: return Reaction3();
            case 4: return Reaction4();
            case 5: return Reaction5();
            case 6: return Reaction6();
            case 7: return Reaction7();
            case 8: return Reaction8();
            default: return null;
        }
    }

    private static ReactionDefinition Reaction1()
    {
        ReactionDefinition d = ScriptableObject.CreateInstance<ReactionDefinition>();
        d.name = "ReactionDefinition_1 (built-in)";
        d.reactionId = 1;
        d.displayName = "Na + H2O -> NaOH + H2";
        d.examDisplayName = "Na + H2O -> NaOH + H2 [Test]";
        d.equation = "2Na + 2H2O -> 2NaOH + H2";
        d.kind = ReactionKind.Measured;
        d.tolerancePercent = 5.0f;
        d.settleSeconds = 1.5f;
        d.enforceOrder = true;
        d.wrongOrderMessage = "Sodium was dropped in before the water was measured out. Alkali metals must meet a known volume of water, never a dry or half-filled vessel.";
        d.procedureFailureReason = "";
        d.procedureFailureHeadline = "";
        d.reagents.Add(new ReagentDefinition
        {
            name = "Water",
            unit = "ml",
            target = 50.0f,
            flowPerSecond = 10.0f,
            tolerancePercentOverride = -1.0f,
            orderMatters = true,
            orderGroup = -1,
            addition = ReagentAddition.Pour,
            overdoseMessage = "Excess water dilutes the reaction - the NaOH produced is too dilute to show a clear basic result.",
            underdoseMessage = "Too little water cannot dissolve the NaOH that forms, so the reaction stalls and heats dangerously.",
            lumpGraceSeconds = 0.0f,
            lumpOverflowPerSecond = 0.0f,
            formula = "H2O",
            coefficient = 2,
            challengeRole = ChallengeRole.Fixed
        });
        d.reagents.Add(new ReagentDefinition
        {
            name = "Sodium",
            unit = "g",
            target = 5.0f,
            flowPerSecond = 0.0f,
            tolerancePercentOverride = -1.0f,
            orderMatters = true,
            orderGroup = -1,
            addition = ReagentAddition.Lump,
            overdoseMessage = "Too much sodium causes a dangerous explosion - the hydrogen released ignites from the reaction heat.",
            underdoseMessage = "Too little sodium leaves most of the water unreacted, so hardly any NaOH is formed.",
            lumpGraceSeconds = 0.0f,
            lumpOverflowPerSecond = 0.0f,
            formula = "Na",
            coefficient = 2,
            challengeRole = ChallengeRole.Fixed
        });
        d.challengeEligible = false;
        d.productName = "sodium hydroxide";
        d.productFormula = "NaOH";
        d.productCoefficient = 2;
        d.challengeBasisReagent = "";
        return d;
    }

    private static ReactionDefinition Reaction2()
    {
        ReactionDefinition d = ScriptableObject.CreateInstance<ReactionDefinition>();
        d.name = "ReactionDefinition_2 (built-in)";
        d.reactionId = 2;
        d.displayName = "H2SO4 + CuO -> CuSO4 + H2O";
        d.examDisplayName = "H2SO4 + CuO -> CuSO4 + H2O [Test]";
        d.equation = "H2SO4 + CuO -> CuSO4 + H2O";
        d.kind = ReactionKind.Measured;
        d.tolerancePercent = 8.0f;
        d.settleSeconds = 1.5f;
        d.enforceOrder = true;
        d.wrongOrderMessage = "Copper(II) oxide was tipped in before any acid was present, so there was no H2SO4 for the oxide to dissolve in.";
        d.procedureFailureReason = "";
        d.procedureFailureHeadline = "";
        d.reagents.Add(new ReagentDefinition
        {
            name = "H2SO4",
            unit = "ml",
            target = 20.0f,
            flowPerSecond = 5.0f,
            tolerancePercentOverride = -1.0f,
            orderMatters = true,
            orderGroup = -1,
            addition = ReagentAddition.Pour,
            overdoseMessage = "Excess acid creates corrosive fumes - the leftover H2SO4 has no CuO left to neutralise it.",
            underdoseMessage = "Too little acid leaves most of the copper oxide undissolved, so no CuSO4 forms.",
            lumpGraceSeconds = 0.0f,
            lumpOverflowPerSecond = 0.0f,
            formula = "H2SO4",
            coefficient = 1,
            challengeRole = ChallengeRole.Reactant
        });
        d.reagents.Add(new ReagentDefinition
        {
            name = "CuO",
            unit = "g",
            target = 8.0f,
            flowPerSecond = 2.0f,
            tolerancePercentOverride = -1.0f,
            orderMatters = true,
            orderGroup = -1,
            addition = ReagentAddition.Pour,
            overdoseMessage = "Excess copper oxide simply settles out - only the acid present can be converted to CuSO4.",
            underdoseMessage = "Insufficient CuO leaves unreacted acid, so the solution stays strongly acidic instead of turning blue.",
            lumpGraceSeconds = 0.0f,
            lumpOverflowPerSecond = 0.0f,
            formula = "CuO",
            coefficient = 1,
            challengeRole = ChallengeRole.Reactant
        });
        d.challengeEligible = true;
        d.productName = "copper(II) sulfate";
        d.productFormula = "CuSO4";
        d.productCoefficient = 1;
        d.challengeBasisReagent = "CuO";
        return d;
    }

    private static ReactionDefinition Reaction3()
    {
        ReactionDefinition d = ScriptableObject.CreateInstance<ReactionDefinition>();
        d.name = "ReactionDefinition_3 (built-in)";
        d.reactionId = 3;
        d.displayName = "HCl + NaHCO3 -> NaCl + H2O + CO2";
        d.examDisplayName = "HCl + NaHCO3 -> NaCl + H2O + CO2 [Test]";
        d.equation = "HCl + NaHCO3 -> NaCl + H2O + CO2";
        d.kind = ReactionKind.Measured;
        d.tolerancePercent = 10.0f;
        d.settleSeconds = 1.5f;
        d.enforceOrder = true;
        d.wrongOrderMessage = "Sodium bicarbonate was tipped in before the acid, so the CO2 escaped from a dry powder instead of a controlled neutralisation.";
        d.procedureFailureReason = "";
        d.procedureFailureHeadline = "";
        d.reagents.Add(new ReagentDefinition
        {
            name = "HCl",
            unit = "ml",
            target = 15.0f,
            flowPerSecond = 5.0f,
            tolerancePercentOverride = -1.0f,
            orderMatters = true,
            orderGroup = -1,
            addition = ReagentAddition.Pour,
            overdoseMessage = "Too much acid produces excessive CO2 gas violently - the froth overflows and the leftover HCl stays in the beaker.",
            underdoseMessage = "Too little acid leaves most of the bicarbonate unreacted, so effervescence stops almost immediately.",
            lumpGraceSeconds = 0.0f,
            lumpOverflowPerSecond = 0.0f,
            formula = "HCl",
            coefficient = 1,
            challengeRole = ChallengeRole.Reactant
        });
        d.reagents.Add(new ReagentDefinition
        {
            name = "NaHCO3",
            unit = "g",
            target = 12.0f,
            flowPerSecond = 3.0f,
            tolerancePercentOverride = -1.0f,
            orderMatters = true,
            orderGroup = -1,
            addition = ReagentAddition.Pour,
            overdoseMessage = "Excess bicarbonate cannot react - only the HCl present can be neutralised, so solid NaHCO3 is left behind.",
            underdoseMessage = "Insufficient bicarbonate leaves an acidic solution, so the neutralisation to NaCl is incomplete.",
            lumpGraceSeconds = 0.0f,
            lumpOverflowPerSecond = 0.0f,
            formula = "NaHCO3",
            coefficient = 1,
            challengeRole = ChallengeRole.Reactant
        });
        d.challengeEligible = true;
        d.productName = "sodium chloride";
        d.productFormula = "NaCl";
        d.productCoefficient = 1;
        d.challengeBasisReagent = "NaHCO3";
        return d;
    }

    private static ReactionDefinition Reaction4()
    {
        ReactionDefinition d = ScriptableObject.CreateInstance<ReactionDefinition>();
        d.name = "ReactionDefinition_4 (built-in)";
        d.reactionId = 4;
        d.displayName = "K + H2O -> KOH + H2";
        d.examDisplayName = "K + H2O -> KOH + H2 [Test]";
        d.equation = "2K + 2H2O -> 2KOH + H2";
        d.kind = ReactionKind.Measured;
        d.tolerancePercent = 5.0f;
        d.settleSeconds = 1.5f;
        d.enforceOrder = true;
        d.wrongOrderMessage = "Potassium was dropped in before the water was measured out. Alkali metals must meet a known volume of water, never a dry or half-filled vessel.";
        d.procedureFailureReason = "";
        d.procedureFailureHeadline = "";
        d.reagents.Add(new ReagentDefinition
        {
            name = "Water",
            unit = "ml",
            target = 50.0f,
            flowPerSecond = 10.0f,
            tolerancePercentOverride = -1.0f,
            orderMatters = true,
            orderGroup = -1,
            addition = ReagentAddition.Pour,
            overdoseMessage = "Excess water dilutes the reaction - the KOH produced is too dilute to show a clear basic result.",
            underdoseMessage = "Too little water cannot dissolve the KOH that forms, so the reaction stalls and heats dangerously.",
            lumpGraceSeconds = 0.0f,
            lumpOverflowPerSecond = 0.0f,
            formula = "H2O",
            coefficient = 2,
            challengeRole = ChallengeRole.Fixed
        });
        d.reagents.Add(new ReagentDefinition
        {
            name = "Potassium",
            unit = "g",
            target = 3.0f,
            flowPerSecond = 0.0f,
            tolerancePercentOverride = -1.0f,
            orderMatters = true,
            orderGroup = -1,
            addition = ReagentAddition.Lump,
            overdoseMessage = "Too much potassium causes a dangerous explosion - the hydrogen released ignites from the reaction heat.",
            underdoseMessage = "Too little potassium leaves most of the water unreacted, so hardly any KOH is formed.",
            lumpGraceSeconds = 2.0f,
            lumpOverflowPerSecond = 1.0f,
            formula = "K",
            coefficient = 2,
            challengeRole = ChallengeRole.Fixed
        });
        d.challengeEligible = false;
        d.productName = "potassium hydroxide";
        d.productFormula = "KOH";
        d.productCoefficient = 2;
        d.challengeBasisReagent = "";
        return d;
    }

    private static ReactionDefinition Reaction5()
    {
        ReactionDefinition d = ScriptableObject.CreateInstance<ReactionDefinition>();
        d.name = "ReactionDefinition_5 (built-in)";
        d.reactionId = 5;
        d.displayName = "2Al + 3I2 -> 2AlI3";
        d.examDisplayName = "2Al + 3I2 -> 2AlI3 [Test]";
        d.equation = "2Al + 3I2 -> 2AlI3";
        d.kind = ReactionKind.Measured;
        d.tolerancePercent = 10.0f;
        d.settleSeconds = 1.5f;
        d.enforceOrder = true;
        d.wrongOrderMessage = "The water was added before both solids were in the dish. Water only acts as the catalyst once aluminium and iodine are already mixed as dry powders.";
        d.procedureFailureReason = "";
        d.procedureFailureHeadline = "";
        d.reagents.Add(new ReagentDefinition
        {
            name = "Aluminium",
            unit = "g",
            target = 5.0f,
            flowPerSecond = 1.25f,
            tolerancePercentOverride = -1.0f,
            orderMatters = true,
            orderGroup = 0,
            addition = ReagentAddition.Pour,
            overdoseMessage = "Incorrect Al:I2 ratio prevents stoichiometric completion - the surplus aluminium stays as grey metal in the dish.",
            underdoseMessage = "Incorrect Al:I2 ratio prevents stoichiometric completion - too little aluminium leaves unreacted violet iodine behind.",
            lumpGraceSeconds = 0.0f,
            lumpOverflowPerSecond = 0.0f,
            formula = "Al",
            coefficient = 2,
            challengeRole = ChallengeRole.Reactant
        });
        d.reagents.Add(new ReagentDefinition
        {
            name = "Iodine",
            unit = "g",
            target = 15.0f,
            flowPerSecond = 3.75f,
            tolerancePercentOverride = -1.0f,
            orderMatters = true,
            orderGroup = 0,
            addition = ReagentAddition.Pour,
            overdoseMessage = "Incorrect Al:I2 ratio prevents stoichiometric completion - excess iodine sublimes off as violet vapour instead of forming AlI3.",
            underdoseMessage = "Incorrect Al:I2 ratio prevents stoichiometric completion - 2Al needs 3I2, so a shortage of iodine caps the yield.",
            lumpGraceSeconds = 0.0f,
            lumpOverflowPerSecond = 0.0f,
            formula = "I2",
            coefficient = 3,
            challengeRole = ChallengeRole.Reactant
        });
        d.reagents.Add(new ReagentDefinition
        {
            name = "Water drops",
            unit = "ml",
            target = 2.0f,
            flowPerSecond = 0.7f,
            tolerancePercentOverride = -1.0f,
            orderMatters = true,
            orderGroup = 1,
            addition = ReagentAddition.Dropper,
            overdoseMessage = "Too much water floods the mixture and carries the heat away, so the catalysed reaction never reaches ignition.",
            underdoseMessage = "Too few drops of catalyst - without enough water the aluminium oxide layer is never broken and the mixture stays inert.",
            lumpGraceSeconds = 0.0f,
            lumpOverflowPerSecond = 0.0f,
            formula = "H2O",
            coefficient = 0,
            challengeRole = ChallengeRole.Fixed
        });
        d.challengeEligible = true;
        d.productName = "aluminium iodide";
        d.productFormula = "AlI3";
        d.productCoefficient = 2;
        d.challengeBasisReagent = "Iodine";
        return d;
    }

    private static ReactionDefinition Reaction6()
    {
        ReactionDefinition d = ScriptableObject.CreateInstance<ReactionDefinition>();
        d.name = "ReactionDefinition_6 (built-in)";
        d.reactionId = 6;
        d.displayName = "CaO + H2O -> Ca(OH)2";
        d.examDisplayName = "CaO + H2O -> Ca(OH)2 [Test]";
        d.equation = "CaO + H2O -> Ca(OH)2";
        d.kind = ReactionKind.Measured;
        d.tolerancePercent = 8.0f;
        d.settleSeconds = 1.5f;
        d.enforceOrder = true;
        d.wrongOrderMessage = "The quicklime went into a dry beaker. CaO must be slaked into a measured volume of water, otherwise the heat released has nothing to absorb it.";
        d.procedureFailureReason = "";
        d.procedureFailureHeadline = "";
        d.reagents.Add(new ReagentDefinition
        {
            name = "Water",
            unit = "ml",
            target = 30.0f,
            flowPerSecond = 5.0f,
            tolerancePercentOverride = -1.0f,
            orderMatters = true,
            orderGroup = -1,
            addition = ReagentAddition.Pour,
            overdoseMessage = "Excess water produces dilute Ca(OH)2 - limewater so weak that the litmus test barely changes colour.",
            underdoseMessage = "Insufficient water leaves unreacted quicklime, so part of the CaO never slakes into calcium hydroxide.",
            lumpGraceSeconds = 0.0f,
            lumpOverflowPerSecond = 0.0f,
            formula = "H2O",
            coefficient = 1,
            challengeRole = ChallengeRole.Solvent
        });
        d.reagents.Add(new ReagentDefinition
        {
            name = "CaO",
            unit = "g",
            target = 15.0f,
            flowPerSecond = 3.0f,
            tolerancePercentOverride = -1.0f,
            orderMatters = true,
            orderGroup = -1,
            addition = ReagentAddition.Pour,
            overdoseMessage = "Too much quicklime for this volume of water - the surplus CaO stays as a dry lump and the mixture boils dangerously.",
            underdoseMessage = "Too little quicklime leaves mostly water in the beaker, so hardly any Ca(OH)2 forms.",
            lumpGraceSeconds = 0.0f,
            lumpOverflowPerSecond = 0.0f,
            formula = "CaO",
            coefficient = 1,
            challengeRole = ChallengeRole.Reactant
        });
        d.challengeEligible = true;
        d.productName = "calcium hydroxide";
        d.productFormula = "Ca(OH)2";
        d.productCoefficient = 1;
        d.challengeBasisReagent = "CaO";
        return d;
    }

    private static ReactionDefinition Reaction7()
    {
        ReactionDefinition d = ScriptableObject.CreateInstance<ReactionDefinition>();
        d.name = "ReactionDefinition_7 (built-in)";
        d.reactionId = 7;
        d.displayName = "CaCO3 -> CaO + CO2 (thermal decomposition)";
        d.examDisplayName = "CaCO3 -> CaO + CO2 [Test]";
        d.equation = "CaCO3 -> CaO + CO2";
        d.kind = ReactionKind.Heating;
        d.tolerancePercent = 15.0f;
        d.settleSeconds = 1.5f;
        d.enforceOrder = true;
        d.wrongOrderMessage = "";
        d.procedureFailureReason = "The CaCO3 was heated before the balloon was fitted, so the carbon dioxide escaped into the room instead of being collected.";
        d.procedureFailureHeadline = "FAILED: CO2 escaped\nBalloon was not fitted before heating";
        d.reagents.Add(new ReagentDefinition
        {
            name = "Heating",
            unit = "s",
            target = 10.0f,
            flowPerSecond = 1.0f,
            tolerancePercentOverride = -1.0f,
            orderMatters = true,
            orderGroup = -1,
            addition = ReagentAddition.Heat,
            overdoseMessage = "The tube was held in the flame far too long - the CaO sinters and the trapped CO2 over-pressurises the balloon.",
            underdoseMessage = "Insufficient heating leaves undissociated CaCO3 - thermal decomposition needs sustained heat above 800 C to drive the CO2 off.",
            lumpGraceSeconds = 0.0f,
            lumpOverflowPerSecond = 0.0f,
            formula = "",
            coefficient = 0,
            challengeRole = ChallengeRole.Fixed
        });
        d.challengeEligible = false;
        d.productName = "calcium oxide";
        d.productFormula = "CaO";
        d.productCoefficient = 1;
        d.challengeBasisReagent = "";
        return d;
    }

    private static ReactionDefinition Reaction8()
    {
        ReactionDefinition d = ScriptableObject.CreateInstance<ReactionDefinition>();
        d.name = "ReactionDefinition_8 (built-in)";
        d.reactionId = 8;
        d.displayName = "2FeSO4 -> Fe2O3 + SO2 + SO3";
        d.examDisplayName = "2FeSO4 -> Fe2O3 + SO2 + SO3 [Test]";
        d.equation = "2FeSO4 -> Fe2O3 + SO2 + SO3";
        d.kind = ReactionKind.Heating;
        d.tolerancePercent = 15.0f;
        d.settleSeconds = 1.5f;
        d.enforceOrder = true;
        d.wrongOrderMessage = "";
        d.procedureFailureReason = "";
        d.procedureFailureHeadline = "";
        d.reagents.Add(new ReagentDefinition
        {
            name = "Heating",
            unit = "s",
            target = 10.0f,
            flowPerSecond = 1.0f,
            tolerancePercentOverride = -1.0f,
            orderMatters = true,
            orderGroup = -1,
            addition = ReagentAddition.Heat,
            overdoseMessage = "The tube was left in the flame long past full decomposition - the Fe2O3 bakes onto the glass and the SO2/SO3 fumes build up dangerously.",
            underdoseMessage = "Insufficient heating produces incomplete decomposition - some FeSO4 never breaks down, so the solid stays green instead of turning reddish brown.",
            lumpGraceSeconds = 0.0f,
            lumpOverflowPerSecond = 0.0f,
            formula = "",
            coefficient = 0,
            challengeRole = ChallengeRole.Fixed
        });
        d.challengeEligible = false;
        d.productName = "iron(III) oxide";
        d.productFormula = "Fe2O3";
        d.productCoefficient = 1;
        d.challengeBasisReagent = "";
        return d;
    }
}
