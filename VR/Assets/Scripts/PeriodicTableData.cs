using System.Collections.Generic;
using UnityEngine;

/// <summary>Broad classification, used only for the colour key.</summary>
public enum ElementCategory
{
    AlkaliMetal,
    AlkalineEarth,
    TransitionMetal,
    PostTransitionMetal,
    Metalloid,
    ReactiveNonmetal,
    NobleGas,
    Lanthanide,
    Actinide,
    Unknown
}

public class ChemicalElement
{
    public int number;
    public string symbol;
    public string name;
    public float mass;              // standard atomic weight; bracketed values for unstable elements
    public ElementCategory category;
    public int group;               // 1-18
    public int period;              // 1-7, with 8/9 used to lay out the f-block strip
    public bool synthetic;          // no stable isotope / not naturally occurring in quantity

    public string MassLabel
    {
        get { return synthetic ? "[" + Mathf.RoundToInt(mass) + "]" : mass.ToString("0.###"); }
    }

    public string CategoryLabel
    {
        get { return PeriodicTableData.CategoryName(category); }
    }
}

/// <summary>
/// The 118 elements, with standard atomic weights (IUPAC conventional values; bracketed
/// mass numbers of the most stable isotope for elements with no stable form).
///
/// Stored as one compact table and parsed once, rather than 118 constructor calls, so the data
/// stays reviewable as data. Atomix is a chemistry teaching tool with a periodic table modelled
/// in the lab scene but no way to actually read it - this is what backs
/// <see cref="PeriodicTableUI"/>.
/// </summary>
public static class PeriodicTableData
{
    // number|symbol|name|mass|category|group|period|synthetic
    // categories: AM alkali, AE alkaline earth, TM transition, PT post-transition, ME metalloid,
    //             NM reactive nonmetal, NG noble gas, LA lanthanide, AC actinide
    private const string Table =
        "1|H|Hydrogen|1.008|NM|1|1|0;" +
        "2|He|Helium|4.0026|NG|18|1|0;" +
        "3|Li|Lithium|6.94|AM|1|2|0;" +
        "4|Be|Beryllium|9.0122|AE|2|2|0;" +
        "5|B|Boron|10.81|ME|13|2|0;" +
        "6|C|Carbon|12.011|NM|14|2|0;" +
        "7|N|Nitrogen|14.007|NM|15|2|0;" +
        "8|O|Oxygen|15.999|NM|16|2|0;" +
        "9|F|Fluorine|18.998|NM|17|2|0;" +
        "10|Ne|Neon|20.180|NG|18|2|0;" +
        "11|Na|Sodium|22.990|AM|1|3|0;" +
        "12|Mg|Magnesium|24.305|AE|2|3|0;" +
        "13|Al|Aluminium|26.982|PT|13|3|0;" +
        "14|Si|Silicon|28.085|ME|14|3|0;" +
        "15|P|Phosphorus|30.974|NM|15|3|0;" +
        "16|S|Sulfur|32.06|NM|16|3|0;" +
        "17|Cl|Chlorine|35.45|NM|17|3|0;" +
        "18|Ar|Argon|39.95|NG|18|3|0;" +
        "19|K|Potassium|39.098|AM|1|4|0;" +
        "20|Ca|Calcium|40.078|AE|2|4|0;" +
        "21|Sc|Scandium|44.956|TM|3|4|0;" +
        "22|Ti|Titanium|47.867|TM|4|4|0;" +
        "23|V|Vanadium|50.942|TM|5|4|0;" +
        "24|Cr|Chromium|51.996|TM|6|4|0;" +
        "25|Mn|Manganese|54.938|TM|7|4|0;" +
        "26|Fe|Iron|55.845|TM|8|4|0;" +
        "27|Co|Cobalt|58.933|TM|9|4|0;" +
        "28|Ni|Nickel|58.693|TM|10|4|0;" +
        "29|Cu|Copper|63.546|TM|11|4|0;" +
        "30|Zn|Zinc|65.38|TM|12|4|0;" +
        "31|Ga|Gallium|69.723|PT|13|4|0;" +
        "32|Ge|Germanium|72.630|ME|14|4|0;" +
        "33|As|Arsenic|74.922|ME|15|4|0;" +
        "34|Se|Selenium|78.971|NM|16|4|0;" +
        "35|Br|Bromine|79.904|NM|17|4|0;" +
        "36|Kr|Krypton|83.798|NG|18|4|0;" +
        "37|Rb|Rubidium|85.468|AM|1|5|0;" +
        "38|Sr|Strontium|87.62|AE|2|5|0;" +
        "39|Y|Yttrium|88.906|TM|3|5|0;" +
        "40|Zr|Zirconium|91.224|TM|4|5|0;" +
        "41|Nb|Niobium|92.906|TM|5|5|0;" +
        "42|Mo|Molybdenum|95.95|TM|6|5|0;" +
        "43|Tc|Technetium|98|TM|7|5|1;" +
        "44|Ru|Ruthenium|101.07|TM|8|5|0;" +
        "45|Rh|Rhodium|102.91|TM|9|5|0;" +
        "46|Pd|Palladium|106.42|TM|10|5|0;" +
        "47|Ag|Silver|107.87|TM|11|5|0;" +
        "48|Cd|Cadmium|112.41|TM|12|5|0;" +
        "49|In|Indium|114.82|PT|13|5|0;" +
        "50|Sn|Tin|118.71|PT|14|5|0;" +
        "51|Sb|Antimony|121.76|ME|15|5|0;" +
        "52|Te|Tellurium|127.60|ME|16|5|0;" +
        "53|I|Iodine|126.90|NM|17|5|0;" +
        "54|Xe|Xenon|131.29|NG|18|5|0;" +
        "55|Cs|Caesium|132.91|AM|1|6|0;" +
        "56|Ba|Barium|137.33|AE|2|6|0;" +
        "57|La|Lanthanum|138.91|LA|3|8|0;" +
        "58|Ce|Cerium|140.12|LA|4|8|0;" +
        "59|Pr|Praseodymium|140.91|LA|5|8|0;" +
        "60|Nd|Neodymium|144.24|LA|6|8|0;" +
        "61|Pm|Promethium|145|LA|7|8|1;" +
        "62|Sm|Samarium|150.36|LA|8|8|0;" +
        "63|Eu|Europium|151.96|LA|9|8|0;" +
        "64|Gd|Gadolinium|157.25|LA|10|8|0;" +
        "65|Tb|Terbium|158.93|LA|11|8|0;" +
        "66|Dy|Dysprosium|162.50|LA|12|8|0;" +
        "67|Ho|Holmium|164.93|LA|13|8|0;" +
        "68|Er|Erbium|167.26|LA|14|8|0;" +
        "69|Tm|Thulium|168.93|LA|15|8|0;" +
        "70|Yb|Ytterbium|173.05|LA|16|8|0;" +
        "71|Lu|Lutetium|174.97|LA|17|8|0;" +
        "72|Hf|Hafnium|178.49|TM|4|6|0;" +
        "73|Ta|Tantalum|180.95|TM|5|6|0;" +
        "74|W|Tungsten|183.84|TM|6|6|0;" +
        "75|Re|Rhenium|186.21|TM|7|6|0;" +
        "76|Os|Osmium|190.23|TM|8|6|0;" +
        "77|Ir|Iridium|192.22|TM|9|6|0;" +
        "78|Pt|Platinum|195.08|TM|10|6|0;" +
        "79|Au|Gold|196.97|TM|11|6|0;" +
        "80|Hg|Mercury|200.59|TM|12|6|0;" +
        "81|Tl|Thallium|204.38|PT|13|6|0;" +
        "82|Pb|Lead|207.2|PT|14|6|0;" +
        "83|Bi|Bismuth|208.98|PT|15|6|0;" +
        "84|Po|Polonium|209|PT|16|6|1;" +
        "85|At|Astatine|210|ME|17|6|1;" +
        "86|Rn|Radon|222|NG|18|6|1;" +
        "87|Fr|Francium|223|AM|1|7|1;" +
        "88|Ra|Radium|226|AE|2|7|1;" +
        "89|Ac|Actinium|227|AC|3|9|1;" +
        "90|Th|Thorium|232.04|AC|4|9|0;" +
        "91|Pa|Protactinium|231.04|AC|5|9|1;" +
        "92|U|Uranium|238.03|AC|6|9|0;" +
        "93|Np|Neptunium|237|AC|7|9|1;" +
        "94|Pu|Plutonium|244|AC|8|9|1;" +
        "95|Am|Americium|243|AC|9|9|1;" +
        "96|Cm|Curium|247|AC|10|9|1;" +
        "97|Bk|Berkelium|247|AC|11|9|1;" +
        "98|Cf|Californium|251|AC|12|9|1;" +
        "99|Es|Einsteinium|252|AC|13|9|1;" +
        "100|Fm|Fermium|257|AC|14|9|1;" +
        "101|Md|Mendelevium|258|AC|15|9|1;" +
        "102|No|Nobelium|259|AC|16|9|1;" +
        "103|Lr|Lawrencium|266|AC|17|9|1;" +
        "104|Rf|Rutherfordium|267|TM|4|7|1;" +
        "105|Db|Dubnium|268|TM|5|7|1;" +
        "106|Sg|Seaborgium|269|TM|6|7|1;" +
        "107|Bh|Bohrium|270|TM|7|7|1;" +
        "108|Hs|Hassium|269|TM|8|7|1;" +
        "109|Mt|Meitnerium|278|TM|9|7|1;" +
        "110|Ds|Darmstadtium|281|TM|10|7|1;" +
        "111|Rg|Roentgenium|282|TM|11|7|1;" +
        "112|Cn|Copernicium|285|TM|12|7|1;" +
        "113|Nh|Nihonium|286|PT|13|7|1;" +
        "114|Fl|Flerovium|289|PT|14|7|1;" +
        "115|Mc|Moscovium|290|PT|15|7|1;" +
        "116|Lv|Livermorium|293|PT|16|7|1;" +
        "117|Ts|Tennessine|294|ME|17|7|1;" +
        "118|Og|Oganesson|294|NG|18|7|1";

    private static List<ChemicalElement> elements;
    private static Dictionary<string, ChemicalElement> bySymbol;

    public static List<ChemicalElement> Elements
    {
        get { EnsureParsed(); return elements; }
    }

    public static ChemicalElement BySymbol(string symbol)
    {
        EnsureParsed();
        ChemicalElement found;
        return bySymbol.TryGetValue(symbol, out found) ? found : null;
    }

    private static void EnsureParsed()
    {
        if (elements != null)
        {
            return;
        }

        elements = new List<ChemicalElement>(118);
        bySymbol = new Dictionary<string, ChemicalElement>(118);

        string[] rows = Table.Split(';');
        for (int i = 0; i < rows.Length; i++)
        {
            string[] parts = rows[i].Split('|');
            if (parts.Length < 8)
            {
                continue;
            }

            ChemicalElement element = new ChemicalElement();
            int.TryParse(parts[0], out element.number);
            element.symbol = parts[1];
            element.name = parts[2];
            float.TryParse(parts[3], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out element.mass);
            element.category = ParseCategory(parts[4]);
            int.TryParse(parts[5], out element.group);
            int.TryParse(parts[6], out element.period);
            element.synthetic = parts[7] == "1";

            elements.Add(element);
            bySymbol[element.symbol] = element;
        }
    }

    private static ElementCategory ParseCategory(string code)
    {
        switch (code)
        {
            case "AM": return ElementCategory.AlkaliMetal;
            case "AE": return ElementCategory.AlkalineEarth;
            case "TM": return ElementCategory.TransitionMetal;
            case "PT": return ElementCategory.PostTransitionMetal;
            case "ME": return ElementCategory.Metalloid;
            case "NM": return ElementCategory.ReactiveNonmetal;
            case "NG": return ElementCategory.NobleGas;
            case "LA": return ElementCategory.Lanthanide;
            case "AC": return ElementCategory.Actinide;
            default: return ElementCategory.Unknown;
        }
    }

    public static string CategoryName(ElementCategory category)
    {
        switch (category)
        {
            case ElementCategory.AlkaliMetal: return "Alkali metal";
            case ElementCategory.AlkalineEarth: return "Alkaline earth metal";
            case ElementCategory.TransitionMetal: return "Transition metal";
            case ElementCategory.PostTransitionMetal: return "Post-transition metal";
            case ElementCategory.Metalloid: return "Metalloid";
            case ElementCategory.ReactiveNonmetal: return "Reactive nonmetal";
            case ElementCategory.NobleGas: return "Noble gas";
            case ElementCategory.Lanthanide: return "Lanthanide";
            case ElementCategory.Actinide: return "Actinide";
            default: return "Unknown";
        }
    }

    /// <summary>Distinct hues, kept apart in lightness too so the key survives a mono projector.</summary>
    public static Color CategoryColour(ElementCategory category)
    {
        switch (category)
        {
            case ElementCategory.AlkaliMetal: return new Color(0.85f, 0.36f, 0.30f, 1.0f);
            case ElementCategory.AlkalineEarth: return new Color(0.90f, 0.60f, 0.26f, 1.0f);
            case ElementCategory.TransitionMetal: return new Color(0.38f, 0.52f, 0.74f, 1.0f);
            case ElementCategory.PostTransitionMetal: return new Color(0.36f, 0.62f, 0.60f, 1.0f);
            case ElementCategory.Metalloid: return new Color(0.55f, 0.50f, 0.74f, 1.0f);
            case ElementCategory.ReactiveNonmetal: return new Color(0.34f, 0.66f, 0.42f, 1.0f);
            case ElementCategory.NobleGas: return new Color(0.72f, 0.42f, 0.66f, 1.0f);
            case ElementCategory.Lanthanide: return new Color(0.58f, 0.44f, 0.32f, 1.0f);
            case ElementCategory.Actinide: return new Color(0.48f, 0.38f, 0.30f, 1.0f);
            default: return new Color(0.35f, 0.38f, 0.44f, 1.0f);
        }
    }

    /// <summary>
    /// The elements each lab reaction involves, so the table can highlight what the student is
    /// actually working with. Keyed by the reaction ids used by the book and the history.
    /// </summary>
    public static string[] ElementsForReaction(int reactionId)
    {
        switch (reactionId)
        {
            case 1: return new[] { "Na", "H", "O" };                  // 2Na + 2H2O -> 2NaOH + H2
            case 2: return new[] { "H", "S", "O", "Cu" };             // H2SO4 + CuO -> CuSO4 + H2O
            case 3: return new[] { "H", "Cl", "Na", "C", "O" };       // HCl + NaHCO3 -> NaCl + H2O + CO2
            case 4: return new[] { "K", "H", "O" };                   // 2K + 2H2O -> 2KOH + H2
            case 5: return new[] { "Al", "I" };                       // 2Al + 3I2 -> 2AlI3
            case 6: return new[] { "Ca", "O", "H" };                  // CaO + H2O -> Ca(OH)2
            case 7: return new[] { "Ca", "C", "O" };                  // CaCO3 -> CaO + CO2
            case 8: return new[] { "Fe", "S", "O" };                  // 2FeSO4 -> Fe2O3 + SO2 + SO3
            default: return new string[0];
        }
    }
}
