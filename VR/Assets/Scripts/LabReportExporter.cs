using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// Turns the recorded experiment history into files a human can actually read.
///
/// <see cref="ExperimentHistoryManager"/> already persists everything as JSON, but JSON is for the
/// game, not for a student writing up a practical or a teacher marking one. This writes a Markdown
/// lab report and a CSV of every attempt next to the save file, so the work can be handed in,
/// opened in a spreadsheet, or attached to the research data.
/// </summary>
public static class LabReportExporter
{
    public static string ReportDirectory
    {
        get { return Application.persistentDataPath; }
    }

    public static string MarkdownPath
    {
        get { return Path.Combine(ReportDirectory, "atomix_lab_report.md"); }
    }

    public static string CsvPath
    {
        get { return Path.Combine(ReportDirectory, "atomix_attempts.csv"); }
    }

    /// <summary>
    /// Writes both files. Returns a short message naming what was written, or the failure reason -
    /// suitable for putting straight on a button's status line.
    /// </summary>
    public static string ExportAll()
    {
        ExperimentHistoryManager manager = ExperimentHistoryManager.Instance;
        if (manager == null)
        {
            return "No history to export yet.";
        }

        List<ExperimentAttempt> attempts = manager.GetHistory();
        if (attempts == null || attempts.Count == 0)
        {
            return "No experiments recorded yet - nothing to export.";
        }

        try
        {
            File.WriteAllText(MarkdownPath, BuildMarkdown(attempts), new UTF8Encoding(false));
            File.WriteAllText(CsvPath, BuildCsv(attempts), new UTF8Encoding(false));
            return "Exported " + attempts.Count + " attempts to\n" + ReportDirectory;
        }
        catch (Exception error)
        {
            Debug.LogWarning("[LabReportExporter] Export failed: " + error.Message);
            return "Export failed: " + error.Message;
        }
    }

    // =========================================================
    // MARKDOWN
    // =========================================================

    private static string BuildMarkdown(List<ExperimentAttempt> attempts)
    {
        StringBuilder text = new StringBuilder();
        LabPerformanceSummary summary = LabPerformanceSummary.FromHistory(attempts);

        text.AppendLine("# Atomix — Laboratory Report");
        text.AppendLine();
        text.AppendLine("Generated " + DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture));
        text.AppendLine();

        text.AppendLine("## Summary");
        text.AppendLine();
        text.AppendLine("| Measure | Value |");
        text.AppendLine("|---|---|");
        text.AppendLine("| Attempts recorded | " + summary.totalAttempts + " |");
        text.AppendLine("| Successful | " + summary.successes + " |");
        text.AppendLine("| Failed | " + summary.failures + " |");
        text.AppendLine("| Abandoned | " + summary.abandoned + " |");
        text.AppendLine("| Distinct experiments attempted | " + summary.distinctAttempted + " of 8 |");
        text.AppendLine("| Distinct experiments completed | " + summary.distinctSucceeded + " of 8 |");
        text.AppendLine("| Success rate | " + summary.SuccessRatePercent.ToString("0.#") + "% |");
        text.AppendLine("| Overall grade | **" + summary.Grade + "** |");
        text.AppendLine();

        if (summary.mistakeCounts.Count > 0)
        {
            text.AppendLine("## Where marks were lost");
            text.AppendLine();
            text.AppendLine("| Mistake | Times |");
            text.AppendLine("|---|---|");
            foreach (KeyValuePair<string, int> pair in summary.mistakeCounts)
            {
                text.AppendLine("| " + pair.Key + " | " + pair.Value + " |");
            }
            text.AppendLine();
        }

        text.AppendLine("## Attempts");
        text.AppendLine();

        for (int i = attempts.Count - 1; i >= 0; i--)
        {
            ExperimentAttempt attempt = attempts[i];
            if (attempt == null)
            {
                continue;
            }

            text.AppendLine("### " + attempt.reactionName);
            text.AppendLine();
            text.AppendLine("- **Outcome:** " + Describe(attempt.outcome));
            text.AppendLine("- **When:** " +
                attempt.Timestamp.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            text.AppendLine("- **Duration:** " + attempt.durationSeconds.ToString("0.0") + " s");

            if (attempt.quantitiesUsed != null && attempt.quantitiesUsed.Count > 0)
            {
                text.AppendLine("- **Quantities used:**");
                foreach (KeyValuePair<string, float> pair in attempt.quantitiesUsed)
                {
                    float target;
                    bool haveTarget = attempt.targetQuantities != null &&
                                      attempt.targetQuantities.TryGetValue(pair.Key, out target);
                    text.AppendLine("  - " + pair.Key + ": " + pair.Value.ToString("0.0") +
                        (haveTarget
                            ? " (target " + attempt.targetQuantities[pair.Key].ToString("0.0") + ")"
                            : string.Empty));
                }
            }

            if (attempt.steps != null && attempt.steps.Count > 0)
            {
                text.AppendLine("- **Procedure:**");
                for (int s = 0; s < attempt.steps.Count; s++)
                {
                    ExperimentStep step = attempt.steps[s];
                    if (step == null)
                    {
                        continue;
                    }
                    text.AppendLine("  - `" + step.timestamp.ToString("0.0") + "s` " +
                        (step.wasCorrect ? "" : "**!** ") + step.action);
                }
            }

            if (attempt.aiInteractions != null && attempt.aiInteractions.Count > 0)
            {
                text.AppendLine("- **Questions asked:**");
                for (int a = 0; a < attempt.aiInteractions.Count; a++)
                {
                    AIInteraction interaction = attempt.aiInteractions[a];
                    if (interaction == null)
                    {
                        continue;
                    }
                    text.AppendLine("  - Q: " + interaction.userQuestion);
                }
            }

            text.AppendLine();
        }

        return text.ToString();
    }

    // =========================================================
    // CSV
    // =========================================================

    private static string BuildCsv(List<ExperimentAttempt> attempts)
    {
        StringBuilder text = new StringBuilder();
        text.AppendLine("timestamp,reaction_id,reaction_name,outcome,duration_seconds,steps,mistakes,questions_asked");

        for (int i = 0; i < attempts.Count; i++)
        {
            ExperimentAttempt attempt = attempts[i];
            if (attempt == null)
            {
                continue;
            }

            int mistakes = 0;
            if (attempt.steps != null)
            {
                for (int s = 0; s < attempt.steps.Count; s++)
                {
                    if (attempt.steps[s] != null && !attempt.steps[s].wasCorrect)
                    {
                        mistakes++;
                    }
                }
            }

            text.AppendLine(string.Join(",", new[]
            {
                Escape(attempt.Timestamp.ToString("s", CultureInfo.InvariantCulture)),
                attempt.reactionId.ToString(CultureInfo.InvariantCulture),
                Escape(attempt.reactionName),
                Escape(attempt.outcome.ToString()),
                attempt.durationSeconds.ToString("0.0", CultureInfo.InvariantCulture),
                (attempt.steps != null ? attempt.steps.Count : 0).ToString(CultureInfo.InvariantCulture),
                mistakes.ToString(CultureInfo.InvariantCulture),
                (attempt.aiInteractions != null ? attempt.aiInteractions.Count : 0)
                    .ToString(CultureInfo.InvariantCulture)
            }));
        }

        return text.ToString();
    }

    /// <summary>Quotes a CSV field only when it needs it, and doubles any embedded quote.</summary>
    private static string Escape(string field)
    {
        if (string.IsNullOrEmpty(field))
        {
            return string.Empty;
        }

        bool needsQuotes = field.IndexOf(',') >= 0 || field.IndexOf('"') >= 0 ||
                           field.IndexOf('\n') >= 0 || field.IndexOf('\r') >= 0;
        if (!needsQuotes)
        {
            return field;
        }

        return "\"" + field.Replace("\"", "\"\"") + "\"";
    }

    public static string Describe(ExperimentOutcome outcome)
    {
        switch (outcome)
        {
            case ExperimentOutcome.Success: return "Success";
            case ExperimentOutcome.FailOverdose: return "Failed — too much of a reagent";
            case ExperimentOutcome.FailUnderdose: return "Failed — not enough of a reagent";
            case ExperimentOutcome.FailWrongOrder: return "Failed — wrong procedure order";
            case ExperimentOutcome.FailTimeout: return "Failed — ran out of time";
            case ExperimentOutcome.Abandoned: return "Abandoned";
            default: return "In progress";
        }
    }
}

/// <summary>
/// Aggregate view of a set of attempts: totals, the mistakes that came up most, and a grade.
/// Pure computation over the history, so it is equally usable by the export, the end-of-test
/// report card, and any future teacher dashboard.
/// </summary>
public class LabPerformanceSummary
{
    public int totalAttempts;
    public int successes;
    public int failures;
    public int abandoned;
    public int distinctAttempted;
    public int distinctSucceeded;
    public float totalSeconds;
    public readonly Dictionary<string, int> mistakeCounts = new Dictionary<string, int>();

    public float SuccessRatePercent
    {
        get
        {
            int judged = successes + failures;
            return judged <= 0 ? 0.0f : 100.0f * successes / judged;
        }
    }

    /// <summary>
    /// A–F on the success rate, with a floor tied to coverage: finishing two experiments
    /// perfectly is not an A when there are eight on the bench.
    /// </summary>
    public string Grade
    {
        get
        {
            int judged = successes + failures;
            if (judged == 0)
            {
                return "—";
            }

            float rate = SuccessRatePercent;
            float coverage = distinctSucceeded / 8.0f;

            string grade;
            if (rate >= 90.0f) grade = "A";
            else if (rate >= 80.0f) grade = "B";
            else if (rate >= 65.0f) grade = "C";
            else if (rate >= 50.0f) grade = "D";
            else grade = "F";

            // Cap the grade when most of the bench has not been attempted successfully.
            if (coverage < 0.5f && (grade == "A" || grade == "B"))
            {
                grade = "C";
            }
            else if (coverage < 0.75f && grade == "A")
            {
                grade = "B";
            }

            return grade;
        }
    }

    public static LabPerformanceSummary FromHistory(List<ExperimentAttempt> attempts)
    {
        LabPerformanceSummary summary = new LabPerformanceSummary();
        if (attempts == null)
        {
            return summary;
        }

        HashSet<int> attempted = new HashSet<int>();
        HashSet<int> succeeded = new HashSet<int>();

        for (int i = 0; i < attempts.Count; i++)
        {
            ExperimentAttempt attempt = attempts[i];
            if (attempt == null)
            {
                continue;
            }

            summary.totalAttempts++;
            summary.totalSeconds += attempt.durationSeconds;
            attempted.Add(attempt.reactionId);

            switch (attempt.outcome)
            {
                case ExperimentOutcome.Success:
                    summary.successes++;
                    succeeded.Add(attempt.reactionId);
                    break;
                case ExperimentOutcome.Abandoned:
                case ExperimentOutcome.InProgress:
                    summary.abandoned++;
                    break;
                default:
                    summary.failures++;
                    Count(summary.mistakeCounts, LabReportExporter.Describe(attempt.outcome));
                    break;
            }
        }

        summary.distinctAttempted = attempted.Count;
        summary.distinctSucceeded = succeeded.Count;
        return summary;
    }

    private static void Count(Dictionary<string, int> counts, string key)
    {
        int existing;
        counts[key] = counts.TryGetValue(key, out existing) ? existing + 1 : 1;
    }
}
