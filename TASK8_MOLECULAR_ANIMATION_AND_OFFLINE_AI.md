# Task 8 — Live Molecular Animation + Offline AI Assistant

**Date:** 2026-09-08
**Branch:** `main`
**Status:** ✅ Built — 0 compile errors, 0 new warnings, **167/167 checks pass** running the real code
**Builds on:** Tasks 1–7
**Brief:** *"analyse my full project … see what is remaining, add that … enhance my project by any means"*

---

## 1. Gap analysis — your description against the code

I mapped your ten stated features onto what is actually in `VR/Assets/Scripts`. Seven were done.
Three were not, and two of those were load-bearing.

| # | Your feature | State before | Evidence |
|---|---|---|---|
| 1–3 | 3D lab, free-hand experimentation, real success/failure | ✅ done | `FreeHandReactionEngine.cs`, 8 reaction scripts |
| 4 | AI answers "why did my experiment go wrong?" | ⚠️ **fragile** | voice-only; cloud-only |
| 5 | Replies in text **and voice** | ⚠️ **fragile** | voice existed only as Convai's base64 WAV |
| 6 | Molecular video after a success | ❌ **broken for 2 of 8** | `videoClip: {fileID: 0}` for R1 and R8 |
| 7 | Ask further doubts *about the molecular process* | ❌ **missing** | no affordance anywhere after the video |
| 8 | Scientific graphs | ✅ done | `ReactionGraphUI.cs` (Task 4) |
| 9 | AI explains graphs verbally | ⚠️ fragile | worked, but died with the cloud |
| 10 | Experiment history | ✅ done | `ExperimentHistoryManager.cs` (Task 2) |

### The two things that were actually broken

**Reactions 1 and 8 had no molecular visualisation at all.** In
`Assets/Resources/ReactionLearningVideoCatalog.asset`:

```yaml
  - reactionId: 1
    videoClip: {fileID: 0}      # Na + H2O   - nothing
  - reactionId: 8
    videoClip: {fileID: 0}      # FeSO4      - nothing
```

Six MP4s exist; two were never shot. A student who succeeded at sodium-and-water — the flagship
reaction, the one the front of the book opens on — got the string
`"Molecular explanation video is unavailable."` and then nothing.

**The AI pillar was one HTTP request away from being gone entirely.** Every answer came from
`ConvaiAssistantBackend.SendRequest` → `POST https://api.convai.com/character/getResponse`. Four of
your ten features route through that one call. No internet, no microphone, an expired free-tier
quota, or a college firewall, and features 4, 5, 7 and 9 all fail together — with the button in the
graph panel greying itself out and `ReactionGraphUI` printing *"The lab assistant is not
connected."*

Those are the two things this task fixes, plus feature 7, which never existed.

---

## 2. What was built

### 8A. A live molecular animation engine — the missing videos, and then some

Rather than asking you to record two more MP4s, the molecular view is now **rendered in-engine, in
real time, for all eight reactions**. It draws into the same `RawImage` the video player already
uses, so the existing panel, its PLAY/PAUSE, its REPLAY and its CONTINUE all work unchanged.

Three new files:

| File | Lines | What it is |
|---|---|---|
| `MolecularScene.cs` | 267 | Data model: atoms, bonds, stages, electron transfers, plus a fluent authoring API and the CPK palette |
| `MolecularSceneCatalog.cs` | 578 | All eight reactions authored — 48 stages, 63 atoms, 12 electron-transfer arrows |
| `MolecularAnimationRenderer.cs` | 937 | Builds a ball-and-stick stage, animates it, renders it to a RenderTexture |

**What it actually shows** — the three things your brief asks for by name:

- **Bond breaking and bond formation.** A bond present in one stage and absent in the next thins
  out and turns **red**; a bond that appears comes in **green** and thickens. Colour is doing real
  work here, not decoration — it is the one thing a molecular animation exists to convey, so it
  gets its own visual channel and a legend on screen.
- **Molecular rearrangement.** Atom positions interpolate between stages with smoothstep easing,
  so groups visibly migrate — the carbonate straightening into linear O=C=O in R7, the three
  iodides arranging trigonal-planar around each aluminium in R5.
- **Electron transfer.** Yellow dots travel along an arc from donor to acceptor, staggered when
  more than one electron makes the trip.

Plus per-atom element symbols, per-stage annotations that appear under the atom they describe
(`"Fe2+ -> Fe3+ + e-"`), thermal jitter driven by Perlin noise for the "apply heat" stages, a warm
background tint while heating, a step counter, a progress bar, and a gentle camera sway so the model
reads as three-dimensional.

**The chemistry is honest, and the tests enforce it.** R1, R4, R5 and R8 show electron transfer
because they are redox. R2, R3 and R6 show **none**, because they are not — and R2's stage caption
says so out loud:

> *"Note what does NOT happen: no electrons change hands. Copper stays 2+ and sulfur stays 6+
> throughout — this is a proton transfer, not a redox reaction."*

That distinction is exactly the thing students get wrong in exams, and a generic animation would
have blurred it.

#### How it stays out of the lab's way

This was the design constraint, and it is why there are no scene, prefab or project-settings edits:

| Risk | How it is handled |
|---|---|
| The stage camera seeing the laboratory | The stage is built at **y = −8000**; the camera's far plane is **60 units**. The lab is not in the frustum. |
| Needing a culling layer | Not needed, because of the above. Adding one would have meant editing `TagManager.asset` — a project-wide change for one feature. |
| Stage lights spilling onto the benches | Two **point** lights, range 40. They cannot reach y = 0. |
| Rendering to the headset in VR | `stereoTargetEye = None` and a `targetTexture` is assigned. |
| The lab's own lighting dimming the model | Materials use Standard **with emission**, so brightness has a floor regardless of the room. |
| Colliders on the stage | Every primitive's collider is destroyed at creation — nothing can raycast the stage. |
| `DesktopBootstrap` adopting the stage camera as the player camera | Its fallback camera scan now skips any camera with a `targetTexture`. A camera that renders off-screen is never the player's view. |

### 8B. `3D VIEW` and `ASK AI` buttons on the learning panel

The video row went from four buttons to six, all on one row — a second row at y = −340 would have
hung off the bottom of the 720-tall panel.

```
PAUSE  |  REPLAY  |  3D VIEW  |  ASK AI  |  PERFORM/CONTINUE  |  BACK
```

- **`3D VIEW`** swaps between the recorded MP4 and the live animation. For R1 and R8 there is no MP4,
  so the animation simply *is* the molecular view and the button does not offer a way back.
- **`ASK AI`** is **feature 7 of your brief**, which had no implementation anywhere. It asks the
  assistant about the step currently on screen, by name:

  > *"In the molecular animation for Sodium + Water, step 3 is "Electron transfer". Can you explain
  > what is happening to the atoms and electrons there?"*

  You could already ask about a failed experiment and about the graphs. The molecular animation was
  the one thing you could watch and not ask about.

### 8C. An offline brain — `ChemistryKnowledgeBase.cs` (664 lines)

Convai stays the primary path. This answers when Convai cannot.

It is **not** a language model and does not pretend to be. It is a grounded lookup over the data the
game already holds, which makes it *more* accurate about this bench than a cloud model, not less:

| Question type | Answered from |
|---|---|
| Why did it fail? | The live `FreeHandReactionEngine` — actual quantities, targets, tolerance, per-reagent percentage over/under |
| What is happening chemically? | Authored per-reaction facts: mechanism, what you should see, the key concept |
| Explain the graphs | `ReactionGraphCatalog` — the real Ea, ΔH, ΔS and Gibbs summary from Task 4 |
| Explain the molecular animation | `MolecularSceneCatalog` — the actual stage captions |
| How much do I need? | The engine's live accepted range per reagent |
| How am I doing? | `ExperimentHistoryManager` — attempts, successes, failures |
| Is this dangerous? | Authored real-lab safety notes |

A failure answer is specific, not generic:

```
What you actually used:
  H2SO4: 27.4 ml  (needed 20.0 ml, 37% over)
  CuO: 5.0 g  (needed 5.0 g)

You went over on at least one reagent. Excess reagent does not make the reaction bigger -
it means the other reagent runs out first and the leftover just sits there contaminating
the product.

The mistake students usually make here: not enough acid to dissolve all the oxide ...
```

**Intent classification is weighted, not flat, and both decisions were forced by a failing test.**

- *First-match* fails on `"why does the energy profile graph have a peak?"` — it contains a failure
  word and a graph word, and first-match on "why" answers the wrong question.
- *Flat scoring* then failed on the graph panel's own generated question, `"Can you explain the
  energy versus reaction progress graph…"`, where the generic verb **"explain"** outscored the
  specific noun **"graph"**. Generic verbs are now worth 1, topic words 3, and multi-word phrases 4,
  because a phrase can only have been typed on purpose.

Both of those were caught by the harness, not by reading. `"how many attempts have I made"` was a
third: `"how many"` scored as a quantity question and beat `"attempts"` on a tie.

### 8D. Offline voice — `OfflineVoice.cs` (215 lines)

Feature 5 says the assistant replies in **both** text and voice. The voice half came only from
Convai's WAV, so offline the assistant went mute even though it still had plenty to say.

This speaks replies using the synthesiser already in Windows.

**Why a PowerShell process and not `System.Speech` directly:** Unity compiles this assembly against
`netstandard2.1`, which does not contain `System.Speech`. The type cannot be referenced at compile
time on any scripting backend. Shelling out to the host PowerShell — which does have it — works in
both the Editor and a Windows standalone build with no native plugin, no package, and no per-platform
define.

**The text is piped through stdin, not embedded in the command line.** An assistant reply contains
quotes, semicolons and newlines; putting any of that into a `-Command` string would be both fragile
and a command-injection hole. The script reads `[Console]::In.ReadToEnd()` and interpolates nothing.

It also trims long answers at a **sentence boundary** rather than mid-word, so the cut is not
audible, and kills the process on `OnApplicationQuit` — without that, a long answer keeps talking
after the window has closed.

### 8E. Typed questions — `LabTextInput.cs` (129 lines)

**Press Enter, type a question, press Enter.** No microphone required. This is the only path to the
assistant that needs neither a mic nor a network — a demo machine without a working mic previously
had no way to reach the assistant at all.

Also **`Y`** — a one-key shortcut for *"Why did my experiment go wrong?"*, the question the entire
free-hand design exists to provoke. Making the student type it every time was friction for no reason.

**Why this needed a global gate.** The lab keeps the OS cursor locked so the crosshair works, which
rules out a uGUI `InputField` — those need a pointer click to focus. So keystrokes have to be read
from `Input.inputString`. But the lab binds nearly every letter: `WASD` move, `E` interacts, `C`
rolls, `H` help, `B` book, `P` periodic table, `F` graphs, `V` mic, `1`–`8` start reactions. Typing
*"why did the sodium fail"* without a gate would walk the player across the room, open three panels
and start a reaction.

`LabTextInput.IsCapturing` is one flag, and fifteen scripts check it and return early. One line each
— a smaller and far more obvious change than rebinding a dozen keys. Ownership is tracked so a
stale `End()` from a closing panel cannot steal the keyboard from a panel that opened after it.

Two places the blanket guard was deliberately **not** applied:

- `LabHudController` — only the key read is gated. The toast and the measurement strip are display
  work, and freezing them mid-question would look like the game had hung.
- Typing is **refused while a pour is in progress**. Gating object interaction also freezes the tilt
  that controls pouring, so opening the field mid-pour would leave the beaker tipped and running
  while the student types — and they would return to an overdose they did not cause. The assistant
  says *"Finish pouring first — then press [Enter] and ask."*

---

## 3. A real input bug found on the way

**`T` was double-bound.** My first pass used `T` for "type a question" — the conventional chat key.
`T` is already `ObjectInteraction.resetHeldPoseKey`. Asking a question would have snapped whatever
you were holding back to its default pose.

This is the same class of bug Task 7 found with `V` (push-to-talk vs. the roll axis). The binding is
now **Enter**, which was genuinely free, is the universal open-the-chat key, and reads as "Enter"
rather than "Return" in the UI because that is what players call it.

`Y` was checked against every `KeyCode` in the project before use.

---

## 4. File manifest

### Created

```
NEW  VR/Assets/Scripts/MolecularScene.cs               267   atoms, bonds, stages, CPK palette
NEW  VR/Assets/Scripts/MolecularSceneCatalog.cs        578   all 8 reactions, 48 stages
NEW  VR/Assets/Scripts/MolecularAnimationRenderer.cs   937   the live 3D stage
NEW  VR/Assets/Scripts/ChemistryKnowledgeBase.cs       664   the offline brain
NEW  VR/Assets/Scripts/OfflineVoice.cs                 215   Windows speech synthesis
NEW  VR/Assets/Scripts/LabTextInput.cs                 129   typed-question capture + global gate
                                                      ----
                                                      2790   (+ 6 .meta files)
```

### Modified — 673 insertions, 33 deletions across 16 files

```
MOD  ReactionLearningController.cs   +308  animation fallback, 3D VIEW / ASK AI, playback routing
MOD  InLabAssistantController.cs     +286  offline routing, typed input, Y shortcut, status UI
MOD  DesktopBootstrap.cs              +24  typing gate; skip off-screen cameras
MOD  ControlsHelpUI.cs                +17  documents Enter, Y, 3D VIEW, ASK AI
MOD  LabHudController.cs               +5  narrowed typing gate (display work keeps running)
MOD  11 other scripts                  +6  each: one typing gate
```

Not modified: **any `.unity` scene, any prefab, any reaction script**, `ControlReactions.cs`,
`Randomize.cs`, `FreeHandReactionEngine.cs`, `ExperimentHistoryManager.cs`, any `ProjectSettings`
asset, or `ReactionLearningVideoCatalog.asset`. Consistent with Tasks 1–7: everything is additive,
built from code at runtime, and survives a Unity reimport.

---

## 5. Verification

Compiled against the real Unity 6000.3.7f1 assemblies:

```
0 Error(s)
2 Warning(s)   <- both pre-existing, identical to the baseline before this task
```

Then **167 checks run the real code** out of `Assembly-CSharp.dll` (only pure-managed paths —
`Vector3`, `Color`, `Mathf`, string logic — so no Unity player loop is needed):

```
=== Molecular animation catalog ===
=== Assistant intent classification ===

167 passed, 0 failed
```

**What the molecular checks actually enforce**, per reaction:

| Check | Why it matters |
|---|---|
| Every stage has a position for every atom | Otherwise the renderer indexes past the array end on the frame it reaches that stage |
| No NaN or infinite positions | A single NaN propagates into the transform and the atom vanishes |
| Every atom stays inside the camera frame | Authored off-screen means the student never sees it |
| No two atoms within 0.30 units | Overlapping spheres read as one atom, and a bond between them has zero length |
| Every bond is 0.35–2.4 units long | Anything else does not read as a bond |
| Every bond and electron indexes a real atom | Guards against an authoring typo |
| Bonds actually change between stages | Otherwise it is a still image with captions |
| Total runtime 12–60 s | Watchable, not a slideshow or a lecture |
| Every element has a CPK colour and radius | Catches a symbol typo, which would otherwise render magenta |
| Redox reactions show electrons; non-redox ones do **not** | The chemistry claim the animation is making |

**What the classifier checks enforce:** 30 questions, including the **exact strings the UI itself
generates** — all three of `ReactionGraphUI`'s tab questions, both of the ASK AI button's branches,
and both forms of the `Y` shortcut. If any of those misclassified, a button in the game would send a
question the offline brain answers with the wrong section, and nothing would visibly break.

---

## 6. New controls

| Key | Does |
|---|---|
| **Enter** | Type a question to the lab assistant (Enter sends, Esc cancels) |
| **Y** | Ask why the last experiment went wrong |
| **3D VIEW** button | Switch to the live molecular animation |
| **ASK AI** button | Ask about the molecular step currently on screen |

Unchanged: `V` push-to-talk, `M` minimise, `Space` play/pause, and everything from Tasks 1–7.

---

## 7. What this does not do

Stated plainly, because it matters for a viva:

- **The offline brain is a grounded lookup, not a language model.** It will answer an unrelated
  chemistry question — "what is Avogadro's number?" — with a general overview rather than the
  answer. Convai handles those when it is reachable. Within these eight reactions the offline path
  is *more* accurate, because it reads your actual measurements.
- **Offline voice is Windows-only.** `OfflineVoice.IsSupported` is false elsewhere and the assistant
  falls back to text silently. Convai's own voice is unaffected on every platform.
- **Nothing was rendered in a running Unity Editor.** Verification is a clean compile against the
  real Unity assemblies plus 167 logic checks over the authored data. The animation's *geometry* is
  tested; its *look on screen* is not, and is worth one play-through of R1 to confirm framing.
