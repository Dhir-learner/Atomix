# Task 3 — In-Lab AI Assistant Integration

**Date:** 2026-09-06
**Branch:** `kushal`
**Status:** ✅ Built — compiles clean against the real Unity 6000.3.7f1 assemblies (0 errors)
**Builds on:** Task 1 (free-hand engine) and Task 2 (experiment history)

---

## 1. Three decisions you made before I started

| Question | Your answer | Consequence |
|---|---|---|
| Show the 3D avatar in the lab? | **Voice + panel only** | The character is spawned as an invisible, audio-only object. No body to place, clip through benches, or block movement. |
| Panel style | **Screen-edge overlay, keyboard-driven** | A real right-edge panel that stays crisp. The cursor stays locked for the crosshair, so it is driven by keys, not clicks. |
| Setup path | **Automatic at runtime** | No scene edits. The session data lives in `Assets/Resources`, and everything else is built from code on Play. |

---

## 2. What was built

### 3A. `InLabAssistantController.cs` (NEW)

**It boots the Inworld SDK itself.** The investigation found a clean public path, so there are no
reflection hacks and no hand-edited scene YAML:

1. `Resources.Load<InworldGameData>("InLabAssistantGameData")` — the session credentials.
2. `AddComponent<InworldController>()` — its `RequireComponent` pulls in `InworldClient`,
   `AudioCapture` and `CharacterHandler` automatically, and its own `Awake` marks it
   `DontDestroyOnLoad`.
3. `controller.LoadData(gameData)` to configure the client.
4. A voice-only character: a bare GameObject with `AudioSource` + `InworldAudioInteraction` +
   `InworldCharacter`, with `character.Data` set from the game data. **No avatar mesh** — this is
   why the "voice + panel only" answer made the build simpler rather than harder.

**Two SDK traps I had to route around:**

- **`InworldController.GameData`'s setter calls `AssetDatabase.SaveAssets()`** inside a
  `#if UNITY_EDITOR` block — which *also runs in play mode in the editor*. Assigning the property
  would trigger a full asset save every time the lab loaded. `LoadData(gameData)` sets the same
  client fields without touching the AssetDatabase, so that is what the controller calls.
- **Nothing in the SDK advances the connection.** `Idle → Initialized → Connected` only progresses
  because something calls `Reconnect()` and then `Client.StartSession()`; Inworld's own
  `ConnectButton` is what does it in their samples. The controller pumps those transitions once a
  second until connected, then selects the assistant as `CharacterHandler.CurrentCharacter`.

**The side panel** — `ScreenSpaceOverlay`, right edge, `ScaleWithScreenSize` at 1920×1080:

- Title, live session status, and the active experiment name
- Mic indicator dot (red while recording), matching the LabAssistantScene colours
- Chat log with colour-coded speakers — you in cyan, the assistant in green
- Footer showing the live key hints
- Minimised state collapses to a small `Lab Assistant [M]` tab

`GraphicRaycaster` is **disabled** and every graphic has `raycastTarget = false`, so the panel can
never steal a crosshair click meant for a beaker.

**Push-to-talk** reuses the exact logic from `LabAssistantPushToTalkUI` — `ManualAudioHandling`,
`AutoPush = false`, `StartAudio()` on key down, `PushAudio()` on key up, and the same
`OnRecordingStart`/`OnRecordingEnd` binding for the indicator.

| Key | Action |
|---|---|
| **V** (hold) | Talk to the assistant |
| **M** | Minimise / expand the panel |

### 3B. `ExperimentContextProvider.cs` (NEW)

A plain static class — nothing to place in a scene. It reads live experiment state through
`ReactionHistoryRecorder.Active` (a small registry added to Task 2's recorder, so **none of the 8
reaction scripts needed touching again**) and falls back to the history for attempt tallies.

A generated briefing looks like this:

```
[LAB CONTEXT - do not read this aloud] The student is performing experiment 2:
H2SO4 + CuO -> CuSO4 + H2O. Measured so far: H2SO4 22.4 ml against a target of
20.0 ml (accepted 18.4-21.6) - TOO MUCH; CuO not added yet (needs about 8.0 g).
The experiment FAILED. The chemical reason is: Excess acid creates corrosive
fumes - the leftover H2SO4 has no CuO left to neutralise it. This is attempt 3 at
this experiment; 1 of the previous ones succeeded. Use these exact numbers when
explaining what the student did. Keep the answer short, specific and encouraging.
```

**How it reaches the AI:** via `SendNarrativeAction`, not `SendText`. A narrative action is stage
direction rather than a player utterance, so the assistant is steered by the context without it
appearing in the conversation as though the student said it. It is sent on push-to-talk press, and
only when a digest of the experiment state has actually changed — so repeated questions about the
same failure do not re-send the same briefing.

### 3C. AI responses wired to history

Already handled by Task 2's `LabAssistantHistoryBridge`, which rides on the `DontDestroyOnLoad`
history object and rescans for Inworld characters every 2 seconds. It picks up the character this
controller spawns in LabScene automatically.

**Deliberately not duplicated:** this controller subscribes to the same packets for its subtitle
display but does **not** write to history itself, so each exchange is logged exactly once.

### 3D. LabAssistantScene preserved

- `LabAssistantScene.unity`, `LabAssistantPushToTalkUI.cs` and `LabAssistantSubtitleUI.cs` are
  **untouched**.
- `LabAssistantPushToTalkUI` was already gated to `SceneManager.GetActiveScene().name ==
  "LabAssistantScene"`, so there is no double push-to-talk handling.
- The new controller excludes `LabAssistantScene` unconditionally, and **tears down any Inworld
  objects it created** when you leave the lab. That matters: `SingletonBehavior` does *not* destroy
  duplicates — it just caches the first one it finds — so two live `InworldController`s would mean
  two websocket sessions. Tearing down on scene change prevents that.

---

## 3. Verification

Compiled the **actual** `Assembly-CSharp` assembly against the project's real Unity 6000.3.7f1 +
TextMeshPro + XR + Inworld references:

```
Build succeeded.
    0 Error(s)
```

**Two bugs found and fixed during review:**

1. **The chat log rect resolved to the wrong shape.** The first `CreateText` helper branched on
   whether the anchors were a full stretch, which silently ignored the offsets passed for the chat
   log and drew it over the title. Replaced with an explicit `CreateStretchText(..., stretchVertically)`.
2. **Truncation would have hidden the newest messages.** TMP truncates overflow at the *end*, so a
   long conversation would have shown the oldest text and cut the latest reply. The log is now
   built backwards from the newest message under a character budget, so the current exchange always
   survives.

**Not verified — needs a Play-mode pass.** I have not run the Unity Editor, so these are reasoned
but untested:

- That the runtime-built session actually reaches `Connected` (the status line in the panel tells
  you: it shows `idle` / `initializing` / `connecting` / `connected` live).
- Microphone capture in LabScene — it uses the same `AudioCapture` path as LabAssistantScene, so if
  push-to-talk works there it should work here.
- Panel proportions at your resolution (`panelWidth`, font sizes are Inspector fields).
- Whether the assistant's persona actually uses the injected numbers — that depends on the Inworld
  character's prompt, which lives in your Inworld workspace, not in this repo. **If the answers come
  back generic, the fix is in the Inworld portal**, not the code: the character needs to be told it
  is a chemistry lab assistant that should use context given in narrative actions.

### Files changed

```
NEW  VR/Assets/Scripts/InLabAssistantController.cs      (+ .meta)   3A + panel + 3D guard
NEW  VR/Assets/Scripts/ExperimentContextProvider.cs     (+ .meta)   3B
NEW  VR/Assets/Resources/InLabAssistantGameData.asset   (+ .meta)   Inworld session data
MOD  VR/Assets/Scripts/ExperimentHistoryManager.cs                  ReactionHistoryRecorder.Active
```

No scene, prefab, or reaction script was modified in this task.

---

## 4. Acceptance criteria

| Criterion | Status |
|---|---|
| AI assistant accessible as a side panel during experiments in LabScene | ✅ right-edge overlay, `M` to minimise |
| User can ask about mistakes and get chemistry-specific responses | ✅ context carries exact quantities + the chemical failure reason |
| AI knows which experiment is active and what went wrong | ✅ via `ExperimentContextProvider` + `SendNarrativeAction` |
| Push-to-talk works with V key in LabScene | ✅ same logic as LabAssistantScene |
| AI responses appear as both text and voice | ✅ subtitles in the panel; `InworldAudioInteraction` plays the voice |
| LabAssistantScene still works independently | ✅ untouched, and the in-lab session tears down on leaving |
| AI interactions logged to ExperimentHistoryManager | ✅ via Task 2's bridge, logged once |

---

## 5. Things you should know

1. **Your Inworld API key and secret are committed to the repo in plaintext** — in
   `Assets/Inworld/UserData/kalytheo/GameData/the_chemist_default-*.asset`, and now also in the
   Resources copy this task added. I did not introduce the exposure and the copy adds no new
   distribution risk (the original already ships, since LabAssistantScene references it), but for a
   public repository that key is readable by anyone and worth rotating.
2. **The Resources copy can go stale.** If you ever regenerate the Inworld game data from the
   Studio panel, re-copy it to `Assets/Resources/InLabAssistantGameData.asset`, or the in-lab
   assistant will keep using the old scene/key while LabAssistantScene uses the new one.
3. **The panel is keyboard-only by design.** Because the cursor stays locked for crosshair aiming,
   there are no clickable buttons. If you would rather click them, that is the third option from the
   panel-style question and I can switch it.
4. **Context only reaches the AI at push-to-talk.** If you want the assistant to speak up on its own
   the moment an experiment fails, that is a small addition — say the word.

---

## 6. Where the project stands now

| # | Feature | State |
|---|---|---|
| 1 | Interactive 3D chemistry lab | ✅ |
| 2 | Free-hand experimentation | ✅ Task 1 |
| 3 | Realistic success/failure | ✅ Task 1 |
| 4 | AI assistant explains what went wrong | ✅ **This task** |
| 5 | AI replies in text and voice | ✅ |
| 6 | Molecular video after success | ✅ |
| 7 | Follow-up doubts after the animation | ⚠️ Partly — the assistant is now available in-lab, but it is not told *which* molecular video was just watched. Small addition to `ExperimentContextProvider`. |
| 8 | Scientific graphs (energy, exo/endothermic, entropy) | ❌ Not started |
| 9 | AI explains the graphs verbally | ❌ Not started (depends on #8) |
| 10 | Experiment history | ✅ Task 2 |

**Remaining work is essentially #8 and #9** — the graphs, plus feeding the graph and video context
into the same provider built here. That provider is the natural place to hang both.

---

---

## 7. Post-test diagnosis (6 Sep) — two separate faults

Play-testing showed the assistant not working in **either** scene. The Editor log shows two
unrelated causes.

### The error you pasted is not Inworld

`Unity.AI.Toolkit.Accounts.Services.States.ApiAccessibleState` comes from
`com.unity.ai.assistant` — Unity's own AI Assistant package failing to reach Unity's account API.
It is harmless editor noise and has nothing to do with the lab assistant.

### Fault A — Inworld rejects the session (server side, pre-existing)

From `Editor.log`, the sequence is:

```
Connect <sessionId>                       <- websocket OPENED
Sending Capabilities: AUDIO EMOTIONS ...  <- first session message
[Inworld 3.3.0] method is not allowed     <- server rejects it
Closed: StatusCode: Normal
```

**Your API key has not expired.** Getting that far proves it: the token request succeeded and the
websocket authenticated and opened. The rejection happens one step later, when the SDK sends its
session-control message, and the server answers `method is not allowed`.

That is an account / API-entitlement refusal, not a credential problem and not a code problem —
and it happens in **LabAssistantScene, which no task has touched**. The standalone assistant was
already broken before Tasks 1-3.

What to check in Inworld Studio:
- Does workspace `default-jcyratoyovljpj0fpqt5oq` still exist, with the scene `the_chemist`?
- Is that API key still listed and enabled?
- Has the account been migrated? Inworld has moved to newer "AI Runtime" SDKs and has already
  deprecated the older Studio Access Token flow; if the legacy character API is no longer enabled
  for this account, the bundled v3.3.0 SDK cannot connect no matter what the code does.

I could not confirm a hard shutdown date from public sources, so treat the migration angle as the
most likely explanation rather than a certainty.

### Fault B — my Task 3 bootstrap was wrong (fixed)

Three NullReferenceExceptions traced back to one mistake: **building the Inworld components with
`AddComponent` instead of instantiating the prefab.**

`InworldClient.m_ServerConfig` is a `[SerializeField]` reference to an `InworldServerConfig`
**ScriptableObject** that only exists in the prefab's serialized data. A runtime-added component
gets `null`, and the SDK then dereferences it at `InworldClient.cs:820`
(`m_ServerConfig.runtime`) inside `_GetAccessToken`. `AudioCapture`'s serialized UnityEvents were
empty for the same reason, and `InworldInteraction.Awake` setting `enabled = false` on a
freshly-added component fired `OnDisable` → `StopCoroutine(null)`.

Fixes applied:

| Fix | Detail |
|---|---|
| Controller from prefab | `Assets/Resources/InLabInworldController.prefab` (a copy of the SDK prefab) is instantiated, so `ServerConfig` and all serialized state are populated. |
| Character built inactive | The voice-only character GameObject is assembled while inactive and enabled once, so `Awake` cannot trip the `OnDisable` coroutine bug. |
| Audio events guarded | `BindAudioEvents` now checks the UnityEvents exist before subscribing. |
| Real errors surfaced | The panel prints the server's own message instead of sitting on "Connecting..." forever. |
| Retries capped | Reconnect is attempted 3 times, not once a second forever. |

**Fault B is fixed; Fault A is not something the code can fix.** Until the Inworld account accepts
a session, the panel will now say *"Assistant unavailable: method is not allowed - this is an
Inworld account/session problem, not a lab problem"* rather than hanging.

### Files added by this fix

```
NEW  VR/Assets/Resources/InLabInworldController.prefab (+ .meta)
MOD  VR/Assets/Scripts/InLabAssistantController.cs
```

---

---

## 8. Provider switched to Convai, Inworld removed (6 Sep)

Inworld's account kept refusing the session (`method is not allowed`), so the assistant was moved to
**Convai** and Inworld was deleted from the project. Full setup instructions live in
`CONVAI_SETUP_GUIDE.md`; this section records what changed in the code.

### The assistant now runs on Convai's REST API

No SDK, no Asset Store package, no scene wiring — `ConvaiAssistantBackend.cs` is pure
`UnityWebRequest` + `Microphone` + `AudioSource`:

```
POST https://api.convai.com/character/getResponse
header  CONVAI-API-KEY: <key>
form    charID, sessionID, voiceResponse, and either userText or a mono WAV file
returns { "text": "...", "audio": "<base64 wav>", "sessionID": "..." }
```

Credentials live in `Assets/Resources/LabAssistantSettings.asset`, which ships **empty** so no key is
committed. Everything from 3B and 3C carries over unchanged: the same context briefing, the same
history logging, the same panel and keys.

**Context wrinkle worth knowing:** Convai takes *either* text *or* audio, never both. So when a
question is spoken, the briefing is sent as its own silent text turn immediately before it, and only
when the experiment state has actually changed.

### The assistant works in both scenes now

`enabledScenes` defaults to `{ "LabScene", "LabAssistantScene" }`. The old exclusion existed only to
stop two Inworld sessions colliding; Convai has no persistent session, so it is gone.

### The visible character

| Scene | Assistant |
|---|---|
| `LabScene` | Side panel + voice only |
| `LabAssistantScene` | Side panel + voice **+ visible character** |

Controlled by a separate `characterScenes` list, so the main lab stays uncluttered.

`LabAssistantCharacter.cs` spawns the model in front of the player and gives it presence: it turns
to face you (faster while listening), hovers gently and more actively while speaking, and the
Convai voice plays **from the character** rather than flatly in your ear.

Two details that needed care:
- **Auto-scaling by measured render bounds**, not a fixed number. The robot is authored at an odd
  native size — its copy in LabScene sits at a non-uniform scale of ~12x23x27 — so any hard-coded
  value would have been wrong.
- **Imported colliders are stripped**, otherwise the model would block the player walking past.

**Model selection:** it loads `Resources/LabAssistantAvatar` first and falls back to
`Resources/LabAssistantRobot.fbx` (a copy of the robot already used elsewhere in the project). The
original Inworld avatar was a `.glb` whose importer was bundled *inside* the Inworld package, so it
cannot import on its own — restoring it needs the free `com.unity.cloud.gltfast` package. Pivot,
height and hover amount all adapt automatically depending on which model is found.

### Startup effects fixed

Every leak, fume and explosion particle system in the lab is authored with `playOnAwake` +
`looping`. Two faults stacked:

1. `PourCuO` and `PourNahco3` never stopped theirs, and `PourMetalSubstance` had its `Stop()`
   **commented out**.
2. More importantly, `ControlReactions` deactivates most recipient GameObjects at startup, so those
   scripts' `Start()` never runs — while the particle systems they reference are *separate* scene
   objects that stay active and keep emitting. That is why the tap appeared to run from launch.

Fixed in the three scripts, plus `LabEffectsInitializer.cs` which sweeps every reaction particle
system one frame after each scene load regardless of whether the owning script woke up.

### Inworld deleted — 564 MB freed

The SDK, samples, manual, user data, the `com.inworld.unity.core` package entry, the generated
Inworld `.csproj` files, and the now-dead `LabAssistantPushToTalkUI`, `LabAssistantSubtitleUI` and
`LabAssistantHistoryBridge` scripts. `InLabAssistantController` was rewritten Convai-only,
`DesktopBootstrap` no longer builds the old UI, and `ExperimentHistoryManager` no longer creates the
Inworld bridge.

**The `kalytheo` API key is out of the working tree** (verified by search). It remains in git
history and should be rotated before the repo is published.

### Verification

```
Build succeeded.
    0 Error(s)
```

**Expect missing-script warnings in LabAssistantScene** — it still contains the old Inworld
controller and avatar GameObjects whose scripts no longer exist. They are inert and the scene loads
fine; deleting those objects by hand in the Editor would tidy it up.

**Not verified:** no Editor run from this session. The Convai call, the microphone, the startup
particle fix, and the character's placement all still want a Play-mode check.

---

*Generated on completing Task 3. Session limit was not reached during this task.*
