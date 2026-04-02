# 🎮 ULTRA DETAILED KEYBOARD & MOUSE SETUP GUIDE
## (WITH VISUAL DESCRIPTIONS - FOR COMPLETE BEGINNERS)

---

## 🚨 CRITICAL: ARE YOU IN THE RIGHT PROJECT?

### ❌ **WRONG:** If you see "My project (1)" anywhere, YOU'RE IN THE WRONG PROJECT!
### ✅ **CORRECT:** Your project folder should be called "VR" and located at:
```
c:\Users\karti\OneDrive\Desktop\new_Atomix\VR
```

---

## 📂 STEP 0: OPENING THE CORRECT PROJECT (START HERE!)

### **IF YOU ALREADY HAVE UNITY OPEN:**

1. **Close Unity completely**
   - Click the **X** button at the top-right of Unity
   - If it asks to save, click "Save"

### **OPENING YOUR EXISTING VR CHEMISTRY LAB PROJECT:**

1. **Open Unity Hub**
   - Look for the Unity Hub icon on your desktop or Start menu
   - It looks like a cube with the Unity logo
   - Double-click to open it

2. **Unity Hub should open - What you see:**
   ```
   ┌─────────────────────────────────────────┐
   │ Unity Hub                        _ □ X  │
   ├─────────────────────────────────────────┤
   │ Projects | Learn | Installs | etc.     │
   ├─────────────────────────────────────────┤
   │                                         │
   │  Your existing projects show here       │
   │  as cards/boxes with project names      │
   │                                         │
   └─────────────────────────────────────────┘
   ```

3. **Do you see your VR chemistry lab project listed?**

   ### ✅ **YES, I SEE IT:**
   - Look for a project card that shows "VR" or has your chemistry lab
   - Click on it to open
   - **SKIP TO STEP 1 BELOW**

   ### ❌ **NO, I DON'T SEE IT:**
   - Click the **OPEN** button (top-right corner of Unity Hub)
   - Or click **ADD** button (might be called "Add project from disk")
   - **What it looks like:**
     ```
     Top right corner:
     [New Project ▼]  [OPEN]  [Settings ⚙]
     ```

   - A file browser window will open
   - Navigate to: `C:\Users\karti\OneDrive\Desktop\new_Atomix\VR`
   - **IMPORTANT:** Select the **VR** folder (NOT "My project (1)")
   - Click **"Select Folder"** or **"Open"**

4. **Unity will now open your project**
   - This takes 1-5 minutes
   - You'll see a loading screen with the Unity logo
   - It might say "Importing Assets" or "Compiling Scripts"
   - **WAIT for it to finish completely**

5. **You might see error messages - THIS IS NORMAL!**
   - Errors about "XR" or "Oculus" or "OpenXR" - **IGNORE THESE**
   - These are from the VR packages we removed - it's OK!
   - Just click "Clear" or "X" to dismiss them

---

## 🖥️ STEP 1: UNDERSTANDING THE UNITY INTERFACE

Once Unity opens, you should see **5 main windows**:

```
┌──────────────────────────────────────────────────────┐
│  Unity - VR - LabScene.unity           File Edit ... │
├──────────┬───────────────────────────────────────────┤
│          │                                           │
│ Hierarchy│          Scene/Game View                  │
│  (left)  │            (center)                       │
│          │                                           │
│          │                                           │
├──────────┴───────────────────────────────┬───────────┤
│                                          │           │
│         Project Window                   │ Inspector │
│           (bottom)                       │  (right)  │
│                                          │           │
└──────────────────────────────────────────┴───────────┘
```

### **1. HIERARCHY WINDOW (Top-Left)**
- **What it looks like:** A list of items with small triangles (▶) next to some
- **What it shows:** All objects in your current scene (like a table of contents)
- **Example:**
  ```
  Hierarchy
  ─────────
  ▶ Lab Environment
    • Floor
    • Walls
  ▶ Lab Equipment
    • Beaker_01
    • Flask_02
  • Main Camera
  • Directional Light
  ```

### **2. SCENE VIEW (Center-Top)**
- **What it looks like:** A 3D view of your lab (like flying around in the lab)
- **Has tabs at top:** "Scene | Game"
- **You can rotate and zoom:** Hold right-mouse and drag
- **Shows:** Your chemistry lab with all equipment

### **3. GAME VIEW (Center-Top - same area as Scene)**
- **What it looks like:** Click the "Game" tab to see this
- **Shows:** What the player sees when playing
- **When playing:** This shows your actual game running

### **4. PROJECT WINDOW (Bottom)**
- **What it looks like:** A file browser showing folders
- **Shows folders like:**
  ```
  Assets ▶
    ├─ Scenes
    ├─ Scripts
    ├─ Materials
    ├─ Prefabs
    └─ etc.
  ```

### **5. INSPECTOR WINDOW (Right)**
- **What it looks like:** A panel with settings and checkboxes
- **Shows:** Details about whatever you clicked on
- **Has:** Components, properties, checkboxes, numbers

---

## 📂 STEP 2: OPEN YOUR LAB SCENE

**If your Scene View is showing an empty blue sky OR nothing:**

1. **Look at the PROJECT WINDOW (bottom)**

2. **Find the "Assets" folder:**
   - You should see "Assets" with a ▶ triangle next to it
   - If the triangle points RIGHT (▶), click it to expand
   - If it points DOWN (▼), it's already expanded

3. **Find the "Scenes" folder:**
   - Inside Assets, look for a folder called **"Scenes"**
   - **What it looks like:**
     ```
     Project
     ─────────────
     ▶ Assets
       ▶ Materials
       ▶ Prefabs
       ▶ Scenes  ← THIS ONE!
       ▶ Scripts
     ```
   - Double-click on "Scenes" to open it

4. **You should see these scene files:**
   - **LabScene** ← **YOUR MAIN LAB!**
   - LabAssistantScene
   - MainMenuScene
   - TestingPhaseLab

   **What scene files look like:**
   - They have a Unity cube icon next to them
   - They end with ".unity" (you might not see this part)

5. **Double-click on "LabScene"**
   - This opens your chemistry lab scene

6. **WAIT for it to load**
   - It takes 5-30 seconds
   - The Scene View (center) should now show your lab!

7. **If you see your chemistry lab - GREAT!**
   - You should see tables, beakers, flasks, equipment, walls, floor
   - Maybe lights, decorations, etc.
   - **Take a moment to look around in the Scene View**
   - Use RIGHT MOUSE BUTTON + drag to rotate view
   - Use SCROLL WHEEL to zoom in/out

---

## 🎥 STEP 3: FIND YOUR CAMERA

Now let's find the camera that the player sees through:

1. **Look at the HIERARCHY WINDOW (top-left)**

2. **Look for one of these:**
   - **"Main Camera"** ← Most common
   - **"Camera"**
   - **"XR Rig"** (if you had VR setup)
   - **"Player"** (might have camera inside)

3. **Found "XR Rig" or something with "XR" in the name?**
   - Click the small triangle (▶) next to it to expand
   - Look inside for "Main Camera" or "Camera"

   **Example:**
   ```
   Hierarchy
   ─────────
   ▼ XR Rig              ← Click triangle to expand
     ▼ Camera Offset
       • Main Camera     ← This is what you want!
       • LeftHand
       • RightHand
   ```

4. **Click on "Main Camera"**
   - Single click to select it
   - It should be highlighted in the Hierarchy
   - You'll see a camera preview appear somewhere

5. **Look at the INSPECTOR (right side)**
   - This now shows all the Camera's settings
   - You should see:
     ```
     Inspector
     ─────────────────
     Main Camera  [✓]  ← Checkbox (should be checked)
     ─────────────────
     Transform
       Position    X: 0  Y: 0  Z: 0
       Rotation    X: 0  Y: 0  Z: 0
       Scale       X: 1  Y: 1  Z: 1
     ─────────────────
     Camera
       Clear Flags: Skybox
       Background: [color]
       ...more settings...
     ─────────────────
     [might have other components below]
     ```

---

## ➕ STEP 4: ADD CHARACTER CONTROLLER TO CAMERA

**With Main Camera still selected (highlighted in Hierarchy):**

1. **Look at the INSPECTOR window (right side)**

2. **Scroll all the way to the BOTTOM of the Inspector**

3. **You should see a button that says "Add Component"**
   - **What it looks like:**
     ```
     [  Add Component  ]
     ```
   - It's usually a gray/white button
   - Click on it

4. **A search box appears**
   - **What it looks like:**
     ```
     ┌────────────────────────────┐
     │ Search: [          ]       │
     ├────────────────────────────┤
     │ New script                 │
     │ Physics                    │
     │ Navigation                 │
     │ Audio                      │
     │ ...                        │
     └────────────────────────────┘
     ```

5. **Type: "Character Controller"**
   - As you type, you'll see suggestions appear

6. **You should see:**
   ```
   ┌────────────────────────────┐
   │ Character Controller       │  ← Click this!
   │ Physics                    │
   └────────────────────────────┘
   ```

7. **Click on "Character Controller"**

8. **It gets added! You should now see in Inspector:**
   ```
   Character Controller
   ─────────────────────
   Slope Limit         45
   Step Offset         0.3
   Skin Width          0.08
   Min Move Distance   0.001
   Center        X: 0  Y: 1  Z: 0
   Radius        0.5
   Height        2
   ```

9. **IMPORTANT: Adjust these settings:**
   - Find **"Center"** - Change **Y** to **1** (should be 1 already, but check)
   - Find **"Radius"** - Should be **0.5**
   - Find **"Height"** - Should be **2**

   **How to change values:**
   - Click on the number
   - Type the new number
   - Press Enter

---

## 📜 STEP 5: ADD FIRSTPERSONCONTROLLER SCRIPT TO CAMERA

**With Main Camera STILL selected:**

1. **Click "Add Component" again** (at bottom of Inspector)

2. **Type: "FirstPersonController"**
   - You should see:
     ```
     ┌───────────────────────────────┐
     │ Scripts                       │
     │   FirstPersonController       │  ← Click this!
     └───────────────────────────────┘
     ```

3. **Click on "FirstPersonController"**

4. **It gets added! You should see in Inspector:**
   ```
   First Person Controller (Script)
   ─────────────────────────────────
   Script                [FirstPersonController]

   Movement Settings
     Move Speed                    5
     Sprint Multiplier             2

   Mouse Look Settings
     Mouse Sensitivity             2
     Max Look Up Angle            80
     Max Look Down Angle          80

   Camera Settings
     Camera Transform          [None]
   ```

5. **Leave Camera Transform as "None"** - that's OK!
   - The script will use itself as the camera

---

## 🖱️ STEP 6: ADD OBJECTINTERACTION SCRIPT TO CAMERA

**With Main Camera STILL selected:**

1. **Click "Add Component" again**

2. **Type: "ObjectInteraction"**

3. **Click on "ObjectInteraction" when you see it**

4. **It gets added! You should see:**
   ```
   Object Interaction (Script)
   ─────────────────────────────
   Script                [ObjectInteraction]

   Interaction Settings
     Interaction Distance      3
     Hold Distance            1.5
     Movement Smoothing       10
     Rotation Speed          100

   Visual Feedback
     Highlight Color      [Yellow color box]
     Interactable Layer   Everything
   ```

5. **These settings are good as-is!** No need to change them yet.

---

## 📍 STEP 7: SET CAMERA POSITION

**With Main Camera STILL selected:**

1. **Look at the TOP of the Inspector**

2. **Find the "Transform" component** (should be at the very top)
   ```
   Transform
   ──────────────────────────
   Position    X: ___  Y: ___  Z: ___
   Rotation    X: ___  Y: ___  Z: ___
   Scale       X: ___  Y: ___  Z: ___
   ```

3. **Set the Position Y value to 1.6:**
   - Click on the number next to Y under Position
   - Type: **1.6**
   - Press Enter

   **This sets the camera to eye height (160cm)**

4. **Set Rotation to all zeros:**
   - Rotation X: **0**
   - Rotation Y: **0**
   - Rotation Z: **0**

   **This makes the camera look straight ahead**

5. **X and Z Position:** Leave these as they are for now
   - You can change them later if you want to start in a different position

---

## 🎯 STEP 8: MAKE OBJECTS GRABBABLE

Now we need to mark which objects can be picked up!

### **FINDING YOUR CHEMISTRY EQUIPMENT:**

1. **Look at HIERARCHY window (left)**

2. **Look for objects like:**
   - Beaker
   - Flask
   - Bottle
   - Test_Tube
   - HCl_Bottle
   - H2SO4_Container
   - Equipment
   - Containers
   - **Anything you want to grab!**

3. **You might need to expand folders:**
   - Look for folders like:
     - "Lab Equipment"
     - "Chemistry Set"
     - "Containers"
     - "Tools"
   - Click the ▶ triangle to expand and look inside

### **MAKING ONE OBJECT GRABBABLE:**

Let's do ONE object first, then repeat for all others.

1. **Find a beaker or flask in the Hierarchy**

2. **Click on it ONCE to select it**
   - It becomes highlighted/selected

3. **Look at the INSPECTOR (right)**
   - You should see the object's name at the top
   - You should see Transform, and maybe other components

4. **CHECK: Does it have a Collider?**
   - Scroll down in the Inspector
   - Look for one of these:
     - "Box Collider"
     - "Sphere Collider"
     - "Mesh Collider"
     - "Capsule Collider"

   **What a collider looks like:**
   ```
   Box Collider
   ────────────────
   [✓] Is Trigger
   Material      [None]
   Center    X: 0  Y: 0  Z: 0
   Size      X: 1  Y: 1  Z: 1
   ```

   ### ✅ **HAS A COLLIDER:** Good! Continue to next step.

   ### ❌ **NO COLLIDER:** Add one!
   - Click "Add Component"
   - Type: "Box Collider"
   - Click to add it

5. **CHECK: Does it have a Rigidbody?**
   - Look in Inspector for "Rigidbody"

   **What a Rigidbody looks like:**
   ```
   Rigidbody
   ────────────────────
   Mass                  1
   Drag                  0
   Angular Drag          0.05
   [✓] Use Gravity
   [ ] Is Kinematic
   ```

   ### ✅ **HAS A RIGIDBODY:** Good! Check settings:
   - **Mass:** Set to **0.5** (or appropriate weight)
   - **Use Gravity:** Should be **CHECKED** ✓
   - **Is Kinematic:** Should be **UNCHECKED** ☐

   ### ❌ **NO RIGIDBODY:** Add one!
   - Click "Add Component"
   - Type: "Rigidbody"
   - Click to add it
   - Set Mass to **0.5**
   - Make sure **Use Gravity** is **CHECKED** ✓
   - Make sure **Is Kinematic** is **UNCHECKED** ☐

6. **ADD the ObjectGrabbable script:**
   - Click "Add Component"
   - Type: "ObjectGrabbable"
   - Click on "ObjectGrabbable" when you see it

   **You should see:**
   ```
   Object Grabbable (Script)
   ─────────────────────────
   Script                    [ObjectGrabbable]

   Grabbable Settings
     [✓] Can Grab
     [ ] Return To Original Position
   ```

7. **Make sure "Can Grab" is CHECKED:** ✓

8. **DONE! This object is now grabbable!**

### **REPEAT FOR ALL OBJECTS YOU WANT TO GRAB:**

Go through your Hierarchy and repeat the above steps for:
- Every beaker
- Every flask
- Every bottle
- Every tool
- Every piece of equipment
- Anything you want to pick up!

**TIP:** Select multiple objects at once!
- Hold **Ctrl** and click objects one by one
- Then add components - they all get added at once!

---

## 🧹 STEP 9: REMOVE OLD VR COMPONENTS

### **REMOVE XR RIG (if you have one):**

1. **Look in HIERARCHY for:**
   - "XR Rig"
   - "VR Rig"
   - "OVR Player
"
   - "OpenXR"
   - Anything with "XR" or "VR" in the name

2. **If you find one:**
   - **WAIT! Did you take the Main Camera out of it?**
   - If Main Camera is INSIDE the XR Rig, drag it out first:
     - Click and HOLD on "Main Camera"
     - Drag it to the top level of Hierarchy
     - Release mouse button

3. **Now you can remove the XR Rig:**
   - RIGHT-CLICK on "XR Rig" (or VR Rig)
   - Click **"Delete"**
   - Click "Yes" if it asks to confirm

### **REMOVE XR COMPONENTS FROM CAMERA:**

1. **Click on "Main Camera" in Hierarchy**

2. **Look in INSPECTOR**

3. **Scroll down and look for components with "XR" in the name:**
   - "Tracked Pose Driver"
   - "XR Controller"
   - "XR Camera"
   - Anything with "XR" or "VR"

4. **For each one you find:**
   - Look for a **gear icon** (⚙) or **three dots** (**⋮**) on the right side of the component
   - **What it looks like:**
     ```
     Tracked Pose Driver         ⚙  ← Click this gear!
     ───────────────────────────────
     ```
   - Click the gear icon
   - Click **"Remove Component"**
   - It disappears - good!

5. **Keep FirstPersonController, ObjectInteraction, and Camera!**
   - DON'T remove those!

---

## 💾 STEP 10: SAVE YOUR SCENE!

**SUPER IMPORTANT - Don't skip this!**

1. **Press Ctrl+S on your keyboard**
   - OR: Click **File** (top menu) → **Save**

2. **You should see:**
   - The scene name (top of Unity) might change
   - That means it saved!

   **What it looks like:**
   ```
   Top of Unity window:
   Unity - VR - LabScene.unity* (You might see *)

   After saving:
   Unity - VR - LabScene.unity (No * = Saved!)
   ```

---

## ▶️ STEP 11: TEST YOUR GAME!

1. **Find the PLAY BUTTON at the top-center of Unity:**
   ```
   Top center of Unity:
   [  ◀  |  ▶  |  ⏸  ]

   ▶ = Play button (should be gray/white)
   ```

2. **Click the PLAY button (▶)**
   - It turns BLUE when playing
   - The Game View appears
   - You should see your lab from first-person view!

3. **TEST THE CONTROLS:**

   ### **Movement:**
   - Press **W** - Move forward
   - Press **S** - Move backward
   - Press **A** - Move left
   - Press **D** - Move right
   - Hold **Left Shift** - Sprint (faster)

   ### **Looking Around:**
   - **Move your mouse** - Look around
   - You should be able to look left, right, up, down

   ### **Grabbing Objects:**
   - **Look at a beaker or flask**
   - **Does it turn YELLOW?**
     - ✅ **YES:** Great! Your interaction is working!
     - ❌ **NO:** The object might not have ObjectGrabbable script
   - **Click LEFT MOUSE BUTTON** - Grab the object
   - **Move mouse** - The object follows your view
   - **Press Q** - Rotate left
   - **Press E** - Rotate right
   - **Click LEFT MOUSE BUTTON again** - Release object
   - **Or press R** - Also releases object

4. **STOP PLAYING:**
   - Click the PLAY button (▶) again
   - It turns back to gray/white
   - You're back in edit mode

   **⚠️ CRITICAL WARNING:**
   - **CHANGES MADE WHILE PLAYING (blue play button) ARE NOT SAVED!**
   - **ALWAYS STOP PLAYING BEFORE MAKING CHANGES!**
   - If you add components while playing, they disappear when you stop!

---

## 🔧 TROUBLESHOOTING

### **PROBLEM: "I can't move!"**

**Check these:**
1. Did you add **Character Controller** to Main Camera?
   - Select Main Camera → Look in Inspector → Should see "Character Controller"
2. Did you add **FirstPersonController** script?
   - Should see "First Person Controller (Script)" in Inspector
3. Is the Play button blue (playing)?
   - If not, click it to start playing
4. Are you clicking in the Game window?
   - The Game window needs to be active/focused

### **PROBLEM: "I can't look around with mouse!"**

**Check these:**
1. Is **FirstPersonController** script added to camera?
2. Is the cursor locked?
   - When you start playing, cursor should disappear
   - If you see cursor, click in Game window
   - Press **Escape** to unlock/lock cursor
3. Check **Mouse Sensitivity** setting:
   - Select Main Camera
   - Find FirstPersonController in Inspector
   - Change "Mouse Sensitivity" to **3** or **4** (higher = more sensitive)

### **PROBLEM: "I can't see anything / screen is gray or blue!"**

**Check these:**
1. Did you open the correct scene?
   - Project Window → Assets → Scenes → Double-click "LabScene"
2. Is the camera position wrong?
   - Select Main Camera
   - Set Position Y to **1.6**
3. Is the camera inside a wall or floor?
   - In Scene View, zoom out and look for the camera icon
   - Adjust position X, Y, Z to move it

### **PROBLEM: "Objects don't highlight when I look at them!"**

**Check these:**
1. Did you add **ObjectInteraction** script to Main Camera?
2. Do the objects have **ObjectGrabbable** script?
   - Select object → Check Inspector
3. Do the objects have **Collider** components?
   - Select object → Look for Box Collider / Sphere Collider / etc.
4. Are you close enough?
   - Default interaction distance is 3 meters
   - Try getting closer

### **PROBLEM: "I grab objects but they fly away crazy!"**

**Check the object's Rigidbody:**
1. Select the object
2. Look at Rigidbody in Inspector
3. Make sure:
   - **Is Kinematic** is **UNCHECKED** ☐
   - **Mass** is reasonable (0.5 to 2)
   - **Use Gravity** is **CHECKED** ✓

### **PROBLEM: "Objects fall through the floor!"**

**The floor needs a collider:**
1. Find your floor/ground object in Hierarchy
2. Click on it
3. Check if it has a Collider
   - If NO: Add Component → Box Collider
4. Make sure collider is NOT marked as "Trigger"
   - **Is Trigger** should be **UNCHECKED** ☐

### **PROBLEM: "I see lots of red error messages!"**

**Check what they say:**
- Errors about **"XR"**, **"Oculus"**, **"OpenXR"**:
  - **NORMAL!** We removed VR packages. Ignore these.
  - Click "Clear" button (bottom of Console window)

- Errors about **"Cannot find..."** or **"Missing reference"**:
  - Some VR components are missing - this is OK
  - Try the steps in STEP 9 to remove XR components

- Errors about **"Script is missing"** or **"MonoBehaviour"**:
  - Make sure you have these files:
    - FirstPersonController.cs
    - ObjectInteraction.cs
    - ObjectGrabbable.cs
  - They should be in: Assets/Scripts/

### **PROBLEM: "Mouse is too sensitive / too slow!"**

**Adjust sensitivity:**
1. Select Main Camera
2. Find "First Person Controller (Script)" in Inspector
3. Change **"Mouse Sensitivity"**:
   - **Too fast?** Try 1.5 or 1
   - **Too slow?** Try 3 or 4
4. Stop and restart Play mode to test

### **PROBLEM: "Character moves too slow / too fast!"**

**Adjust move speed:**
1. Select Main Camera
2. Find "First Person Controller (Script)" in Inspector
3. Change **"Move Speed"**:
   - **Too slow?** Try 7 or 10
   - **Too fast?** Try 3 or 2

---

## 🎮 CONTROLS REFERENCE CARD

```
╔═══════════════════════════════════════════════╗
║          KEYBOARD & MOUSE CONTROLS            ║
╠═══════════════════════════════════════════════╣
║ MOVEMENT                                      ║
║   W ...................... Move Forward       ║
║   S ...................... Move Backward      ║
║   A ...................... Move Left          ║
║   D ...................... Move Right         ║
║   Left Shift (hold) ...... Sprint            ║
║                                               ║
║ CAMERA                                        ║
║   Mouse Movement ......... Look Around        ║
║   Escape ................. Show/Hide Cursor   ║
║                                               ║
║ INTERACTION                                   ║
║   Left Click ............. Grab/Release       ║
║   R ...................... Release Object     ║
║                                               ║
║ ROTATE HELD OBJECT                            ║
║   Q & E .................. Rotate Left/Right  ║
║   Z & X .................. Tilt Forward/Back  ║
║   C & V .................. Roll               ║
║   Mouse Scroll Wheel ..... Rotate             ║
╚═══════════════════════════════════════════════╝
```

---

## ✅ FINAL CHECKLIST

Before you consider yourself done:

- [ ] Opened the correct VR project (NOT "My project (1)")
- [ ] Opened LabScene.unity
- [ ] Can see the chemistry lab in Scene View
- [ ] Added Character Controller to Main Camera
- [ ] Added FirstPersonController script to Main Camera
- [ ] Added ObjectInteraction script to Main Camera
- [ ] Set camera Position Y to 1.6
- [ ] Added ObjectGrabbable to at least one object
- [ ] Added Rigidbody to grabbable objects
- [ ] Removed XR Rig (if you had one)
- [ ] Saved the scene (Ctrl+S)
- [ ] Tested in Play mode
- [ ] Can move with WASD
- [ ] Can look with mouse
- [ ] Objects highlight yellow when looking at them
- [ ] Can grab and release objects

---

## 🎓 LEARNING TIPS

Now that you have it working:

1. **Experiment with settings:**
   - Try different move speeds
   - Try different mouse sensitivities
   - See what feels best to you!

2. **Make more objects grabbable:**
   - Select each object
   - Add ObjectGrabbable script
   - Add Rigidbody
   - Test!

3. **Adjust object properties:**
   - Change Mass for heavier/lighter feel
   - Change Drag for air resistance
   - Experiment!

4. **Always save your work:**
   - **Ctrl+S** frequently!
   - **NEVER** make changes while Play button is blue!

---

## 🎉 CONGRATULATIONS!

If you've made it this far and everything is working:

**YOU DID IT! 🎉🎊🎈**

Your VR chemistry lab is now a fully functional keyboard and mouse game!

You can now:
- Walk around your lab
- Look around with mouse
- Grab and manipulate objects
- Perform chemistry experiments without VR!

---

## 📞 STILL NEED HELP?

If you're stuck on a specific step:
1. Note which step number you're on
2. Note what you see on your screen
3. Note any error messages
4. Ask for help with those specific details!

---

**Good luck and have fun with your chemistry lab! 🧪🔬🎮**
