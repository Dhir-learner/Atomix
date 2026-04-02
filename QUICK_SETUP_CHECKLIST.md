# 🎮 VR TO KEYBOARD/MOUSE CONVERSION - QUICK SETUP CHECKLIST

## ✅ FILES I'VE CREATED/MODIFIED:

### New Scripts Created:
- ✅ `FirstPersonController.cs` - WASD movement + mouse look
- ✅ `ObjectInteraction.cs` - Grab and manipulate objects
- ✅ `ObjectGrabbable.cs` - Mark objects as grabbable
- ✅ `ControlsHelpUI.cs` - Show controls on screen (BONUS!)

### Modified Scripts:
- ✅ `HandAnimationController.cs` - Updated for keyboard/mouse

### Configuration:
- ✅ `Packages/manifest.json` - Removed VR packages (XR Toolkit, Oculus, OpenXR)

---

## 📋 YOUR QUICK SETUP CHECKLIST

### Part 1: Camera Setup (5 minutes)
- [ ] Open Unity project
- [ ] Open your main lab scene (`Assets/Scenes/LabScene.unity`)
- [ ] Find the Main Camera in Hierarchy
- [ ] Add **FirstPersonController** script to camera
- [ ] Add **ObjectInteraction** script to camera
- [ ] Add **Character Controller** component to camera
- [ ] Set camera position Y to 1.6

### Part 2: Make Objects Grabbable (10-15 minutes)
For each object you want to grab (do this for ALL chemistry equipment):
- [ ] Select the object in Hierarchy
- [ ] Add **Rigidbody** component (if missing)
- [ ] Make sure it has a **Collider** component
- [ ] Add **ObjectGrabbable** script
- [ ] Check "Can Grab" is enabled

**Objects to make grabbable:**
- [ ] Beakers
- [ ] Flasks
- [ ] Test tubes
- [ ] Bottles (HCl, H2SO4, etc.)
- [ ] Pipettes
- [ ] Burners
- [ ] Any other equipment you want to pick up

### Part 3: Remove Old VR Stuff (5 minutes)
- [ ] Find "XR Rig" or VR-related GameObjects
- [ ] Disable or delete them
- [ ] Remove XR components from camera (if any)
- [ ] Check for "Tracked Pose Driver" and remove it

### Part 4: Add Help UI (OPTIONAL - 2 minutes)
- [ ] In Hierarchy, right-click → **UI** → **Canvas**
- [ ] Select the Canvas
- [ ] Add **ControlsHelpUI** script to the Canvas
- [ ] Done! Press H in-game to show/hide controls

### Part 5: Test! (5 minutes)
- [ ] Click Play button ▶️
- [ ] Test WASD movement
- [ ] Test mouse look
- [ ] Look at an object - does it highlight yellow?
- [ ] Click to grab an object
- [ ] Test rotating with Q/E keys
- [ ] Release with R or click
- [ ] Everything works? **SAVE THE SCENE!** (Ctrl+S)

---

## ⚡ SUPER QUICK START (If you know Unity well)

1. **Camera:** Add `FirstPersonController`, `ObjectInteraction`, and `CharacterController` to Main Camera
2. **Objects:** Add `ObjectGrabbable` + `Rigidbody` to all grabbable items
3. **Cleanup:** Remove XR Rig and XR components
4. **Test:** Play and verify grabbing works
5. **Save:** Ctrl+S

---

## 🎮 CONTROLS AT A GLANCE

```
MOVE:     W/A/S/D
LOOK:     Mouse
SPRINT:   Left Shift
GRAB:     Left Click
ROTATE:   Q/E (yaw), Z/X (pitch), C/V (roll)
RELEASE:  R or Left Click
CURSOR:   Escape
HELP:     H (if you added ControlsHelpUI)
```

---

## 🔴 COMMON MISTAKES TO AVOID

1. ❌ **Forgetting Character Controller** on camera → Can't move
2. ❌ **No Rigidbody on objects** → Can't grab them
3. ❌ **No ObjectGrabbable script** → Objects won't highlight
4. ❌ **Making changes in Play Mode** → They don't save!
5. ❌ **Not saving the scene** → Lose all your work!

---

## 📞 IF SOMETHING DOESN'T WORK

1. Check the **Console** window for errors (red messages)
2. Make sure you're NOT in Play Mode when adding components
3. Verify ALL required components are attached
4. Read the full instructions: `KEYBOARD_MOUSE_SETUP_INSTRUCTIONS.md`

---

## 🎯 ESTIMATED TOTAL TIME

- **If you're new to Unity:** 30-45 minutes
- **If you know Unity:** 15-20 minutes
- **Just camera setup:** 5 minutes
- **Just one object grabbable:** 1 minute

---

## 🎉 AFTER SETUP

Once everything is working:

1. **Save your scene** (Ctrl+S)
2. **Test thoroughly** - Try all interactions
3. **Adjust settings** as needed (speed, sensitivity, etc.)
4. **Build your game:**
   - File → Build Settings
   - Add Open Scenes
   - Click Build
   - Choose a location
   - Done! You have a playable game!

---

**Good luck! Your VR chemistry lab is now a keyboard and mouse game! 🧪🎮**
