# Atomix Mobile — Plan (Android + iPhone, free with ads)

Status: **approved, in progress on the `mobile` branch.**

### Answers to section 3 (2026-09-16)
| # | Answer |
|---|---|
| D1 iPhone | **Same as Android** — free, child-safe ads for under-13s. You accepted the higher App Store rejection risk |
| D4 Videos | **Compress to 720p** on phones; Windows keeps the 1080p originals |
| D7 Oculus Hands | **Remove** from all scenes (fixes Windows too) |
| Modules | **Android only for now**; iOS module later, when a Mac is available |
| D2, D3, D5, D6 | Still open — recommendations in section 3 are the working assumption (no ads during timed tests, personal Play account) |

## 1. What you decided

| Decision | Choice |
|---|---|
| Platforms | Android **and** iPhone |
| Controls | Touch only — no keyboard or mouse |
| Monetization | Free, Google AdMob ads |
| Audience | **Mixed ages** — a neutral age question at first launch; children get child-safe ads |
| When ads show | **After a successful experiment only**, at most **one every 3 minutes**; failures and retries never trigger an ad |
| Approach | Plan first, then build |

The Windows itch.io version stays exactly as it is: paid, no ads, keyboard and mouse. Both versions are built from the same project.

---

## 2. Facts that shape the plan

### The project today
- **No mobile build support is installed.** Unity 6000.3.7f1 only has the Windows module. Android and iOS support must be added through Unity Hub (several GB).
- **Keyboard and mouse are read directly in 21 scripts, 71 places** (full list in section 5). There is no touch code anywhere.
- **Aiming is centre-screen.** `ObjectInteraction` picks up objects and clicks lab panels through a raycast from the middle of the screen. That model works on a phone: drag to aim the reticle, tap a button to act.
- **Menus already accept touch.** `DesktopUIInput` adds Unity's `StandaloneInputModule`, which handles touches.
- **The experiment-end hook exists.** `ExperimentHistoryManager.Completed` fires only on success (`Resolved` fires on every outcome). The graph panel and post-success learning video already listen to `Completed`, so an ad must wait until those close.
- **Windows-only features:**
  - `OfflineVoice` speaks through PowerShell, and `IsSupported` is true only on Windows, so the assistant would be text-only on phones.
  - `LabTextInput` reads the physical keyboard (`Input.inputString`), so typed questions need the phone's on-screen keyboard.
  - `ExamReportExporter` saves test reports to the Windows Desktop and opens the folder; phones need save-and-share.
- **Size is far over mobile limits.** The 7 reaction MP4s total about 480 MB. Add the 53 MB desk model (`pupitru1.fbx`) and several 20–26 MB 4K textures. The Windows build is 891 MB.
- **Android settings are still set up for Meta Quest:** Vulkan only, target SDK 29, identifier `com.AlinaInc.UnityLab`. Phones need different values.

### Rules from Google and Apple
| Rule | Consequence |
|---|---|
| Google Play: base download capped at **200 MB**; larger apps use **Play Asset Delivery** | Videos and textures must shrink or move out of the base download |
| Google Play: from **31 Aug 2026**, apps must target **Android 16 (API 36)**. Unity 6.3 supports it | Target API 36 |
| Google Play Families policy, mixed audience: needs a **neutral age screen**; child-directed ads for children **and users of unknown age**; max ad rating **G**; a Families self-certified ad SDK (AdMob is one); no advertising ID for children | An age gate before any ad loads; per-age ad settings |
| AdMob: apps not listed on a supported store (e.g. an itch.io APK) get **limited ad serving** | The ad version must be published on Google Play / the App Store to earn anything |
| Google Play: **personal** developer accounts created after Nov 2023 need a **closed test with 12 testers for 14 days** before production. Organization accounts are exempt | Add at least 2 weeks to the Android launch |
| AdMob consent: Google's UMP consent SDK does **not** pass the under-age tag to ads; it must be set on every ad request | The ad code sets it explicitly |
| **Apple:** *"Apps intended for kids may not include third-party advertising or analytics"*, except contextual ads from services that human-review creatives for kids | **AdMob on iPhone is a real rejection risk** for a school chemistry app — see decision D1 |
| Apple: iOS apps must be compiled in Xcode on a **Mac**; publishing needs the **Apple Developer Program ($99/year)** | iPhone builds need a Mac |
| Unity 6.3 **Build Profiles** can override Player settings and scene lists per profile | One project yields Windows, Android and iOS builds with different settings |

---

## 3. Decisions needed before building

| # | Question | Recommendation |
|---|---|---|
| **D1** | **iPhone monetization.** AdMob in an app Apple may consider "intended for kids" can be rejected. Options: (a) iPhone version **paid, no ads**; (b) ads only for users who say they are **13+**, no ads at all for younger users, accepting review risk; (c) same as Android and hope review passes | **(a)** paid, or (b) if you want free on iPhone |
| **D2** | **Free mobile vs $4.99 Windows.** The same 8 experiments free on phones may reduce itch.io sales | Accept it (different audiences), or keep some content Windows-only |
| **D3** | **Ads in the Testing scene.** Showing an ad after each correct task would interrupt a timed test | No ads during a test; one ad opportunity when the results card appears |
| **D4** | **Reaction videos on phones.** They are ~480 MB. Options: (a) compress to 720p (roughly 60–100 MB total, to confirm by measuring); (b) leave them out and use the existing live 3D molecular view, which covers all 8 reactions; (c) download them through Play Asset Delivery / On-Demand Resources | **(a)**; fall back to (b) if the size is still too big |
| **D5** | **Google Play account type.** Personal ($25) needs the 12-tester, 14-day closed test. Organization ($25, needs a registered business and D-U-N-S number) is exempt | Personal, unless the team is a registered business |
| **D6** | **Mac access for iPhone builds.** A team member's Mac, or a cloud Mac (for example GitHub Actions macOS runners, free for public repos but signing is fiddly) | A team member's Mac |
| **D7** | **Oculus Hands license** — still unresolved from the Windows guide. Meta's license limits these models to Meta devices, and phones are not | Remove or replace the hands on the mobile branch; this also fixes the Windows issue |

---

## 4. Architecture

### 4.1 One project, three build profiles
| Profile | Target | Scripting defines | Notes |
|---|---|---|---|
| `Windows (itch.io)` | Windows x64 | — | Unchanged; the existing `Atomix → Build Windows (itch.io)` |
| `Android Phone` | Android ARM64, IL2CPP, **target API 36**, min API 29, Vulkan **+ OpenGL ES 3** fallback, landscape only, `com.TeamAtomix.Atomix` | `ATOMIX_MOBILE;ATOMIX_ADS` | App Bundle (AAB) plus Play Asset Delivery if needed |
| `iOS` | iPhone, landscape only | `ATOMIX_MOBILE` (+ `ATOMIX_ADS` only if D1 ≠ a) | Exports an Xcode project; compiled on a Mac |

Ad code compiles only when `ATOMIX_ADS` is set, so **the Windows build contains no ad code**.

### 4.2 Input: one abstraction, two backends
A new static `LabInput` replaces the 71 direct `Input.*` calls:

```text
LabInput.Move            (Vector2)   WASD            | left virtual joystick
LabInput.Look            (Vector2)   mouse delta     | drag on the right half of the screen
LabInput.Vertical        (float)     Space / Ctrl    | up / down buttons
LabInput.Sprint          (bool)      Left Shift      | joystick pushed to the edge
LabInput.Down(LabAction) (bool)      existing KeyCode fields | on-screen buttons
LabInput.Held(LabAction) (bool)      existing KeyCode fields | held buttons
LabInput.Scroll          (float)     mouse wheel     | vertical drag inside a panel
LabInput.IsTouch         (bool)      false           | true
```

- On Windows, `LabInput` reads the same `KeyCode` fields the scripts already expose, so **keyboard behaviour stays identical**.
- On mobile, `MobileControlsUI` is a screen-space overlay built in code, like the existing panels, that feeds `LabInput`.
- On mobile, **"cursor locked" means "no full-screen panel is open"**. `FirstPersonController.IsCursorLocked` gates world interaction in several places, so it gains a mobile path instead of touching `Cursor.lockState`.

### 4.3 Touch control layout (landscape)
```text
┌─────────────────────────────────────────────────────────────────────┐
│ [≡ Menu] [📖 Book] [🕘 History] [📈 Graphs] [⚛ Table]   [💬 Assistant] │
│                                                                     │
│                           ( reticle )                               │
│                                                                     │
│   drag anywhere on the right half to look around                    │
│                                                         [↑]         │
│  ( joystick )                                           [↓]         │
│                                   [ USE / GRAB ]  [ RESET BENCH ]   │
└─────────────────────────────────────────────────────────────────────┘
While holding an object, the bottom-right swaps to:
   [ RELEASE ]  [↺ Yaw ↻]  [Tilt ▲ ▼]  [Roll ⟲ ⟳]  [Reset pose]
```
Tilt and roll are **hold** buttons: the pour rate follows how far and how long the object is tipped, as on desktop. The hold buttons are large, since pouring precision is what the experiments measure.

### 4.4 Ads flow
```text
First launch ─► AgeGate (neutral: "What year were you born?")
                  ├─ under 13 or skipped ─► child mode: no consent form, child-directed tag, max rating G, non-personalized
                  └─ 13+ ─► UMP consent form (where legally required) ─► standard ads per consent
                  (the answer is stored locally; changeable only by reinstalling, per the neutral-screen rule)

AdsService.Initialize() ─► RequestConfiguration per age mode ─► preload one interstitial

ExperimentHistoryManager.Completed (success)
   └─► AdScheduler: mark "ad owed" if ≥ 3 min since the last ad, not in a test (D3), and one is loaded
         └─► show at the next natural break:
               the graph panel closes, OR the learning video closes, OR another experiment is selected,
               OR the player returns to the main menu
         └─► after close: preload the next ad. No network, or no fill ─► skip silently, never block play
```

### 4.5 Phone-specific replacements
| Desktop feature | Mobile version |
|---|---|
| Typed questions (`LabTextInput` + physical keyboard) | `TouchScreenKeyboard` opened by the 💬 button |
| Offline voice (PowerShell TTS) | Text only in v1. v2: Android `TextToSpeech` via `AndroidJavaObject`, iOS `AVSpeechSynthesizer` via a small native plugin |
| Report saved to Desktop, "Open folder" | Save to `Application.persistentDataPath`, then the OS share sheet (email, Drive, Files) |
| Scroll wheel in panels | Drag to scroll |
| Hover highlight | Highlight whatever the reticle points at (already how it works) |
| F1 pause menu | ≡ Menu button; the Android back button also opens or closes it |

### 4.6 Size and performance budget
| Item | Plan |
|---|---|
| Videos (~480 MB) | Per D4 — Unity VideoClip import overrides for Android/iOS: transcode to 720p H.264 |
| 4K textures | Android/iOS max size overrides of 1024–2048, ASTC compression |
| `pupitru1.fbx` (53 MB) | Mesh compression High; decimate in Blender if still heavy |
| Post-processing | Ambient occlusion off on mobile; keep light bloom only if the frame rate allows |
| Quality | A mobile quality level: shadows medium, MSAA 2×, reflection probe baked |
| Target | **30 fps** on a mid-range 2022 Android phone; base AAB **< 200 MB** |

---

## 5. Every keyboard/mouse input and its touch replacement

| File | Current input | Touch replacement |
|---|---|---|
| `FirstPersonController.cs` | Mouse X/Y, WASD, Shift, Space, Ctrl, Escape | Look drag, joystick, edge-push sprint, ↑/↓ buttons, ≡ Menu |
| `ObjectInteraction.cs` | Left click (grab/use/release), R, T, Q/E, Z/X, C (+Shift), mouse wheel | USE/GRAB, RELEASE, Reset pose, Yaw, Tilt, Roll hold buttons |
| `DesktopBootstrap.cs` | B (book) | 📖 Book |
| `FlipPages.cs` | ← → pages, 1–8 quick-select | Swipe left/right on the book; tap a reaction in a picker |
| `OpenTheBook.cs` | Mouse click (commented raycast) | Handled by USE |
| `ExperimentHistoryUI.cs` | Tab, Escape, wheel, ↑/↓ | 🕘 History, ✕ close, drag scroll |
| `ReactionGraphUI.cs` | F, Escape | 📈 Graphs, ✕ close |
| `PeriodicTableUI.cs` | P, Escape | ⚛ Table, ✕ close |
| `PauseMenuUI.cs` | F1 | ≡ Menu |
| `InLabAssistantController.cs` | V (hold), Enter, Y, M | 💬 opens keyboard; "Why did it fail?" button; tap panel header to minimise; no voice |
| `LabTextInput.cs` | `Input.inputString`, Enter, Escape | `TouchScreenKeyboard` done/cancel |
| `LabHudController.cs` | L (label mode) | Tap the measurement label to cycle |
| `LabRetryController.cs` | F5 | RESET BENCH |
| `LabOnboarding.cs` | B, F1, Escape, click to dismiss | Tap to dismiss; mobile wording |
| `ReactionLearningController.cs` | Escape, Space, recentre key | ✕ close, ▶/❚❚ button, recentre button |
| `ExamCoinHud.cs` | F2 / F3 / F4 | Hint / +30s / Skip buttons on the coin strip |
| `TestResultsUI.cs` | Escape | ✕ close |
| `DesktopUIInput.cs` | Escape closes settings | ✕ / Android back |
| `LabPanelBuilder.cs` | Mouse wheel | Drag scroll |
| `CameraJuice.cs` | Shift (sprint kick) | `LabInput.Sprint` |
| `HandAnimationController.cs` | Mouse buttons, G | Follows grab state (hands likely removed per D7) |

---

## 6. File change plan

**New**
- `Scripts/Input/LabInput.cs`, `LabAction.cs` — the abstraction
- `Scripts/Mobile/MobileControlsUI.cs` — joystick, look pad, buttons (built in code, like the other panels)
- `Scripts/Mobile/MobileKeyboardInput.cs` — `TouchScreenKeyboard` bridge for `LabTextInput`
- `Scripts/Mobile/NativeShare.cs` — report sharing (Android intent / iOS share sheet)
- `Scripts/Ads/AgeGate.cs`, `AdConsent.cs`, `AdsService.cs`, `AdScheduler.cs` — compiled only with `ATOMIX_ADS`
- `Editor/AtomixMobileBuild.cs` — Android AAB and iOS Xcode export, with safety checks (test ad IDs never in release, target API 36)
- Build profiles for Android and iOS; a mobile quality level
- A privacy policy page (required by both stores when ads are present) — free on GitHub Pages

**Modified** — the 21 scripts in section 5 (input calls only), `DesktopBootstrap.cs` (skip cursor lock on mobile, spawn mobile controls), `OfflineVoice.cs` (mobile path later), `ExamReportExporter.cs` (share on mobile), video/texture/model import settings (platform overrides only)

**Package added** — Google Mobile Ads Unity plugin, which includes the UMP consent SDK. The version is pinned at implementation time.

**Unchanged** — reaction logic, `FreeHandReactionEngine`, history, graphs, molecular animation, coin economy, the Windows build pipeline

---

## 7. Implementation phases

| Phase | Work | Done when |
|---|---|---|
| **0. Setup** (you + me) | Answer D1–D7; install Android + iOS modules; create the AdMob account and apps; register Google Play ($25) and, if needed, Apple ($99/yr); create the `mobile` branch | Modules installed, AdMob app IDs available |
| **1. Input abstraction** | `LabInput` with the desktop backend; replace all 71 calls | **Windows build plays exactly as before** (run the itch.io test checklist) |
| **2. Touch controls** | `MobileControlsUI`, look/joystick, panel buttons, book swipe, drag scroll, on-screen keyboard, Android back button | Every experiment completable on an Android phone with touch only |
| **3. Phone features** | Share reports, mobile onboarding text, Windows-only features hidden or replaced | No dead buttons or Windows-only text on mobile |
| **4. Size & performance** | Texture/video/mesh overrides, mobile quality level, Play Asset Delivery if needed | Base AAB < 200 MB, ≥ 30 fps on the test phone |
| **5. Ads** | Age gate, consent, AdMob with **test ad units**, scheduler, child/adult request configuration | Test matrix (section 8) passes with test ads |
| **6. Android release** | Real ad unit IDs, signing key, privacy policy, Data safety form, Families/target audience declarations, content rating | Closed test live |
| **7. Closed test** | 12+ testers, 14 days (personal accounts) | Production access granted |
| **8. iOS** | Xcode export, Mac build, App Tracking Transparency prompt (13+ only, if ads), App Store privacy labels, TestFlight | TestFlight build approved, then App Store submission |

Phases 1–5 are code work I can do. Phase 0 installs, accounts, the Mac, testers and store forms need you.

---

## 8. Test matrix (mobile)

| # | Scenario | Expected |
|---|---|---|
| 1 | Fresh install, enter birth year under 13 | No consent form; ads child-directed, rating G; no advertising ID sent |
| 2 | Fresh install, skip the age question | Treated as a child (same as 1) |
| 3 | Fresh install, 13+, in the EEA/UK | UMP consent form shown; ads respect the choice |
| 4 | Complete an experiment successfully | No ad over the graphs/video; ad at the next natural break |
| 5 | Second success within 3 minutes | No ad |
| 6 | Fail an experiment, retry repeatedly | Never an ad |
| 7 | Timed test in the Testing scene | No ad during tasks (D3) |
| 8 | Airplane mode | Game fully playable, no ad, no error message |
| 9 | Ad fails to load / no fill | Silently skipped |
| 10 | Close the app during an ad, reopen | No crash; 3-minute cap still respected |
| 11 | Every experiment 1–8 with touch only | Completable, including precise pours |
| 12 | Book, history, graphs, table, pause, assistant | Open, scroll, close by touch; Android back closes panels |
| 13 | Typed question | Keyboard opens, answer appears |
| 14 | Download a test report | Share sheet opens with the HTML/CSV files |
| 15 | Phone rotated / different aspect ratios (16:9, 20:9, tablet) | Landscape layout fits, no buttons off-screen |
| 16 | Low-end phone | ≥ 30 fps or a documented minimum spec |
| 17 | Windows build after all changes | Identical to the itch.io checklist; no ad code present |

---

## 9. Store checklist

**Google Play** — developer account ($25) · app created with **Target audience includes children → mixed** · Ads declaration **Yes** · Data safety form (advertising ID for 13+ users only; local-only game data) · privacy policy URL · content rating questionnaire · signed AAB (API 36) · closed test with 12 testers for 14 days · production.

**AdMob** — account · Android and iOS apps · one interstitial ad unit per platform · link each app to its store listing once published · payments profile.

**Apple** — Developer Program ($99/yr) · App Store Connect app · age rating · privacy nutrition labels · App Tracking Transparency text (if ads for 13+) · Kids Category **not** selected unless D1 = paid, no ads · TestFlight → review.

---

## 10. Costs

| Item | Cost |
|---|---|
| Unity Personal, Android/iOS modules, AdMob, Google Mobile Ads plugin | $0 |
| Google Play developer registration | **$25 once** |
| Apple Developer Program | **$99 per year** |
| A Mac for iOS builds | $0 if a team member has one |
| Privacy policy hosting (GitHub Pages) | $0 |
| Ad revenue | Google keeps a share; child-directed and non-personalized ads earn noticeably less than standard ads |

---

## 11. Risks

- **Apple rejection** over ads in a kids-intended app (D1).
- **Low ad income.** Ads show only after successes, capped at 3 minutes, and many users are children.
- **Pouring precision by touch** may feel worse than keyboard. It needs playtesting with students, and the hold-button design may change.
- **Performance** of the built-in render pipeline with post-processing on low-end phones; it is unmeasured.
- **Size.** Even after compression, the base download may exceed 200 MB, which would need Play Asset Delivery (more work).
- **The 71-call input refactor touches the Windows build.** Phase 1 exists to prove Windows is unchanged before any mobile work.
- **Unresolved licensing** (Oculus Hands, unverified textures/sounds) applies equally to the mobile stores.

---

### Sources
- Google Play app size limits: https://support.google.com/googleplay/android-developer/answer/9859372
- Unity Play Asset Delivery: https://docs.unity3d.com/6000.3/Documentation/Manual/play-asset-delivery.html
- Target API 36 deadline: https://docs.unity3d.com/6000.5/Documentation/Manual/android-distribution-google-play.html
- Google Play Families policies: https://support.google.com/googleplay/android-developer/answer/9893335
- AdMob Families compliance: https://support.google.com/admob/answer/6223431
- AdMob Unity targeting (child-directed / under age of consent): https://developers.google.com/admob/unity/targeting
- UMP SDK for Unity: https://developers.google.com/admob/unity/privacy
- Google Mobile Ads Unity plugin: https://developers.google.com/admob/unity/quick-start · https://github.com/googleads/googleads-mobile-unity/releases
- AdMob app readiness / unlinked apps: https://support.google.com/admob/answer/10564477
- Google Play testing requirement for new personal accounts: https://support.google.com/googleplay/android-developer/answer/14151465
- Apple App Review Guidelines (1.3 Kids Category): https://developer.apple.com/app-store/review/guidelines/
- Unity build profiles: https://docs.unity3d.com/6000.3/Documentation/Manual/build-profiles-override-settings.html
- Unity Hub CLI (module install): https://docs.unity.com/en-us/hub/use-hub-cli
