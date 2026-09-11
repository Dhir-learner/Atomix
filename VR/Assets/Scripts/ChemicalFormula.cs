/// <summary>
/// Molar masses from formulas, using the standard atomic weights in <see cref="PeriodicTableData"/>.
///
/// Handles what the eight lab reactions actually write: element symbols with counts ("H2SO4"),
/// and bracketed groups with a multiplier ("Ca(OH)2"). Anything it cannot read returns -1 rather
/// than a plausible-looking wrong number, so a caller can refuse to set a calculation it cannot
/// get right.
/// </summary>
public static class ChemicalFormula
{
    /// <summary>g/mol, or -1 when the formula cannot be parsed or names an unknown element.</summary>
    public static float MolarMass(string formula)
    {
        if (string.IsNullOrEmpty(formula))
        {
            return -1.0f;
        }

        int index = 0;
        double mass = ParseGroup(formula, ref index);
        if (mass <= 0.0 || index != formula.Length)
        {
            return -1.0f;
        }

        return (float)mass;
    }

    /// <summary>Reads up to the end of the string or a closing bracket. Returns -1 on any error.</summary>
    private static double ParseGroup(string formula, ref int index)
    {
        double total = 0.0;

        while (index < formula.Length)
        {
            char c = formula[index];

            if (c == ')')
            {
                break;
            }

            double partMass;
            if (c == '(')
            {
                index++;
                partMass = ParseGroup(formula, ref index);
                if (partMass <= 0.0 || index >= formula.Length || formula[index] != ')')
                {
                    return -1.0;
                }
                index++;
            }
            else if (char.IsUpper(c))
            {
                int start = index;
                index++;
                while (index < formula.Length && char.IsLower(formula[index]))
                {
                    index++;
                }

                ChemicalElement element = PeriodicTableData.BySymbol(formula.Substring(start, index - start));
                if (element == null || element.mass <= 0.0f)
                {
                    return -1.0;
                }
                partMass = element.mass;
            }
            else
            {
                return -1.0;
            }

            total += partMass * ReadCount(formula, ref index);
        }

        return total;
    }

    /// <summary>The number after a symbol or bracket; 1 when there is none.</summary>
    private static int ReadCount(string formula, ref int index)
    {
        int count = 0;
        bool any = false;
        while (index < formula.Length && char.IsDigit(formula[index]))
        {
            count = (count * 10) + (formula[index] - '0');
            index++;
            any = true;
        }
        return any ? count : 1;
    }
}
