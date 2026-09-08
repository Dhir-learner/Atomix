using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// Writes the report for one testing run to a file the student can actually open.
///
/// Two things separate this from the existing <see cref="LabReportExporter"/>:
///
///  1. **Scope.** LabReportExporter dumps the whole experiment history from every session in the
///     free-practice lab. This is one exam: the tasks of this run, in order, practical and
///     theoretical, with the coins each earned.
///  2. **Where it lands.** LabReportExporter writes to `Application.persistentDataPath`, which on
///     Windows is `AppData\LocalLow\&lt;company&gt;\&lt;product&gt;` - a hidden folder most students
///     will never find. This writes to the Desktop, falling back to Documents and then to
///     persistentDataPath, so "download my report" produces a file that is simply there.
///
/// The HTML report is the one meant to be handed in: it is self-contained, styled, prints cleanly
/// to PDF from any browser, and needs no network. The CSV is for a marker who wants the numbers in
/// a spreadsheet.
/// </summary>
public static class ExamReportExporter
{
    /// <summary>
    /// Desktop first, then Documents, then the Unity data folder. Each is only used if it exists
    /// and is writable - a locked-down lab machine can have a redirected or read-only Desktop.
    /// </summary>
    public static string ReportDirectory
    {
        get
        {
            string desktop = SafeFolder(Environment.SpecialFolder.DesktopDirectory);
            if (!string.IsNullOrEmpty(desktop))
            {
                return desktop;
            }

            string documents = SafeFolder(Environment.SpecialFolder.MyDocuments);
            if (!string.IsNullOrEmpty(documents))
            {
                return documents;
            }

            return Application.persistentDataPath;
        }
    }

    private static string SafeFolder(Environment.SpecialFolder folder)
    {
        try
        {
            string path = Environment.GetFolderPath(folder);

            if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
            {
                return null;
            }

            // Existing is not the same as writable. Prove it before returning it, or the export
            // fails at the last step with a permissions error the student cannot act on.
            string probe = Path.Combine(path, ".atomix_write_test");
            File.WriteAllText(probe, "x");
            File.Delete(probe);

            return path;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static string Stamp
    {
        get { return DateTime.Now.ToString("yyyy-MM-dd_HHmm"); }
    }

    /// <summary>The folder the last successful export wrote into, for the "open folder" button.</summary>
    public static string LastExportDirectory { get; private set; }

    /// <summary>
    /// Writes the HTML and CSV reports. Returns a message fit to show on the report card, naming
    /// the folder on success or the reason on failure.
    /// </summary>
    public static string ExportRun()
    {
        IReadOnlyList<ExamTaskRecord> tasks = ExamSession.Tasks;

        if (tasks == null || tasks.Count == 0)
        {
            return "Nothing to export - no tasks were completed in this run.";
        }

        string directory = ReportDirectory;

        try
        {
            string baseName = "Atomix_Test_Report_" + Stamp;
            string htmlPath = Path.Combine(directory, baseName + ".html");
            string csvPath = Path.Combine(directory, baseName + ".csv");

            UTF8Encoding utf8 = new UTF8Encoding(false);
            File.WriteAllText(htmlPath, BuildHtml(tasks), utf8);
            File.WriteAllText(csvPath, BuildCsv(tasks), utf8);

            LastExportDirectory = directory;

            return "Saved to " + FriendlyFolderName(directory) + "\n" +
                   baseName + ".html  and  .csv";
        }
        catch (Exception error)
        {
            Debug.LogWarning("[ExamReportExporter] Export failed: " + error.Message);
            return "Could not save the report: " + error.Message;
        }
    }

    /// <summary>Opens the folder the report was written to in the system file browser.</summary>
    public static bool OpenReportFolder()
    {
        string directory = string.IsNullOrEmpty(LastExportDirectory)
            ? ReportDirectory
            : LastExportDirectory;

        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
        {
            return false;
        }

        Application.OpenURL("file:///" + directory.Replace('\\', '/'));
        return true;
    }

    private static string FriendlyFolderName(string directory)
    {
        try
        {
            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            if (!string.IsNullOrEmpty(desktop) && directory == desktop)
            {
                return "your Desktop";
            }

            string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (!string.IsNullOrEmpty(documents) && directory == documents)
            {
                return "your Documents folder";
            }
        }
        catch (Exception)
        {
        }

        return directory;
    }

    // =====================================================================================
    // CSV
    // =====================================================================================

    private static string BuildCsv(IReadOnlyList<ExamTaskRecord> tasks)
    {
        StringBuilder b = new StringBuilder();
        b.AppendLine("Task,Type,Title,Question,Outcome,Detail,Seconds taken,Seconds left,Coins,Help used");

        for (int i = 0; i < tasks.Count; i++)
        {
            ExamTaskRecord t = tasks[i];
            b.Append(t.order).Append(',')
             .Append(Csv(t.kind.ToString())).Append(',')
             .Append(Csv(t.title)).Append(',')
             .Append(Csv(t.question)).Append(',')
             .Append(Csv(t.OutcomeLabel)).Append(',')
             .Append(Csv(t.detail)).Append(',')
             .Append(t.secondsTaken.ToString("0.0")).Append(',')
             .Append(t.secondsRemaining.ToString("0.0")).Append(',')
             .Append(t.coinsEarned).Append(',')
             .Append(Csv(t.helpUsed))
             .AppendLine();
        }

        b.AppendLine();
        b.Append("Totals,,,,,,,").Append(ExamSession.RunCoins).AppendLine(",");
        b.Append("Passed,").Append(ExamSession.Passed).Append(" of ").Append(tasks.Count)
         .AppendLine();
        b.Append("Grade,").Append(ExamSession.Grade).AppendLine();

        return b.ToString();
    }

    /// <summary>
    /// Quotes a CSV field. Always quoting is deliberate: chemistry names contain commas and the
    /// failure reasons contain both commas and quotes.
    /// </summary>
    private static string Csv(string value)
    {
        if (value == null)
        {
            return "\"\"";
        }

        return "\"" + value.Replace("\"", "\"\"").Replace("\r", " ").Replace("\n", " ") + "\"";
    }

    // =====================================================================================
    // HTML
    // =====================================================================================

    private static string BuildHtml(IReadOnlyList<ExamTaskRecord> tasks)
    {
        AtomixCoinBank bank = AtomixCoinBank.Instance;

        int passed = ExamSession.Passed;
        int total = tasks.Count;
        string grade = ExamSession.Grade;

        StringBuilder b = new StringBuilder(8192);

        b.AppendLine("<!doctype html>");
        b.AppendLine("<html lang=\"en\"><head><meta charset=\"utf-8\">");
        b.AppendLine("<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">");
        b.AppendLine("<title>Atomix Test Report - " +
                     Escape(ExamSession.StartedUtc.ToLocalTime().ToString("d MMM yyyy, HH:mm")) +
                     "</title>");
        b.AppendLine(Style);
        b.AppendLine("</head><body><div class=\"page\">");

        // --- header ---------------------------------------------------------------
        b.AppendLine("<header>");
        b.AppendLine("<h1>Atomix &mdash; Virtual Chemistry Laboratory</h1>");
        b.AppendLine("<p class=\"sub\">Testing phase report &middot; " +
                     Escape(ExamSession.StartedUtc.ToLocalTime().ToString("dddd d MMMM yyyy, HH:mm")) +
                     "</p>");
        b.AppendLine("</header>");

        // --- headline figures -----------------------------------------------------
        b.AppendLine("<section class=\"cards\">");
        Card(b, "Grade", grade, GradeClass(grade));
        Card(b, "Passed", passed + " / " + total, passed == total ? "good" : "");
        Card(b, "Coins earned", ExamSession.RunCoins.ToString(), "");
        Card(b, "Time taken", FormatDuration(ExamSession.TotalSeconds), "");
        b.AppendLine("</section>");

        // --- run details ----------------------------------------------------------
        b.AppendLine("<section><h2>This run</h2><table class=\"meta\">");
        Row(b, "Task mix", ModeName(ExamSession.Mode));
        Row(b, "Longest correct streak", ExamSession.LongestStreak.ToString());
        Row(b, "Coins spent on help", ExamSession.RunSpent == 0
            ? "None &mdash; finished unaided"
            : ExamSession.RunSpent.ToString());
        Row(b, "Success rate", ExamSession.SuccessRatePercent.ToString("0") + "%");
        b.AppendLine("</table></section>");

        // --- the tasks ------------------------------------------------------------
        b.AppendLine("<section><h2>Every task, in order</h2>");
        b.AppendLine("<table class=\"tasks\"><thead><tr>" +
                     "<th>#</th><th>Type</th><th>Task</th><th>Result</th>" +
                     "<th>Detail</th><th class=\"num\">Time</th><th class=\"num\">Coins</th>" +
                     "</tr></thead><tbody>");

        for (int i = 0; i < tasks.Count; i++)
        {
            ExamTaskRecord t = tasks[i];
            string rowClass = t.Passed ? "pass" : "fail";

            b.Append("<tr class=\"").Append(rowClass).Append("\">");
            b.Append("<td>").Append(t.order).Append("</td>");
            b.Append("<td>").Append(t.kind == ExamTaskKind.Theory ? "Theory" : "Practical").Append("</td>");

            b.Append("<td><strong>").Append(Escape(t.title)).Append("</strong>");
            if (!string.IsNullOrEmpty(t.question))
            {
                b.Append("<br><span class=\"q\">").Append(Escape(t.question)).Append("</span>");
            }
            b.Append("</td>");

            b.Append("<td class=\"").Append(rowClass).Append("\">")
             .Append(Escape(t.OutcomeLabel)).Append("</td>");

            b.Append("<td class=\"detail\">").Append(Escape(t.detail));
            if (!string.IsNullOrEmpty(t.helpUsed))
            {
                b.Append("<br><span class=\"help\">Help used: ")
                 .Append(Escape(t.helpUsed)).Append("</span>");
            }
            b.Append("</td>");

            b.Append("<td class=\"num\">").Append(t.secondsTaken.ToString("0")).Append("s</td>");
            b.Append("<td class=\"num\">").Append(t.coinsEarned > 0 ? "+" + t.coinsEarned : "-")
             .Append("</td>");
            b.AppendLine("</tr>");
        }

        b.AppendLine("</tbody></table></section>");

        // --- what to work on ------------------------------------------------------
        string advice = BuildAdvice(tasks);
        if (!string.IsNullOrEmpty(advice))
        {
            b.AppendLine("<section class=\"advice\"><h2>What to work on</h2><p>" +
                         Escape(advice) + "</p></section>");
        }

        // --- lifetime -------------------------------------------------------------
        b.AppendLine("<section><h2>Overall progress</h2><table class=\"meta\">");
        Row(b, "Rank", Escape(bank.RankName));
        Row(b, "Lifetime coins earned", bank.LifetimeEarned.ToString());
        Row(b, "Coin balance", bank.Balance.ToString());
        Row(b, "Tests completed", bank.RunsCompleted.ToString());
        Row(b, "Best single run", bank.BestRun.ToString() + " coins");
        Row(b, "Experiments cleared at least once", bank.DistinctReactionsCleared + " of 8");
        b.AppendLine("</table></section>");

        b.AppendLine("<footer>Generated by Atomix. This file is self-contained &mdash; " +
                     "use your browser's Print command to save it as a PDF.</footer>");
        b.AppendLine("</div></body></html>");

        return b.ToString();
    }

    private static void Card(StringBuilder b, string label, string value, string extraClass)
    {
        b.Append("<div class=\"card ").Append(extraClass).Append("\">")
         .Append("<div class=\"card-value\">").Append(Escape(value)).Append("</div>")
         .Append("<div class=\"card-label\">").Append(Escape(label)).Append("</div>")
         .AppendLine("</div>");
    }

    private static void Row(StringBuilder b, string label, string value)
    {
        b.Append("<tr><th>").Append(Escape(label)).Append("</th><td>")
         .Append(value).AppendLine("</td></tr>");
    }

    private static string GradeClass(string grade)
    {
        if (grade == "A" || grade == "B") { return "good"; }
        if (grade == "C" || grade == "D") { return "okay"; }
        return grade == "-" ? "" : "bad";
    }

    private static string ModeName(int mode)
    {
        switch (mode)
        {
            case 0: return "Practical experiments only";
            case 1: return "Theory questions only";
            default: return "Practical experiments and theory questions";
        }
    }

    private static string FormatDuration(float seconds)
    {
        int total = Mathf.Max(0, Mathf.RoundToInt(seconds));
        int minutes = total / 60;
        return minutes > 0
            ? minutes + " min " + (total % 60) + " s"
            : total + " s";
    }

    private static string BuildAdvice(IReadOnlyList<ExamTaskRecord> tasks)
    {
        Dictionary<string, int> reasons = new Dictionary<string, int>();
        int timedOut = 0;
        int theoryWrong = 0;

        for (int i = 0; i < tasks.Count; i++)
        {
            ExamTaskRecord t = tasks[i];
            if (t.Passed)
            {
                continue;
            }

            if (t.outcome == ExamTaskOutcome.TimedOut)
            {
                timedOut++;
            }

            if (t.kind == ExamTaskKind.Theory)
            {
                theoryWrong++;
            }
            else if (!string.IsNullOrEmpty(t.detail))
            {
                int count;
                reasons.TryGetValue(t.detail, out count);
                reasons[t.detail] = count + 1;
            }
        }

        if (reasons.Count == 0 && timedOut == 0 && theoryWrong == 0)
        {
            return tasks.Count > 0
                ? "Nothing to correct - every task in this run was answered correctly."
                : string.Empty;
        }

        StringBuilder b = new StringBuilder();

        if (timedOut > 0)
        {
            b.Append(timedOut == 1
                ? "One task ran out of time. "
                : timedOut + " tasks ran out of time. ");
        }

        if (theoryWrong > 0)
        {
            b.Append(theoryWrong == 1
                ? "One theory question was answered incorrectly. "
                : theoryWrong + " theory questions were answered incorrectly. ");
        }

        string worst = null;
        int worstCount = 0;
        foreach (KeyValuePair<string, int> pair in reasons)
        {
            if (pair.Value > worstCount)
            {
                worst = pair.Key;
                worstCount = pair.Value;
            }
        }

        if (!string.IsNullOrEmpty(worst))
        {
            b.Append("The most common practical mistake was: ").Append(worst).Append(' ');
        }

        b.Append("Ask the lab assistant why a task went wrong - it can read back the exact " +
                 "quantities you used.");

        return b.ToString();
    }

    /// <summary>
    /// Escapes text for HTML. Necessary rather than cosmetic: failure reasons and answer text are
    /// authored strings that contain &lt;, &gt; and &amp; in chemical notation, and an unescaped
    /// one silently swallows the rest of a table cell.
    /// </summary>
    private static string Escape(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;");
    }

    private const string Style =
"<style>" +
":root{--ink:#12161d;--muted:#5c6673;--line:#dfe4ea;--good:#137a4a;--bad:#b02a2a;--okay:#a06a12;--accent:#0b5fa5;}" +
"*{box-sizing:border-box}" +
"body{margin:0;background:#eef1f5;color:var(--ink);" +
"font:15px/1.55 -apple-system,Segoe UI,Roboto,Helvetica,Arial,sans-serif}" +
".page{max-width:940px;margin:32px auto;background:#fff;padding:40px 44px;" +
"border-radius:10px;box-shadow:0 2px 14px rgba(20,30,50,.10)}" +
"header{border-bottom:3px solid var(--accent);padding-bottom:16px;margin-bottom:26px}" +
"h1{margin:0;font-size:25px;letter-spacing:-.2px}" +
"h2{font-size:16px;text-transform:uppercase;letter-spacing:.7px;color:var(--muted);" +
"margin:32px 0 12px}" +
".sub{margin:6px 0 0;color:var(--muted)}" +
".cards{display:flex;gap:14px;flex-wrap:wrap}" +
".card{flex:1 1 150px;border:1px solid var(--line);border-radius:8px;padding:16px;text-align:center}" +
".card-value{font-size:30px;font-weight:700;line-height:1.1}" +
".card-label{font-size:12px;text-transform:uppercase;letter-spacing:.6px;color:var(--muted);" +
"margin-top:6px}" +
".card.good .card-value{color:var(--good)}" +
".card.okay .card-value{color:var(--okay)}" +
".card.bad .card-value{color:var(--bad)}" +
"table{width:100%;border-collapse:collapse}" +
"table.meta th{text-align:left;font-weight:600;color:var(--muted);width:250px;" +
"padding:7px 0;border-bottom:1px solid var(--line);font-size:14px}" +
"table.meta td{padding:7px 0;border-bottom:1px solid var(--line)}" +
"table.tasks{font-size:14px}" +
"table.tasks th{text-align:left;background:#f4f6f9;padding:9px 10px;border-bottom:2px solid var(--line);" +
"font-size:12px;text-transform:uppercase;letter-spacing:.5px;color:var(--muted)}" +
"table.tasks td{padding:10px;border-bottom:1px solid var(--line);vertical-align:top}" +
"table.tasks .num{text-align:right;white-space:nowrap}" +
"td.pass{color:var(--good);font-weight:600}" +
"td.fail{color:var(--bad);font-weight:600}" +
"tr.pass td:first-child{border-left:4px solid var(--good)}" +
"tr.fail td:first-child{border-left:4px solid var(--bad)}" +
".q{color:var(--muted);font-size:13px}" +
".detail{color:var(--muted);font-size:13px;max-width:290px}" +
".help{color:var(--okay);font-size:12px}" +
".advice p{background:#f4f6f9;border-left:4px solid var(--accent);padding:14px 16px;margin:0;" +
"border-radius:0 6px 6px 0}" +
"footer{margin-top:36px;padding-top:16px;border-top:1px solid var(--line);" +
"color:var(--muted);font-size:12px}" +
"@media print{body{background:#fff}.page{box-shadow:none;margin:0;max-width:none;padding:0}}" +
"</style>";
}
