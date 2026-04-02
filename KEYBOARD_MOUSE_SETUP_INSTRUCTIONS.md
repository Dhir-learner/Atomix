# 🎮 KEYBOARD & MOUSE SETUP INSTRUCTIONS FOR YOUR CHEMISTRY LAB

---

## 🚨 **NEW TO UNITY? START HERE INSTEAD!**

**If you're new to Unity or want EXTREMELY detailed instructions with visual descriptions:**

👉 **READ THIS FILE INSTEAD:** `ULTRA_DETAILED_SETUP_GUIDE.md`

It includes:
- Visual descriptions of what everything looks like
- Step-by-step with explanations of every button and window
- Troubleshooting for common problems
- Assumes you know NOTHING about Unity

**This file below is for people with some Unity experience.**

---

## ✅ WHAT I'VE DONE FOR YOU

I've converted your VR chemistry lab into a keyboard and mouse game! Here's what's been updated:

1. ✅ Created **FirstPersonController.cs** - For walking around with WASD and looking with mouse
2. ✅ Created **ObjectInteraction.cs** - For grabbing and manipulating objects with mouse
3. ✅ Created **ObjectGrabbable.cs** - Marks which objects can be grabbed
4. ✅ Updated **HandAnimationController.cs** - Now works with keyboard/mouse instead of VR controllers
5. ✅ Removed VR packages from your project (XR Toolkit, Oculus, OpenXR, etc.)

---

## 🎯 WHAT YOU NEED TO DO IN UNITY

### STEP 1: Open Your Project in Unity

1. Open Unity Hub
2. Open your project located at: `c:\Users\karti\OneDrive\Desktop\new_Atomix\VR`
3. Unity will reimport everything (this may take a few minutes)
4. You might see some errors about missing XR packages - **this is normal!** We removed the VR packages intentionally.

---

### STEP 2: Set Up the Player Camera

**You need to find your Main Camera in your lab scene:**

1. In Unity, open your main lab scene:
   - Go to **File** → **Open Scene**
   - Navigate to `Assets/Scenes` folder
   - Open **LabScene.unity** (or whichever scene is your main chemistry lab)

2. In the **Hierarchy** window (left side), find the **Main Camera** (or Camera)
   - If you have an "XR Rig" or "Player" object, look inside it for the camera

3. Click on the **Main Camera** to select it

4. In the **Inspector** window (right side), click **Add Component**

5. Search for and add these TWO scripts:
   - Type **"FirstPersonController"** and click to add it
   - Type **"ObjectInteraction"** and click to add it

6. **IMPORTANT:** Add a **Character Controller** component:
   - Click **Add Component** again
   - Search for **"Character Controller"**
   - Click to add it
   - In the Character Controller settings, set:
     - **Height:** 2
     - **Radius:** 0.5
     - **Center Y:** 1

7. Position the camera correctly:
   - In the **Transform** component at the top of Inspector:
   - Set **Position Y** to about 1.6 (eye height)
   - Set **Rotation** to (0, 0, 0)

---

### STEP 3: Make Objects Grabbable

**For EVERY object you want to grab (beakers, flasks, bottles, etc.):**

1. In the **Hierarchy**, find the object (e.g., "Beaker", "Flask", "HCl_Bottle")

2. Click on it to select it

3. In the **Inspector**, check if it has:
   - ✅ **Collider** component (BoxCollider, SphereCollider, or MeshCollider)
   - ✅ **Rigidbody** component

4. If it's missing a **Rigidbody**:
   - Click **Add Component**
   - Search for **"Rigidbody"**
   - Add it
   - **IMPORTANT:** In the Rigidbody settings:
     - Set **Mass:** 0.5 (or appropriate for the object)
     - Check **Use Gravity:** ON
     - **Uncheck** "Is Kinematic"

5. Add the **ObjectGrabbable** script:
   - Click **Add Component**
   - Search for **"ObjectGrabbable"**
   - Add it
   - Make sure **Can Grab** is checked ✅

6. **Repeat this for ALL chemistry equipment you want to interact with!**

---

### STEP 4: Remove Old VR Components (IMPORTANT!)

**Find and disable/remove old VR scripts:**

1. Look for any GameObject with "XR Rig" or "VR" in the name
2. You can either:
   - **Option A (Recommended):** Disable it by unchecking the checkbox next to its name in Inspector
   - **Option B:** Delete it (right-click → Delete)

3. Remove XR-related components from your camera:
   - Select the Main Camera
   - In Inspector, look for components like:
     - "Tracked Pose Driver"
     - "XR Controller"
     - Any component with "XR" in the name
   - Remove them by clicking the ⚙️ (gear icon) → Remove Component

---

### STEP 5: Test Your Game!

1. Click the **Play** button (▶️) at the top of Unity

2. **Controls:**
   - **W, A, S, D** - Move around
   - **Mouse** - Look around
   - **Left Shift** - Sprint
   - **Left Mouse Click** - Grab/Release object
   - **Q & E** - Rotate held object left/right
   - **Z & X** - Tilt held object forward/backward
   - **C & V** - Roll held object
   - **Mouse Scroll Wheel** - Rotate held object
   - **R** - Release object
   - **Escape** - Show/hide cursor

3. **Test grabbing:**
   - Look at a beaker or flask
   - It should highlight in **yellow**
   - Click the **Left Mouse Button** to grab it
   - Move the mouse to move it around
   - Click again or press **R** to release

---

## 🔧 TROUBLESHOOTING

### Problem: "I can't move!"
**Solution:**
- Make sure you added the **Character Controller** component to your Main Camera
- Make sure you added the **FirstPersonController** script to your Main Camera
- Click Play and check the Console (bottom) for errors

### Problem: "I can't grab anything!"
**Solution:**
- Make sure the objects have the **ObjectGrabbable** script
- Make sure objects have a **Collider** and **Rigidbody**
- Make sure you added the **ObjectInteraction** script to the camera
- Look at the object - it should turn **yellow** when you can grab it

### Problem: "The camera is too low/high"
**Solution:**
- Select the Main Camera
- In Inspector → Transform → Position
- Adjust the **Y value** (1.6 is normal eye height)

### Problem: "Objects fall through the floor"
**Solution:**
- Make sure your floor/ground has a **Collider** component
- Select the floor object → Add Component → Box Collider

### Problem: "I see errors about XR or Input System"
**Solution:**
- This is normal after removing VR packages
- Go to **Edit** → **Project Settings** → **XR Plugin Management**
- Uncheck all XR options
- Close the window

### Problem: "Mouse look is too fast/slow"
**Solution:**
- Select the Main Camera
- In Inspector, find the **FirstPersonController** component
- Adjust the **Mouse Sensitivity** value (try values between 1-5)

---

## 🎮 GAME CONTROLS SUMMARY

| Action | Control |
|--------|---------|
| Move Forward | W |
| Move Backward | S |
| Move Left | A |
| Move Right | D |
| Sprint | Left Shift (hold) |
| Look Around | Move Mouse |
| Grab/Release Object | Left Mouse Click |
| Rotate Object (Y-axis) | Q & E keys |
| Tilt Object (X-axis) | Z & X keys |
| Roll Object (Z-axis) | C & V keys |
| Rotate with Mouse | Scroll Wheel |
| Release Held Object | R key or Left Click |
| Show/Hide Cursor | Escape |

---

## 📝 ADVANCED: Adjusting Settings

### FirstPersonController Settings (on Main Camera):
- **Move Speed:** How fast you walk (default: 5)
- **Sprint Multiplier:** How much faster sprinting is (default: 2)
- **Mouse Sensitivity:** How sensitive mouse look is (default: 2)

### ObjectInteraction Settings (on Main Camera):
- **Interaction Distance:** How far you can grab objects (default: 3)
- **Hold Distance:** How far from camera the object floats (default: 1.5)
- **Movement Smoothing:** How smoothly objects follow mouse (default: 10)
- **Rotation Speed:** How fast objects rotate with keys (default: 100)
- **Highlight Color:** Color of objects you can grab (default: Yellow)

### ObjectGrabbable Settings (on each grabbable object):
- **Can Grab:** Enable/disable grabbing for this object
- **Return To Original Position:** Object returns to start position when released

---

## 🚨 IMPORTANT NOTES

1. **SAVE YOUR SCENE:** After making changes, press **Ctrl+S** to save!

2. **Test in Play Mode:** Always test by clicking the Play button ▶️

3. **Exit Play Mode:** Click the Play button again to stop testing
   - ⚠️ **WARNING:** Changes made in Play Mode are NOT saved!

4. **Backup Your Project:** Before making major changes, copy your entire `VR` folder

5. **Build Settings:** When you're ready to build your game:
   - Go to **File** → **Build Settings**
   - Make sure **PC, Mac & Linux Standalone** is selected
   - Click **Build** to create your game

---

## ✨ TIPS FOR BEST EXPERIENCE

1. **Lighting:** Make sure your scene has good lighting so you can see objects clearly

2. **Colliders:** All furniture and walls should have colliders so you don't walk through them

3. **Test Everything:** After setup, test grabbing every piece of equipment

4. **Performance:** If the game runs slowly, reduce the quality settings:
   - **Edit** → **Project Settings** → **Quality**
   - Set quality to "Medium" or "Low"

5. **Camera Clipping:** If objects disappear when too close:
   - Select Main Camera
   - In Inspector → Camera component
   - Set **Near Clipping Plane** to 0.01

---

## 📞 NEED HELP?

If you get stuck:
1. Check the **Console** window in Unity (bottom panel) for error messages
2. Make sure you followed ALL steps in order
3. Try creating a simple test scene first to verify everything works
4. Check that all scripts are properly attached to the right objects

---

## 🎉 YOU'RE DONE!

Once you've completed all the steps above, your VR chemistry lab will be fully playable with keyboard and mouse!

**Remember to save your scene frequently (Ctrl+S) and test often!**

Good luck with your chemistry lab! 🧪🔬
