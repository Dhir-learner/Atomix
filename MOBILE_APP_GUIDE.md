# Atomix on Android — build and test guide

The mobile version lives in the same Unity project (`VR/`). The touch controls switch on by
themselves on a phone; the Windows build is unchanged and still uses keyboard and mouse.

## 1. One-time setup

1. Open **Unity Hub → Installs → 6000.3.7f1 → ⚙ → Add modules**.
2. Tick **Android Build Support**, including **OpenJDK** and **Android SDK & NDK Tools**. Install.
3. Open the `VR` project. Unity creates `.meta` files for the new scripts. Commit them with the scripts.

## 2. Try it in the Editor (no phone needed)

1. **Window → General → Device Simulator**, and pick any Android phone in landscape.
2. Press Play from `MainMenuScene`. The simulator reports a mobile platform, so the touch HUD
   appears automatically, and mouse clicks/drags act as touches. Multi-touch (joystick + look at
   the same time) can only be tested on a real phone.
3. Optional: **Tools → Atomix → Simulate Mobile Controls** forces the mobile layout in the
   normal Game view too (useful to check layout; there are no touches there).

## 3. Build the APK

1. **File → Build Profiles → Android → Switch Platform** (first switch re-imports; it takes a while).
2. Connect the phone with USB debugging on, then **Build And Run**. To get a file instead, use **Build**
   to get an `.apk` you can copy to the phone.

Already configured: IL2CPP, ARM64, min Android 10 (API 29), landscape only, up to 20:9 screens.

## 4. Controls on the phone

| On screen | Does | Desktop key |
|---|---|---|
| Left joystick (floats to your thumb) | Move | W A S D |
| Drag anywhere else | Look | Mouse |
| Tap an object | Pick up / operate it | Left click |
| Tap a panel button | Press it | Crosshair click |
| RUN | Sprint toggle (turns off when you stop) | Shift |
| UP / DOWN | Fly up / down | Space / Ctrl |
| BOOK | Reaction book | B |
| AI — tap / hold | Open assistant / talk | M / V |
| TYPE · WHY? · HIDE (in assistant) | Type question · why did it fail · close | Enter · Y · M |
| TURN L/R · TILT L/R · RESET · DROP (only while holding) | Rotate · pour · straighten · put down | Q/E · Z/X · T · R |
| ☰ menu (top left) | Periodic table, graphs, history, labels, retry, re-centre video, exam shop, settings | P F Tab L F5 O F2–F4 F1 |
| Android Back | Close the open panel / menu | Esc |

## 5. Mobile-specific behaviour

- **AI assistant:** Convai works over the phone's internet. Android asks for the microphone the
  first time AI is held. Offline answers are read aloud by the phone's text-to-speech.
- **Graphics preset:** phones start with visual effects off, 60 fps, and the project's Android
  quality level (Medium). All of these can be changed in ☰ → Settings.
- **Settings added for phones:** Look sensitivity and Touch controls size.

## 6. Where the code is

| File | Role |
|---|---|
| `Assets/Scripts/AtomixInput.cs` | One input layer: every script asks it, keyboard or touch answers |
| `Assets/Scripts/MobileControls.cs` | HUD, joystick, look, tap, ☰ menu, panel scaling for phones |
| `Assets/Scripts/MobileHudButton.cs` | Press/hold/release touch button |
| `Assets/Scripts/Editor/AtomixMobileMenu.cs` | Tools menu toggle for Editor testing |

## 7. Test checklist on a real phone

- [ ] Walk with the joystick while looking with the other thumb
- [ ] Tap glassware to pick it up; TILT L/R pours; DROP puts it down on the bench
- [ ] BOOK → choose an experiment → complete it
- [ ] AI: tap opens, TYPE opens the keyboard, hold to talk (online), WHY? answers offline
- [ ] ☰ → each item opens its panel; Back closes it
- [ ] Testing scene: ☰ shop buttons, coin strip at top left
- [ ] Text readable on your phone (report any panel that is too small)
