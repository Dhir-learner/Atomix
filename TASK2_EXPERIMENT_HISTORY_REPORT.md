# Task 2 — Experiment History System

**Date:** 2026-09-06
**Branch:** `kushal`
**Status:** ✅ Built — compiles clean against the real Unity 6000.3.7f1 assemblies (0 errors)
**Builds on:** Task 1 (`TASK1_FREEHAND_ENGINE_REPORT.md`)

---

## 1. Two decisions you made before I started

| Question | Your answer | Why it mattered |
|---|---|---|
| Toggle key | **Tab** | The task suggested `H`, but `H` is already bound to the Controls Help overlay (`ControlsHelpUI.toggleKey`). Tab was free. |
| Retry behaviour | **Allow retries** | Re-selecting an experiment used to leave the Task 1 engine in its finished state, so you got at most one attempt per reaction per session — a very thin history. |

---

## 2. What was built

### 2A. `ExperimentHistoryManager.cs` (NEW)

Singleton MonoBehaviour that survives scene changes — necessary because the lab and the AI
assistant live in **different scenes** (`LabScene` vs `LabAssistantScene`).

**It creates itself.** A `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]` bootstrap builds the
manager, the UI and the AI bridge on a `DontDestroyOnLoad` object, so **no scene or prefab edits
were needed** — consistent with the Task 1 constraint.

**Data structures** (all `[Serializable]`):

| Type | Fields |
|---|---|
| `ExperimentAttempt` | `attemptId` (GUID), `reactionId`, `reactionName`, timestamp, `durationSeconds`, `outcome`, `quantitiesUsed`, `targetQuantities`, `steps`, `aiInteractions` |
| `ExperimentStep` | `timestamp` (s since attempt start), `action`, `quantityAtStep`, `wasCorrect` |
| `AIInteraction` | `timestamp`, `userQuestion`, `aiResponse` |
| `ExperimentOutcome` | `InProgress, Success, FailOverdose, FailUnderdose, FailWrongOrder, FailTimeout, Abandoned` |

**Two serialization problems the spec's shape runs into, and how they're solved:**

- **`Dictionary<string,float>` is not serializable by `JsonUtility`.** The dictionaries remain the
  runtime API exactly as specified, but `ExperimentAttempt` implements
  `ISerializationCallbackReceiver` and mirrors them into `List<QuantityEntry>` on save, rebuilding
  them on load. The spec's API is preserved; the file still round-trips.
- **`DateTime` is not serializable by `JsonUtility` either.** Stored as a round-trip ISO-8601
  string (`timestampIso`) with a `DateTime Timestamp` property over it.

**Methods** — all as specified, plus a few the spec implied but did not name:

```
StartAttempt(reactionId, reactionName) -> attemptId
LogStep(attemptId, action, quantity, wasCorrect)
LogAIInteraction(attemptId, question, response)
EndAttempt(attemptId, outcome)
GetHistory() / GetHistoryForReaction(reactionId)
SaveToJson() / LoadFromJson()          // Application.persistentDataPath
RecordQuantities(attemptId, used, targets)   // fills the two dictionaries
GetAttemptCount(reactionId) / GetRecordedReactionIds() / ClearHistory()
```

Details worth knowing:
- Save file: `Application.persistentDataPath/atomix_experiment_history.json`, written on every
  attempt end and on quit/pause.
- `maxStoredAttempts` (300) drops the oldest rows so the file cannot grow without bound.
- An attempt left `InProgress` by a crash is re-labelled `Abandoned` on load rather than lingering.
- `LogAIInteraction` falls back to the **most recent** attempt when no attempt is running —
  students almost always ask the assistant *after* an experiment has already failed.

**`ReactionHistoryRecorder`** (same file) is the per-reaction glue that keeps 2C small. It owns the
attempt lifecycle for one reaction script, and its `Tick()` turns the Task 1 engine's state into
readable steps by watching **pour start/stop edges** rather than writing one row per frame:

```
2.4s  OK  Started pouring H2SO4
6.1s  OK  Added 19.6 ml of H2SO4 (target 20.0)
9.0s  !!  Added 5.2 g of CuO (target 8.0)
10.5s !!  Why it failed: Insufficient CuO leaves unreacted acid, so the solution
          stays strongly acidic instead of turning blue.
```

Heating experiments phrase themselves correctly ("Held the sample over the flame" / "Removed from
the flame after 7.3 s") off the `"s"` unit rather than needing special-casing per reaction.

### 2B. `ExperimentHistoryUI.cs` (NEW)

WorldSpace canvas built from code in the same style as `ReactionLearningController` (same
`RenderMode.WorldSpace`, same 0.001 scale, same 1.5 m-in-front-of-camera placement, same
button/text helpers), so it is clickable with the **existing crosshair interaction** in
`ObjectInteraction.TryUiInteraction()` — no new input plumbing.

- **Tab** opens/closes, **Esc** closes.
- Scrollable list of every attempt, newest first.
- **Filter row**: `All`, then one button per reaction id that actually appears in the history.
- **Click a row to expand it** in place — quantities vs targets, the full step list with OK/`!!`
  markers, and any AI questions. A single expanding column rather than two panes, which avoids
  ambiguity about which pane the scroll wheel is driving.
- Colour-coded: green success, red failure, grey abandoned.
- Footer shows the save-file path; a `Clear History` button wipes it.
- Scrolling is driven manually from `Input.mouseScrollDelta` and the arrow keys, because the OS
  cursor stays **locked** for crosshair aiming and a `ScrollRect` never receives a pointer in that
  state.

### 2C. Wired into all 8 reaction scripts

Each reaction gained an `[Header("Experiment History")]` block — `reactionId`,
`reactionDisplayName`, `restartAttemptOnReSelect` — and calls:

| Hook | Where |
|---|---|
| `BeginIfNeeded()` | lazily, on the first real action (via `Tick`/`LogAction`) |
| `Tick()` | each frame after `engine.CheckReactionOutcome()` |
| `Complete(result)` | in the existing success block and the existing failure block |
| `Abandon()` | `OnDisable` — i.e. switching to another experiment mid-run |
| `RestartAttemptIfRequested()` | `OnEnable` — the retry behaviour you asked for |

| Id | Script | Logged name |
|---|---|---|
| 1 | `Reaction.cs` | Na + H2O -> NaOH + H2 |
| 2 | `Reaction_h2so4_cuo.cs` | H2SO4 + CuO -> CuSO4 + H2O |
| 3 | `Reaction_hcl_nahco3.cs` | HCl + NaHCO3 -> NaCl + H2O + CO2 |
| 4 | `KOHReaction.cs` | K + H2O -> KOH + H2 |
| 5 | `ReactionAli3.cs` | 2Al + 3I2 -> 2AlI3 |
| 6 | `reactionCaOH.cs` | CaO + H2O -> Ca(OH)2 |
| 7 | `CaCO3Reaction.cs` | CaCO3 -> CaO + CO2 |
| 8 | `Feso4Reaction.cs` | 2FeSO4 -> Fe2O3 + SO2 + SO3 |

**R1 is the odd one out.** `Reaction.cs` tracks its quantities inline rather than through
`FreeHandReactionEngine`, so its recorder is fed by hand: explicit pour-edge logging, an
`Added 5.0 g of Sodium` step for the discrete block, and a `CompleteAttempt()` helper that writes
the quantities before closing the attempt.

**Extra one-off steps** were wired where the scripts already had a clean one-shot gate:
"Attached the balloon to the test tube" and "Lit the Bunsen burner" (R7/R8), "Added phenolphthalein
indicator" (R1).

**Retry support** (`RestartAttemptIfRequested`): on re-enable, closes any open attempt, calls
`engine.Reset()`, and clears that script's one-shot gates so guidance and success/failure can fire
again. It deliberately **returns early when `engine == null`**, because `OnEnable` also runs before
`Start()` on the very first activation — without that guard the first selection would misfire.

### 2D. `LabAssistantHistoryBridge.cs` (NEW — covers the 4th acceptance criterion)

The spec listed `LogAIInteraction` as an API, but "AI interactions are logged alongside their
associated experiment" is only true if something actually calls it. This subscribes to Inworld's
`InworldCharacter.Event.onPacketReceived` and records both sides:

- `SourceType.PLAYER` + `final` → the question
- `SourceType.AGENT` → reply chunks, stitched together and flushed after 2 s of silence (or when
  the next question arrives)

It rescans for characters every 2 s because the assistant lives in another scene, and every access
is null-guarded — if the Inworld session never connects, it simply records nothing.

### 2E. Small addition to Task 1's engine

`FreeHandReactionEngine` gained read-only helpers the recorder needs — `IsPouring(name)`,
`IsWithinTolerance(name)`, `GetQuantitiesSnapshot()`, `GetTargetsSnapshot()`. No behaviour change.

---

## 3. Not modified

- `ControlReactions.cs`, `FlipPages.cs`, `ReactionLearningController.cs`
- Any `.unity` scene or prefab
- `ControlsHelpUI.cs` — kept `H`, which is why history is on Tab

---

## 4. Verification

Compiled the **actual** `Assembly-CSharp` assembly (all gameplay scripts) against the project's
real Unity 6000.3.7f1 + TextMeshPro + XR + Inworld references:

```
Build succeeded.
    0 Error(s)
```

The only warning is pre-existing, in `ReactionCaOHTest.cs`, which I did not touch.

**Three issues found and fixed during review, before you see them:**

1. **`CharacterEvents` lives in `Inworld.Entities`, not `Inworld`** — caught by the compiler.
2. **A forced failure with no prior pour logged nothing.** R7 fails the instant you heat CaCO3
   without the balloon, before any pour edge exists, so `Complete()` bailed on "attempt never
   started". It now opens the attempt for a real verdict, while still refusing to log an empty row
   for an untouched experiment that is merely abandoned.
3. **`Destroy()` is deferred to end of frame**, so rebuilt list rows briefly coexisted with the old
   ones inside the layout group. Rows are now unparented before being destroyed.

**Not verified — needs a Play-mode pass.** I have not run the Unity Editor from this session, so
the following are reasoned but untested:

- The panel's on-screen layout and readability at 1.5 m (all sizes are Inspector fields).
- That crosshair clicks land on the rows and filter buttons as they do on the learning UI.
- The JSON round-trip on a real save file.
- That `OnEnable`/`OnDisable` genuinely fire per experiment — this depends on `ControlReactions`
  toggling the GameObjects that carry these scripts. Task 1's tooltips already rely on the same
  assumption, so if tooltips show/hide correctly per experiment, the history hooks fire correctly
  too. **This is the one thing worth checking first**, since retries and abandon-logging both hang
  off it.

### Files changed

```
NEW  VR/Assets/Scripts/ExperimentHistoryManager.cs      (+ .meta)   2A
NEW  VR/Assets/Scripts/ExperimentHistoryUI.cs           (+ .meta)   2B
NEW  VR/Assets/Scripts/LabAssistantHistoryBridge.cs     (+ .meta)   2D
MOD  VR/Assets/Scripts/FreeHandReactionEngine.cs                    2E
MOD  VR/Assets/Scripts/Reaction.cs, Reaction_h2so4_cuo.cs,
     Reaction_hcl_nahco3.cs, KOHReaction.cs, ReactionAli3.cs,
     reactionCaOH.cs, CaCO3Reaction.cs, Feso4Reaction.cs           2C
```

---

## 5. Acceptance criteria

| Criterion | Status |
|---|---|
| Every attempt logged with full detail (quantities, timing, steps, outcome) | ✅ all 8 reactions |
| History persists across play sessions (JSON in `persistentDataPath`) | ✅ code complete, round-trip untested at runtime |
| In-lab UI allows reviewing past experiments | ✅ Tab, filterable, expandable |
| AI interactions logged alongside their experiment | ✅ via the Inworld bridge |
| No regressions in existing experiment behavior | ✅ all hooks are additive and null-guarded; the retry reset is opt-out per reaction |

---

## 6. Known limitations

1. **Retry resets logic, not visuals.** `RestartAttemptIfRequested` clears the measured quantities
   and gates, but the beaker still shows the substance material from the previous run and the pour
   scripts' `containsX` flags stay true. In free-hand mode success is driven by the engine, so the
   experiment is genuinely re-runnable — it just doesn't look freshly washed. Fully resetting the
   visuals would mean touching the pour scripts and the recipient objects, which is a larger change
   with real regression risk. Set `restartAttemptOnReSelect = false` per reaction to opt out.
2. **History requires free-hand mode.** With `enableFreeHandMode = false` the engine is never
   built, so nothing is recorded for R2–R8. R1 is unaffected (its recorder does not need an engine).
3. **AI logging pairs by timing, not by conversation id.** A question and the reply that follows it
   are matched by sequence and a 2 s silence window. That is right in normal use but could mis-pair
   if you talk over the assistant.

---

## 7. Where the project stands now

| # | Feature (from your project description) | State |
|---|---|---|
| 1 | Interactive 3D chemistry lab | ✅ |
| 2 | Free-hand experimentation | ✅ Task 1 |
| 3 | Realistic success/failure | ✅ Task 1 |
| 4 | AI assistant explains what went wrong | ⚠️ **Still the main gap** — the assistant now *records* conversations, but nothing *feeds it* the failure context. `GetFailureExplanation()` and the attempt history are both sitting there ready to be injected as a prompt. |
| 5 | AI replies in text and voice | ✅ Inworld |
| 6 | Molecular video after success | ✅ |
| 7 | Follow-up doubts after the animation | ⚠️ Same gap as #4 |
| 8 | Scientific graphs (energy, exo/endothermic, entropy) | ❌ Not started |
| 9 | AI explains the graphs verbally | ❌ Not started (depends on #8) |
| 10 | Experiment history (attempts, mistakes, corrections) | ✅ **This task** |

**Suggested next task:** the AI context bridge (#4/#7). It is now a small piece of glue — the
failure explanations from Task 1 and the attempt history from Task 2 are exactly the material the
assistant needs — and it unlocks two of the three remaining features. The graphs (#8/#9) are the
larger remaining build.

---

*Generated on completing Task 2. Session limit was not reached during this task.*
