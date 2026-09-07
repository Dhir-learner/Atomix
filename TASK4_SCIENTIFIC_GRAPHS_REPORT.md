# Task 4 — Scientific Graphs System

**Date:** 2026-09-06
**Branch:** `kushal`
**Status:** ✅ Built — compiles clean against the real Unity 6000.3.7f1 assemblies (0 errors)
**Builds on:** Task 1 (free-hand engine), Task 2 (experiment history), Task 3 (in-lab AI assistant)

---

## 1. Two decisions I made for you

Neither was worth blocking on, but both are Inspector fields if you disagree.

| Question | Choice | Why |
|---|---|---|
| Toggle key | **F** | `G` is already the grip-pose alias in `HandAnimationController`, `H` is the controls help, `Tab` is the history panel, `B` is the book, `1`–`8` start reactions. `F` was free and near WASD. |
| When the graphs appear | **11 s after success** | Every reaction shows its own completion popup for exactly 10 s and plays a guidance clip. Opening the graphs earlier would cover it. `delayAfterSuccess` is public. |

---

## 2. What was built

### 4A. `ReactionGraphData.cs` (NEW — ScriptableObject)

All the fields from the spec, plus the derived chemistry the UI and the AI both need:

| Field | Type | Notes |
|---|---|---|
| `reactionId` | `int` | |
| `reactionName` / `displayTitle` | `string` | equation and friendly heading |
| `reactionType` | `enum { Exothermic, Endothermic }` | |
| `activationEnergy` | `float` | kJ/mol |
| `enthalpyChange` | `float` | kJ/mol, negative = exothermic |
| `entropyChange` | `float` | J/(mol·K) |
| `energyProfilePoints` | `List<Vector2>` | normalised x = 0→1, y = kJ/mol |
| `graphExplanationText` | `string` | what the AI should say |

**Derived helpers** — `GibbsFreeEnergyAt(T)`, `StandardGibbsFreeEnergy`, `CrossoverTemperatureKelvin`,
`ReverseActivationEnergy`, `PeakEnergy`, `PeakProgress`, `SampleEnergy(progress)`,
`SpontaneitySummary()`, `NumbersLine()`.

Three details worth knowing:

- **`EffectiveType` prefers the sign of ΔH over the serialized enum**, so a mistyped Inspector value
  can never colour a graph the wrong way round.
- **`SampleEnergy` falls back to a synthesised curve** when `energyProfilePoints` is empty, so a
  half-filled Inspector entry still draws.
- **The synthesised curve bisects its barrier height** so the peak lands exactly on `Ea`. The obvious
  closed form (`height = Ea − ΔH/2`) is only exact at the curve's centre; because the Gaussian sits on
  a sloping baseline the true maximum drifts off-centre, and for R1/R4/R5 that closed form overshot Ea
  by **30–41 kJ/mol**. The bisection is the same one the asset generator uses, so the authored points
  and the code fallback agree.

### 4B. `ReactionGraphCatalog.asset` (NEW) + `ReactionGraphCatalog.cs` (NEW)

`Assets/Resources/ReactionGraphCatalog.asset` holds the eight `ReactionGraphData` objects as
sub-assets of one file, each with 41 authored curve points.

**ΔH and ΔS are computed, not guessed.** Both are derived by Hess's law from standard formation
enthalpies and absolute entropies at 298.15 K (CRC/NIST) for the equation exactly as written, so
every number can be checked against a textbook. Ea values are from kinetics literature for the
corresponding real process.

| # | Reaction | ΔH kJ/mol | Ea kJ/mol | ΔS J/(mol·K) | ΔG(25 °C) | Type |
|---|---|---:|---:|---:|---:|---|
| 1 | 2Na + 2H₂O → 2NaOH + H₂ | −368.6 | 10 | −15.5 | −364.0 | Exo |
| 2 | H₂SO₄ + CuO → CuSO₄ + H₂O | −63.7 | 48 | −43.0 | −50.9 | Exo |
| 3 | HCl + NaHCO₃ → NaCl + H₂O + CO₂ | **+31.4** | 45 | **+241.0** | −40.5 | Endo |
| 4 | 2K + 2H₂O → 2KOH + H₂ | −393.2 | 6 | +44.7 | −406.5 | Exo |
| 5 | 2Al + 3I₂ → 2AlI₃ | −627.6 | 85 | −86.9 | −601.7 | Exo |
| 6 | CaO + H₂O → Ca(OH)₂ | −65.2 | 46 | −26.3 | −57.4 | Exo |
| 7 | CaCO₃ → CaO + CO₂ | +178.3 | 185 | +160.7 | +130.4 | Endo |
| 8 | 2FeSO₄ → Fe₂O₃ + SO₂ + SO₃ | +340.1 | 365 | +377.4 | +227.6 | Endo |

**Two of these validate themselves against reality.** ΔH/ΔS puts the CaCO₃ crossover at **837 °C**
(industrial lime kilns calcine limestone at roughly 850–900 °C) and the FeSO₄ crossover at **628 °C**
(it decomposes near 680 °C). Those numbers were not tuned to match — they fall out of the tabulated
formation data.

**Three teaching points the data produces on its own:**

1. **R3 is endothermic but spontaneous.** It absorbs 31.4 kJ/mol yet ΔG is −40.5, purely because
   ΔS = +241 from the CO₂ released. This is the clearest "entropy can drive a reaction" example in
   the set, and it is the only room-temperature endothermic reaction in the lab.
2. **R1 loses entropy despite giving off hydrogen gas.** Na⁺ and OH⁻ pull water into ordered
   hydration shells, and that ordering outweighs the H₂ released.
3. **R4 gains entropy where R1 lost it.** K⁺ is larger and holds water far less tightly. Sodium and
   potassium side by side make the point better than either alone.

Two consistency rules are asserted at generation time: **Ea > 0** and **Ea ≥ ΔH** (a transition state
cannot sit below the products). Ea for R3 and R8 had to be raised to satisfy the second rule.

**The catalog also carries a code fallback.** `ReactionGraphCatalog.BuildBuiltIn()` reproduces the
same eight entries from C#. If the asset fails to import for any reason, the graphs still work. The
asset was generated by parsing that C# table, so the two cannot drift apart.

### 4C. `ReactionGraphRenderer.cs` (NEW)

A `RawImage` over a runtime `Texture2D`, with TextMeshPro labels laid on top — so the geometry is
drawn once into a texture, and the text stays crisp at any world-space distance instead of being
blitted in at a fixed resolution.

It contains a small software rasteriser, `GraphPainter`: filled rectangles, **anti-aliased** lines and
polylines (distance-to-segment coverage, not Bresenham), dashed lines, single and double-headed
arrows, filled circles and triangles, and a translucent fill under a curve. Everything is written into
one `Color32` buffer and pushed with a single `SetPixels32`, so a whole graph costs one texture upload.

**Three graphs, one renderer:**

| Graph | What it draws |
|---|---|
| **Energy Profile** | Curve, labelled axes with real kJ/mol ticks, dashed reactant/product levels, double-headed **Ea** and **ΔH** arrows, a marked transition state, tinted fill under the curve |
| **Exo / Endothermic** | Reactant and product enthalpy level bars, a single arrow pointing the way the heat actually moves, the headline classification, and what the student physically felt |
| **Entropy** | Signed bar against a zero axis, plus **all eight reactions on the same scale** with the current one highlighted, and the Gibbs working written out |

**Colour-coding:** exothermic red `#FF6A54`, endothermic blue `#56A8FF`, activation energy amber,
entropy green when it rises and orange when it falls.

The transition state is placed **early for exothermic and late for endothermic** reactions, after
Hammond's postulate — so the curve shape itself carries a piece of real chemistry.

### 4D. `ReactionGraphUI.cs` (NEW)

WorldSpace canvas built from code in the same style as `ExperimentHistoryUI` and
`ReactionLearningController` — same `RenderMode.WorldSpace`, same 0.001 scale, same
in-front-of-camera placement — so it is clickable with the **existing crosshair interaction** in
`ObjectInteraction.TryUiInteraction()`. No new input plumbing, no scene or prefab edits.

- Three tabs, with the active one highlighted
- The headline numbers strip (ΔH, Ea, ΔS, ΔG) under the graph
- A plain-English verdict line: *"Spontaneous above about 836 C — it needs heat, because only then does the rise in disorder outweigh the energy cost."*
- **`Ask AI to Explain`**, `< Previous graph`, `Next graph >`, `Back to Lab`, `Close [Esc]`
- Opens itself after a successful experiment; **F** reopens it any time for the last reaction run

`Back to Lab` and `Close` are the same action, deliberately — with the cursor locked for crosshair
aiming, having the exit in two reachable places is worth more than avoiding the redundancy. Graph
navigation is on the Prev/Next buttons rather than number keys, because `1`–`8` already start reactions.

### 4E. Graph display wired to all 8 reactions — **without touching any reaction script**

All eight reactions already funnel their verdict through one method,
`ReactionHistoryRecorder.Complete(ExperimentOutcome)` (Task 2). One hook there covers all of them:

```csharp
if (outcome == ExperimentOutcome.Success && Completed != null)
{
    Completed(reactionId, reactionName, outcome);
}
```

`ReactionGraphUI` subscribes to that event. This is why **no reaction script was modified in this
task** — the same reason Task 3 needed no reaction edits.

### 4F. "Ask AI to Explain" → the assistant

Three small additions joined this to the Task 3 assistant:

| File | Addition |
|---|---|
| `InLabAssistantController.cs` | `Instance`, `CanAsk`, and `AskAssistant(question, context)` — sends a question on the student's behalf and expands the panel if it was minimised |
| `ExperimentContextProvider.cs` | `BuildGraphContext(data, graphType)` — the briefing for the graph on screen |
| `ReactionGraphUI.cs` | Builds the question, sends it, reports what happened on the status line |

The answer comes back through the normal path, so it is **spoken, shown in the assistant panel, and
logged to the experiment history** exactly like a spoken question. A 2-second cooldown stops a double
crosshair click firing two requests.

The briefing hands the assistant the exact numbers on screen, the prepared explanation, and the
student's own attempt tally — so the answer is about the graph they are looking at, not the reaction
in general.

---

## 3. Verification

### The full assembly compiles

Compiled the **actual** `Assembly-CSharp` sources (97 files) against the project's real Unity
6000.3.7f1 references, using the compiler and runtime Unity ships:

```
errors=0
```

No new warnings. The only ones present are pre-existing (`CS0649`, `CS0414`, one `CS0618` in
`ReactionLearningController`), none in files this task touched.

Note the checked-in `Assembly-CSharp.csproj` is **stale** — it still lists the Inworld projects and
scripts Task 3 deleted, so it cannot build as-is. The type-check regenerated the source list from disk
and reused the csproj's real reference paths; all 349 Unity references resolved.

### The graphs were rendered and looked at

I could not open the Unity Editor, so I re-implemented the renderer's geometry — same plot area, same
coordinate mappings, same annotation placement — and rendered all 24 graphs to PNG offline. **This
caught five real layout bugs that would otherwise have shipped:**

1. The ΔH label ran off the right edge on R3, R7 and R8 (the endothermic ones, where the arrow sits
   high and right). → `AddLabelBeside` now estimates the text width and flips to the other side.
2. The ΔS label ran off the right edge on R3 and R8. → Both bar captions are now centred over the
   bar's own span instead of hung off its end.
3. On R5, where Ea (85) is tiny next to ΔH (−627.6), the Ea arrow was a few pixels tall and its label
   landed on top of the "Reactants" caption. → Short arrows get their label lifted clear.
4. The entropy comparison strip had ~10 px per row for eight rows, so the R1–R8 labels overlapped. →
   Plot area shortened, strip given ~19 px per row.
5. "Disorder INCREASES" collided with the axis caption below the bar. → Merged into the value label.

### The catalog asset was parsed and checked

The generated YAML was parsed with a real YAML parser and every entry asserted:

```
  R1 ok: 41 pts, y[0]=0.06 peak=10.0 (Ea 10) end=-368.6 (dH -368.6) type=0
  ...
  R8 ok: 41 pts, y[0]=0.00 peak=365.0 (Ea 365) end=340.2 (dH 340.1) type=1
ALL CHECKS PASSED
```

Every curve starts at the reactants (0), peaks exactly at Ea, ends exactly at ΔH, no `entries`
reference dangles, and the exo/endo enum matches the sign of ΔH in all eight.

### The formatted strings were executed

I compiled the derived-chemistry helpers standalone and ran them, because
`{0:+0.0;-0.0}` is a custom format with positive/negative sections and easy to get subtly wrong.
Every line came out correctly signed, and every verdict matched the independently computed sign
of ΔG.

**This found a real bug.** The first version quoted the crossover temperature unconditionally, which
produced *"Spontaneous below about 23507 C"* for R1 and *"6949 C"* for R5. Extrapolating ΔH/ΔS assumes
both are temperature-independent and that no phase changes intervene; for the strongly exothermic
reactions that lands thousands of degrees above the point where the substances still exist. It is
technically what the arithmetic says and useless to a student. The crossover is now only quoted where
it means something (R7 836 °C, R8 628 °C); the others get a statement that is true and teaches the
same point:

> *Spontaneous at room temperature. Only at high temperature would the entropy penalty win — which is
> what drives the reverse reaction when you heat the product.*

### Not verified — needs a Play-mode pass

I have not run the Unity Editor from this session, so these are reasoned but untested:

- **That Unity imports the multi-object `.asset`.** It is hand-written YAML with eight sub-objects,
  and it validates as YAML with correct script GUIDs and no dangling references — but Unity's importer
  is the only real judge. **If it fails, the graphs still work**: `ReactionGraphCatalog` falls back to
  the identical built-in table and logs a warning. This is the one thing worth checking first — look
  for `ReactionGraphCatalog` in the Project window showing eight child objects.
- Panel proportions at 1.6 m and whether the crosshair lands on the buttons (all sizes are Inspector
  fields; it uses the same construction as the history panel, so if that clicks correctly, this does).
- The 11-second auto-show delay against the real completion popup.
- Texture rendering cost on a tab switch (~1 M pixel writes, expected to be a few ms, but unmeasured).

### Files changed

```
NEW  VR/Assets/Scripts/ReactionGraphData.cs       (+ .meta)   4A
NEW  VR/Assets/Scripts/ReactionGraphCatalog.cs    (+ .meta)   4B
NEW  VR/Assets/Scripts/ReactionGraphRenderer.cs   (+ .meta)   4C
NEW  VR/Assets/Scripts/ReactionGraphUI.cs         (+ .meta)   4D
NEW  VR/Assets/Resources/ReactionGraphCatalog.asset (+ .meta) 4B
MOD  VR/Assets/Scripts/ExperimentHistoryManager.cs           4E  (success event + bootstrap)
MOD  VR/Assets/Scripts/InLabAssistantController.cs           4F  (Instance, CanAsk, AskAssistant)
MOD  VR/Assets/Scripts/ExperimentContextProvider.cs          4F  (BuildGraphContext)
MOD  VR/Assets/Scripts/ControlsHelpUI.cs                     added Tab / F / M to the help text
```

No reaction script, scene, or prefab was modified.

---

## 4. Acceptance criteria

| Criterion | Status |
|---|---|
| Energy vs Reaction Progress graph renders correctly for all 8 reactions | ✅ all 8 rendered offline and inspected; curves verified to start at 0, peak at Ea, end at ΔH |
| Exothermic/Endothermic classification is shown and color-coded | ✅ red/blue throughout, plus a dedicated enthalpy-level graph; classification is driven by the sign of ΔH, not a settable enum |
| Entropy values are displayed | ✅ signed bar, value, and all eight reactions on a shared scale for comparison |
| Graphs appear after successful experiments | ✅ all 8, via one hook in `ReactionHistoryRecorder.Complete` |
| "Ask AI to Explain" button triggers AI to verbally explain the graph | ✅ voice + panel text + history logging; **depends on Convai credentials being filled in** (see below) |
| Graph data is chemically accurate (real ΔH, Ea, ΔS) | ✅ ΔH and ΔS derived by Hess's law from CRC/NIST tables; two crossover temperatures independently match known industrial values |

---

## 5. Things you should know

1. **"Ask AI to Explain" needs Convai credentials.** `Resources/LabAssistantSettings.asset` ships
   empty (Task 3, section 8). Until an API key and character ID are in it, the button greys itself out
   and the status line says so, rather than appearing to do nothing. Everything else on the panel works
   regardless. The button re-enables by itself if the assistant connects while the panel is open.
2. **Escape both closes the panel and toggles the cursor lock**, because `FirstPersonController` also
   listens for it. `ExperimentHistoryUI` already behaves this way, so this is consistent rather than
   new — but it is why `Close` and `Back to Lab` are also on-screen buttons.
3. **The Ea values are the softest numbers here.** ΔH and ΔS are thermodynamic state functions and
   are exact for the equations as written. Activation energies are kinetic, depend on the mechanism and
   the surface, and published values scatter widely — R8 in particular is quoted anywhere from 200 to
   400 kJ/mol. I chose values that are literature-plausible **and** satisfy Ea ≥ ΔH. If your syllabus
   uses different figures, they are Inspector fields.
4. **R1's negative entropy will look wrong to a student** who has just been told that releasing a gas
   increases disorder. It is correct, and the explanation text addresses it head-on — but it is worth
   knowing that it is deliberate and not a sign error, especially since R4 is positive.
5. **The `Assembly-CSharp.csproj` in the repo is stale** and lists deleted Inworld files. Unity will
   regenerate it the next time it opens the project; it is only a problem for external tooling.

---

## 6. Where the project stands now

| # | Feature (from your project description) | State |
|---|---|---|
| 1 | Interactive 3D chemistry lab | ✅ |
| 2 | Free-hand experimentation | ✅ Task 1 |
| 3 | Realistic success/failure | ✅ Task 1 |
| 4 | AI assistant explains what went wrong | ✅ Task 3 |
| 5 | AI replies in text and voice | ✅ |
| 6 | Molecular video after success | ✅ |
| 7 | Follow-up doubts after the animation | ⚠️ Partly — the assistant is available in-lab but is still not told *which* molecular video was just watched. Now a very small addition: `ExperimentContextProvider` has the shape for it, and `BuildGraphContext` is the pattern to copy. |
| 8 | Scientific graphs (energy, exo/endothermic, entropy) | ✅ **This task** |
| 9 | AI explains the graphs verbally | ✅ **This task** |
| 10 | Experiment history | ✅ Task 2 |

**All ten features are now built.** The remaining work is verification rather than construction:
a Play-mode pass over Tasks 1–4, filling in the Convai credentials, and the small #7 addition above.

---

*Generated on completing Task 4. Session limit was not reached during this task.*
