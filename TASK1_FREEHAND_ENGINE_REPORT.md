# Task 1 — Free-Hand Experiment Engine & Conversion of 7 Fixed Reactions

**Date:** 2026-09-05
**Branch:** `kushal`
**Status:** ✅ Complete — compiles clean against the real Unity 6000.3.7f1 assemblies (0 errors)

---

## 1. What was built

### 1A. `FreeHandReactionEngine.cs` (NEW)

`VR/Assets/Scripts/FreeHandReactionEngine.cs` — a plain C# helper (not a MonoBehaviour), so
any reaction script can own one without touching scenes or prefabs.

**Public state (as specified):**

| Member | Type | Purpose |
|---|---|---|
| `targetQuantities` | `Dictionary<string,float>` | e.g. `"H2SO4" → 20.0` |
| `currentQuantities` | `Dictionary<string,float>` | live measured amount |
| `tolerancePercent` | `float` (default 5) | accepted deviation |
| `settleTimeRequired` | `float` (default 1.5s) | pause before judging |
| `reactionState` | `enum FreeHandReactionState` | `WaitingForInputs / Pouring / Settling / Success / Failed` |
| `failureReason` | `string` | chemistry-accurate reason |

**Methods:**

- `AddSubstance(name, target, unit, tolerance, overdose, underdose, orderMatters, orderGroup)` — registers one measurable input; registration order is the expected procedure order.
- `UpdatePouringQuantity(substanceName, mlPerSecond, isPouring)` — call every frame per input.
- `AddDiscreteQuantity(name, amount)` / `SetQuantity(name, amount)` — for solid blocks dropped in.
- `CheckReactionOutcome()` → `ReactionResult { InProgress, Success, FailOverdose, FailUnderdose, FailWrongOrder }`
- `ForceFailure(name, reason, result, tooltipHeadline)` — for failures the engine cannot observe (e.g. heating before the balloon is fitted).
- `GetTrackerText()` — multi-line canvas text with live measurements + accepted range.
- `GetTooltipText()` / `GetFailureHeadline()` — compact text for the floating world-space tooltip.
- `GetFailureExplanation()` — full chemistry explanation, always ending with *"Ask your AI Lab Assistant what went wrong and how to correct it."*
- `Reset()`

**Judging logic (matches `Reaction.cs`):**
1. **Wrong order** → checked first, using order groups (reagents sharing a group are interchangeable).
2. **Overdose** → detected the instant it happens, even mid-pour.
3. **Underdose** → only judged after *every* reagent is in and the mixture has settled for `settleTimeRequired`, so pausing between reagents never fails you unfairly.
4. **Success** → all reagents within `[target·(1−tol), target·(1+tol)]` after settling.

**Bonus helper — `FreeHandTooltip`** (same file): builds the floating `TextMeshPro` label from
code, puts it on layer 2 (Ignore Raycast) so it can never block pointer grabs, billboards it to
`Camera.main`, and colours it cyan → green (success) → red (failure).

### 1B. `IsPouring` added to pour scripts

Added `public bool IsPouring { get { return play; } }` to:

- `PourHCL.cs`
- `PourCuO.cs`
- `PourH2so4.cs`
- `PourNahco3.cs`
- `PourFromPipette.cs`

The existing `containsX` booleans are **untouched** — the new property sits alongside them for
full backward compatibility. (`PourSubstance.cs` and `PourMetalSubstance.cs` already had it.)

### 1C. All 7 reactions converted

Every script keeps its original code path behind a `enableFreeHandMode` toggle (default **on**),
exactly like `enableIntelligentMode` in the reference `Reaction.cs`. Turning the toggle off in the
Inspector restores the old fixed behaviour byte-for-byte.

| # | Script | Targets | Tol. | Flow rate | Order enforced |
|---|---|---|---|---|---|
| R2 | `Reaction_h2so4_cuo.cs` | H₂SO₄ 20 ml, CuO 8 g | ±8% | 5 ml/s, 2 g/s | acid → CuO |
| R3 | `Reaction_hcl_nahco3.cs` | HCl 15 ml, NaHCO₃ 12 g | ±10% | 5 ml/s, 3 g/s | acid → bicarbonate |
| R4 | `KOHReaction.cs` | H₂O 50 ml, K 3 g | ±5% | 10 ml/s, see note | water → potassium |
| R5 | `ReactionAli3.cs` | Al 5 g, I₂ 15 g, H₂O 2 ml | ±10% | 1.25 g/s, 3.75 g/s, 0.7 ml/s | Al & I₂ (either order) → water |
| R6 | `reactionCaOH.cs` | H₂O 30 ml, CaO 15 g | ±8% | 5 ml/s, 3 g/s | water → CaO |
| R7 | `CaCO3Reaction.cs` | Heating 10 s | ±15% | 1 s/s over flame | balloon fitted before heating |
| R8 | `Feso4Reaction.cs` | Heating 10 s (`heatingDuration`) | ±15% | 1 s/s over flame | — |

**Note on R4 (potassium):** the first tip of the container drops one correctly-measured lump
(`SetQuantity` = 3 g). Keeping the container tipped past `potassiumPourGraceSeconds` (2 s) keeps
metal falling in at 1 g/s → overdose → *"Too much potassium causes a dangerous explosion."*
This is the only way to make a discrete solid overdose-able, and it mirrors the real mistake.

**Note on R7/R8 (heating):** heat accumulates only while the tube is over a **lit** burner. Pull it
out too early → after the 1.5 s settle the experiment fails as underdose; leave it in past 11.5 s →
fails as overdose. R7 additionally fails if you heat before fitting the balloon (the CO₂ escapes).
The R7 balloon inflation and the R8 colour gradient are now driven by the *measured* heating
progress instead of a fixed 10-second ramp, so they stay visually correct.

**Every converted reaction now has:**
- A floating `TextMeshPro` tooltip above the target container showing live measurements, turning green on success and red with the specific failure on failure.
- `canvasText` showing the real-time `[Lab Measurement Tracker]` / `[Lab Heating Tracker]` while pouring or heating, including the accepted min–max range and the settle countdown.
- Chemistry-accurate failure text that ends by pointing the user at the AI assistant.
- Optional `audioSource_failure` / `clip_failure` fields (unassigned = silently skipped, so no scene edits are required).

### 1D. Not modified (as required)

- `ControlReactions.cs` — `StartReaction1..8` flow untouched
- `FlipPages.cs`
- `ReactionLearningController.cs` (video playback)
- Any `.unity` scene YAML or prefab

---

## 2. Verification

Type-checked by compiling the **actual** `Assembly-CSharp` assembly (all 98 gameplay scripts)
against the project's real Unity 6000.3.7f1 + TextMeshPro + XR + Inworld references via a
temporary copy of `Assembly-CSharp.csproj`:

```
Build succeeded.
    0 Error(s)
```

The temporary csproj was deleted afterwards. One real bug was caught and fixed during this pass
(a coroutine accidentally dropped from `reactionCaOH.cs`), plus two issues found on review:
the success message being overwritten by the tracker text while the tube stayed over the flame
(R7/R8), and pour flow rates that made the accepted window only ~0.3 s wide — halved so the
windows are now ~0.6–1.0 s of careful pouring.

**Not verified:** the project has not been run in the Unity Editor from this session, so in-game
tooltip placement (`tooltipHeightOffset` per reaction) and the exact feel of the pour windows
should be checked in Play mode and nudged in the Inspector if needed. All of those values are
public fields precisely so they can be tuned without code changes.

### Files changed

```
NEW  VR/Assets/Scripts/FreeHandReactionEngine.cs (+ .cs.meta)
MOD  VR/Assets/Scripts/PourHCL.cs, PourCuO.cs, PourH2so4.cs, PourNahco3.cs, PourFromPipette.cs
MOD  VR/Assets/Scripts/Reaction_h2so4_cuo.cs, Reaction_hcl_nahco3.cs, KOHReaction.cs,
     ReactionAli3.cs, reactionCaOH.cs, CaCO3Reaction.cs, Feso4Reaction.cs
MOD  VR/Assets/Scripts/FillPipette.cs, Reaction.cs          (round 2 - see section 5)
```

---

## 3. Acceptance criteria

| Criterion | Status |
|---|---|
| All 8 experiments have quantity-tracked pouring with configurable targets + tolerance | ✅ (R1 already had it inline; R2–R8 now via the engine) |
| Each experiment can fail from overdose, underdose, or wrong procedure order | ✅ |
| Each experiment shows a floating tooltip with real-time measurements | ✅ |
| Failure messages are chemistry-accurate and suggest asking the AI | ✅ |
| All existing success paths still work (effects, materials, sounds, completion text) | ✅ code paths preserved; needs a Play-mode pass to confirm visually |
| No regressions in ControlReactions, FlipPages, or video playback | ✅ those files were not touched |

---

## 4. Where the project stands vs. your description

Checked against the 10 features in your project overview:

| # | Feature | State |
|---|---|---|
| 1 | Interactive 3D chemistry lab in Unity | ✅ Done (4 scenes, 8 reactions, VR + desktop) |
| 2 | Free-hand experimentation (user decides quantity/procedure) | ✅ **Done by this task** — was 1 of 8, now 8 of 8 |
| 3 | Realistic success/failure from chemistry rules | ✅ **Done by this task** |
| 4 | Side AI lab assistant chatbot | ⚠️ Partial — Inworld AI is wired up (`LabAssistantPushToTalkUI`, `LabAssistantSubtitleUI`) but the assistant is **not fed the experiment context**. It cannot yet answer "why did my experiment go wrong" because nothing hands it `GetFailureExplanation()`. |
| 5 | AI replies in text **and** voice | ✅ Inworld provides both |
| 6 | Molecular-level video after success | ✅ Done (`ReactionLearningController`, `ReactionLearningVideoCatalog`) |
| 7 | Ask follow-up doubts about the molecular animation | ⚠️ Partial — video and AI exist but are not connected |
| 8 | Scientific graphs (energy vs. progress, exo/endothermic, entropy) | ❌ **Not started** — no graph/chart code exists in the project |
| 9 | AI explains the graphs verbally | ❌ Not started (depends on #8) |
| 10 | Experiment history (attempts, mistakes, corrections, concepts) | ❌ **Not started** — no persistence code at all (no `PlayerPrefs`, no JSON save) |

**Suggested order for the remaining tasks:**
1. **Bridge the engine → AI assistant** — the failure explanations this task produces are exactly what feature #4 needs; this is now a small piece of glue and unlocks the most value.
2. **Experiment history / attempt log** (#10) — also feeds off the engine's state, and the AI needs it to say "you made this same mistake last time".
3. **Scientific graphs** (#8, #9) — the largest remaining build.

---

---

## 5. Round 2 — playtest fixes

After the first Play-mode test three problems showed up. All three are fixed.

### 5.1 The measurement label swallowed the screen

**Cause.** TextMeshPro world-space text renders at roughly `fontSize × 0.12` **metres** per line.
The tooltips were created at `fontSize` 1.4–1.8, i.e. **17–22 cm tall lines** — three of those
lines is a half-metre block of text sitting on the bench, which is exactly what the screenshot
showed. `Reaction.cs` (R1) had the same bug at 1.8.

**Fix — `FreeHandTooltip` in `FreeHandReactionEngine.cs`:**

- Default font size dropped to **0.30** → about **3.6 cm per line**, a normal label above a beaker.
- **Apparent-size cap:** below `maxApparentSizeDistance` (1.2 m) the label scales down with camera
  distance, so leaning right over the bench can no longer blow it up across the view.
- **Readability**, since cyan-on-white was washing out:
  - each line now sits on a dark translucent plate (TMP `<mark>` tag — no extra objects, no
    custom shader, so nothing can fail to load in a build),
  - a black outline (`outlineWidth` 0.18) keeps glyphs legible where the plate is clipped,
  - softer, higher-contrast colours (cyan `#5AF2FF`, green, red) and wider line spacing,
  - `NoWrap` + an explicit rect, so lines never wrap mid-number,
  - pivot moved to the bottom edge so the block grows **upward** from the container.
- Per-reaction `tooltipFontSize` field added to all 8 reactions so it stays tunable in the Inspector.
- `tooltipHeightOffset` defaults tightened now the label is small (0.24 beakers / 0.18 dish / 0.26 tubes).

`Reaction.cs` (R1) was migrated onto the shared `FreeHandTooltip` in the process, so all eight
experiments now render identically instead of R1 keeping its own duplicated copy.

### 5.2 The pipette did not work (R5)

Two genuine bugs, both in the original code:

**Filling was practically impossible.** `FillPipette` required the tip to land inside a **5 cm box
on all three axes** simultaneously (`Math.Abs(dx) < 0.05 && dy < 0.05 && dz < 0.05`). While
carrying the pipette in front of the camera that window is almost unhittable, so the pipette
simply never filled — and nothing on screen said why.

- Replaced with one forgiving cylinder around the flask mouth: `fillDistance` 18 cm horizontally,
  with `fillHeightSlack` 12 cm of extra room above so holding it just over the neck also counts.
- Added `IsFull`, `IsInFillZone` and `DistanceToWater` so the UI can react to it.
- Added fallbacks + warnings if the `pivott` / `Pivot` child objects are missing, instead of a
  silent `NullReferenceException` that dead-locks the experiment.

**Dispensing was judged from the wrong object.** `PourFromPipette` tested the pipette's **root**
position against the dish, but the root can sit well off to the side while the tip is right over
the dish — which is what the user actually aims.

- Now judged from the tip pivot, with a configurable `dishRadius` (14 cm) and `maxDripHeight` (45 cm).
- The 3-second empty timer used wall-clock `DateTime.Now` from the moment pouring started, so
  wobbling out of the zone and back handed the user a whole fresh 3 seconds of water. It now
  **accumulates** `Time.deltaTime` while actually dripping, so the dose is honest.
- Null-guarded throughout; `containsWater` behaviour unchanged for backward compatibility.

**Water quantities now check out end to end:** a full pipette dispenses over `dispenseSeconds`
(3 s) at `pipetteFlowMlPerSecond` (0.7 ml/s) = **2.1 ml** against a target of 2.0 ml ±10%
(1.8–2.2 ml). So one complete dispense succeeds; pulling away early underdoses; refilling and
dispensing twice overdoses. All three outcomes are reachable, which is the point of free-hand mode.

**Discoverability.** The pipette also *read* as broken because nothing ever said whether it held
water. `ReactionAli3` now appends a live pipette status to the guidance text:

- `Pipette: EMPTY - dip the tip into the H2O flask to draw water up.`
- `Pipette: FULL - hold the tip over the crystallizing dish to release the drops.`
- `Pipette: dripping into the dish...`

### 5.3 The label floated near the ceiling

The 5.1 fix made the text the right *size* but put it in the wrong *place* — it ended up roughly
1.5 m above the bench instead of just above the container.

**Cause — a bug introduced by the 5.1 fix, not pre-existing.** The rect was sized
`fontSize × 10` = **3 metres tall** with the pivot moved to its bottom edge, but the alignment
was still `TextAlignmentOptions.Center`, which centres the text *vertically inside the rect*.
Bottom pivot + centred text = the block rendering at half the rect height, i.e. 1.5 m up.

**Fix:** alignment changed to `TextAlignmentOptions.Bottom` (horizontally centred, vertically
bottom) so the text hugs the bottom edge of the rect, which the bottom pivot puts exactly at the
transform position. The rect was also shrunk to `fontSize × 2` tall so its height cannot influence
placement again. The anchor point is now literally the bottom of the text block, so
`tooltipHeightOffset` means what it says: centimetres above the container, growing upwards.

`tooltipHeightOffset` defaults were then tightened, since the offset is no longer being added to a
hidden 1.5 m: **0.20 m** beakers / **0.13 m** dish / **0.22 m** test tubes.

### 5.4 The label was too small and washed out

With the position fixed, the label was legible in principle but too small to read comfortably and
low-contrast against the white benches.

**Cause.** The dark backing plate was being drawn with TMP's rich-text `<mark>` tag. That tag
renders *through the font atlas material*, and in practice it came out a pale cream colour rather
than the dark navy it was given — so bright cyan text ended up sitting on a near-white plate on a
white bench. The 0.30 font size was also conservative.

**Fix — the plate is now a real object whose colour we actually control:**

- `<mark>` replaced with a **backing quad** parented to the label, tinted from `backgroundHex`
  (`#0A1020F0`, near-black at 94% opacity) and auto-fitted every frame to the measured text
  bounds (`ForceMeshUpdate` + `GetRenderedValues`) with padding.
- Shader chosen from a **fallback chain** — `Sprites/Default` → `UI/Default` →
  `Unlit/Transparent` → `Unlit/Color` → `Legacy Shaders/Transparent/Diffuse`. `Sprites/Default` is
  preferred because it alpha-blends and is `Cull Off`, so the quad shows whichever way it faces.
  If none of them exist in a stripped build the panel is simply skipped and the outline alone
  still carries the text — it can never throw.
- The quad's auto-generated **collider is destroyed** (primitives ship with one, and it would have
  blocked pointer grabs) and it inherits the Ignore Raycast layer.
- Font size default **0.30 → 0.55** (~6.6 cm per line), now **bold**, with a harder black outline
  (0.22).
- Consistent named colours across all 8 experiments: `ProgressColor` bright cyan `#7AECFF`,
  `SuccessColor` bright green `#86F7A0`, `FailureColor` bright salmon `#FF9180`.
- Apparent-size cap relaxed to suit the bigger text (`maxApparentSizeDistance` 1.2 → 1.0, minimum
  scale 0.25 → 0.45) so leaning in shrinks it far less aggressively.

### 5.5 Verification

Rebuilt the full `Assembly-CSharp` assembly against the project's real Unity 6000.3.7f1 references:

```
Build succeeded.
    0 Error(s)
```

**Still needs a Play-mode pass:** the font size (0.55), the anchor heights and the pipette
fill/drip radii are educated numbers derived from TMP's world-space scaling and the existing scene
distances, not values measured in-game. If the label still reads slightly large or small, sits a
little high or low, or the pipette fill zone feels off, every one of those values is an Inspector
field — no code change needed.

### Inspector fields for tuning the label

| Field | Where | Default | Effect |
|---|---|---|---|
| `tooltipFontSize` | every reaction script | 0.55 | ~`fontSize × 0.12` metres per line |
| `tooltipHeightOffset` | every reaction script | 0.20 / 0.13 / 0.22 | metres from the container up to the **bottom** of the text |
| `maxApparentSizeDistance` | `FreeHandTooltip` | 1.0 | below this camera distance the label stops growing on screen |
| `backgroundHex` | `FreeHandTooltip` | `0A1020F0` | colour/opacity of the backing panel |
| `ProgressColor` / `SuccessColor` / `FailureColor` | `FreeHandTooltip` | cyan / green / salmon | text colour per state |

---

*Generated while completing Task 1. Session limit was not reached during this task.*
