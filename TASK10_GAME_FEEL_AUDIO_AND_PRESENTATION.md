# Task 10 — Game Feel, Audio Feedback and Presentation

**Date:** 2026-09-09
**Branch:** `kushal`
**Status:** ✅ Built — 0 errors, 68 warnings (identical to the pre-change baseline; **no new warnings**)
**Scope:** A full project audit, followed by a presentation and game-feel pass across the whole game

---

## 1. Executive summary

Atomix's *chemistry* is in good shape. Tasks 1–9 built a genuinely careful simulation: the
free-hand engine judges quantity, order and settling time; failures come with chemistry-specific
explanations; there is a grounded offline AI, an experiment history, thermodynamic graphs, live
molecular animation, a coin economy and a downloadable report card. All of that was verified
against the actual implementation and all of it works.

What the audit found is that the **presentation layer was never built at all**. The simulation was
telling the student things the game was not communicating:

| What the game knew | What the player was given |
|---|---|
| The experiment just succeeded after four failed attempts | One word in a HUD strip changed from blue to green |
| The experiment just failed | One word changed to orange |
| You picked up a beaker | Nothing. No sound, no motion |
| You clicked a button in the lab | Nothing until the result happened to become visible |
| That equipment is locked for this task | Nothing at all — indistinguishable from a missed click |
| The scene is loading | The window stopped responding for several seconds |
| Coins were awarded | Silence |

Three whole systems the project already **paid for** were sitting unused:
`com.unity.postprocessing` was in the manifest and referenced by `Assembly-CSharp` with not one
`PostProcessVolume` in any scene; there was no reflection probe anywhere, so a windowless
laboratory's glassware was mirroring a procedural blue sky; and `Application.onBeforeRender`, the
correct hook for camera effects, was unused because there were no camera effects.

This task builds that layer. **Ten new scripts, no new binary assets, and no `.unity` or `.prefab`
file touched** — the same architecture every task since Task 1 has used, so the VR authoring is
byte-for-byte intact and reinstalling the XR packages still restores VR behaviour cleanly.

---

## 2. File manifest

### Created

```
NEW  VR/Assets/Scripts/AtomixAudio.cs                  Cue router + 12 synthesised cues + 2 beds
NEW  VR/Assets/Scripts/CameraJuice.cs                  Head bob, sprint lens kick, screen shake
NEW  VR/Assets/Scripts/AtomixPostFx.cs                 Bloom, ACES grade, vignette, AO, SMAA
NEW  VR/Assets/Scripts/LabReflections.cs               Room reflection probe
NEW  VR/Assets/Scripts/ExperimentFeedbackDirector.cs   Verdict sting, shake, hit-stop, edge flash
NEW  VR/Assets/Scripts/SceneTransition.cs              Async loading behind a fade, with tips
NEW  VR/Assets/Scripts/LabOnboarding.cs                First-run card + live objective strip
NEW  VR/Assets/Scripts/UiButtonFeel.cs                 Hover lift / press dip / hover tick
NEW  VR/Assets/Scripts/PourAudio.cs                    Sound for the three silent pours
NEW  VR/Assets/Resources/PostProcessResources.asset    Required for a runtime PostProcessLayer
```

### Modified

```
MOD  AtomixSettings.cs           +3 audio buses, shake, head bob, post-fx, quality, fullscreen, fps cap
MOD  PauseMenuUI.cs              Settings tab rebuilt as two columns; 3 missing key bindings; intro button
MOD  LabPanelBuilder.cs          Rows lay out from their width; every button gains sound + feel
MOD  ObjectInteraction.cs        Emission highlight, locked-object feedback, frame-rate-independent hold
MOD  FirstPersonController.cs    Movement acceleration and deceleration
MOD  DesktopCrosshairUI.cs       Crosshair opens outward on hover (a non-colour signal)
MOD  DesktopBootstrap.cs         Attaches CameraJuice and the post stack
MOD  ExperimentHistoryManager.cs ReactionHistoryRecorder.Resolved event; 3 new bootstrap entries
MOD  FreeHandReactionEngine.cs   Per-frame readouts moved to a shared StringBuilder
MOD  LabBoundary.cs              Exposes HasInterior
MOD  AtomixCoinBank.cs           Coin award is audible
MOD  LabRetryController.cs       Bench reset is audible; reload goes through the fade
MOD  MainMenu.cs                 Null-guards; async loads; button sounds
MOD  ExitMenu.cs / SceneManagerScript.cs / ExamSetupUI.cs / TestResultsUI.cs   Loads via SceneTransition
MOD  ProjectSettings/TagManager.asset       User layer 9 = "PostProcessing"
MOD  ProjectSettings/ProjectSettings.asset  resizableWindow 0 -> 1
```

**No scene file. No prefab.** Confirmed by `git status`.

---

## 3. Audit findings — what was verified, not assumed

Every task marked complete in `CHANGELOG_TASKS.md` was checked against the implementation. Tasks
1–9 are genuinely done. The audit's real output was the list of gaps below.

### P0 — defects fixed

| # | Defect | Consequence |
|---|---|---|
| 1 | `ObjectInteraction.RemoveHighlight` wrote a cached colour back through a parallel array | Several reaction scripts swap materials *while the vessel is held*. Un-highlighting then stamped the previous material's colour onto the new one. Now keyed per-renderer with the material recorded alongside, and skipped if the material changed underneath. |
| 2 | Highlight replaced `material.color` outright with flat yellow | Copper sulfate solution, brown iodine and clear glass all became the same yellow plastic. Now additive emission, so the object glows and still looks like itself. |
| 3 | `MainMenu.Start()` dereferenced `settingsPanel` unguarded | One lost inspector reference throws in `Start`, which skips `InitializeToggles`, which leaves **every menu button unlistened**. The whole menu dies from one null. |
| 4 | Held-object and UI smoothing used `Lerp(a, b, dt * k)` | Frame-rate dependent: a beaker chased the crosshair visibly harder at 144 Hz than 60, and overshot outright below `1/k` fps. Now `1 - exp(-k·dt)`. |
| 5 | An ungrabbable object gave **no** feedback of any kind | This is a designed state — the labs lock equipment that is not part of the current task (Task 7 Round 4 was entirely about it). Aiming at a locked tube produced no highlight, and clicking produced no highlight, sound or message. Now a refusal cue and a toast naming the reason. |
| 6 | Synchronous `SceneManager.LoadScene` on a 3.6 MB scene | The window stops answering the OS; Windows greys it out and offers to close it. Now async behind a fade. |

### P0 — defects prevented in new code (caught by verification, not by review)

| # | Defect | How it was caught |
|---|---|---|
| 7 | A runtime-added `PostProcessLayer` null-references **every rendered frame** | Reading the package source: `OnEnable` calls `Init(null)`, and `m_Resources` is normally filled in by an inspector that does not exist at runtime. Fixed by shipping the resources asset in `Assets/Resources` and calling `Init` explicitly, with the whole stack disabling itself if the asset is ever missing. |
| 8 | Both audio beds had a 4 ms hole at the loop point | The offline harness (§7) measured the seam. `Finish()`'s 2 ms anti-click guard fade was zeroing both ends, undoing the seamless-loop construction. Looping beds now skip the guard. |
| 9 | Head bob would have fought `LabBoundary`'s per-frame position clamp | Reasoned through before shipping: the boundary clamps all three axes in `LateUpdate` and would give back less than the bob added, so the next frame's subtraction would over-correct and a player at the floor or ceiling limit would jitter. Restructured — see §5. |

### Identified but deliberately **not** changed

| Finding | Why not |
|---|---|
| `productName` is `UnityLab`, `companyName` is `AlinaInc` — the window title of a game called Atomix reads "UnityLab" | On Windows, PlayerPrefs live at `HKCU\Software\<company>\<product>` and `persistentDataPath` is `LocalLow/<company>/<product>`. Renaming silently discards **every player's coins, achievements, settings and experiment history**. A clean migration needs `Microsoft.Win32.Registry`, which this assembly does not reference. Not worth a window title. Ship it as a rename plus a migration build, deliberately. |
| `m_AmbientMode: 0` (Skybox) in all four scenes, while the authored trilight colours below it are plainly interior-tuned and currently **dead** | The evidence says someone set indoor ambient colours and the mode was left on Skybox, so a windowless lab is ambient-lit by a procedural sky. Almost certainly a latent authoring bug — but switching it makes the room darker, and that is a judgement that needs eyes on the scene. Flagged, not guessed at. |
| The Convai API key is still committed in `Assets/Resources/LabAssistantSettings.asset` and in git history | Already documented in the README. Rotating a key is the owner's action, not a code change. |

---

## 4. Audio — the largest gap

`Assets/Sounds` holds 38 clips. **Every one of them is narration** — "add the sodium now", "this
is how copper sulfate forms". Nothing in the game made a sound because the student *did*
something. There was no click, no pickup, no success, no failure, no ambience, no music.

### Everything is synthesised at runtime

No new binary assets. That is a design decision, not a shortcut:

- the audio cannot go missing from a build the way an unassigned `AudioClip` field silently can;
- no scene or prefab has to be edited to wire a source up;
- the cues are consistent **by construction** — one pitch palette, one envelope family — rather
  than eight sample packs that disagree about loudness.

Two rules keep the set coherent. Every cue is drawn from a **D major triad** (D5/F♯5/A5/D6), so
two cues landing on the same frame — a success sting and a coin award routinely do — agree with
each other. And every cue uses the same envelope family: a few milliseconds of attack so nothing
clicks, then exponential decay, because a percussive sound with a linear release sounds synthetic.

| Cue | Where it fires | Design note |
|---|---|---|
| `UiClick` | Every runtime-built button | Most-heard sound in the game, so the quietest |
| `UiHover` | Crosshair crossing a grabbable; pointer entering a button | Barely there by design |
| `UiDenied` | Grabbing locked equipment | Two low blips — reads as "no" without being a buzzer |
| `UiOpen` / `UiClose` | Panels, scene changes | Noise swept through a moving filter, so it reads as movement not hiss |
| `Grab` / `Release` | Picking up and setting down glassware | **Pitched by object size**, from renderer bounds — a test tube ticks, a berzelius thunks |
| `Success` | Experiment verdict | Rising D–F♯–A–D bell arpeggio. The single most important sound in the game and the one it did not have |
| `Failure` | Experiment verdict | Deliberately **not** a buzzer. This is a teaching tool, failure is the expected path, and a student hears it many times an hour. Two warm falling tones: "that did not work", not "you are bad at this" |
| `Coin` | `AtomixCoinBank.Earn` | Pitched by award size, so a big payout sounds like one |
| `Achievement` | Unlock, delayed 0.55 s | Held back so it does not collide with the verdict sting that earned it |
| `Reset` | F5 bench reset | Two-note fall: "back to the start" |

Plus three loops: an 8-second **laboratory room tone** (fume-hood extraction, mains hum, room air),
a 12-second **main-menu pad** (slow D major, no melody — a menu seen forty times must not have a
tune), and two **pour loops** (granular for powders and solids, droplets for the pipette).

### Making the loops actually seamless

The hum uses only frequencies that complete a whole number of cycles in the loop length, so it is
periodic by construction. The noise components cannot be, so their tails are equal-power
cross-faded into their own heads. §7 measures the result.

### Threading

The two beds are ~1.5 MB and ~2.3 MB of samples, costing 33 ms and 74 ms to synthesise. Building
them inline would hitch the exact frame the lab loads on — the one frame a student is already
waiting through. They are generated on a background thread and collected when they land; the beds
fade in over half a second anyway, so the delay is invisible. The sample generators were checked
to touch no Unity API (the sample rate is read on the main thread and passed in).

The short cues are warmed one per frame while the lab is idle, so the success sting is never built
on the frame it is first needed.

### Routing

`MasterVolume` stays exactly what it was — `AudioListener.volume`, over everything. Three buses sit
underneath it, so a student can silence the room tone **without** silencing the narration that
explains the experiment.

---

## 5. Camera — and the ordering problem it had to solve

Walking across the lab moved the view in a perfectly straight line at a perfectly constant speed.
Sprinting looked identical to walking. A sodium explosion two feet away did not disturb the frame.

`CameraJuice` adds a walk bob, a sprint lens kick and a shake. The interesting part is *where* the
offset is applied, because three systems already write to that transform every frame:

- `FirstPersonController` moves it in `Update` and rewrites its rotation outright;
- `LabBoundary` clamps its position on **all three axes** in `LateUpdate`;
- `ObjectInteraction` reads it in `Update` to place held glassware.

A bob in `LateUpdate` is inside all of that. The boundary would clamp a bobbed position and return
less than was added, so the next frame's subtraction over-corrects — a player standing at the floor
or ceiling limit would jitter against it. And held glassware would inherit the bob and shake in the
student's hands while they were trying to read a graduation off it.

So the offset lives in the narrowest possible window: **applied in `Application.onBeforeRender`**,
after every `LateUpdate` and immediately before the frame is drawn, and **removed at the top of the
next frame's `Update`** at execution order `-200`, ahead of everything else. No other system ever
observes the camera in its offset state. The bob is purely visual by construction and cannot fight
anything. The rotation is undone explicitly rather than relying on the controller to overwrite it,
because the controller only rewrites rotation while the cursor is locked — with the pause menu open
an un-removed roll would accumulate until the horizon was upside down.

Both effects scale to zero from the pause menu. Motion sensitivity is common enough in a classroom
that a shake a student cannot switch off is a shake that should not ship.

---

## 6. Visuals

| Change | Why |
|---|---|
| **Post-processing** — bloom, ACES colour grade, vignette, ambient occlusion, SMAA | The package was already a dependency and entirely unused. Bloom is thresholded *above* white so it catches the burner flame and wet-glass highlights and leaves UI alone. AO is what sells the bench — without contact shadowing every beaker looks like it is hovering a millimetre above the surface. |
| **Antialiasing** | The single biggest legibility win: the scene is full of thin glass rims and burette graduations, and the project ships MSAA off on four of its six quality levels. SMAA at High and above, FXAA below. |
| **Reflection probe** | 72 materials use the Standard shader, which takes specular reflection from the nearest probe — and there was none. A windowless basement laboratory's glassware, tap and steel sink were mirroring a clear blue sky. One box-projected probe, captured once when the scene settles. |
| **Emission highlight** | Replaced the flat yellow albedo overwrite. Adds light instead of replacing the surface, which is also how a highlight should read on transparent glass, where a tint reads as dirt. Pulses slowly so it reads as *responding to you*. |
| **Grade is laboratory-only** | `MainMenuScene`'s menu is a **world-space** canvas 1.1 m from the camera — it is *inside* the post stack, not composited over it. ACES would pull its white text towards grey and the vignette would dim whichever corner a button sat in. The menu keeps antialiasing and nothing else. |

Cost control: AO and SMAA only above quality level 3; bloom drops to fast mode below it; the whole
pass is one toggle in the pause menu.

---

## 7. Verification

### Compilation

Compiled against the **real Unity 6000.3.7f1 assemblies** (all 344 references resolving, including
`Unity.Postprocessing.Runtime`) using Unity's own bundled Roslyn, after every change.

```
Baseline (before any change):  0 errors, 68 warnings
Final:                         0 errors, 68 warnings
New warnings introduced:       0
```

Every one of the 68 warnings is pre-existing `CS0649` / `CS0414` on scene-assigned fields in the
original reaction scripts. **No warning appears in any file created or modified by this task.**

### Audio — 20 automated checks against the shipped code

The synthesis class was extracted **verbatim** from `AtomixAudio.cs` and run outside Unity against
a minimal `Mathf`/`AudioClip` shim, so these checks exercise the code that actually ships:

```
=== ONE-SHOT CUES  (must start and end at silence) ===
  PASS  UiClick        0.055s  peak 0.289  rms 0.103
  PASS  UiHover        0.040s  peak 0.160  rms 0.069
  PASS  UiDenied       0.220s  peak 0.340  rms 0.114
  PASS  UiOpen         0.280s  peak 0.220  rms 0.047
  PASS  UiClose        0.280s  peak 0.220  rms 0.035
  PASS  Grab           0.160s  peak 0.340  rms 0.082
  PASS  Release        0.130s  peak 0.300  rms 0.067
  PASS  Success        1.150s  peak 0.500  rms 0.094
  PASS  Failure        0.800s  peak 0.460  rms 0.094
  PASS  Coin           0.300s  peak 0.340  rms 0.073
  PASS  Achievement    1.500s  peak 0.500  rms 0.093
  PASS  Reset          0.400s  peak 0.320  rms 0.078

=== LOOPING BEDS  (the loop seam is what matters) ===
  (LabAmbience synthesised off-thread in 33 ms)
  PASS  LabAmbience level        8.0s  peak 0.220  rms 0.0448
  PASS  LabAmbience loop seam    seam 3.70E-004 vs mean interior step 1.93E-003   ratio 0.2x
  (MenuPad synthesised off-thread in 74 ms)
  PASS  MenuPad level           12.0s  peak 0.300  rms 0.0813
  PASS  MenuPad loop seam        seam 7.60E-003 vs mean interior step 1.62E-003   ratio 4.7x

=== POUR LOOPS ===
  PASS  PourGranular level       2.0s  peak 0.300  rms 0.0723
  PASS  PourGranular loop seam   seam 7.14E-002 vs mean interior step 5.64E-002   ratio 1.3x
  PASS  PourDroplets level       2.0s  peak 0.340  rms 0.0511
  PASS  PourDroplets loop seam   seam 0.00E+000 vs mean interior step 3.63E-003   ratio 0.0x

ALL CHECKS PASSED
```

The seam test is the meaningful one: it compares the sample-to-sample step *at the loop point*
against the mean step inside the clip. A room-tone seam **smaller** than a typical interior step is
inaudible by definition. This test is what caught defect #8 — on the first run every seam measured
exactly `0.0`, which looked perfect and was in fact the guard fade silencing both ends.

### Not verified

**A Play-mode pass was not possible: the Unity Editor is open on this project** (PID 61768), which
holds the project lock and rules out a second batch-mode instance. Everything above is static
verification. The items that specifically want eyes on them are listed in §9.

---

## 8. Performance

| Change | Effect |
|---|---|
| `FreeHandReactionEngine` readouts moved to a shared `StringBuilder` | These are rebuilt **every frame by every live reaction script**. Built with `+=` and `string.Format`, a three-reagent experiment produced roughly a dozen short-lived strings per readout per frame — several hundred kB a minute of pure garbage. Now zero steady-state allocation. |
| `UiButtonFeel` idle fast path | A button nobody is pointing at returns immediately. Every panel in the game is built from these. |
| `AtomixSettings.Apply` | Runs every frame for two seconds after each scene load. `QualitySettings.names` allocates a fresh `string[]` per read; the cheap comparison now short-circuits it, and `targetFrameRate` is only written when it changes. |
| Reflection probe captured **once** | `ViaScripting` refresh, not `EveryFrame`. The room is static, so one capture is as correct as sixty a second. |
| Audio beds built off-thread | Removes a 33 ms and a 74 ms main-thread hitch at the two worst possible moments. |
| Post-processing tiered by quality level | AO and SMAA above level 3 only; bloom fast mode below. |

---

## 9. Remaining opportunities

Ordered by value.

1. **Play-mode regression pass.** Everything here is statically verified. The grade, the bob
   amplitude and the reflection probe's framing in particular want a human eye.
2. **Ambient lighting mode** (§3). Strong evidence it is a latent bug; needs a look, not a guess.
3. **Product rename plus a PlayerPrefs migration build** (§3), so the window stops saying
   "UnityLab" without discarding player progress.
4. **Rotate the committed Convai API key.**
5. **Spatialise the five existing pour sounds.** They are 2D `PlayOneShot` calls; the three added
   here are spatialised and positioned at the leak. Making the other five match would be a small,
   consistent improvement.
6. **A per-reaction success flourish.** The verdict feedback is uniform. Reaction 5
   (aluminium + iodine, three explosion systems and a violet cloud) deserves more than reaction 6.
7. **Music/ambience ducking under narration.** The narration clips and the room tone currently
   share the mix with no side-chain.
8. **Controller support.** Everything is `Input.GetKey`; there is no gamepad path.
9. **Localisation.** All strings are inline English literals.

---

## 10. Round 2 — playtest fixes

Four problems reported from an actual play session. All four were mine; three are cases where a
technically-correct effect was simply the wrong call for this game, and one was a genuine
pre-existing bug that the polish pass exposed rather than caused.

### 10.1 Glassware inside the bench, and glassware falling through it

**Two separate causes, both real.**

**Cause A — the settler's raycast started inside the object.** `DesktopObjectSettler.TryMeasureGap`
cast downward from `bounds.center`, the middle of the object, and took the *first* upward-facing
surface it met. For an object resting correctly that is fine. For an object even slightly sunk into
the bench it is a disaster: the ray starts **below** the bench top, never sees it, carries on down
and hits **the floor**. The gap came back as the height of the table — about 0.75 m — which is
inside the 1.2 m release limit, so *letting go of a beaker that was a centimetre into the bench
teleported it to the floor.* That is the "objects fall below the table" report exactly.

And because a negative gap failed the range test outright, an object that was already embedded
could never be rescued: it stayed sunk in the table for the rest of the session.

The cast now starts at `bounds.max.y` — above the object, so a surface it is currently inside is
still found — and takes the **highest** qualifying surface rather than the nearest, so the bench
always wins over the floor beneath it. A negative gap now means "embedded" and lifts the object
out, capped at 0.35 m, and **only off static geometry**: a great deal of this lab's equipment is
meant to interpenetrate — a tube inside its clamp, a stopper in a flask neck, a balloon over a tube
mouth — so anything that is itself an `ObjectGrabbable` is never climbed out of.

**Cause B — held objects had no collision at all.** Held glassware is positioned by direct
`transform.position` assignment, which sweeps nothing. Walking up to the bench pushed whatever you
were carrying straight through it, and releasing at that moment is what created the embedded state
in the first place. `ObjectInteraction` now sphere-casts from the camera to the intended hold point
and stops the object short of anything solid, with the sphere sized from the object's own
horizontal extent so a large beaker stops further out than a stopper does.

### 10.2 The walking motion caused motion sickness

Reported directly, and there is no version of that trade worth making in software a student may be
required to use for an hour.

- **The head bob now ships off.** Its ceiling was also halved (2.2 cm to 1.1 cm vertical) and the
  roll cut by nearly two-thirds (0.32 to 0.12 degrees), since rotational movement is the worse
  offender of the two.
- **The sprint field-of-view kick is now behind the same switch.** A lens that zooms when you start
  running is exactly as nauseating as a bob, and someone turning the bob off because it makes them
  ill should not then have to find a second control. The row is now labelled **Walking motion**.

### 10.3 The image looked dull

> *"the colour contrast was perfect before, now it is looking dull"*

Correct, and the diagnosis is not subtle in hindsight. Three things I added were each removing
contrast:

1. **ACES tonemapping.** ACES is a display transform for HDR content. Atomix's lighting is authored
   to look right as it is, so tonemapping it a second time compresses highlights that were never
   blown out and desaturates colour that was already correct. White walls go grey.
2. **A shadow lift.** Raising black to 0.012 to stop shadows reading as "holes in the geometry"
   costs exactly the depth that made the bench look solid. Lifting blacks *is* reducing contrast.
3. **Bloom far too low and far too strong** — threshold 1.10, intensity 2.1. In a white-walled
   laboratory under bright lights a great many pixels sit just above 1.0, so instead of catching
   the burner flame it laid a haze over the entire room.

**Colour grading and the vignette are now off entirely.** The pass no longer touches a single
colour value. Bloom is raised to 1.35 and cut to 0.75, so it is something you notice on the flame
and nowhere else. What remains is only what is unambiguously additive: **antialiasing** and
**ambient occlusion**, neither of which alters the palette.

The authored look was good. The right amount of grading to apply to it was none.

### 10.4 The water tap sound on every scene load

Two independent causes, and the second is a pre-existing bug the polish pass merely made audible.

**Cause A — my room tone was, acoustically, running water.** The bed layered a low hum over
low-pass-filtered white noise, intended as "the wash of air a hard room has in it". But filtered
broadband noise *is* what running water sounds like — a tap, a shower and a waterfall are all
shaped noise, and the ear identifies them by spectrum, not by context. Under a scene containing a
visible sink and tap, the identification was certain.

The noise layer is gone. What remains is only what a laboratory's *machinery* sounds like: mains
hum at 50 Hz with its harmonics, and the low beat of an extraction fan — nothing above 200 Hz.
Measured at **840,000x less spectrally flat** than the bed that was reported. It also now **ships
off**, along with a settings-file version bump so installs that already saved the old default
actually receive the new one — a changed default alone would not have reached anyone who had ever
opened the pause menu.

**Cause B — the game really was turning the tap on.** `RotateButton` toggles the water whenever its
handle *stops moving*, which is how VR operates it: grab, twist, let go. Meanwhile
`DesktopBootstrap.SetupInteractables` made every candidate object grabbable **before** attaching
the click actions — so on the first pass the tap handle picked up an `ObjectGrabbable`,
`DesktopSettleWatcher` treated it as loose glassware and lowered it onto the nearest surface, and
that movement stopping **turned the water on**. Every lab scene load, exactly as reported.

Fixed at three levels:

1. **Root cause** — `AttachActuators` now runs *before* anything is made grabbable, so controls
   never acquire an `ObjectGrabbable` at all.
2. **Defence** — the settler skips anything carrying a `DesktopInteractable`. A tap handle, burner
   support or container lid is authored where it is and must never be moved.
3. **Defence** — `RotateButton` ignores handle movement for the first two seconds, since the rig
   spends that time placing the room and this script cannot tell that from a hand.

---

## 11. Round 2 verification

Compile unchanged: **0 errors, 68 warnings, zero new.**

The audio suite grew a spectral check, because "does not sound like water" is a measurable claim.
Spectral flatness — the geometric mean of the power spectrum over its arithmetic mean — is the
standard noise-versus-tone measure: flat spectra approach 1, spectra whose energy sits in discrete
partials approach 0. The old bed is reconstructed inside the test so the comparison is against
something real rather than against a number picked out of the air:

```
=== ROOM TONE SPECTRUM  (must not be broadband, or it sounds like a tap) ===
  spectral flatness   old noise bed 0.0007   rebuilt hum bed 0.000000
  PASS  rebuilt bed is far more tonal        840073x less spectrally flat than the bed
                                             that sounded like a tap
  PASS  rebuilt bed is essentially pure tone flatness 8.54E-010

ALL CHECKS PASSED        (22 checks)
```

> A note on that test: my first attempt asserted "less than 5% of energy above 400 Hz" and **failed
> at 14%**. The assertion was wrong, not the fix — a first-order high-pass at 400 Hz still passes a
> good deal of a 200 Hz sine, so it was measuring filter leakage from the hum rather than broadband
> content. Spectral flatness is the correct instrument. Worth recording, because a test that fails
> for the wrong reason is as misleading as one that passes for the wrong reason.

**Still not Play-tested** — the Unity Editor holds the project lock. The object-settling fixes in
particular are reasoned from the geometry rather than observed, and want a pass with eyes on them.

---

## 12. Round 3 — the Bunsen burner

Two reports: the burner is still standing inside the table, and it is alight before the player has
lit it. They turn out to share a root, and chasing it found the same bug in two more places.

### 12.1 The burner was alight at every scene load

`LightFire` has **the identical movement-detection bug I fixed in `RotateButton` last round and did
not fix here**: it toggles the flame whenever `burnerSupport` stops moving, which is how VR
operates it — grab the support, move it, let go. On desktop the rig moves objects during startup,
so the burner lit itself.

Checking properly this time, the pattern appears **four** times, written out identically:

| Script | Control | What it actuates |
|---|---|---|
| `RotateButton` | tap handle | the water — fixed in Round 2 |
| `LightFire` | burner support | **the flame — the reported bug** |
| `NatriumContainerScriptAnimation` | sodium container lid | unscrew animation |
| `UnscrewPotassiumContainer` | potassium container lid | unscrew animation |

The two lids were exposed to exactly the same misfire; they had gone unnoticed only because both
also guard on `hasTriggered`, so they can do it at most once per session.

Rather than paste the same guard into three more files, the logic now lives once in a new
`MovementLatch`, and all four use it. It ignores movement for a two-second settle-in window while
the scene assembles itself. Desktop players click these controls anyway —
`DesktopBootstrap` wires a `DesktopInvokeInteractable` onto each — so the window costs desktop
nothing, and in VR two seconds passes long before a headset user has reached the bench.

Two further causes of a lit burner, both fixed:

- **The flame is authored active with `playOnAwake: 1` and `looping: 1`.** `LightFire.Start()` did
  switch it off, but `Start` never runs while the burner's GameObject is disabled — and
  `ControlReactions` keeps equipment disabled until its experiment is chosen. The flame is now put
  out in **`Awake`**, and `LabEffectsInitializer` — which already sweeps every other stray particle
  system in the lab and had simply never covered this one — now stops it too.
- `LightFire` dereferenced `fireAnimation` and `audioSource` unguarded in `Update` and in its
  sound coroutine. Guarded.

### 12.2 The burner was still inside the table — and my Round 2 fix is why

This one is my error, and it is worth being precise about.

`LightFire.burnerSupport` points at **the burner's own GameObject** — the component and the control
are the same object. In Round 2 I added a rule that the settler must never move anything carrying a
`DesktopInteractable`, to stop the tap handle being dragged around. That rule is what excluded the
burner: making it clickable also made it un-settleable, so the one object that most needed lifting
out of the bench became the one object never looked at.

Worse, the exclusion was doubly effective. `DesktopBootstrap.EnsureGrabbable` skips anything that
already has a `DesktopInteractable`, so once the burner became an actuator it stopped receiving an
`ObjectGrabbable` at all — and both settle passes iterate `ObjectGrabbable`. The burner had fallen
out of the scan entirely.

Fixed by making the rule directional instead of absolute, which is what it should always have been:

| Direction | Controls | Why |
|---|---|---|
| **Lowering** onto a surface | never | A control is authored where it belongs, often mounted on or into something. Moving it down is a guess, and the movement can actuate it. |
| **Lifting** out of solid geometry | always allowed | An object sunk into the bench is never correct, control or not. |

And the scan now collects `DesktopInteractable` alongside `ObjectGrabbable`, so controls are
actually considered. The lift still refuses to climb out of anything that is itself a grabbable or
a control, so a tube in its clamp and a lid on its jar are left alone.

The ordering also works out: the settle passes run within the first second of a scene load, which
is inside `MovementLatch`'s two-second window — so lifting the burner out of the table cannot light
it.

### 12.3 Files

```
NEW  VR/Assets/Scripts/MovementLatch.cs      One correct implementation, shared by four controls
MOD  LightFire.cs                            Latch; flame out in Awake; null guards
MOD  RotateButton.cs                         Round 2's bespoke guard replaced by the shared latch
MOD  NatriumContainerScriptAnimation.cs      Latch (same latent bug)
MOD  UnscrewPotassiumContainer.cs            Latch (same latent bug)
MOD  DesktopObjectSettler.cs                 Directional control rule; controls included in the scan
MOD  LabEffectsInitializer.cs                Sweeps the burner flame
```

Compile unchanged: **0 errors, 68 warnings, zero new.** Audio suite still 22/22.

Still not Play-tested — the editor holds the project lock. The burner lift in particular is
reasoned from the scene data (`burnerSupport` and the `LightFire` component resolve to the same
`fileID`, and the flame's `playOnAwake` flag was read out of `LabScene.unity`) rather than observed.

### 12.4 Round 3 follow-up — the burner was never in the scan

The Round 3 fix was still not enough, and reading the scene data rather than assuming showed why.

`LightFire.burnerSupport` is **not the burner**. Resolving the fileIDs in `LabScene.unity`:
GameObject `802329528` carries the `LightFire` component and is a *stripped* entry belonging to
prefab instance `802329525` — it is a **child inside the burner prefab**, not its root. So:

- settling the object the script sits on would have lifted a support ring out of the burner and
  left the burner in the table;
- and the burner's actual root has no `ObjectGrabbable` (it is not referenced by any script's
  GameObject field) and no `DesktopInteractable` (that is on the child) — so it was **not in the
  settle scan at all**, which is why every other object came out right and this one did not.

The burner is now submitted to the scan as a whole object, via `burnerSupport.transform.root`.
That is safe *specifically because* the burner prefab instance is top-level in the scene
(`m_TransformParent: 0`, read from the file). It would not be safe as a general rule — this scene
nests glassware under grouping transforms called `Containers` and `BerzeliusGlasses`, and walking
to the root of one of those would try to settle the whole group — so three guards bound it:

| Guard | Purpose |
|---|---|
| `AddIfNotNested` | A transform inside something already listed is never listed again. Without it the burner would be settled twice — once whole, then again by its own support child, which would pull the support back out of the burner. |
| `MaxFootprint` (1.5 m) | Nothing the size of a bench, a group or the room is ever moved. A safety net, not a tuning value. |
| Lift-only for anything containing a control | `isControl` now looks at **children as well as parents**. The burner's control hangs below the transform being measured, so a parents-only test would have called the burner "not a control" and allowed it to be lowered. Anything containing a control can now only be lifted out of solid geometry, capped at 35 cm. |

Two further corrections found along the way:

- **Hidden geometry was inflating bounds.** `TryGetWorldBounds` skipped disabled *components* but
  not renderers and colliders on inactive GameObjects. The burner's flame is a child of the burner
  and is switched off until lit, so the burner measured as reaching to the top of a flame that was
  not there. Now skipped via `activeInHierarchy`.
- **The movement latch is re-armed in `OnEnable`, not just `Start`.** Both labs switch equipment
  off until its experiment is chosen; a burner re-activated later would be placed by the settler
  long after the window opened by `Start` had expired, and lighting itself again.

Compile unchanged: **0 errors, 68 warnings, zero new.** Audio suite still 22/22.
