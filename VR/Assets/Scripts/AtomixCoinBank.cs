using UnityEngine;

/// <summary>
/// The persistent coin wallet behind the testing scene's score strip.
///
/// What it replaces: <see cref="CountdownTimer"/> held the score in a plain `int score` field that
/// was set to 0 in `Start()`. Every time the testing scene loaded - and it reloads on every
/// "Run the test again" - the number went back to zero and everything the student had earned was
/// gone. It was a counter, not a score: nothing carried, nothing accumulated, nothing could be
/// spent, and finishing a run left no trace.
///
/// Coins now do three jobs:
///
///  1. **They persist.** Lifetime totals survive scene reloads and application restarts.
///  2. **They rank you.** Lifetime earnings drive a title, so a long-term player has something to
///     climb.
///  3. **They can be spent.** During a test you can buy a hint, more time, or a skip - which turns
///     the score from a number you watch into a decision you make. Spending is recorded, so the
///     report can say the run was finished unaided.
///
/// PlayerPrefs rather than a JSON file, matching <see cref="AtomixSettings"/>: this is a handful of
/// integers, and the experiment history already owns the JSON file for the things that need one.
/// </summary>
public class AtomixCoinBank : MonoBehaviour
{
    private const string KeyLifetime = "atomix.coins.lifetime";
    private const string KeySpent = "atomix.coins.spent";
    private const string KeyBestRun = "atomix.coins.bestRun";
    private const string KeyRuns = "atomix.coins.runs";
    private const string KeyPerfectRuns = "atomix.coins.perfectRuns";
    private const string KeyBestStreak = "atomix.coins.bestStreak";
    private const string KeyClearedMask = "atomix.coins.clearedMask";

    // --- Prices ------------------------------------------------------------------------
    public const int PriceHint = 40;
    public const int PriceExtraTime = 60;
    public const int PriceSkip = 100;

    /// <summary>Seconds added to the clock by the extra-time purchase.</summary>
    public const float ExtraTimeSeconds = 30.0f;

    // --- Bonuses -----------------------------------------------------------------------
    public const int BonusFirstClear = 25;
    public const int BonusStreakThree = 30;
    public const int BonusStreakFive = 60;
    public const int BonusUnaidedRun = 50;

    private static AtomixCoinBank instance;

    public static AtomixCoinBank Instance
    {
        get
        {
            if (instance == null)
            {
                GameObject host = new GameObject("AtomixCoinBank");
                instance = host.AddComponent<AtomixCoinBank>();
                DontDestroyOnLoad(host);
            }
            return instance;
        }
    }

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else if (instance != this)
        {
            Destroy(gameObject);
        }
    }

    // =====================================================================================
    // TOTALS
    // =====================================================================================

    /// <summary>Every coin ever earned. Never goes down - this is what drives the rank.</summary>
    public int LifetimeEarned
    {
        get { return PlayerPrefs.GetInt(KeyLifetime, 0); }
    }

    /// <summary>Every coin ever spent on help.</summary>
    public int LifetimeSpent
    {
        get { return PlayerPrefs.GetInt(KeySpent, 0); }
    }

    /// <summary>What is actually available to spend right now.</summary>
    public int Balance
    {
        get { return Mathf.Max(0, LifetimeEarned - LifetimeSpent); }
    }

    public int BestRun { get { return PlayerPrefs.GetInt(KeyBestRun, 0); } }
    public int RunsCompleted { get { return PlayerPrefs.GetInt(KeyRuns, 0); } }
    public int PerfectRuns { get { return PlayerPrefs.GetInt(KeyPerfectRuns, 0); } }
    public int BestStreak { get { return PlayerPrefs.GetInt(KeyBestStreak, 0); } }

    // =====================================================================================
    // RANK
    // =====================================================================================

    private static readonly int[] RankThresholds = { 0, 500, 1500, 3500, 7000 };

    private static readonly string[] RankNames =
    {
        "Apprentice",
        "Lab Technician",
        "Chemist",
        "Senior Chemist",
        "Lab Master"
    };

    public int RankIndex
    {
        get
        {
            int lifetime = LifetimeEarned;
            int rank = 0;
            for (int i = 0; i < RankThresholds.Length; i++)
            {
                if (lifetime >= RankThresholds[i])
                {
                    rank = i;
                }
            }
            return rank;
        }
    }

    public string RankName { get { return RankNames[RankIndex]; } }

    /// <summary>Coins still needed for the next rank, or 0 at the top.</summary>
    public int CoinsToNextRank
    {
        get
        {
            int rank = RankIndex;
            if (rank >= RankThresholds.Length - 1)
            {
                return 0;
            }
            return RankThresholds[rank + 1] - LifetimeEarned;
        }
    }

    public string NextRankName
    {
        get
        {
            int rank = RankIndex;
            return rank >= RankNames.Length - 1 ? string.Empty : RankNames[rank + 1];
        }
    }

    /// <summary>0-1 through the current rank band, for a progress bar.</summary>
    public float RankProgress
    {
        get
        {
            int rank = RankIndex;
            if (rank >= RankThresholds.Length - 1)
            {
                return 1.0f;
            }

            int floor = RankThresholds[rank];
            int ceiling = RankThresholds[rank + 1];
            if (ceiling <= floor)
            {
                return 1.0f;
            }

            return Mathf.Clamp01((LifetimeEarned - floor) / (float)(ceiling - floor));
        }
    }

    // =====================================================================================
    // EARNING AND SPENDING
    // =====================================================================================

    /// <summary>Adds coins to the lifetime total. Negative and zero awards are ignored.</summary>
    public void Earn(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        PlayerPrefs.SetInt(KeyLifetime, LifetimeEarned + amount);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Takes coins for a purchase. Returns false and changes nothing when the balance is short,
    /// so a caller can offer the purchase and let this be the single place that says no.
    /// </summary>
    public bool Spend(int amount)
    {
        if (amount <= 0 || Balance < amount)
        {
            return false;
        }

        PlayerPrefs.SetInt(KeySpent, LifetimeSpent + amount);
        PlayerPrefs.Save();
        return true;
    }

    public bool CanAfford(int amount)
    {
        return Balance >= amount;
    }

    // =====================================================================================
    // FIRST CLEARS
    // =====================================================================================

    /// <summary>
    /// Whether reaction 1-8 has ever been cleared in a test. Stored as a bitmask in one int so
    /// there are not eight more preference keys to keep in step.
    /// </summary>
    public bool HasCleared(int reactionId)
    {
        if (reactionId < 1 || reactionId > 8)
        {
            return true; // Not a practical reaction; never award a first-clear bonus for it.
        }

        return (PlayerPrefs.GetInt(KeyClearedMask, 0) & (1 << (reactionId - 1))) != 0;
    }

    /// <summary>Marks a reaction cleared. Returns true if this was the first time ever.</summary>
    public bool MarkCleared(int reactionId)
    {
        if (reactionId < 1 || reactionId > 8 || HasCleared(reactionId))
        {
            return false;
        }

        int mask = PlayerPrefs.GetInt(KeyClearedMask, 0) | (1 << (reactionId - 1));
        PlayerPrefs.SetInt(KeyClearedMask, mask);
        PlayerPrefs.Save();
        return true;
    }

    public int DistinctReactionsCleared
    {
        get
        {
            int mask = PlayerPrefs.GetInt(KeyClearedMask, 0);
            int count = 0;
            for (int i = 0; i < 8; i++)
            {
                if ((mask & (1 << i)) != 0)
                {
                    count++;
                }
            }
            return count;
        }
    }

    // =====================================================================================
    // END OF RUN
    // =====================================================================================

    /// <summary>
    /// Files a completed run. Called once by <see cref="ExamSession"/> when the test finishes.
    /// </summary>
    public void CommitRun(int runCoins, bool perfect, int longestStreak)
    {
        PlayerPrefs.SetInt(KeyRuns, RunsCompleted + 1);

        if (runCoins > BestRun)
        {
            PlayerPrefs.SetInt(KeyBestRun, runCoins);
        }

        if (perfect)
        {
            PlayerPrefs.SetInt(KeyPerfectRuns, PerfectRuns + 1);
        }

        if (longestStreak > BestStreak)
        {
            PlayerPrefs.SetInt(KeyBestStreak, longestStreak);
        }

        PlayerPrefs.Save();
    }

    /// <summary>Wipes every coin statistic. Offered in the pause menu next to the history reset.</summary>
    public void ResetAll()
    {
        PlayerPrefs.DeleteKey(KeyLifetime);
        PlayerPrefs.DeleteKey(KeySpent);
        PlayerPrefs.DeleteKey(KeyBestRun);
        PlayerPrefs.DeleteKey(KeyRuns);
        PlayerPrefs.DeleteKey(KeyPerfectRuns);
        PlayerPrefs.DeleteKey(KeyBestStreak);
        PlayerPrefs.DeleteKey(KeyClearedMask);
        PlayerPrefs.Save();
    }
}
