using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StaticData : MonoBehaviour
{
    /// <summary>0 = practical only, 1 = theory only, 2 = both. Set from the main menu settings.</summary>
    public static int includedTasksValue;

    /// <summary>
    /// How many tasks the next testing run should set, chosen on the setup screen when the
    /// testing scene opens.
    ///
    /// Zero means "not chosen yet"; callers fall back to <see cref="DefaultTaskCount"/>, which is
    /// the hard-coded 10 the testing scene used before the count was selectable.
    /// </summary>
    public static int taskCountValue;

    public const int DefaultTaskCount = 10;

    /// <summary>The chosen count, or the default when nothing has been chosen.</summary>
    public static int TaskCount
    {
        get { return taskCountValue > 0 ? taskCountValue : DefaultTaskCount; }
    }

    /// <summary>How many tasks exist at all in the current mode.</summary>
    public static int MaxTasksForMode
    {
        get
        {
            switch (includedTasksValue)
            {
                case 0: return 8;    // the eight practical experiments
                case 1: return 10;   // the ten theory questions
                default: return 18;  // both
            }
        }
    }
}
