using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Holds one <see cref="ReactionGraphData"/> per reaction, looked up by reaction id.
///
/// The shipped asset lives at Resources/ReactionGraphCatalog.asset and carries the eight entries
/// as sub-objects, so the values can be tuned in the Inspector. If that asset is missing or was
/// not imported, <see cref="BuildBuiltIn"/> reproduces exactly the same eight entries from code,
/// so the graphs never dead-end on a missing asset.
/// </summary>
[CreateAssetMenu(fileName = "ReactionGraphCatalog", menuName = "Atomix/Reaction Graph Catalog")]
public class ReactionGraphCatalog : ScriptableObject
{
    public const string ResourcePath = "ReactionGraphCatalog";

    [SerializeField] private List<ReactionGraphData> entries = new List<ReactionGraphData>();

    private static ReactionGraphCatalog runtimeCatalog;

    public IReadOnlyList<ReactionGraphData> Entries
    {
        get { return entries; }
    }

    /// <summary>
    /// The catalog every caller should use: the asset if it loads, otherwise the built-in copy.
    /// Cached, so the Resources lookup happens once.
    /// </summary>
    public static ReactionGraphCatalog Shared
    {
        get
        {
            if (runtimeCatalog != null)
            {
                return runtimeCatalog;
            }

            ReactionGraphCatalog loaded = Resources.Load<ReactionGraphCatalog>(ResourcePath);
            if (loaded != null && loaded.CountValidEntries() > 0)
            {
                loaded.Normalise();
                runtimeCatalog = loaded;
                return runtimeCatalog;
            }

            if (loaded != null)
            {
                Debug.LogWarning(
                    "ReactionGraphCatalog: the asset loaded but has no usable entries. " +
                    "Falling back to the built-in chemistry data.");
            }

            runtimeCatalog = BuildBuiltIn();
            return runtimeCatalog;
        }
    }

    public bool TryGet(int reactionId, out ReactionGraphData data)
    {
        for (int i = 0; i < entries.Count; i++)
        {
            ReactionGraphData candidate = entries[i];
            if (candidate != null && candidate.reactionId == reactionId)
            {
                data = candidate;
                return true;
            }
        }

        data = null;
        return false;
    }

    /// <summary>Convenience lookup on the shared catalog. Returns null when the id is unknown.</summary>
    public static ReactionGraphData Get(int reactionId)
    {
        ReactionGraphCatalog catalog = Shared;
        ReactionGraphData data;
        return catalog != null && catalog.TryGet(reactionId, out data) ? data : null;
    }

    private int CountValidEntries()
    {
        int count = 0;
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i] != null)
            {
                count++;
            }
        }
        return count;
    }

    private void Normalise()
    {
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i] != null)
            {
                entries[i].NormaliseInPlace();
            }
        }
    }

    // =========================================================
    // BUILT-IN CHEMISTRY
    // =========================================================
    //
    // dH and dS below are derived by Hess's law from standard formation enthalpies and absolute
    // entropies at 298.15 K (CRC / NIST), for the equation exactly as written. Ea values are
    // taken from kinetics literature for the corresponding real process, and every one of them
    // sits above the products, as a transition state must.
    //
    //  id  reaction                          dH kJ/mol   Ea kJ/mol   dS J/(mol K)   dG(25 C)
    //   1  2Na + 2H2O -> 2NaOH + H2             -368.6        10.0          -15.5     -364.0
    //   2  H2SO4 + CuO -> CuSO4 + H2O            -63.7        48.0          -43.0      -50.9
    //   3  HCl + NaHCO3 -> NaCl + H2O + CO2      +31.4        45.0         +241.0      -40.5
    //   4  2K + 2H2O -> 2KOH + H2               -393.2         6.0          +44.7     -406.5
    //   5  2Al + 3I2 -> 2AlI3                   -627.6        85.0          -86.9     -601.7
    //   6  CaO + H2O -> Ca(OH)2                  -65.2        46.0          -26.3      -57.4
    //   7  CaCO3 -> CaO + CO2                   +178.3       185.0         +160.7     +130.4
    //   8  2FeSO4 -> Fe2O3 + SO2 + SO3          +340.1       365.0         +377.4     +227.6
    //
    // Two of these are worth checking against reality: dH/dS puts the CaCO3 crossover at 837 C
    // (limestone is calcined at roughly 840-900 C) and the FeSO4 crossover at 628 C (it decomposes
    // near 680 C). The curve shapes are left to be synthesised from Ea and dH.

    /// <summary>Recreates the shipped catalog from code. Used when the asset cannot be loaded.</summary>
    public static ReactionGraphCatalog BuildBuiltIn()
    {
        ReactionGraphCatalog catalog = CreateInstance<ReactionGraphCatalog>();
        catalog.name = "ReactionGraphCatalog (built-in)";
        catalog.entries = new List<ReactionGraphData>();

        catalog.entries.Add(ReactionGraphData.Create(
            1, "2Na + 2H2O -> 2NaOH + H2", "Sodium + Water",
            10.0f, -368.6f, -15.5f,
            "This is one of the most strongly exothermic reactions in the lab: 368.6 kilojoules " +
            "come out for every two moles of sodium, which is why the curve falls off a cliff on " +
            "the product side. The activation energy is only about 10 kilojoules per mole, " +
            "essentially no barrier at all, so the reaction begins the instant the metal touches " +
            "the water - nothing has to be heated first, and that is exactly why sodium is stored " +
            "under oil. The entropy is the interesting part: it drops slightly, by 15.5 joules per " +
            "mole per kelvin, even though hydrogen gas is being given off. The sodium and hydroxide " +
            "ions that form drag the surrounding water molecules into tight, ordered hydration " +
            "shells, and that ordering slightly outweighs the disorder gained from the escaping " +
            "hydrogen. The reaction still runs away with itself because the enormous energy release " +
            "completely dominates that small entropy penalty.",
            null));

        catalog.entries.Add(ReactionGraphData.Create(
            2, "H2SO4 + CuO -> CuSO4 + H2O", "Sulfuric Acid + Copper Oxide",
            48.0f, -63.7f, -43.0f,
            "This is a moderate exothermic reaction - 63.7 kilojoules per mole released, enough to " +
            "warm the beaker noticeably but nothing dramatic. Notice that the barrier at 48 " +
            "kilojoules per mole is comparable to the energy released, which is why it is not " +
            "instantaneous the way sodium and water is: the acid has to attack the solid copper " +
            "oxide surface one site at a time, and that surface step is what sets the rate. It is " +
            "also why stirring and warming speed it up so much. Entropy falls by 43 joules per mole " +
            "per kelvin because a solid is being consumed to make dissolved copper and sulfate ions, " +
            "and those ions hold water molecules around them in an ordered shell. The reaction is " +
            "still spontaneous at room temperature because the energy released more than pays for " +
            "that loss of disorder.",
            null));

        catalog.entries.Add(ReactionGraphData.Create(
            3, "HCl + NaHCO3 -> NaCl + H2O + CO2", "Hydrochloric Acid + Sodium Bicarbonate",
            45.0f, 31.4f, 241.0f,
            "This is the one endothermic reaction that happens at room temperature, and it is the " +
            "best example in the whole lab of why entropy matters. It absorbs 31.4 kilojoules per " +
            "mole - the beaker actually gets colder as it fizzes - so on energy alone it should not " +
            "happen at all. What drives it is the entropy change of plus 241 joules per mole per " +
            "kelvin, which is huge: a solid and a dissolved acid turn into dissolved ions, liquid " +
            "water and a whole mole of carbon dioxide gas that escapes into the room. Put those into " +
            "the Gibbs equation and delta G comes out at about minus 40 kilojoules per mole at room " +
            "temperature, comfortably negative. So the reaction is spontaneous not because it " +
            "releases energy, but because it creates so much disorder that it is worth paying the " +
            "energy cost.",
            null));

        catalog.entries.Add(ReactionGraphData.Create(
            4, "2K + 2H2O -> 2KOH + H2", "Potassium + Water",
            6.0f, -393.2f, 44.7f,
            "Compare this with the sodium experiment and you can see reactivity down the group in " +
            "the numbers. Potassium releases 393.2 kilojoules per two moles against sodium's 368.6, " +
            "and its activation barrier is lower still - about 6 kilojoules per mole against 10 - " +
            "because potassium gives up its outer electron more easily. A lower barrier and more " +
            "energy released is exactly why potassium ignites the hydrogen while sodium usually does " +
            "not. There is a second difference worth noticing: entropy rises here, by 44.7 joules " +
            "per mole per kelvin, where it fell for sodium. The potassium ion is larger and holds " +
            "the surrounding water far less tightly than the sodium ion does, so it orders the " +
            "solvent much less, and the hydrogen gas released wins out. This reaction is spontaneous " +
            "at any temperature: it both releases energy and increases disorder.",
            null));

        catalog.entries.Add(ReactionGraphData.Create(
            5, "2Al + 3I2 -> 2AlI3", "Aluminium + Iodine",
            85.0f, -627.6f, -86.9f,
            "This is by far the most exothermic reaction in the lab - 627.6 kilojoules per mole of " +
            "reaction - which is why it produces that violent cloud of purple iodine vapour. But " +
            "look at the barrier: 85 kilojoules per mole, the highest of any of the room-temperature " +
            "experiments. Two dry solids simply cannot reach each other well enough to react, which " +
            "is why the mixture sits there doing nothing until you add water. The water acts as a " +
            "catalyst: it dissolves a little iodine and lets it reach the aluminium surface, " +
            "lowering the effective barrier so the reaction can start. Once started, the energy it " +
            "releases keeps it going. Entropy falls by 86.9 joules per mole per kelvin, because five " +
            "moles of solid reactants become two moles of solid product - fewer particles, less " +
            "disorder. Only the sheer size of the energy release makes it spontaneous anyway.",
            null));

        catalog.entries.Add(ReactionGraphData.Create(
            6, "CaO + H2O -> Ca(OH)2", "Calcium Oxide + Water",
            46.0f, -65.2f, -26.3f,
            "This is lime slaking, and it releases 65.2 kilojoules per mole - enough to boil some of " +
            "the water and send steam off the dish, which is exactly what you saw. The barrier is 46 " +
            "kilojoules per mole, so it needs the water to actually wet the solid surface before it " +
            "gets going, and then the heat it releases accelerates the rest. Entropy falls by 26.3 " +
            "joules per mole per kelvin because a solid and a liquid combine into a single ordered " +
            "solid - two things becoming one, with the water molecules locked into the hydroxide " +
            "lattice. The reaction is spontaneous at room temperature on the strength of the energy " +
            "released, but that negative entropy is why the reverse process works: heat calcium " +
            "hydroxide strongly enough and it gives the water back.",
            null));

        catalog.entries.Add(ReactionGraphData.Create(
            7, "CaCO3 -> CaO + CO2", "Calcium Carbonate Decomposition",
            185.0f, 178.3f, 160.7f,
            "This is a thermal decomposition, so it is endothermic - it absorbs 178.3 kilojoules per " +
            "mole, which is why it only happens while you hold it over the flame. Notice how close " +
            "the activation energy is to the enthalpy change: 185 against 178.3. That means there is " +
            "almost no extra barrier beyond simply paying the uphill energy cost, so the reaction is " +
            "controlled by temperature rather than by kinetics. Entropy rises sharply, by 160.7 " +
            "joules per mole per kelvin, because an ordered solid releases a mole of carbon dioxide " +
            "gas. Divide the enthalpy by the entropy and you get the temperature where delta G " +
            "changes sign - about 837 degrees Celsius. That is a real prediction you can check: " +
            "industrial lime kilns calcine limestone at roughly 850 to 900 degrees, just above the " +
            "figure these two numbers give.",
            null));

        catalog.entries.Add(ReactionGraphData.Create(
            8, "2FeSO4 -> Fe2O3 + SO2 + SO3", "Iron(II) Sulfate Decomposition",
            365.0f, 340.1f, 377.4f,
            "This is the most endothermic reaction in the lab: 340.1 kilojoules per mole of reaction " +
            "have to be put in, which is why the green crystals need sustained strong heating before " +
            "the reddish-brown iron oxide appears. The barrier of 365 kilojoules per mole sits only " +
            "just above that, so like the limestone this is a temperature-driven decomposition " +
            "rather than a kinetically hindered one. What makes it possible at all is the entropy: " +
            "plus 377.4 joules per mole per kelvin, the largest change of any reaction here, because " +
            "two moles of ordered solid break apart into a solid plus two different gases, sulfur " +
            "dioxide and sulfur trioxide. Those two escaping gases are the whole reason it works. " +
            "The crossover temperature comes out around 628 degrees Celsius, which matches how hot " +
            "you actually have to get iron sulfate before it will decompose.",
            null));

        return catalog;
    }
}
