using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The molecular animation for each of the eight reactions, authored in code.
///
/// Why code and not a ScriptableObject asset: every runtime system Tasks 1-7 added is built from
/// code precisely so a Unity reimport cannot lose it and so no scene or prefab has to be touched.
/// A .asset here would also mean hand-writing several hundred lines of YAML with atom indices in
/// it, which is far harder to review than the fluent form below.
///
/// Reaction 1 (Na + H2O) has no molecular video: its entry in ReactionLearningVideoCatalog.asset
/// is `videoClip: {fileID: 0}`, so the LEARN screen told the student "Molecular explanation video
/// is unavailable." These animations close that gap, and the other seven get one too, so the
/// molecular view is uniform across the bench.
/// </summary>
public static class MolecularSceneCatalog
{
    private static Dictionary<int, MolecularScene> cache;

    private static readonly Color HeatTint = new Color(0.20f, 0.07f, 0.04f, 1.0f);

    public static MolecularScene Get(int reactionId)
    {
        if (cache == null)
        {
            Build();
        }

        MolecularScene scene;
        return cache.TryGetValue(reactionId, out scene) ? scene : null;
    }

    public static bool Has(int reactionId)
    {
        return Get(reactionId) != null;
    }

    public static IEnumerable<int> ReactionIds
    {
        get
        {
            if (cache == null)
            {
                Build();
            }
            return cache.Keys;
        }
    }

    private static void Build()
    {
        cache = new Dictionary<int, MolecularScene>();

        Add(AlkaliMetalAndWater(
            1, "Na",
            "2Na + 2H2O -> 2NaOH + H2",
            "Sodium + Water",
            "Sodium is soft enough to cut with a knife and reacts the instant it touches water."));

        Add(SulfuricAcidAndCopperOxide());
        Add(AcidAndBicarbonate());

        Add(AlkaliMetalAndWater(
            4, "K",
            "2K + 2H2O -> 2KOH + H2",
            "Potassium + Water",
            "Potassium holds its outer electron even more loosely than sodium, so this is faster " +
            "and the hydrogen usually ignites."));

        Add(AluminiumAndIodine());
        Add(QuicklimeAndWater());
        Add(LimestoneDecomposition());
        Add(IronSulfateDecomposition());
    }

    private static void Add(MolecularScene scene)
    {
        cache[scene.reactionId] = scene;
    }

    // =================================================================================
    // R1 / R4 - alkali metal + water. Same mechanism, so one builder authors both.
    // =================================================================================

    private static MolecularScene AlkaliMetalAndWater(
        int id, string metal, string equation, string title, string opening)
    {
        MolecularScene s = new MolecularScene();
        s.reactionId = id;
        s.equation = equation;
        s.title = title;

        int m1 = s.Atom(metal);
        int m2 = s.Atom(metal);
        int o1 = s.Atom("O");
        int h1a = s.Atom("H");
        int h1b = s.Atom("H");
        int o2 = s.Atom("O");
        int h2a = s.Atom("H");
        int h2b = s.Atom("H");

        s.Stage("Reactants", opening, 3.0f);
        s.At(m1, -4.2f, 1.2f, 0f).At(m2, -4.2f, -1.2f, 0f)
         .At(o1, 2.2f, 1.4f, 0f).At(h1a, 1.5f, 2.1f, 0.2f).At(h1b, 3.0f, 1.95f, -0.2f)
         .At(o2, 2.2f, -1.4f, 0f).At(h2a, 1.5f, -2.1f, 0.2f).At(h2b, 3.0f, -1.95f, -0.2f)
         .Bond(o1, h1a).Bond(o1, h1b).Bond(o2, h2a).Bond(o2, h2b)
         .Note(m1, "one loose outer electron")
         .Note(o1, "polar O-H bonds");

        s.Stage("The metal meets the water",
            "Water is polar: the oxygen end carries a partial negative charge, and that is the end " +
            "that turns to face the metal.", 3.0f);
        s.At(m1, -1.0f, 1.3f, 0f).At(m2, -1.0f, -1.3f, 0f)
         .At(o1, 1.4f, 1.4f, 0f).At(h1a, 0.75f, 2.1f, 0.2f).At(h1b, 2.2f, 1.95f, -0.2f)
         .At(o2, 1.4f, -1.4f, 0f).At(h2a, 0.75f, -2.1f, 0.2f).At(h2b, 2.2f, -1.95f, -0.2f);

        s.Stage("Electron transfer",
            "Each metal atom hands its single outer electron to a water molecule. This is the " +
            "oxidation half of the reaction: " + metal + " -> " + metal + "+ + e-.", 3.5f);
        s.At(m1, 0.1f, 1.35f, 0f).At(m2, 0.1f, -1.35f, 0f)
         .Electron(m1, o1, 1).Electron(m2, o2, 1)
         .Note(m1, "loses 1 electron -> " + metal + "+");

        s.Stage("An O-H bond breaks",
            "The extra electron makes one O-H bond untenable. The hydrogen leaves without its " +
            "electron pair, and what is left behind is a hydroxide ion, OH-.", 3.0f);
        s.Break(o1, h1b).Break(o2, h2b)
         .At(h1b, 3.6f, 0.5f, 0f).At(h2b, 3.6f, -0.5f, 0f)
         .Note(o1, "now OH-");

        s.Stage("New bonds form",
            "The two freed hydrogen atoms pair up into H2 gas, and each metal ion is held by a " +
            "hydroxide ion. The dashed links are ionic - electrons transferred, not shared.", 3.5f);
        s.Bond(m1, o1, 1, true).Bond(m2, o2, 1, true).Bond(h1b, h2b, 1, false)
         .At(m1, 0.45f, 1.4f, 0f).At(m2, 0.45f, -1.4f, 0f)
         .At(h1b, 4.0f, 0.35f, 0f).At(h2b, 4.0f, -0.35f, 0f)
         .Note(h1b, "H2 gas");

        s.Stage("Products",
            equation.Replace("->", "→") + "  -  two hydroxide units in solution, and hydrogen " +
            "bubbling off. The reaction is strongly exothermic, which is why the metal skates and " +
            "the hydrogen can catch fire.", 3.5f);
        s.At(m1, -2.6f, 1.6f, 0f).At(o1, -1.5f, 1.6f, 0f).At(h1a, -0.8f, 2.3f, 0f)
         .At(m2, -2.6f, -1.6f, 0f).At(o2, -1.5f, -1.6f, 0f).At(h2a, -0.8f, -2.3f, 0f)
         .At(h1b, 3.2f, 2.2f, 0f).At(h2b, 3.9f, 2.5f, 0f)
         .Note(m1, metal + "OH")
         .Note(h1b, "escapes as H2");

        return s;
    }

    // =================================================================================
    // R2 - H2SO4 + CuO -> CuSO4 + H2O
    // =================================================================================

    private static MolecularScene SulfuricAcidAndCopperOxide()
    {
        MolecularScene s = new MolecularScene();
        s.reactionId = 2;
        s.equation = "H2SO4 + CuO -> CuSO4 + H2O";
        s.title = "Sulfuric Acid + Copper(II) Oxide";

        int su = s.Atom("S");
        int oa = s.Atom("O");
        int ob = s.Atom("O");
        int oc = s.Atom("O");
        int od = s.Atom("O");
        int h1 = s.Atom("H");
        int h2 = s.Atom("H");
        int cu = s.Atom("Cu");
        int ox = s.Atom("O");

        s.Stage("Reactants",
            "Sulfuric acid on the left, black copper(II) oxide on the right. Both acidic hydrogens " +
            "sit on oxygens, not on the sulfur.", 3.0f);
        s.At(su, -2.6f, 0f, 0f)
         .At(oa, -3.5f, 1.0f, 0f).At(ob, -3.5f, -1.0f, 0f)
         .At(oc, -1.7f, 1.0f, 0f).At(od, -1.7f, -1.0f, 0f)
         .At(h1, -4.3f, 1.65f, 0f).At(h2, -4.3f, -1.65f, 0f)
         .At(cu, 2.6f, 0.5f, 0f).At(ox, 3.8f, 0.0f, 0f)
         .Bond(su, oa).Bond(su, ob).Bond(su, oc, 2, false).Bond(su, od, 2, false)
         .Bond(oa, h1).Bond(ob, h2).Bond(cu, ox, 1, true)
         .Note(h1, "acidic H")
         .Note(cu, "black CuO");

        s.Stage("The acid reaches the oxide",
            "Warming the beaker is what gets them close enough: this needs about 48 kJ/mol of " +
            "activation energy.", 2.5f);
        s.At(cu, 1.2f, 0.5f, 0f).At(ox, 2.3f, 0.0f, 0f);

        s.Stage("The acid gives up its protons",
            "Both O-H bonds break and two H+ ions leave. Note what does NOT happen: no electrons " +
            "change hands. Copper stays 2+ and sulfur stays 6+ throughout - this is a proton " +
            "transfer, not a redox reaction.", 3.5f);
        s.Break(oa, h1).Break(ob, h2)
         .At(h1, 0.6f, 1.4f, 0f).At(h2, 0.6f, -1.4f, 0f)
         .Note(oa, "sulfate is now 2-")
         .Note(h1, "H+ - no electron with it");

        s.Stage("Water forms and the copper is freed",
            "The oxide ion takes both protons and becomes a water molecule, leaving the copper ion " +
            "with nothing holding it.", 3.5f);
        s.Break(cu, ox).Bond(ox, h1).Bond(ox, h2)
         .At(ox, 1.9f, 0.0f, 0f).At(h1, 1.3f, 0.9f, 0f).At(h2, 1.3f, -0.9f, 0f)
         .At(cu, -0.4f, 0.0f, 0f)
         .Note(ox, "H2O");

        s.Stage("Copper joins the sulfate",
            "The Cu2+ ion is drawn to the 2- sulfate. The link is ionic, so it is drawn dashed.", 3.0f);
        s.Bond(cu, oa, 1, true).Bond(cu, ob, 1, true)
         .At(cu, -4.5f, 0.0f, 0f)
         .At(ox, 2.7f, 0.7f, 0f).At(h1, 2.0f, 1.5f, 0f).At(h2, 3.4f, 1.4f, 0f);

        s.Stage("Products",
            "H2SO4 + CuO → CuSO4 + H2O. In the beaker you see the black solid disappear and " +
            "the solution turn blue - that blue is the hydrated Cu2+ ion.", 3.5f);
        s.At(ox, 3.0f, 1.6f, 0f).At(h1, 2.3f, 2.4f, 0f).At(h2, 3.7f, 2.3f, 0f)
         .Note(cu, "solution turns blue")
         .Note(ox, "water");

        return s;
    }

    // =================================================================================
    // R3 - HCl + NaHCO3 -> NaCl + H2O + CO2
    // =================================================================================

    private static MolecularScene AcidAndBicarbonate()
    {
        MolecularScene s = new MolecularScene();
        s.reactionId = 3;
        s.equation = "HCl + NaHCO3 -> NaCl + H2O + CO2";
        s.title = "Hydrochloric Acid + Sodium Bicarbonate";

        int ha = s.Atom("H");
        int cl = s.Atom("Cl");
        int na = s.Atom("Na");
        int c = s.Atom("C");
        int o1 = s.Atom("O");
        int o2 = s.Atom("O");
        int o3 = s.Atom("O");
        int hb = s.Atom("H");

        s.Stage("Reactants",
            "Hydrochloric acid above, sodium bicarbonate below. The bicarbonate already carries one " +
            "hydrogen of its own.", 3.0f);
        s.At(ha, -4.0f, 1.7f, 0f).At(cl, -2.9f, 1.7f, 0f)
         .At(c, 1.0f, -0.2f, 0f).At(o1, 0.0f, 0.7f, 0f).At(o2, 2.1f, 0.4f, 0f)
         .At(o3, 1.1f, -1.6f, 0f).At(na, -1.1f, 1.2f, 0f).At(hb, 2.0f, -2.2f, 0f)
         .Bond(ha, cl).Bond(c, o1).Bond(c, o2, 2, false).Bond(c, o3).Bond(o3, hb)
         .Bond(na, o1, 1, true)
         .Note(ha, "acidic H")
         .Note(hb, "bicarbonate H");

        s.Stage("Both dissolve",
            "In water, HCl splits completely into H+ and Cl-, and the sodium ion drifts away from " +
            "the bicarbonate. Nothing has reacted yet - they are just free to move.", 3.0f);
        s.Break(ha, cl).Break(na, o1)
         .At(ha, -1.0f, 0.2f, 0f).At(cl, -3.6f, 2.0f, 0f).At(na, -2.4f, 2.0f, 0f)
         .Note(cl, "Cl- spectator ion")
         .Note(na, "Na+ spectator ion");

        s.Stage("The proton lands on the bicarbonate",
            "H+ attaches to the bicarbonate, making carbonic acid, H2CO3. This molecule is famously " +
            "unstable - it exists for a fraction of a second.", 3.0f);
        s.Bond(o3, ha)
         .At(ha, 0.5f, -2.0f, 0f).At(o3, 1.3f, -1.7f, 0f)
         .Note(o3, "H2CO3 - very short-lived");

        s.Stage("Carbonic acid falls apart",
            "The C-O bond to the newly protonated oxygen breaks. That oxygen leaves as a complete " +
            "water molecule, and the remaining carbon keeps two oxygens.", 3.5f);
        s.Break(c, o3)
         .At(o3, 0.9f, -2.4f, 0f).At(ha, 0.1f, -2.0f, 0f).At(hb, 1.7f, -2.9f, 0f)
         .At(c, 1.4f, 0.3f, 0f)
         .Note(o3, "H2O");

        s.Stage("Carbon dioxide straightens out",
            "With only two oxygens left the carbon goes linear: both bonds become double bonds and " +
            "the O=C=O angle opens to 180 degrees. Sodium and chloride pair up as NaCl.", 3.5f);
        s.Bond(c, o1, 2, false).Bond(na, cl, 1, true)
         .At(c, 2.2f, 1.6f, 0f).At(o1, 1.0f, 1.6f, 0f).At(o2, 3.4f, 1.6f, 0f)
         .At(na, -3.3f, 0.6f, 0f).At(cl, -2.1f, 0.6f, 0f)
         .Note(c, "O=C=O, linear");

        s.Stage("Products",
            "HCl + NaHCO3 → NaCl + H2O + CO2. The fizzing you see in the beaker is this CO2 " +
            "leaving the liquid. The reaction is endothermic - the mixture actually gets colder.", 3.5f);
        s.At(c, 2.6f, 2.7f, 0f).At(o1, 1.4f, 2.7f, 0f).At(o2, 3.8f, 2.7f, 0f)
         .Note(c, "bubbles away as CO2")
         .Note(na, "NaCl stays in solution");

        return s;
    }

    // =================================================================================
    // R5 - 2Al + 3I2 -> 2AlI3
    // =================================================================================

    private static MolecularScene AluminiumAndIodine()
    {
        MolecularScene s = new MolecularScene();
        s.reactionId = 5;
        s.equation = "2Al + 3I2 -> 2AlI3";
        s.title = "Aluminium + Iodine";

        int al1 = s.Atom("Al");
        int al2 = s.Atom("Al");
        int i1 = s.Atom("I");
        int i2 = s.Atom("I");
        int i3 = s.Atom("I");
        int i4 = s.Atom("I");
        int i5 = s.Atom("I");
        int i6 = s.Atom("I");

        s.Stage("Reactants",
            "Aluminium powder and three iodine molecules. Iodine is a diatomic element: it never " +
            "sits around as single atoms.", 3.0f);
        s.At(al1, -4.0f, 1.1f, 0f).At(al2, -4.0f, -1.1f, 0f)
         .At(i1, 2.1f, 2.0f, 0f).At(i2, 3.4f, 2.0f, 0f)
         .At(i3, 2.1f, 0.0f, 0f).At(i4, 3.4f, 0.0f, 0f)
         .At(i5, 2.1f, -2.0f, 0f).At(i6, 3.4f, -2.0f, 0f)
         .Bond(i1, i2).Bond(i3, i4).Bond(i5, i6)
         .Note(al1, "3 outer electrons")
         .Note(i1, "I2, a shared pair");

        s.Stage("A drop of water starts it",
            "Dry aluminium and iodine can sit together indefinitely. A single drop of water acts as " +
            "a catalyst and the 85 kJ/mol barrier is suddenly reachable.", 3.0f);
        s.At(al1, -1.9f, 1.1f, 0f).At(al2, -1.9f, -1.1f, 0f)
         .Heat(0.05f, new Color(0.13f, 0.06f, 0.14f, 1.0f));

        s.Stage("The I-I bonds snap",
            "Each iodine molecule splits into two atoms, and each of those wants one more electron " +
            "to complete its outer shell.", 3.0f);
        s.Break(i1, i2).Break(i3, i4).Break(i5, i6)
         .At(i1, 1.6f, 2.4f, 0f).At(i2, 3.9f, 2.4f, 0f)
         .At(i3, 1.6f, 0.0f, 0f).At(i4, 3.9f, 0.0f, 0f)
         .At(i5, 1.6f, -2.4f, 0f).At(i6, 3.9f, -2.4f, 0f);

        s.Stage("Each aluminium hands over three electrons",
            "Al -> Al3+ + 3e-, and every iodine atom takes exactly one: I + e- -> I-. This is a " +
            "genuine redox reaction; aluminium is oxidised and iodine is reduced.", 4.0f);
        s.Electron(al1, i1, 1).Electron(al1, i2, 1).Electron(al1, i3, 1)
         .Electron(al2, i4, 1).Electron(al2, i5, 1).Electron(al2, i6, 1)
         .Note(al1, "Al -> Al3+ + 3e-")
         .Note(i1, "I + e- -> I-");

        s.Stage("Aluminium-iodine bonds form",
            "Three iodides arrange themselves around each aluminium ion in a flat triangle - " +
            "trigonal planar, 120 degrees apart.", 3.5f);
        s.Bond(al1, i1, 1, true).Bond(al1, i2, 1, true).Bond(al1, i3, 1, true)
         .Bond(al2, i4, 1, true).Bond(al2, i5, 1, true).Bond(al2, i6, 1, true)
         .At(al1, -1.9f, 1.4f, 0f)
         .At(i1, -1.9f, 2.9f, 0f).At(i2, -3.2f, 0.65f, 0f).At(i3, -0.6f, 0.65f, 0f)
         .At(al2, 2.2f, -1.4f, 0f)
         .At(i4, 2.2f, -2.9f, 0f).At(i5, 0.9f, -0.65f, 0f).At(i6, 3.5f, -0.65f, 0f);

        s.Stage("Products",
            "2Al + 3I2 → 2AlI3, releasing 628 kJ/mol. In the fume hood you see a violent " +
            "purple cloud - that is unreacted iodine subliming from the heat this releases.", 3.5f);
        s.At(al1, -2.6f, 1.3f, 0f)
         .At(i1, -2.6f, 2.8f, 0f).At(i2, -3.9f, 0.55f, 0f).At(i3, -1.3f, 0.55f, 0f)
         .At(al2, 2.7f, -1.3f, 0f)
         .At(i4, 2.7f, -2.8f, 0f).At(i5, 1.4f, -0.55f, 0f).At(i6, 4.0f, -0.55f, 0f)
         .Note(al1, "AlI3")
         .Heat(0.06f, new Color(0.16f, 0.05f, 0.18f, 1.0f));

        return s;
    }

    // =================================================================================
    // R6 - CaO + H2O -> Ca(OH)2
    // =================================================================================

    private static MolecularScene QuicklimeAndWater()
    {
        MolecularScene s = new MolecularScene();
        s.reactionId = 6;
        s.equation = "CaO + H2O -> Ca(OH)2";
        s.title = "Quicklime + Water";

        int ca = s.Atom("Ca");
        int o1 = s.Atom("O");
        int o2 = s.Atom("O");
        int h1 = s.Atom("H");
        int h2 = s.Atom("H");

        s.Stage("Reactants",
            "Quicklime, CaO, on the left and a water molecule on the right. The oxide ion in " +
            "quicklime carries a full 2- charge, which makes it a very strong base.", 3.0f);
        s.At(ca, -3.2f, 0.0f, 0f).At(o1, -1.7f, 0.0f, 0f)
         .At(o2, 2.4f, 0.0f, 0f).At(h1, 1.6f, 0.95f, 0f).At(h2, 3.2f, 0.95f, 0f)
         .Bond(ca, o1, 1, true).Bond(o2, h1).Bond(o2, h2)
         .Note(o1, "O2- - a very strong base")
         .Note(o2, "water");

        s.Stage("Water reaches the quicklime",
            "This is what the word 'slaking' describes: adding water to quicklime.", 2.5f);
        s.At(o2, 0.9f, 0.0f, 0f).At(h1, 0.1f, 0.95f, 0f).At(h2, 1.7f, 0.95f, 0f);

        s.Stage("One O-H bond breaks",
            "The oxide ion is a strong enough base to pull a proton straight off the water.", 3.0f);
        s.Break(o2, h2)
         .At(h2, -0.4f, -0.85f, 0f)
         .Note(o2, "left as OH-");

        s.Stage("The proton lands on the oxide",
            "The oxide gains that hydrogen and becomes a hydroxide as well. One water and one oxide " +
            "have now become two hydroxides.", 3.0f);
        s.Bond(o1, h2)
         .At(h2, -1.7f, -1.2f, 0f)
         .Note(o1, "now OH- too");

        s.Stage("Calcium takes both hydroxides",
            "The Ca2+ ion holds two OH- ions - hence the formula Ca(OH)2, calcium hydroxide.", 3.0f);
        s.Bond(ca, o2, 1, true)
         .At(ca, -0.4f, 0.0f, 0f)
         .At(o1, -1.9f, 0.65f, 0f).At(h2, -2.7f, 1.3f, 0f)
         .At(o2, 1.1f, -0.65f, 0f).At(h1, 1.9f, -1.3f, 0f)
         .Heat(0.04f, HeatTint);

        s.Stage("Products",
            "CaO + H2O → Ca(OH)2, releasing 65 kJ/mol. This is why the beaker becomes genuinely " +
            "hot to hold - slaking lime on a building site can boil the water you add.", 3.5f);
        s.Note(ca, "slaked lime")
         .Heat(0.06f, HeatTint);

        return s;
    }

    // =================================================================================
    // R7 - CaCO3 -> CaO + CO2
    // =================================================================================

    private static MolecularScene LimestoneDecomposition()
    {
        MolecularScene s = new MolecularScene();
        s.reactionId = 7;
        s.equation = "CaCO3 -> CaO + CO2";
        s.title = "Thermal Decomposition of Limestone";

        int ca = s.Atom("Ca");
        int o1 = s.Atom("O");
        int c = s.Atom("C");
        int o2 = s.Atom("O");
        int o3 = s.Atom("O");

        s.Stage("One formula unit of limestone",
            "Calcium carbonate. The carbonate ion is a flat triangle of oxygens around a carbon, " +
            "and the calcium ion sits beside it.", 3.0f);
        s.At(ca, -2.6f, 0.0f, 0f).At(o1, -1.0f, 0.0f, 0f).At(c, 0.4f, 0.0f, 0f)
         .At(o2, 1.2f, 1.3f, 0f).At(o3, 1.2f, -1.3f, 0f)
         .Bond(ca, o1, 1, true).Bond(o1, c).Bond(c, o2, 2, false).Bond(c, o3)
         .Note(ca, "Ca2+")
         .Note(c, "carbonate, CO3 2-");

        s.Stage("Heat it hard",
            "Nothing happens until roughly 840 degrees C. This reaction needs 185 kJ/mol of " +
            "activation energy - one of the highest barriers on this bench.", 3.5f);
        s.Heat(0.12f, HeatTint);

        s.Stage("The C-O bridge breaks",
            "The bond between the carbon and the oxygen nearest the calcium is the one that gives " +
            "way. That oxygen stays with the calcium.", 3.5f);
        s.Break(c, o1)
         .At(c, 1.3f, 0.5f, 0f).At(o2, 2.1f, 1.6f, 0f).At(o3, 2.1f, -0.7f, 0f)
         .Heat(0.12f, HeatTint);

        s.Stage("Carbon dioxide straightens and leaves",
            "With one oxygen gone the carbon keeps two, both as double bonds, and the molecule goes " +
            "linear. As a gas it escapes immediately.", 3.5f);
        s.Bond(c, o3, 2, false)
         .At(c, 2.4f, 1.7f, 0f).At(o2, 1.2f, 1.7f, 0f).At(o3, 3.6f, 1.7f, 0f)
         .Heat(0.08f, HeatTint)
         .Note(c, "O=C=O escapes");

        s.Stage("Quicklime is left behind",
            "What remains in the crucible is calcium oxide - quicklime. Feed it water and you get " +
            "reaction 6 on this bench.", 3.5f);
        s.At(ca, -2.4f, -0.8f, 0f).At(o1, -1.0f, -0.8f, 0f)
         .At(c, 2.7f, 2.8f, 0f).At(o2, 1.5f, 2.8f, 0f).At(o3, 3.9f, 2.8f, 0f)
         .Note(ca, "CaO, quicklime");

        s.Stage("Products",
            "CaCO3 → CaO + CO2. This one is endothermic: it absorbs 178 kJ/mol, so it only " +
            "keeps going while you keep heating it. Stop the flame and the reaction stops.", 3.5f);
        s.At(c, 2.9f, 3.1f, 0f).At(o2, 1.7f, 3.1f, 0f).At(o3, 4.1f, 3.1f, 0f);

        return s;
    }

    // =================================================================================
    // R8 - 2FeSO4 -> Fe2O3 + SO2 + SO3
    // =================================================================================

    private static MolecularScene IronSulfateDecomposition()
    {
        MolecularScene s = new MolecularScene();
        s.reactionId = 8;
        s.equation = "2FeSO4 -> Fe2O3 + SO2 + SO3";
        s.title = "Thermal Decomposition of Iron(II) Sulfate";

        int fe1 = s.Atom("Fe");
        int fe2 = s.Atom("Fe");
        int s1 = s.Atom("S");
        int s2 = s.Atom("S");
        int oa = s.Atom("O");
        int ob = s.Atom("O");
        int oc = s.Atom("O");
        int od = s.Atom("O");
        int oe = s.Atom("O");
        int of = s.Atom("O");
        int og = s.Atom("O");
        int oh = s.Atom("O");

        s.Stage("Two formula units of iron(II) sulfate",
            "Green vitriol. Each iron is 2+ and each sulfate is 2-, with the sulfur at its highest " +
            "oxidation state, 6+.", 3.5f);
        s.At(fe1, -4.4f, 1.7f, 0f).At(oa, -3.0f, 1.7f, 0f).At(s1, -1.6f, 1.7f, 0f)
         .At(ob, -1.6f, 3.1f, 0f).At(oc, -1.6f, 0.35f, 0f).At(od, -0.2f, 1.7f, 0f)
         .At(fe2, -4.4f, -1.7f, 0f).At(oe, -3.0f, -1.7f, 0f).At(s2, -1.6f, -1.7f, 0f)
         .At(of, -1.6f, -3.1f, 0f).At(og, -1.6f, -0.35f, 0f).At(oh, -0.2f, -1.7f, 0f)
         .Bond(fe1, oa, 1, true).Bond(s1, oa).Bond(s1, ob, 2, false).Bond(s1, oc, 2, false).Bond(s1, od)
         .Bond(fe2, oe, 1, true).Bond(s2, oe).Bond(s2, of, 2, false).Bond(s2, og, 2, false).Bond(s2, oh)
         .Note(fe1, "Fe2+")
         .Note(s1, "S is 6+");

        s.Stage("Roast it above 680 degrees C",
            "This has the highest barrier of any reaction on the bench - 365 kJ/mol. Ordinary " +
            "heating will not touch it.", 3.5f);
        s.Heat(0.14f, HeatTint);

        s.Stage("The Fe-O links break",
            "The ionic bonds holding each iron to its sulfate give way first, and the two iron ions " +
            "drift together.", 3.5f);
        s.Break(fe1, oa).Break(fe2, oe)
         .At(fe1, -4.7f, 1.0f, 0f).At(fe2, -4.7f, -1.0f, 0f)
         .Heat(0.14f, HeatTint);

        s.Stage("Oxygen migrates, and iron is oxidised",
            "Three oxygens leave the sulfates for the irons. At the same time each Fe2+ gives up an " +
            "electron to become Fe3+, and those two electrons go to one sulfur, taking it from 6+ " +
            "down to 4+. That reduced sulfur is the one that leaves as SO2.", 4.0f);
        s.Break(s1, oa).Break(s2, oe).Break(s1, oc)
         .At(oa, -3.4f, 0.0f, 0f).At(oe, -3.6f, -1.9f, 0f).At(oc, -3.6f, 1.9f, 0f)
         .Electron(fe1, s1, 1).Electron(fe2, s1, 1)
         .Note(fe1, "Fe2+ -> Fe3+ + e-")
         .Note(s1, "S 6+ + 2e- -> S 4+")
         .Heat(0.10f, HeatTint);

        s.Stage("Three products separate",
            "The irons and three oxygens lock into Fe2O3. The reduced sulfur leaves with two " +
            "oxygens as SO2; the untouched sulfur leaves with three as SO3.", 4.0f);
        s.Bond(fe1, oa, 1, true).Bond(fe2, oa, 1, true)
         .Bond(fe1, oc, 1, true).Bond(fe2, oe, 1, true)
         .At(fe1, -4.0f, 0.9f, 0f).At(fe2, -4.0f, -0.9f, 0f)
         .At(oa, -2.8f, 0.0f, 0f).At(oc, -5.3f, 1.8f, 0f).At(oe, -5.3f, -1.8f, 0f)
         .At(s1, 0.8f, 2.0f, 0f).At(ob, -0.3f, 2.7f, 0f).At(od, 1.9f, 2.7f, 0f)
         .At(s2, 0.8f, -2.0f, 0f).At(of, 0.8f, -3.3f, 0f).At(og, -0.3f, -1.3f, 0f)
         .At(oh, 1.9f, -1.3f, 0f)
         .Note(s1, "SO2 - sulfur is 4+")
         .Note(s2, "SO3 - sulfur is still 6+")
         .Heat(0.06f, HeatTint);

        s.Stage("Products",
            "2FeSO4 → Fe2O3 + SO2 + SO3. The green crystals turn to a red-brown residue - that " +
            "is iron(III) oxide, the same compound as rust - and the two sulfur oxides leave as " +
            "choking fumes. Endothermic, 340 kJ/mol.", 3.5f);
        s.At(s1, 1.2f, 2.6f, 0f).At(ob, 0.1f, 3.3f, 0f).At(od, 2.3f, 3.3f, 0f)
         .At(s2, 1.2f, -2.6f, 0f).At(of, 1.2f, -3.9f, 0f).At(og, 0.1f, -1.9f, 0f)
         .At(oh, 2.3f, -1.9f, 0f)
         .Note(fe1, "red-brown Fe2O3");

        return s;
    }
}
