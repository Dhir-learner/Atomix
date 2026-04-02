# 🖼️ UNITY WINDOW LAYOUT VISUAL GUIDE

## 📺 WHAT YOUR UNITY SCREEN LOOKS LIKE

```
╔═══════════════════════════════════════════════════════════════════════════════╗
║  Unity - VR - LabScene.unity                        File  Edit  Assets  ...   ║
╠═══════════════════════════════╦═══════════════════════════════════════════════╣
║                               ║                                               ║
║  ╔═══════════════════════╗    ║    ╔═════════════════════════════════════╗    ║
║  ║   HIERARCHY           ║    ║    ║   SCENE / GAME VIEW                 ║    ║
║  ║   (Top-Left)          ║    ║    ║   (Center-Top)                      ║    ║
║  ╠═══════════════════════╣    ║    ║                                     ║    ║
║  ║                       ║    ║    ║  Your 3D chemistry lab shows here   ║    ║
║  ║ ▼ Lab Environment     ║    ║    ║                                     ║    ║
║  ║   • Floor             ║    ║    ║  [Scene Tab | Game Tab]             ║    ║
║  ║   • Walls             ║    ║    ║                                     ║    ║
║  ║ ▶ Lab Equipment       ║    ║    ║  When playing, Game tab shows       ║    ║
║  ║   • Beaker_01         ║    ║    ║  what player sees                   ║    ║
║  ║   • Flask_02          ║    ║    ║                                     ║    ║
║  ║ • Main Camera      ←──╫────╫───→║  Camera sees this view              ║    ║
║  ║ • Directional Light   ║    ║    ║                                     ║    ║
║  ║                       ║    ║    ║                                     ║    ║
║  ║ Click objects here    ║    ║    ║  Navigate with mouse here           ║    ║
║  ║ to select them        ║    ║    ║                                     ║    ║
║  ╚═══════════════════════╝    ║    ╚═════════════════════════════════════╝    ║
╠═══════════════════════════════╩═══════════════════════════════════════════════╣
║                                                                               ║
║  ╔════════════════════════════════════════════╗    ╔════════════════════════╗ ║
║  ║  PROJECT                                   ║    ║  INSPECTOR             ║ ║
║  ║  (Bottom-Left)                             ║    ║  (Right Side)          ║ ║
║  ╠════════════════════════════════════════════╣    ╠════════════════════════╣ ║
║  ║  📁 Assets                                 ║    ║  Main Camera      [✓]  ║ ║
║  ║    📁 Scenes        ← Your scenes here     ║    ║  ═══════════════════   ║ ║
║  ║      • LabScene    ← Double-click to open  ║    ║  Transform             ║ ║
║  ║      • MainMenuScene                       ║    ║    Position  X Y Z     ║ ║
║  ║    📁 Scripts       ← Scripts here         ║    ║    Rotation  X Y Z     ║ ║
║  ║      • FirstPersonController               ║    ║    Scale     X Y Z     ║ ║
║  ║      • ObjectInteraction                   ║    ║  ═══════════════════   ║ ║
║  ║      • ObjectGrabbable                     ║    ║  Camera                ║ ║
║  ║    📁 Materials                            ║    ║    Clear Flags         ║ ║
║  ║    📁 Prefabs                              ║    ║    Background          ║ ║
║  ║                                            ║    ║  ═══════════════════   ║ ║
║  ║  File browser showing your project files   ║    ║  [Add Component]       ║ ║
║  ║                                            ║    ║                        ║ ║
║  ╚════════════════════════════════════════════╝    ║  Shows settings for    ║ ║
║                                                    ║  selected object       ║ ║
╠════════════════════════════════════════════════════╩════════════════════════╣ ║
║  Console (Errors and messages show here)                                     ║ ║
║  ⚠ Warning: XR package missing (This is OK - we removed VR!)                ║ ║
╚═══════════════════════════════════════════════════════════════════════════════╝
```

---

## 🎯 WHERE TO ADD SCRIPTS (VISUAL)

### **MAIN CAMERA - Add 3 Things:**

```
╔═════════════════════════════════════════╗
║  INSPECTOR (when Main Camera selected)  ║
╠═════════════════════════════════════════╣
║  Main Camera                       [✓]  ║
║  ═══════════════════════════════════    ║
║  Transform                              ║
║    Position  X: 0   Y: 1.6   Z: 0       ║  ← Set Y to 1.6!
║               ↑ Eye height              ║
║  ═══════════════════════════════════    ║
║  Camera                                 ║
║    (camera settings...)                 ║
║  ═══════════════════════════════════    ║
║  🔧 Character Controller                 ║  ← ADD THIS! (Step 4)
║    Height: 2                            ║
║    Radius: 0.5                          ║
║  ═══════════════════════════════════    ║
║  📜 First Person Controller (Script)     ║  ← ADD THIS! (Step 5)
║    Move Speed: 5                        ║
║    Mouse Sensitivity: 2                 ║
║  ═══════════════════════════════════    ║
║  🖱️ Object Interaction (Script)          ║  ← ADD THIS! (Step 6)
║    Interaction Distance: 3              ║
║    Highlight Color: Yellow              ║
║  ═══════════════════════════════════    ║
║                                         ║
║        [  Add Component  ]              ║  ← Click to add more
║                                         ║
╚═════════════════════════════════════════╝
```

---

## 🎯 GRABBABLE OBJECTS - Add 3 Things:

### **Each Beaker, Flask, Bottle, etc:**

```
╔═════════════════════════════════════════╗
║  INSPECTOR (when Beaker selected)       ║
╠═════════════════════════════════════════╣
║  Beaker                            [✓]  ║
║  ═══════════════════════════════════    ║
║  Transform                              ║
║    Position  X: _   Y: _   Z: _         ║
║  ═══════════════════════════════════    ║
║  🔲 Box Collider (or other collider)     ║  ← MUST HAVE THIS!
║    Center: 0, 0, 0                      ║    (or Sphere/Mesh/Capsule)
║    Size: 1, 1, 1                        ║
║  ═══════════════════════════════════    ║
║  ⚙️ Rigidbody                            ║  ← MUST HAVE THIS!
║    Mass: 0.5                            ║
║    Drag: 0                              ║
║    [✓] Use Gravity                      ║  ← Must be CHECKED!
║    [ ] Is Kinematic                     ║  ← Must be UNCHECKED!
║  ═══════════════════════════════════    ║
║  ✋ Object Grabbable (Script)            ║  ← ADD THIS!
║    [✓] Can Grab                         ║  ← Must be CHECKED!
║    [ ] Return To Original Position      ║
║  ═══════════════════════════════════    ║
║                                         ║
║        [  Add Component  ]              ║
║                                         ║
╚═════════════════════════════════════════╝
```

---

## 🎮 HOW THE CONTROLS WORK (VISUAL)

```
     KEYBOARD                          RESULT
     ════════                          ══════
    ┌───┬───┬───┐
    │ Q │ W │ E │                    Q/E = Rotate held object
    └───┴───┴───┘                    W = Move forward
    ┌───┬───┬───┐
    │ A │ S │ D │                    A/D = Move left/right
    └───┴───┴───┘                    S = Move backward

    ┌─────────────┐
    │  Left Shift │                  Hold = Sprint (faster)
    └─────────────┘

    ┌───┬───┬───┐
    │ Z │ X │   │                    Z/X = Tilt object
    └───┴───┴───┘                    C/V = Roll object
    ┌───┬───┬───┐
    │ C │ V │   │
    └───┴───┴───┘

    ┌───┐
    │ R │                            R = Release held object
    └───┘

    ┌────┐
    │Esc │                           Esc = Show/hide cursor
    └────┘


     MOUSE                            RESULT
     ═════                            ══════
    ╔═══════════════╗
    ║               ║
    ║   Move Mouse  ║ ──────────────> Look around (camera)
    ║               ║
    ╚═══════════════╝

    ┌──────────────┐
    │ LEFT  CLICK  │  ──────────────> Grab/Release object
    └──────────────┘

    ┌──────────────┐
    │ SCROLL WHEEL │  ──────────────> Rotate held object
    └──────────────┘
         ↑↓
```

---

## 🎯 STEP-BY-STEP FLOW (VISUAL)

```
START HERE
    │
    ↓
┌────────────────────┐
│ Open Unity Hub     │
└────────┬───────────┘
         │
         ↓
┌────────────────────┐       ┌──────────────────────────┐
│ Find VR project    │──────>│ NOT "My project (1)"     │
│ in project list    │       │ Look for actual VR lab!  │
└────────┬───────────┘       └──────────────────────────┘
         │
         ↓
┌────────────────────┐
│ Open the project   │
│ (Wait to load)     │
└────────┬───────────┘
         │
         ↓
┌────────────────────┐       ┌──────────────────────────┐
│ Open LabScene      │──────>│ Project → Assets →       │
│                    │       │ Scenes → LabScene        │
└────────┬───────────┘       └──────────────────────────┘
         │
         ↓
┌────────────────────┐       ┌──────────────────────────┐
│ Find Main Camera   │──────>│ Look in Hierarchy        │
│ in Hierarchy       │       │ (top-left window)        │
└────────┬───────────┘       └──────────────────────────┘
         │
         ↓
┌────────────────────┐
│ Select Main Camera │───────┐
└────────────────────┘       │
                              │
    ┌─────────────────────────┘
    │
    ├──> Add Character Controller
    │
    ├──> Add FirstPersonController script
    │
    ├──> Add ObjectInteraction script
    │
    └──> Set Position Y to 1.6
         │
         ↓
┌────────────────────┐
│ For EACH object    │───────┐
│ you want to grab:  │       │
└────────────────────┘       │
                              │
    ┌─────────────────────────┘
    │
    ├──> Select object in Hierarchy
    │
    ├──> Add/Check Collider
    │
    ├──> Add/Check Rigidbody
    │
    └──> Add ObjectGrabbable script
         │
         ↓
┌────────────────────┐
│ Remove XR Rig      │
│ and VR components  │
└────────┬───────────┘
         │
         ↓
┌────────────────────┐
│ SAVE! (Ctrl+S)     │ ◄──── DON'T FORGET THIS!
└────────┬───────────┘
         │
         ↓
┌────────────────────┐
│ Click PLAY button  │
│ Test the game!     │
└────────┬───────────┘
         │
         ↓
    ┌────┴────┐
    │ Working? │
    └────┬────┘
         │
    ┌────┴────────────┐
    │                 │
   YES   ╔═══════╗   NO
    │    ║ DONE! ║    │
    └───>║  🎉   ║    └──> Check troubleshooting
         ╚═══════╝         in ULTRA_DETAILED_SETUP_GUIDE.md
```

---

## 🎯 QUICK CHECKLIST (Print This!)

```
MAIN CAMERA SETUP:
  ☐  Selected Main Camera in Hierarchy
  ☐  Added Character Controller
  ☐  Added FirstPersonController script
  ☐  Added ObjectInteraction script
  ☐  Set Position Y to 1.6
  ☐  Removed old XR components

MAKE OBJECTS GRABBABLE:
  ☐  Selected object (beaker/flask/etc)
  ☐  Has Collider ✓
  ☐  Has Rigidbody ✓
  ☐  Rigidbody: Use Gravity = ON ✓
  ☐  Rigidbody: Is Kinematic = OFF ☐
  ☐  Added ObjectGrabbable script
  ☐  ObjectGrabbable: Can Grab = ON ✓
  ☐  Repeat for ALL grabbable objects!

TESTING:
  ☐  Saved scene (Ctrl+S)
  ☐  Clicked Play button
  ☐  Can move with WASD
  ☐  Can look with mouse
  ☐  Objects highlight yellow
  ☐  Can grab objects with click
  ☐  Can rotate with Q/E
  ☐  Can release with R or click

IF SOMETHING'S WRONG:
  ☐  Read ULTRA_DETAILED_SETUP_GUIDE.md
  ☐  Check troubleshooting section
  ☐  Make sure Play button is OFF when editing!
  ☐  Save again (Ctrl+S)
```

---

## 📁 FILES IN YOUR PROJECT

```
new_Atomix/
├── VR/                                    ← YOUR PROJECT FOLDER
│   ├── Assets/
│   │   ├── Scenes/
│   │   │   ├── LabScene.unity            ← Open this scene!
│   │   │   ├── MainMenuScene.unity
│   │   │   └── ...
│   │   ├── Scripts/
│   │   │   ├── FirstPersonController.cs  ← New! Movement
│   │   │   ├── ObjectInteraction.cs      ← New! Grabbing
│   │   │   ├── ObjectGrabbable.cs        ← New! Marks objects
│   │   │   ├── ControlsHelpUI.cs         ← New! Help display
│   │   │   └── HandAnimationController.cs ← Updated!
│   │   └── ...
│   └── ...
│
├── ULTRA_DETAILED_SETUP_GUIDE.md         ← Read this if you're new!
├── KEYBOARD_MOUSE_SETUP_INSTRUCTIONS.md  ← This file
├── QUICK_SETUP_CHECKLIST.md              ← Quick reference
└── UNITY_WINDOWS_VISUAL_GUIDE.md         ← You are here!
```

---

## 🆘 STUCK? START HERE:

1. **New to Unity?**
   👉 Read: `ULTRA_DETAILED_SETUP_GUIDE.md`

2. **Know Unity basics?**
   👉 Read: `KEYBOARD_MOUSE_SETUP_INSTRUCTIONS.md`

3. **Just want a checklist?**
   👉 Read: `QUICK_SETUP_CHECKLIST.md`

4. **Want to see window layouts?**
   👉 You're already here! This file!

---

## 🎮 REMEMBER THESE KEYBOARD SHORTCUTS!

```
Ctrl + S     = Save scene (DO THIS OFTEN!)
Ctrl + D     = Duplicate selected object
Ctrl + Z     = Undo
Ctrl + Y     = Redo
Delete       = Delete selected object
F            = Focus on selected object in Scene view

RIGHT CLICK + DRAG (in Scene view) = Rotate view
SCROLL WHEEL (in Scene view)        = Zoom in/out
MIDDLE CLICK + DRAG (in Scene view) = Pan view

▶ Button (Play)  = Start game
▶ Button (Blue)  = Stop game
```

---

## 🎯 MOST COMMON MISTAKES:

```
❌  Creating NEW project instead of opening existing VR folder
❌  Forgetting to add Character Controller to camera
❌  Forgetting to add scripts to camera
❌  Forgetting to add ObjectGrabbable to objects
❌  Making changes while Play button is BLUE
❌  Not saving (Ctrl+S)
❌  Is Kinematic CHECKED (should be UNCHECKED for grabbable objects)
❌  Use Gravity UNCHECKED (should be CHECKED for grabbable objects)
```

---

## ✅ SUCCESS INDICATORS:

You know it's working when:
- ✅ You can see your chemistry lab in Scene view
- ✅ Play button works (turns blue when clicked)
- ✅ WASD moves you around in Game view
- ✅ Mouse look works (view rotates)
- ✅ Objects turn **YELLOW** when you look at them
- ✅ Click grabs the yellow highlighted objects
- ✅ Objects follow your mouse when held
- ✅ Q/E rotates held objects
- ✅ Click or R releases objects

---

**Good luck! You've got this! 🎮🧪🔬**
