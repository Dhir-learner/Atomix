# Task 6 — Measurement Label Occlusion Fix + UX Polish

**Date:** 2026-09-07
**Branch:** `kushal`
**Status:** ✅ Built — 0 compile errors, and the automated suite is green at **73/73** checks
**Builds on:** Tasks 1–5

---

## The problem you reported

> *"dynamic views of the tab maintaining the amount of chemical poured sometimes block the view of
> the experiment the user is performing"*

That is the **floating measurement label** — the block of text that hovers just above the beaker
showing `H2SO4: 12.4 / 20.0 ml`.

Here is exactly why it got in the way, and it was not a small oversight — it was three things
stacking up:

| # | Cause | Detail |
|---|---|---|
| 1 | **It sits where you look** | The label is anchored 13–22 cm directly above the vessel. That is precisely the spot your eyes go to while pouring. |
| 2 | **It is opaque** | The backing plate is `#0A1020F0` — 94 % opaque. Whatever is behind it is *completely* hidden, not dimmed. |
| 3 | **It never got out of the way** | Leaning in only shrank it (to a floor of 0.45×). It never moved, never faded, and could not be turned off. |

So the closer you leaned in to pour carefully — exactly when you most need to see the glassware —
the more of the beaker it covered.

---

## The fix — four layers

I did not just shrink it. Each layer handles a different situation.

### 1. It lifts out of the way as you lean in

The label now measures how close your camera is to the vessel and rises as you approach.

```
distance >= 0.95 m   ->  sits where it always did
distance <= 0.35 m   ->  lifted a further 12 cm, clear of the working area
in between           ->  smoothly interpolated
```

A detail worth recording: the distance is measured to the **anchor** (the vessel), not to the
label. Measuring to the label would have been a feedback loop — the label's position depends on
the distance, so the distance would chase its own tail and the label would drift or jitter.

### 2. It turns translucent up close

Lifting alone is not enough when the vessel is at eye level. Below the comfort distance the label
fades to **35 % opacity**, so you can read the numbers *and* see the beaker straight through them.
It never fades to nothing — you always keep the reading.

### 3. `L` cycles how much label you want

New key, chosen because `L` was genuinely free (checked against every `KeyCode` in the project —
`G` is the grip pose, `H` help, `F` graphs, `Tab` history, `B` book, `1`–`8` reactions).

| Press | Mode | What you get |
|---|---|---|
| — | **FULL** | Every reagent on its own line, above the vessel. The default. |
| `L` | **COMPACT** | One line — just the reagent you are pouring right now. |
| `L` | **OFF** | Nothing above the bench at all. |
| `L` | back to FULL | |

A short toast confirms each change, so it is discoverable rather than a hidden feature.

**Success and failure messages are always shown in full**, whatever the mode. Those are the payoff
of the experiment; only the running measurements get compacted.

### 4. A permanent readout at the top of the screen

This is the piece that makes turning the label off actually free.

```
H2SO4 + CuO -> CuSO4 + H2O   H2SO4 20.0 ml / 20.0  OK    CuO 6.2 g / 8.0    settling 0.8s / 1.5s
```

- Always visible while an experiment is running, at the **top centre** — clear of the help panel
  (left edge), the assistant panel (right edge) and the crosshair (centre).
- Each reagent turns **green with an `OK`** once it is inside the accepted range.
- It shows the **settle countdown**, which fixes the single most confusing pause in the whole lab:
  every reagent is in, nothing is moving, and the verdict is still 1.5 s away. It used to look like
  the lab had frozen. Now it says so.
- Then it shows **SUCCESS** or **FAILED**.

**Why this matters:** before, the scene's own tracker text only appeared *while you were actively
pouring* — the rest of the time it showed guidance instead. So hiding the floating label would have
left you with no reading at all between pours. Now the numbers simply move to the edge of the
screen instead of disappearing.

---

## Other bugs fixed

### Two world-space panels could stack on top of each other

A real bug, and easy to hit: press `Tab` (history) while the graphs are open and both render at
essentially the same place.

| Panel | Distance from camera | Sort order |
|---|---|---|
| Video / book | 1.5 m | 500 |
| History | 1.5 m | 520 |
| Graphs | 1.6 m | 540 |

All three occupy the same space. Two open at once is an unreadable stack.

**Fixed** with mutual exclusion:
- Opening the history closes the graphs, and vice versa.
- Neither opens while the book/video panel is showing — that one wins, because it is mid-flow.

The normal success sequence is unaffected: the sequencer hides the video *before* it asks for the
graphs, so by then there is nothing to conflict with. I traced that ordering explicitly rather than
assuming it.

### Four per-frame costs that did not need to be paid

All in code that runs **every single frame**. The first two were pre-existing; the last two were in
my own new code and came out of a self-review pass before finishing.

1. **The label rebuilt its text mesh 60× a second.** `Show()` was calling `ResizePanel()`
   unconditionally, and that does a `ForceMeshUpdate()` plus `GetRenderedValues()` — a full text
   re-layout. A reading like `20.4 / 20.0 ml` only changes a few times a second. Now it only
   re-lays-out when the string actually changed.

2. **The fade would have rebuilt vertex colours 60× a second.** Assigning `TMP_Text.color` marks
   the mesh dirty. The new fade only writes when the alpha has actually moved by ≥ 0.01.

   This one needed care: `Show()` also writes the colour, so a naive cache would have made the
   label flash back to full opacity for one frame every time the reading changed. It now writes at
   the current fade level instead.

3. **The new top readout wrote to TMP every frame.** Same fix — only assign when the line changed.

4. **`Scene.name` allocates a string on every access**, and the new HUD consulted it every frame in
   every scene, including the main menu. Now cached and recomputed only on an actual scene change.

The scene-change hook does double duty: it also hides the readout on the way out, so it cannot
follow the player into another scene still showing the last experiment's numbers.

### Discoverability

`L` added to the in-game controls help (`H`), alongside the `Tab`/`F`/`M` entries.

---

## What I checked and deliberately did *not* change

Being explicit, because "fix all bugs" can quietly mean "rewrite things that were fine".

| Thing | Finding | Decision |
|---|---|---|
| **Exam-mode answer leaks** | I suspected the testing scene might leak targets through the AI briefing or the `Tab` history panel. **It does not.** The AI is not enabled in `TestingPhaseLab`, and exam attempts store an empty targets dictionary so the history panel has nothing to reveal. `hideTargets` is honoured by all five text builders. | Already correct — left alone. I added tests locking it in. |
| **~50 unguarded audio calls** | `audioSource_guidance.Stop()` etc. with no null check, across all 8 reactions. | Original Atomix code, safe because the scenes assign those references. 50 edits of churn for zero behavioural gain is how regressions get introduced. **Left alone**, documented. |
| **`transform.Find("Substance")` chains** | Throw if that child is ever renamed. | Same reasoning. Left alone, documented. |
| **Retry does not reset visuals** | Known limitation from Task 2 — re-running an experiment leaves the old material on the beaker. | Genuinely worth fixing, but it means touching the pour scripts and recipient objects, and I have **no way to Play-test** the result. Shipping untested visual changes against "don't break anything" is the wrong trade. **Flagged for a session where you can test it.** |
| **Tooltip material leak** | Suspected the per-tooltip `Material` was never released. | It is — `FreeHandTooltip.Destroy()` exists and all 9 call sites use it. Already correct. |

---

## Verification

### Compiles clean

```
sources 106, references 342 -> errors=0
```

No new warnings. The only ones present are pre-existing (`CS0649`, `CS0414`, `CS0618`).

### Automated suite: 73/73

The suite runs the **real `FreeHandReactionEngine`**, extracted verbatim from the shipped source
(its only Unity dependency is `Time.deltaTime`, which a one-field shim supplies). It is not a
re-implementation — it is the shipped judging code.

```
R2..R8   success / overdose / underdose / wrong order / pause-mid-procedure / accept-window   (55)
5C       R1 and engine failure messages have identical shape                                   (7)
NEW      compact label picks the right reagent in every situation                              (6)
NEW      top-of-screen readout, including exam-mode leak guard and settle state                (5)
                                                                                        ALL 73 PASSED
```

**Three of the new tests failed on first run, and all three were the test's fault, not the code's** —
worth recording because each nearly became a false bug report:

1. I poured 100 ml instead of 20 ml, which tripped the overdose failure and froze the engine before
   the second reagent ever started. The compact label was correctly showing the *offending*
   reagent.
2. I asserted exam mode must not contain `"20.0"` — but that was the student's **own measured
   amount**, which they are entitled to see. The real leak signal is the `/ target` separator, so
   the assertion now checks that instead.
3. (From Task 5, re-confirmed here.) The order-group default is `-1`, not `0`.

### Not verified — needs a Play-mode pass

I cannot run the Unity Editor from here, so these are reasoned but untested:

- **The exact feel of the lift and fade.** `comfortDistance` (0.95 m), `nearDistance` (0.35 m),
  `extraLiftWhenClose` (0.12 m) and `minProximityAlpha` (0.35) are educated numbers derived from
  the existing anchor offsets and TMP's world-space scaling. **Every one is a public field**, so
  tuning needs no code change.
- The top readout's size and position at your resolution (`measurementFontSize`, and the plate rect
  in `LabHudController`).
- That the `L` toast reads well in-game.

---

## Files changed

```
NEW  VR/Assets/Scripts/LabHudController.cs   (+ .meta)   L key, toast, top-of-screen readout
MOD  VR/Assets/Scripts/FreeHandReactionEngine.cs         lift + fade + display modes,
                                                         GetCompactTooltipText, GetHudText,
                                                         two per-frame optimisations
MOD  VR/Assets/Scripts/ExperimentHistoryManager.cs       bootstrap the HUD controller
MOD  VR/Assets/Scripts/ExperimentHistoryUI.cs            panel mutual exclusion
MOD  VR/Assets/Scripts/ReactionGraphUI.cs                panel mutual exclusion
MOD  VR/Assets/Scripts/ReactionLearningController.cs     IsAnyPanelVisible
MOD  VR/Assets/Scripts/ControlsHelpUI.cs                 document the L key
```

**No scene, prefab, or reaction script was modified.** Only four lines were removed from the engine
in total, each replaced by a superset of its old behaviour:

```
- tooltipObject.transform.position = anchor.position + Vector3.up * heightOffset;
      -> same, plus the proximity lift (and preserved verbatim in the no-camera fallback)
- float distance = Vector3.Distance(tooltipObject.transform.position, camera...);
      -> measured to the anchor instead, which removes the feedback loop
- tooltipText.color = color;
      -> same colour, written at the current fade level
- Show(ProgressColor, engine.GetTooltipText());
      -> same, unless the student has chosen compact
```

The `VR/Packages/manifest.json` change in your working tree is **yours** (the Unity MCP package) —
I left it untouched.

---

## Tuning cheat-sheet

Everything below is an Inspector field. No code change needed.

| Field | Where | Default | Effect |
|---|---|---|---|
| `comfortDistance` | `FreeHandTooltip` | 0.95 m | Beyond this the label behaves exactly as before |
| `nearDistance` | `FreeHandTooltip` | 0.35 m | Fully lifted and faded at this distance |
| `extraLiftWhenClose` | `FreeHandTooltip` | 0.12 m | How far it rises. Raise if it still overlaps |
| `minProximityAlpha` | `FreeHandTooltip` | 0.35 | Lower = more see-through up close |
| `tooltipHeightOffset` | every reaction | 0.13–0.22 m | Base height above the vessel |
| `tooltipFontSize` | every reaction | 0.55 | ≈ `fontSize × 0.12` metres per line |
| `cycleLabelKey` | `LabHudController` | `L` | The cycle key |
| `showMeasurementHud` | `LabHudController` | on | Turns the top readout off entirely |
| `measurementFontSize` | `LabHudController` | 24 | Readout text size |

**If the label still bothers you after a Play-test, the quickest wins in order:** raise
`extraLiftWhenClose` to ~0.20, then drop `minProximityAlpha` to ~0.2, then just leave it on
COMPACT — the top readout carries the numbers regardless.

---

## Where the project stands

All ten original features remain built and working. This task was polish and correctness on top of
them, not new features.

**Still outstanding, unchanged from Task 5:**

1. **Convai credentials** — `Resources/LabAssistantSettings.asset` ships empty, so the AI assistant
   cannot be demonstrated. This is the last thing standing between the project and a complete
   end-to-end demo, and it is a data entry job, not a code job.
2. **A Play-mode pass** over the whole thing — rendering, audio, particles and the new label
   behaviour.
3. **Retry visual reset** (Task 2 limitation) — deliberately deferred, see the table above.

---

*Generated on completing the polish pass.*
