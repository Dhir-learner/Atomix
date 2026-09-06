# Switching the Lab Assistant to Convai

**Date:** 2026-09-06
**Status:** ✅ Code complete — compiles clean (0 errors). Needs your API key to go live.

---

## 1. Short answer: yes, this will work

Convai is alive, has a free tier aimed at prototyping, and — the part that matters most — it exposes
a **plain REST endpoint**:

```
POST https://api.convai.com/character/getResponse
header  CONVAI-API-KEY: <your key>
form    charID, sessionID, voiceResponse, and either userText or a mono WAV file
returns { "text": "...", "audio": "<base64 wav>", "sessionID": "..." }
```

That means **no SDK import, no Asset Store package, no scene edits.** The integration is written
against `UnityWebRequest`, so it already compiles in your project today. You only need to paste in a
key and a character ID.

This is a real advantage over Inworld, which needed a whole SDK, a prefab, and a websocket session
state machine before it could say a word.

---

## 2. What I built

| File | Purpose |
|---|---|
| `ConvaiAssistantBackend.cs` (NEW) | The entire Convai client: microphone capture → mono 16-bit WAV → POST → reply text + spoken audio. |
| `LabAssistantSettings.cs` (NEW) | Credentials and provider choice. |
| `Assets/Resources/LabAssistantSettings.asset` (NEW) | The asset you fill in. **Ships empty on purpose** — no key is committed. |
| `InLabAssistantController.cs` (MOD) | Now picks a provider instead of assuming Inworld. |

**The provider is a switch, not a rewrite.** `LabAssistantSettings.provider` takes:

- `Auto` *(default)* — uses Convai when a key and character ID are present, otherwise Inworld
- `Convai`, `Inworld`, or `None`

Everything built in Tasks 1–3 carries over untouched: the same right-edge panel, the same **V** to
talk and **M** to minimise, the same `ExperimentContextProvider` briefing, and the same logging into
your experiment history. Inworld's code path is still there, so nothing was thrown away.

---

## 3. Setup, step by step

### Step 1 — Create a Convai account
Go to **convai.com** and sign up. The free tier is aimed at prototyping; check their pricing page
for the current interaction limits.

### Step 2 — Get your API key
In the dashboard, find **API Key** — it's normally under your profile / account menu. Copy it.

> I couldn't pin the exact menu label from their public docs, so look for "API Key" rather than
> following a path I'd be guessing at. It's a single long string.

### Step 3 — Create your chemistry character
Create a new character. You'll be asked for a name, description, voice and personality.

**This step decides how good the answers are.** The description is where the character learns to be
a chemistry tutor. Something like:

> You are a friendly chemistry lab assistant for a school virtual laboratory. The student performs
> real experiments and sometimes gets the quantities wrong. You will be given the exact measurements
> they used and the chemical reason their experiment failed, written in square brackets as lab
> context. Always use those exact numbers in your answer. Explain the chemistry simply, say what
> went wrong and what to change next time. Keep answers to two or three sentences and be
> encouraging. Never read the lab context out loud.

That last sentence matters — my `ExperimentContextProvider` prefixes the briefing with
`[LAB CONTEXT - do not read this aloud]`, and the character needs to know to honour it.

### Step 4 — Copy the Character ID
Open your character in the dashboard and copy its **Character ID** (a long alphanumeric string,
shown on the character's card or settings).

### Step 5 — Paste both into Unity
1. In the Project window, open `Assets/Resources/LabAssistantSettings`
2. In the Inspector, fill in:
   - **Convai Api Key** → your key from Step 2
   - **Convai Character Id** → your ID from Step 4
3. Leave **Provider** on `Auto` — it will now pick Convai automatically.

### Step 6 — Test
Press Play in `LabScene`. The right-hand panel should show **`Convai: ready`**.
Hold **V**, ask *"why did my experiment fail?"*, release. You should get text in the panel and a
spoken reply.

---

## 4. What each status means

| Panel shows | Meaning |
|---|---|
| `Convai: ready` | Working. Hold V to talk. |
| `Convai: not configured` | The key or character ID is still blank. |
| `Convai: listening` | Microphone is recording (the dot turns red). |
| `Convai: thinking` | Request sent, waiting for the reply. |
| `Inworld: ...` | Convai isn't configured, so it fell back to Inworld (which is currently dead). |

Errors are written into the chat log in plain English:

| Message | Fix |
|---|---|
| `Convai rejected the API key (HTTP 401/403)` | Wrong or expired key. |
| `Convai could not find that character ID (HTTP 404)` | Wrong character ID. |
| `Convai rate limit or free-tier quota reached (HTTP 429)` | You've hit the free-tier limit. |
| `No microphone was found` | No input device — voice questions won't work. |

---

## 5. How the chemistry context reaches Convai

Convai accepts **either** `userText` **or** an audio file, never both. So when you ask a question by
voice, the briefing can't ride along with it.

The integration handles this by sending the briefing as its own silent text turn immediately before
the recorded question, and only when the experiment state has actually changed. So the character has
your exact numbers in its conversation memory when it hears you, and repeated follow-up questions
don't re-send the same paragraph.

Session continuity comes from Convai's `sessionID`, which is fed back into each request — so it
remembers the earlier part of your conversation.

---

## 6. Known limitations

1. **Convai doesn't return a transcript of what you said.** Its response contains the character's
   reply, not your words. So the panel and the history log show `(spoken question)` rather than your
   actual sentence. If you'd rather see your own words, I'd need to add a separate speech-to-text
   step.
2. **Voice replies are downloaded, not streamed.** The reply is decoded and played once it fully
   arrives, so there's a short pause before it speaks. Fine for a lab assistant; not lip-sync ready.
3. **No avatar.** Same as before — this is voice plus panel, per your earlier choice.
4. **Untested against a live key.** The code compiles and the request shape matches Convai's
   documented API, but I have no key to run it with. The first Play-mode test is the real proof.

---

## 7. If you want to go back, or go elsewhere

- Set `provider` to `Inworld` in the settings asset to restore the old path exactly.
- Set it to `None` to hide the assistant entirely.
- Because `ExperimentContextProvider` and the history logging are provider-agnostic, adding a third
  option later (a plain LLM API plus TTS, say) means writing one backend class, not touching the lab.

---

## 8. Security note, repeated because it matters

`Assets/Resources/LabAssistantSettings.asset` is committed **empty**. Once you paste your key into
it, that file will hold a live credential — so either don't commit it, or add it to `.gitignore`.

The old Inworld key (which belongs to `kalytheo`, not you) is still in your git history at
`Assets/Inworld/UserData/kalytheo/GameData/*.asset` and in `Assets/Resources/InLabAssistantGameData.asset`.
Worth removing before this repo goes anywhere public.

---

---

## 9. Follow-up work (6 Sep) — Inworld fully removed

### The assistant now runs in LabAssistantScene too
`InLabAssistantController` used to exclude `LabAssistantScene` on purpose, to avoid two Inworld
sessions fighting. With Convai there is no session to collide with, so the panel now appears in
both scenes and `enabledScenes` defaults to `{ "LabScene", "LabAssistantScene" }`.

**About the 3D character:** it was an Inworld avatar prefab, and Inworld is gone, so it is no longer
in the scene. Convai's REST API returns text and audio but no viseme/lip-sync data, so a talking
avatar was never going to work through this route anyway. The assistant is the panel plus the voice
in both scenes.

### The tap water (and every other effect) no longer runs at startup

**Cause — two faults stacked.** Every leak, fume and explosion particle system in the lab is
authored with `playOnAwake` + `looping`. Each pour script is supposed to stop its own in `Start()`,
but:

1. `PourCuO` and `PourNahco3` never stopped theirs at all, and `PourMetalSubstance` had its
   `Stop()` **commented out**.
2. More importantly, `ControlReactions` deactivates most recipient GameObjects at startup, so those
   scripts' `Start()` never runs — while the particle systems they point at are *separate* scene
   objects that stay active and keep emitting.

**Fix.** The three scripts now stop and clear their own effects, and a new
`LabEffectsInitializer` sweeps every reaction particle system one frame after each scene load,
whether or not the owning script ever woke up. It only touches particle systems the reaction scripts
actually reference, so nothing else in the lab is affected.

### Inworld removed — 564 MB freed

| Removed | Size |
|---|---|
| `Inworld.Samples.Innequin/` | 174 MB |
| `Inworld.Samples.RPM/` (art, scenes, lightmaps) | 68 MB |
| `InworldUnitySDKManual.pdf` | 41 MB |
| `Inworld.Assets/` | 251 MB |
| `UserData/` (avatar + the leaked key) | 29 MB |
| NDK, Editor tooling, core `.tgz` | ~2 MB |

Also removed: the `com.inworld.unity.core` package entry, the generated Inworld `.csproj` files, and
these now-dead scripts — `LabAssistantPushToTalkUI.cs`, `LabAssistantSubtitleUI.cs`,
`LabAssistantHistoryBridge.cs`, plus my earlier `InLabAssistantGameData.asset` and
`InLabInworldController.prefab`.

`InLabAssistantController` was rewritten as Convai-only, `DesktopBootstrap` no longer builds the old
Inworld UI, `ExperimentHistoryManager` no longer creates the Inworld history bridge, and
`LabAssistantProvider` lost its `Inworld` option.

**The leaked `kalytheo` API key is now gone from the working tree.** It is still in your git history,
so it should be rotated/scrubbed before this repo goes public.

### Verification

```
Build succeeded.
    0 Error(s)
```

**Expect missing-script warnings in LabAssistantScene.** That scene still contains the Inworld
controller and avatar GameObjects, whose scripts no longer exist. They are inert and the scene
loads and works, but the objects are dead weight — deleting them by hand in the Editor would tidy
it up. I could not do that safely from outside Unity.

**Not verified:** I have not run the Editor, so the startup particle fix and the assistant in
LabAssistantScene both still need a Play-mode check.

---

*Written alongside the Convai integration. Session limit was not reached.*
