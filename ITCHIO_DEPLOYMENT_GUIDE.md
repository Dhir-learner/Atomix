# Atomix — itch.io Windows Release Guide ($4.99)

Everything needed to sell Atomix as a paid Windows download on itch.io, in order.
The **project changes are already done** (Part A). **You** do Parts B–G.

| | |
|---|---|
| Price | **$4.99**, whole game, no in-game paywall — itch.io's checkout is the only lock |
| Platform | Windows 10/11, 64-bit, keyboard + mouse |
| Unity | 6000.3.7f1, Mono scripting backend (the IL2CPP module is not installed, and is not needed) |
| AI assistant | **Offline only** — built-in chemistry knowledge base, no Convai, no internet needed |
| Upfront cost | **$0** (itch.io, Unity Personal, butler are free). itch.io and PayPal/Stripe take fees per sale — see Part G |

---

## Part A — Changes already made to the project

| Change | File(s) | Why |
|---|---|---|
| Convai API key removed; assistant set to new `Offline` mode | `VR/Assets/Resources/LabAssistantSettings.asset` | The key was committed to a **public** GitHub repo and would also have been extractable from the build by every buyer. |
| New `Offline` provider mode | `VR/Assets/Scripts/LabAssistantSettings.cs`, `InLabAssistantController.cs` | With no key, the old code resolved to `None`, which **also disabled the offline assistant**. `Offline` keeps typed questions (Enter), the "why did it fail" key (Y), graph and animation explanations, and Windows speech — without ever contacting Convai. Pressing V now says voice isn't available instead of silently doing nothing, and the panel footer no longer advertises "Hold [V] talk". |
| Controls tab explains V is unavailable offline | `VR/Assets/Scripts/PauseMenuUI.cs` | The F1 controls list said "Hold to talk". |
| Renamed to **Atomix** by **Team Atomix**, version **1.0.0**, Windows identifier `com.TeamAtomix.Atomix` | `VR/ProjectSettings/ProjectSettings.asset` | Window title, install/save folders showed `UnityLab` / `AlinaInc`. Android identifier left untouched. |
| **MockHMD** removed from the Windows XR loaders (OpenXR entry kept) | `VR/Assets/XR/XRGeneralSettingsPerBuildTarget.asset` | MockHMD fakes a headset when none is present. In practice this setting is inert today — the XR packages aren't installed (see D2) — but it keeps the configuration safe if they're added later. |
| One-click release build: menu **Atomix → Build Windows (itch.io)** | `VR/Assets/Editor/AtomixWindowsBuild.cs` (new) | Builds 64-bit to `VR/Builds/Windows/Atomix.exe`, **refuses to build if a Convai key is back in the settings asset**, clears stale files from old builds, deletes Burst's `*_DoNotShip` folder, and copies the license notices next to the exe. |
| License notices shipped with the game | `VR/BuildExtras/THIRD_PARTY_NOTICES.txt` (new) | Required by the MIT license of the base project and by Meta's sample license (see B3). |
| **Unity AI Assistant** package (`com.unity.ai.assistant` 2.4.0-pre.1) removed | `VR/Packages/manifest.json` | Its `Unity.AI.Tracing` module fails to compile for player builds (`TraceSinkConfigManager does not exist`), which blocked every Windows build. It is an Editor-only AI chat tool — no game code used it. Re-add it later from Package Manager if the team wants it, but check a Windows build still succeeds. |
| README notes corrected | `README.md` | Old notes about the app name and key were no longer true. |

**Side effect of the rename:** on any PC that ran an older `UnityLab` build, coins, achievements, settings and history start fresh (they live under the new names now). No public players exist yet, so no migration was written.

**These changes are not committed yet.** Review them with `git diff`, then commit.

---

## Part B — Blockers: resolve these BEFORE the page goes public

### B1. Revoke the leaked Convai key
The old key is still in your **public** git history. Removing it from the file does not un-leak it.
1. Log in at convai.com → your profile → API key.
2. Regenerate/revoke the key so the old one stops working.

### B2. Decide whether the GitHub repo stays public
`github.com/Dhir-learner/Atomix` is public and contains the full game. Anyone can clone it and build Atomix for free instead of paying $4.99. Before selling, the team should decide: **make the repo private** (GitHub → Settings → General → Danger Zone → Change visibility), or accept that the paid build is essentially a convenience/support purchase.

### B3. Oculus Hands license — ⚠️ likely conflict
`VR/Assets/Oculus Hands/` (hand models + animations) is referenced by **all 4 scenes**, so it ships in the Windows build. Meta's SDK license states: *"You may only use the SDK to develop Applications in connection with MPT approved hardware and software products"* — i.e. Meta headsets. A Windows desktop game sold on itch.io is very likely outside that. Options:
- **(Recommended)** Replace the hand models with ones whose license allows PC distribution, or remove the hand-model references from the scenes (desktop mode doesn't use hands). This is a scene change — ask Claude to do it with you in the Editor.
- Confirm where your copy came from and that its license allows this use.

### B4. Confirm you have rights to every other asset
- **Base project** — [alinaduca/BachelorsThesis-UnityLab](https://github.com/alinaduca/BachelorsThesis-UnityLab) is **MIT licensed (Copyright (c) 2024 Alina Duca)**. MIT **allows selling**, provided the notice ships with the game — already handled by `THIRD_PARTY_NOTICES.txt`, and credit her on the store page (Part E).
- **Icons8 icons** (`VR/Assets/UI/icons8-*.png`) — free for commercial use **only with a visible link to icons8.com**. Put it on the store page (Part E). Icons8 also asks apps to show it in an About/Settings screen; adding an in-game credits line is recommended.
- **Unverified provenance — check each is original, came from the MIT base repo, or is licensed:** `VR/Assets/Textures/` (e.g. `JXnK7F5CpiPpU64rLa8tUM.jpg`, `Periodic Table.jpg`, `fire.jpg`, `Hand Painted Grass Texture`, the `Screenshot 2024-…` images), the MP3s in `VR/Assets/Sounds/`, and the reaction learning videos. Anything you can't account for: replace it.
- Unity Particle Pack (`VR/Assets/UnityTechnologies/`) — Unity Asset Store EULA, fine for commercial games.

### B5. Agree how the money is split
Five people have commits in this repo. itch.io pays **the account that owns the project**. Agree in writing who owns the itch.io account and how revenue is shared *before* the first sale.

---

## Part C — One-time setup (all free)

### C1. itch.io account + payments
1. Create an account at **itch.io** (use a team-owned email).
2. Account **Settings → Payments / Payouts**. Choose a payment mode:
   - **"Collected by itch.io, paid later" (recommended)** — itch.io is the merchant of record and handles currency conversion and sales tax/VAT remittance. Revenue becomes available **7 days** after each purchase; payout requests are reviewed in about **7–14 days**.
   - "Direct to you" — money goes straight to your own PayPal/Stripe; you are the merchant of record and responsible for taxes.
3. Complete the **tax interview** (needs a Tax Identification Number) and add a payout method (**PayPal or Payoneer**). itch.io: *"Before your account can collect any payments you should complete the tax interview."*
4. Set your **revenue share** to itch.io (default 10%, adjustable 0–100%).

### C2. Install butler (itch.io's official upload tool)
1. Download butler from **https://itchio.itch.io/butler** (Windows 64-bit), extract to e.g. `C:\butler`.
2. Add `C:\butler` to PATH: Win+X → System → Advanced system settings → Environment Variables → System variables → `Path` → Edit → New → `C:\butler`.
3. Open a **new** terminal and check:
   ```bash
   butler version
   ```
4. Log in (opens your browser):
   ```bash
   butler login
   ```
   Credentials are saved to `%USERPROFILE%\.config\itch\butler_creds`. Treat that file as a secret.

---

## Part D — Build the game

### D1. Add the app icon (you are providing the PNG)
1. Make a square **1024×1024 PNG**, save it as `VR/Assets/Branding/AtomixIcon.png`.
2. Open the `VR` folder in Unity 6000.3.7f1. Wait for the import.
3. **Edit → Project Settings → Player** → at the top, set **Default Icon** to `AtomixIcon`.

### D2. Check Player settings (already set — just verify)
**Edit → Project Settings → Player → PC, Mac & Linux Standalone tab:**
- Company Name `Team Atomix`, Product Name `Atomix`, Version `1.0.0`
- Resolution and Presentation: Fullscreen Mode *Fullscreen Window*, Default Is Native Resolution ✔, Resizable Window ✔, Run In Background ✔
- Other Settings: Scripting Backend *Mono* (leave it)
- Splash Image: Unity 6 made the "Made with Unity" splash **optional for Unity Personal**. Turn it off if the toggle is enabled for you; if it's greyed out, leave it on — it's harmless.

**Assistant:** select `VR/Assets/Resources/LabAssistantSettings` → **Provider = Offline**, **Convai Api Key empty**. (The build script enforces the empty key.)

**XR / VR — read this:** this build is **keyboard-and-mouse only**. The XR packages (XR Interaction Toolkit, OpenXR, XR Plug-in Management) are **not installed** in this project — they weren't at the last commit either — so no headset runtime is started, and `DesktopBootstrap` runs every scene in desktop mode. The XR settings under `VR/Assets/XR/` are inert. The Player.log shows harmless warnings like *"The referenced script on this Behaviour (Game Object 'XR Rig') is missing!"* for the same reason. **Do not advertise VR support.** Adding PC VR later would mean installing those packages and making `DesktopBootstrap` detect a headset — a separate project.

### D3. Run the build
**Option 1 — Unity menu:** **Atomix → Build Windows (itch.io)**. Watch the Console for `Atomix Windows build succeeded`.

**Option 2 — command line** (Unity Editor must be closed):
```bash
"C:\Program Files\Unity\Hub\Editor\6000.3.7f1\Editor\Unity.exe" -batchmode -quit -buildTarget Win64 -projectPath "C:\Users\dhirt\Desktop\Atomix_5sep\VR" -executeMethod AtomixWindowsBuild.BuildFromCommandLine -logFile build.log
```
Exit code `0` = success; otherwise search `build.log` for `error`.

Output: `VR/Builds/Windows/` containing `Atomix.exe`, `Atomix_Data/`, `D3D12/`, `MonoBleedingEdge/`, `DirectML.dll`, `UnityPlayer.dll`, `UnityCrashHandler64.exe`, `THIRD_PARTY_NOTICES.txt`. Upload the **whole folder** — every file is needed. (`VR/Builds/` is git-ignored.)

> **Verified 2026-09-13:** this build succeeded from the command line in about 5 minutes — **891 MB, 192 files**, notices included, no `_DoNotShip` folder. `Atomix.exe` was then launched windowed for 40 seconds: it reached the main menu with no exceptions (only the expected missing-XR-script warnings, see D2). That is a startup check, not a playthrough — the D4 checklist is still yours to do. 891 MB is under the 1 GB website limit, but it's close, so **use butler** (Part F) rather than a web upload.

### D4. Test the build like a customer
Copy `VR/Builds/Windows` to a **different PC without Unity** if you can. Then check:

- [ ] `Atomix.exe` launches; the window title says **Atomix**
- [ ] Windows SmartScreen: "Windows protected your PC" → **More info → Run anyway** works (expected for unsigned indie games)
- [ ] Main menu buttons work with the mouse: Lab, Testing, Lab Assistant, Settings, Quit
- [ ] LabScene: all **8 experiments** can be selected (B / 1–8) and completed; F5 resets
- [ ] Assistant panel says *"Lab assistant ready - I answer from this lab's own chemistry data"*
- [ ] **Enter** → type a question → answer appears (and is spoken)
- [ ] **Y** after a failed experiment → explanation appears
- [ ] **V** → message *"Voice questions are not available in this version…"*
- [ ] Graphs (F) → "Ask AI to Explain" answers offline; learning video "ASK AI" answers
- [ ] **F1** → Controls tab shows the V line as unavailable; settings apply
- [ ] TestingPhaseLab: run a test, earn coins, **Download this test report** → files on Desktop
- [ ] Quit and relaunch → coins, achievements, history are still there
- [ ] **Disconnect from the internet** → everything above still works
- [ ] Alt+Enter toggles fullscreen / windowed

Where things live on the player's PC (useful for support):
- Settings/coins (PlayerPrefs): registry `HKEY_CURRENT_USER\Software\Team Atomix\Atomix`
- History, `Player.log`: `%USERPROFILE%\AppData\LocalLow\Team Atomix\Atomix\`

---

## Part E — Create the itch.io page

### E1. Prepare media
- **Cover image:** 630×500 PNG (315:250 ratio, minimum 315×250)
- **Screenshots:** 3–5 at 1920×1080 — lab bench, a reaction, molecular 3D view, a graph, the test report
- **Trailer (optional):** upload the demo video to YouTube and paste the link

### E2. Create the project
itch.io → avatar menu → **Upload new project**:

| Field | Value |
|---|---|
| Title | `Atomix` |
| Project URL | `atomix` → `https://<your-username>.itch.io/atomix` |
| Short description | e.g. *A hands-on virtual chemistry lab: 8 real reactions, real quantities, real mistakes.* |
| Classification | Games |
| Kind of project | **Downloadable** |
| Release status | Released |
| Pricing | **Paid** → **$4.99**. This is a minimum price — buyers may choose to pay more. |
| Uploads | Leave empty for now — butler adds the build in Part F |
| Description | See E3 |
| Genre | Educational |
| Tags (max 10) | chemistry, educational, simulation, science, laboratory, first-person, 3d, singleplayer, learning, offline |
| Generative AI disclosure | Answer honestly for your project (optional for games, required for asset packs) |
| Visibility & access | **Draft** — until Part F is done |

### E3. Description — include these sections
1. **What it is** — 8 experiments (list them), free-hand quantities, realistic failure, molecular 3D animations, energy/enthalpy/entropy graphs, offline lab assistant, testing mode with coins and downloadable HTML/CSV reports, periodic table, achievements.
2. **System requirements** — Windows 10/11 64-bit, keyboard and mouse, DirectX 11 GPU, disk space = size of the zip. No internet required. (Fill RAM/GPU after testing on a low-end PC — don't guess.)
3. **Controls** — copy the table from `README.md`, but mark **V** as not available.
4. **Install** — Download the zip → extract → run `Atomix.exe`. If SmartScreen appears: *More info → Run anyway*. Or install with the itch desktop app.
5. **Privacy** — Atomix works offline and sends no data. Test reports save to your Desktop.
6. **Credits** (required by licenses):
   ```text
   Based on UnityLab by Alina Duca (MIT License) — https://github.com/alinaduca/BachelorsThesis-UnityLab
   Icons by Icons8 — https://icons8.com
   Hand models © Meta Platform Technologies, LLC   (remove this line if you replace them — see B3)
   Made with Unity
   ```

Click **Save**. Leave it as Draft.

---

## Part F — Upload and publish

### F1. Push the build with butler
Replace `<your-username>` with your itch.io username:
```bash
butler push "C:\Users\dhirt\Desktop\Atomix_5sep\VR\Builds\Windows" <your-username>/atomix:windows --userversion 1.0.0
```
- The channel name `windows` automatically tags the upload as a Windows download.
- butler uploads the folder directly (no zip needed) and later only uploads what changed.
- Check processing status:
  ```bash
  butler status <your-username>/atomix:windows
  ```
- Upload limits: **1 GB per file via the website, 2 GB via butler** (itch.io support can raise this).

**Alternative without butler:** zip the build and upload it on the project's edit page (tick **Windows**):
```bash
powershell -Command "Compress-Archive -Path 'C:\Users\dhirt\Desktop\Atomix_5sep\VR\Builds\Windows\*' -DestinationPath 'C:\Users\dhirt\Desktop\Atomix-Windows-1.0.0.zip'"
```

### F2. Test the buyer experience while still a Draft
1. Project edit page → **Distribute** tab → **Download keys** → generate **one** key, label it `QA`.
2. Open that key's URL in a private browser window or a teammate's account → download → install on a clean PC → repeat the D4 checklist.
3. Revoke the `QA` key afterwards.

### F3. Publish
1. Edit page → **Visibility & access → Public** → Save.
2. Open the page **logged out**: the price shows **$4.99**, the download button leads to checkout, and **no file is downloadable without paying**.
3. Optional: have a teammate do one real purchase to confirm the full checkout → download flow.

---

## Part G — After launch

### Money per sale (approximate)
Processing: PayPal/Stripe charge **$0.30 + 2.9%** ≈ $0.45 on a $4.99 sale. With the default 10% itch.io share (≈ $0.50) you keep roughly **$4.05**; at a 0% share roughly **$4.55**. Payout-method fees and income tax are extra. Buyers who pay above $4.99 raise this.

### Releasing an update
1. Fix/change in Unity → **Project Settings → Player → Version** e.g. `1.0.1`.
2. **Atomix → Build Windows (itch.io)**.
3. Push with the new version:
   ```bash
   butler push "C:\Users\dhirt\Desktop\Atomix_5sep\VR\Builds\Windows" <your-username>/atomix:windows --userversion 1.0.1
   ```
4. Post a **devlog** on the project page describing the change. Existing buyers keep access; itch app users get the update automatically.

### Keep doing
- Answer comments and the email/contact info on your page
- Never put an API key back into `LabAssistantSettings.asset` for a public build (the build script will stop you)
- Keep `THIRD_PARTY_NOTICES.txt` and the store-page credits in sync with the assets you ship

---

## Troubleshooting

| Problem | Fix |
|---|---|
| Build stops: *"contains a Convai API key"* | Clear **Convai Api Key** in `Resources/LabAssistantSettings`, set Provider = Offline, build again |
| Build stops: *"THIRD_PARTY_NOTICES.txt is missing"* | Restore `VR/BuildExtras/THIRD_PARTY_NOTICES.txt` |
| Command-line build exits immediately | Close the Unity Editor (the project can't be open twice); check `build.log` |
| Build fails with `TraceSinkConfigManager does not exist` in `Unity.AI.Tracing` | The Unity AI Assistant package was re-added. Remove it (Window → Package Manager → In Project → AI Assistant → Remove) and build again |
| Build leaves `PerformanceTestRun*.json` in `Assets/Resources` | A failed build didn't clean up after the Performance Testing package. Delete them — anything in `Resources` ships in the game |
| Player.log shows *"The referenced script on this Behaviour (Game Object 'XR Rig') is missing!"* | Expected and harmless — the XR packages aren't installed and the game runs in desktop mode (see D2) |
| "Windows protected your PC" | Normal for unsigned games — *More info → Run anyway*; say so on the page. Code signing costs money and is optional |
| Antivirus flags the exe | Report a false positive to the vendor; mention it in the page's install notes |
| Upload fails as too large | Use butler (2 GB) or contact itch.io support |
| Player reports lost progress | Check `%USERPROFILE%\AppData\LocalLow\Team Atomix\Atomix\` and `Player.log` there |

---

## Final checklist

**Blockers (Part B)**
- [ ] Old Convai key revoked
- [ ] Repo visibility decided
- [ ] Oculus Hands replaced/removed or license confirmed
- [ ] Every texture, sound and video accounted for
- [ ] Revenue split agreed

**Setup (Part C)**
- [ ] itch.io account, payment mode chosen, tax interview done, payout method added
- [ ] butler installed and logged in

**Build (Part D)**
- [ ] Icon set
- [ ] Changes committed to git
- [ ] `Atomix → Build Windows (itch.io)` succeeds
- [ ] D4 checklist passed on a clean PC, including with the internet disconnected

**Page (Parts E–F)**
- [ ] Cover, 3–5 screenshots, description with requirements, controls, install and credits
- [ ] Kind: Downloadable · Pricing: Paid $4.99 · Genre: Educational
- [ ] butler push with `--userversion 1.0.0`; upload shows the Windows tag
- [ ] Download-key test passed, key revoked
- [ ] Visibility → Public; logged-out check shows $4.99 and no free download

---

### Sources
- itch.io payments & payouts: https://itch.io/docs/creators/payments
- itch.io pricing: https://itch.io/docs/creators/pricing
- itch.io first page / cover & screenshots: https://itch.io/docs/creators/getting-started
- itch.io download keys: https://itch.io/docs/creators/download-keys
- butler install / login / push: https://itch.io/docs/butler/installing.html · https://itch.io/docs/butler/login.html · https://itch.io/docs/butler/pushing.html
- itch.io generative AI disclosure: https://itch.io/t/4309690/generative-ai-disclosure-tagging
- Base project (MIT): https://github.com/alinaduca/BachelorsThesis-UnityLab
- Meta SDK license: https://developers.meta.com/horizon/licenses/oculussdk/
- Icons8 attribution: https://intercom.help/icons8-7fb7577e8170/en/articles/4725508-where-do-i-add-the-attribution-link
- Unity PlayerPrefs / persistentDataPath: https://docs.unity3d.com/6000.3/Documentation/ScriptReference/PlayerPrefs.html · https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Application-persistentDataPath.html
