using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One atom in a molecular animation. Positions live on the stages, not here, so the same atom
/// can be followed across the whole reaction - which is the entire point of watching one.
/// </summary>
public class MolAtom
{
    public string symbol;

    public MolAtom(string symbol)
    {
        this.symbol = symbol;
    }
}

/// <summary>A bond between two atom indices. Presence across stages is what animates it.</summary>
public struct MolBond
{
    public int a;
    public int b;

    /// <summary>1 = single, 2 = double, 3 = triple. Drawn as parallel rods.</summary>
    public int order;

    /// <summary>Ionic bonds are drawn dashed, because they are not shared pairs.</summary>
    public bool ionic;

    public MolBond(int a, int b, int order, bool ionic)
    {
        // Normalised so a bond is the same key whichever way round it was authored.
        this.a = Mathf.Min(a, b);
        this.b = Mathf.Max(a, b);
        this.order = order;
        this.ionic = ionic;
    }

    public long Key { get { return ((long)a << 32) | (uint)b; } }
}

/// <summary>An electron travelling from one atom to another during a stage.</summary>
public struct MolElectron
{
    public int from;
    public int to;

    /// <summary>How many electrons make the trip, drawn as separate dots.</summary>
    public int count;

    public MolElectron(int from, int to, int count)
    {
        this.from = from;
        this.to = to;
        this.count = count;
    }
}

/// <summary>
/// One step of the reaction. Atom positions are the state at the *end* of the stage; the renderer
/// interpolates from the previous stage's positions, so a stage is really "how we get to here".
/// </summary>
public class MolStage
{
    public string caption;
    public string detail;
    public float duration = 3.0f;

    /// <summary>One entry per atom, same order as <see cref="MolecularScene.atoms"/>.</summary>
    public Vector3[] positions;

    /// <summary>Bonds present at the end of this stage.</summary>
    public List<MolBond> bonds = new List<MolBond>();

    public List<MolElectron> electrons = new List<MolElectron>();

    /// <summary>
    /// Per-atom captions for this stage only, e.g. "escapes as H2 gas". Stage-scoped rather than
    /// atom-scoped on purpose: "leaves as gas" is true at the end and wrong at the start.
    /// </summary>
    public Dictionary<int, string> notes = new Dictionary<int, string>();

    /// <summary>Shakes every atom for this stage - how "apply heat" reads visually.</summary>
    public float thermalJitter = 0.0f;

    /// <summary>Tints the stage background, e.g. warm while heating.</summary>
    public Color? backgroundTint = null;
}

/// <summary>
/// A complete molecular animation for one reaction: the atoms, and the stages they move through.
/// </summary>
public class MolecularScene
{
    public int reactionId;
    public string equation;
    public string title;

    public readonly List<MolAtom> atoms = new List<MolAtom>();
    public readonly List<MolStage> stages = new List<MolStage>();

    public float TotalDuration
    {
        get
        {
            float total = 0.0f;
            for (int i = 0; i < stages.Count; i++)
            {
                total += stages[i].duration;
            }
            return total;
        }
    }

    // ---------------------------------------------------------------------------------
    // Authoring helpers - these keep the catalog below readable instead of a wall of new().
    // ---------------------------------------------------------------------------------

    public int Atom(string symbol)
    {
        atoms.Add(new MolAtom(symbol));
        return atoms.Count - 1;
    }

    public MolStage Stage(string caption, string detail, float duration)
    {
        MolStage stage = new MolStage();
        stage.caption = caption;
        stage.detail = detail;
        stage.duration = duration;
        stage.positions = new Vector3[atoms.Count];

        // Start from the previous stage so a stage only has to state what actually moved.
        if (stages.Count > 0)
        {
            MolStage previous = stages[stages.Count - 1];
            System.Array.Copy(previous.positions, stage.positions,
                Mathf.Min(previous.positions.Length, stage.positions.Length));
            stage.bonds.AddRange(previous.bonds);
        }

        stages.Add(stage);
        return stage;
    }

    /// <summary>Places atoms for the current stage. Pass index/position pairs.</summary>
    public MolecularScene At(int atomIndex, float x, float y, float z)
    {
        MolStage stage = stages[stages.Count - 1];
        stage.positions[atomIndex] = new Vector3(x, y, z);
        return this;
    }

    public MolecularScene Bond(int a, int b) { return Bond(a, b, 1, false); }

    public MolecularScene Bond(int a, int b, int order, bool ionic)
    {
        MolStage stage = stages[stages.Count - 1];
        MolBond bond = new MolBond(a, b, order, ionic);
        RemoveBond(stage, bond.a, bond.b);
        stage.bonds.Add(bond);
        return this;
    }

    public MolecularScene Break(int a, int b)
    {
        MolStage stage = stages[stages.Count - 1];
        RemoveBond(stage, Mathf.Min(a, b), Mathf.Max(a, b));
        return this;
    }

    public MolecularScene Electron(int from, int to, int count)
    {
        stages[stages.Count - 1].electrons.Add(new MolElectron(from, to, count));
        return this;
    }

    public MolecularScene Heat(float jitter, Color tint)
    {
        MolStage stage = stages[stages.Count - 1];
        stage.thermalJitter = jitter;
        stage.backgroundTint = tint;
        return this;
    }

    public MolecularScene Note(int atomIndex, string note)
    {
        stages[stages.Count - 1].notes[atomIndex] = note;
        return this;
    }

    private static void RemoveBond(MolStage stage, int a, int b)
    {
        for (int i = stage.bonds.Count - 1; i >= 0; i--)
        {
            if (stage.bonds[i].a == a && stage.bonds[i].b == b)
            {
                stage.bonds.RemoveAt(i);
            }
        }
    }
}

/// <summary>
/// CPK colours and display radii for the fourteen elements these eight reactions involve.
///
/// Deliberately separate from <see cref="PeriodicTableData"/>: that table colours by category for
/// the periodic-table panel, which is the wrong scheme for a ball-and-stick model. Chemists read
/// CPK - red oxygen, white hydrogen, yellow sulfur - and a molecular view that used category
/// colours would be actively confusing.
/// </summary>
public static class MolecularPalette
{
    public static Color Colour(string symbol)
    {
        switch (symbol)
        {
            case "H": return new Color(0.95f, 0.95f, 0.95f, 1.0f);
            case "C": return new Color(0.25f, 0.25f, 0.28f, 1.0f);
            case "N": return new Color(0.19f, 0.31f, 0.97f, 1.0f);
            case "O": return new Color(0.94f, 0.16f, 0.13f, 1.0f);
            case "Na": return new Color(0.67f, 0.36f, 0.95f, 1.0f);
            case "Al": return new Color(0.75f, 0.65f, 0.65f, 1.0f);
            case "S": return new Color(1.00f, 0.85f, 0.19f, 1.0f);
            case "Cl": return new Color(0.12f, 0.94f, 0.12f, 1.0f);
            case "K": return new Color(0.56f, 0.25f, 0.83f, 1.0f);
            case "Ca": return new Color(0.24f, 1.00f, 0.00f, 1.0f);
            case "Fe": return new Color(0.88f, 0.40f, 0.10f, 1.0f);
            case "Cu": return new Color(0.78f, 0.50f, 0.20f, 1.0f);
            case "I": return new Color(0.58f, 0.00f, 0.58f, 1.0f);
            default: return new Color(0.85f, 0.45f, 0.85f, 1.0f);
        }
    }

    /// <summary>
    /// Display radius, not the real covalent radius. Real radii would make hydrogen almost
    /// invisible next to potassium; these are compressed so every atom stays readable.
    /// </summary>
    public static float Radius(string symbol)
    {
        switch (symbol)
        {
            case "H": return 0.26f;
            case "C": return 0.40f;
            case "N": return 0.38f;
            case "O": return 0.38f;
            case "S": return 0.48f;
            case "Cl": return 0.46f;
            case "Na": return 0.54f;
            case "K": return 0.60f;
            case "Ca": return 0.56f;
            case "Al": return 0.50f;
            case "Fe": return 0.50f;
            case "Cu": return 0.50f;
            case "I": return 0.56f;
            default: return 0.44f;
        }
    }

    /// <summary>Dark text on pale atoms, white on dark ones, so the symbol is always legible.</summary>
    public static Color LabelColour(string symbol)
    {
        Color c = Colour(symbol);
        float luminance = 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;
        return luminance > 0.55f ? new Color(0.06f, 0.07f, 0.10f, 1.0f) : Color.white;
    }
}
