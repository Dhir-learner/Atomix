# Task 9 — Coin Economy, Exam Records and a Downloadable Test Report

**Date:** 2026-09-08
**Branch:** `main`
**Status:** ✅ Built — 0 compile errors, 0 new warnings, **226/226 checks pass** running the real code
**Builds on:** Tasks 1–8
**Brief:** *"in test scene there is coin system … it every time reset so make something with it … I also want a separate report for experiments performed in test scene and that we can download … do enhancement, give your best"*

---

## 1. What the coin system actually was

`CountdownTimer` held the score in a plain field:

```csharp
int score;

void Start()
{
    currentTime = startingTime;
    score = 0;          // <- every scene load
    scoreText.text = "0";
}
```

The testing scene reloads on every *"Run the test again"*, so the number went back to zero each
time. It was a counter, not a score: nothing carried between runs, nothing accumulated, nothing
could be spent, and finishing a run left no trace of itself anywhere.

### And it had a bug that made half of it unreachable

```csharp
if      (currentTime >= 30) { score += 100; }
else if (currentTime >= 30) { score += 50; }    // can never be true
else if (currentTime >= 20) { score += 40; }
```

The second test duplicates the first. **The 50-point tier was dead code**, and every task finished
with more than 30 seconds left scored the maximum 100 — so finishing with 31 seconds left and
finishing with 59 paid exactly the same. The point values are the original author's; only the top
threshold is changed, to 45 s, which is the minimum edit that makes the 50 tier reachable again.

The verification suite now pins **all seven tiers as reachable** and asserts the curve is monotonic,
which is precisely the check that would have caught the original bug.

---

## 2. What the coins do now

Three jobs, so the number is worth watching:

### They persist — `AtomixCoinBank.cs` (309 lines)

Lifetime earnings, spending, best run, runs completed, best streak and a bitmask of which of the
eight experiments have ever been cleared, all in PlayerPrefs (matching `AtomixSettings`; the
experiment history already owns the JSON file for things that need one).

### They rank you

| Lifetime coins | Rank |
|---|---|
| 0 | Apprentice |
| 500 | Lab Technician |
| 1 500 | Chemist |
| 3 500 | Senior Chemist |
| 7 000 | Lab Master |

Shown on the report card and in the pause menu's Achievements tab — the one place outside a test
where a student can see where they stand.

### They can be spent — `ExamCoinHud.cs` (406 lines)

| Key | Buys | Cost |
|---|---|---|
| **F2** | A hint for the current experiment | 40 |
| **F3** | 30 more seconds on the clock | 60 |
| **F4** | Skip this task | 100 |

This is what turns the score from a number you watch into a decision you make: spend it now, or
bank it toward the next rank.

**The hint deliberately does not name a quantity.** It returns the *procedure* —
`ChemistryKnowledgeBase.ProcedureHint` — because knowing the right amount is exactly what the
testing scene exists to measure, and selling that would be selling the answer. There is a test
asserting no hint contains "ml", "gram" or a bare " g ".

Hints are also refused on theory questions, where a hint would just be the answer.

### New bonuses, so a good run pays better than a slow one

| Bonus | Worth |
|---|---|
| First time ever clearing an experiment | +25 |
| 3 correct in a row | +30 |
| 5 correct in a row | +60 |
| Finishing a run perfectly **without buying help** | +50 |

Streak bonuses fire on the exact task that reaches the threshold, never repeatedly — there are
tests for streak 4 and streak 6 paying nothing extra, because "bonus repeats forever" is the
obvious way to get this wrong.

Every award is announced on screen with its reason (`+90 coins  (finished with 47s to spare)
+streak x3 bonus`). Coins that appear without explanation are just a number going up.

---

## 3. A bug that made the report card blind

`TheoreticalTasksManager` recorded **nothing at all**. It set `countdown.wasScored` and moved on.
Practical reactions reached the report card through `ReactionHistoryRecorder`; theory questions
reached it through nothing.

So with the **Theory only** setting — a real, selectable mode, `StaticData.includedTasksValue == 1`
— a student could answer ten questions and be told:

> *"No graded experiments were recorded in this run."*

And in mixed mode the report card said "passed 4 of 4" on a run where six tasks were set, because
the two it could not see simply were not counted.

### `ExamSession.cs` (492 lines) — one record for the whole run

Everything in a run now lands in one place, and both the report card and the exported file read
from it, so the screen and the file cannot disagree.

| Source | Now files |
|---|---|
| `ExamReactionRunner.CompleteSuccess()` | a passed practical (all 8 test scripts route through it) |
| `ExamReactionRunner.HandleFailure()` | a failed practical, with the engine's own failure headline |
| `TheoreticalTasksManager.RecordAnswer()` | a theory question, with the question text and the answer chosen |
| `ExamCoinHud.BuySkip()` | a skipped task |
| `ExamSessionTracker.FlushUnfinishedTask()` | **a task that timed out** |

That last one matters. A task the student never finished used to vanish from the record entirely.
It is a result, and it belongs in the report — the tracker watches
`Randomize.currentReactionInTestPhase` and, if the task changes with nothing filed for the old one,
files a timeout for it.

---

## 4. The downloadable report — `ExamReportExporter.cs` (501 lines)

Two things separate this from the existing `LabReportExporter`:

**Scope.** `LabReportExporter` dumps the entire experiment history from every session in the
free-practice lab. This is *one exam*: the tasks of this run, in order, practical and theoretical,
with the coins each earned and any help bought.

**Where it lands.** `LabReportExporter` writes to `Application.persistentDataPath`, which on Windows
is `AppData\LocalLow\<company>\<product>` — a hidden folder most students will never find. This
writes to the **Desktop**, falling back to Documents and then to `persistentDataPath`, so "download
my report" produces a file that is simply *there*.

Each candidate folder is **probed for writability**, not just existence — a lab machine can have a
redirected or read-only Desktop, and the alternative is failing at the last step with a permissions
error the student cannot act on.

### Two files

- **`Atomix_Test_Report_<date>_<time>.html`** — the one meant to be handed in. Self-contained,
  styled, needs no network, and prints cleanly to PDF from any browser. Headline cards for grade,
  passed/total, coins and time; a table of every task with its question, result, what went wrong,
  time and coins; a "what to work on" section; and lifetime progress.
- **`…​.csv`** — the same rows for a marker who wants them in a spreadsheet. Every field is quoted,
  because chemistry names contain commas and failure reasons contain both commas and quotes.

Buttons on the report card: **Download this test report**, **Open folder** (opens the file browser
straight at it), **Full history**, **Run the test again**, **Main menu**.

HTML output is escaped. That is necessary rather than cosmetic: failure reasons and answer text
contain `<`, `>` and `&` in chemical notation, and one unescaped `<` silently swallows the rest of a
table cell.

---

## 5. File manifest

### Created

```
NEW  VR/Assets/Scripts/AtomixCoinBank.cs        309   persistent wallet, ranks, prices, first-clears
NEW  VR/Assets/Scripts/ExamSession.cs           492   one run's record + the tracker that opens/closes it
NEW  VR/Assets/Scripts/ExamReportExporter.cs    501   HTML + CSV to the Desktop
NEW  VR/Assets/Scripts/ExamCoinHud.cs           406   coin strip and the F2/F3/F4 help shop
                                                ----
                                                1708   (+ 4 .meta files)
```

### Modified

```
MOD  TestResultsUI.cs           +196  reads ExamSession, shows coins/rank, download buttons
MOD  CountdownTimer.cs           +75  score now comes from the bank; dead tier removed
MOD  TheoreticalTasksManager.cs  +30  records every answer  <- the blindness fix
MOD  PauseMenuUI.cs              +32  wallet and rank in the Achievements tab
MOD  ExamReactionRunner.cs       +16  files each practical outcome
MOD  ExperimentHistoryManager.cs +13  bootstraps the tracker and the HUD
MOD  ChemistryKnowledgeBase.cs   +21  ProcedureHint, for the paid hint
MOD  ControlsHelpUI.cs            +5  documents F2/F3/F4
```

No `.unity` scene, prefab, or `ProjectSettings` asset was touched — same as Tasks 1–8. `F2`–`F4`
were checked against every `KeyCode` in the project before use.

---

## 6. Verification

```
0 Error(s)
2 Warning(s)   <- both pre-existing, identical to the baseline
```

**226 checks pass** running the real code out of `Assembly-CSharp.dll` (167 from Task 8, 59 new):

```
=== Molecular animation catalog ===
=== Assistant intent classification ===
=== Coin economy and exam records ===

226 passed, 0 failed
```

To make the coin maths testable at all, the award calculation was split out of `AwardForPass` into
a pure `ExamSession.ComputeAward(secondsRemaining, streak, firstClear)` with no PlayerPrefs in it.
That is not incidental — **the original tier table shipped with an unreachable branch precisely
because nothing could ever run it.**

What the new checks enforce:

| Check | Why |
|---|---|
| Every tier boundary, both sides (45/44, 30/29, 20/19, 15/14, 10/9, 5/4) | The bug was an off-by-one-branch at exactly such a boundary |
| All 7 tiers reachable across 0–60 s | The exact assertion that fails on the original code |
| More time left never pays fewer coins | A tier table can be made non-monotonic by a single typo |
| Streak bonus at 3 and 5 only, not 4 or 6 | "Bonus repeats forever" is the obvious way to get this wrong |
| Bonuses stack correctly | Streak + first clear on the same task |
| Prices ordered hint < time < skip | A skip cheaper than a hint would make hints pointless |
| All 18 task ids have a real name | A missing name would print "Task 13" in a handed-in report |
| No paid hint names a quantity | Selling the answer is the failure mode that matters here |

---

## 7. New controls

| Key | Does |
|---|---|
| **F2** | Buy a hint — 40 coins (practical experiments only) |
| **F3** | Buy 30 more seconds — 60 coins |
| **F4** | Skip the current task — 100 coins |

---

## 8. What this does not do

- **Nothing was rendered in a running Unity Editor.** Unity's own log shows a clean import of
  everything up to `AtomixCoinBank.cs` with zero `error CS`; the four files written after that
  point are covered by the dotnet build against the real Unity assemblies and the 226 logic checks,
  not by a Unity import. Worth one play-through of the testing scene to confirm the HUD framing.
- **The coin HUD is keyboard-driven.** The testing scene keeps the cursor locked for the crosshair,
  so F2/F3/F4 are keys rather than clickable buttons — the same choice every runtime panel since
  Task 1 has made.
- **Coins are per-machine.** PlayerPrefs is local; there is no account or leaderboard.
