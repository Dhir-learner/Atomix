# Task Report — Main Menu Desktop Input & Lab Boundary Enforcement

**Date:** 2026-09-07
**Branch:** `master`
**Status:** ✅ Built — compiles clean against the real Unity 6000.3.7f1 assemblies (0 errors, 0 new warnings)
**Scope:** Task 1 (desktop input in `MainMenuScene`) and Task 2 (lab boundary containment)

---

## 1. Executive Summary

Both faults turned out to have a single shared cause underneath them, and it is worth stating
before anything else:

> **The XR packages are no longer installed in this project.** `Packages/manifest.json` contains no
> `com.unity.xr.interaction.toolkit`, `com.unity.xr.openxr`, `com.unity.xr.management` or
> `com.unity.inputsystem`, and `Library/PackageCache` confirms it. Every XR component in every
> scene is therefore a **missing script** right now.

That is why the main menu is dead on desktop: the EventSystem's only input module *is* an XR
component, so the menu has no input module at all and dispatches no pointer events to anything.
It is also why the lab has no containment: the scenes were built expecting XR locomotion to handle
movement, and the desktop fallback that replaced it never collided with anything.

Two independent problems were fixed:

| Task | Root cause | Fix |
|---|---|---|
| **1 — Main menu** | The EventSystem's input module is a missing `XRUIInputModule`, and `DesktopBootstrap` skipped the scene entirely | A `StandaloneInputModule` is attached at runtime, and `DesktopBootstrap` now sets the menu up instead of bailing out |
| **2 — Lab boundary** | `FirstPersonController` moved the player with `transform.position +=`, which bypasses `CharacterController` collision completely (noclip), and the lab room mesh has no collider at all | Movement is routed through `CharacterController.Move()`, and a new `LabBoundary` component measures the room and builds an invisible collider cage plus a per-frame clamp |

### Architectural choices

**Everything is built from code at runtime; no scene or prefab was edited.** This matches how
Tasks 1–5 were delivered (`DesktopBootstrap`, `ExperimentHistoryManager`, `ReactionGraphUI` and
`PostSuccessSequencer` all bootstrap themselves) and it means:

- the four `.unity` files are byte-identical to before, so nothing about the VR authoring is lost;
- reinstalling the XR packages later restores VR behaviour with no merge conflict to resolve;
- the same code covers `LabScene`, `LabAssistantScene` and `TestingPhaseLab` without three edits.

**The boundary is measured, not hard-coded.** `LabBoundary` reads the room shell's
`Renderer.bounds` at runtime — which Unity computes exactly, including the room's awkward negative
parent scale — and only falls back to fixed constants if that measurement fails or looks wrong.
Hard-coded extents would have been a guess derived from scene YAML; measured extents are correct
by construction and survive any future change to the room geometry.

**The fix is XR-safe by construction rather than by an XR API call.** `DesktopUIInput` adds a
`StandaloneInputModule` only when the EventSystem has *no* live input module. If the XR Interaction
Toolkit is ever reinstalled, `XRUIInputModule` resolves, becomes a live module, and nothing is
added — the menu goes back to XR ray-pointing untouched. This deliberately avoids referencing
`UnityEngine.XR`, which is not available here (the VR engine module is not in the manifest either).

---

## 2. Detailed File & Scene Manifest

### Created

```
NEW  VR/Assets/Scripts/DesktopUIInput.cs      (+ .cs.meta, guid 0dc0ab11989d40c6a3b297685b68600c)
NEW  VR/Assets/Scripts/LabBoundary.cs         (+ .cs.meta, guid eb94bbd9bf5343e5a336ad08f3538410)
NEW  CHANGELOG_TASKS.md                       (this file)
```

### Modified

```
MOD  VR/Assets/Scripts/DesktopBootstrap.cs        Task 1 + 2 — menu setup, UI input, boundary spawn
MOD  VR/Assets/Scripts/FirstPersonController.cs   Task 2 — one line: transform.position += -> Move()
MOD  VR/Assets/Scripts/ObjectInteraction.cs       Task 2 — held objects confined to the lab
MOD  VR/ProjectSettings/TagManager.asset          Task 2 — user layer 8 named "LabBoundary"
```

Total source diff: **+67 / −7 lines** across three existing scripts and one project setting,
plus two new files (`DesktopUIInput.cs` 259 lines, `LabBoundary.cs` 549 lines).

### Not modified

- **No `.unity` scene file.** `MainMenuScene`, `LabScene`, `LabAssistantScene` and
  `TestingPhaseLab` are unchanged.
- **No prefab.**
- `ControlReactions.cs`, `PourSubstance.cs`, all eight reaction scripts, `FreeHandReactionEngine`,
  `ExperimentHistoryManager`, `ReactionGraph*`, `PostSuccessSequencer`, `ConvaiAssistantBackend`,
  `InLabAssistantController` — untouched, as required.
- Three files show as modified in `git status` (`EditorBuildSettings.asset`,
  `ShaderGraphSettings.asset`, `codecoverage/Settings.json`) with **zero content diff** — this is
  pre-existing CRLF/LF normalisation noise, not a change made here.

---

## 3. Task 1 Resolution — Main Menu Desktop Input

### 3.1 Exact root cause

Two faults, either of which alone would have killed mouse input.

**Fault A — the EventSystem has no input module.**

`MainMenuScene.unity:27608` defines the `EventSystem` GameObject with three components: a
`Transform`, the `EventSystem` itself (`guid: 76c392e42b5098c458856cdf6ecaaaa1`), and at
`MainMenuScene.unity:27653` a MonoBehaviour with:

```yaml
m_Script: {fileID: 11500000, guid: ab68ce6587aab0146b8dabefbd806791, type: 3}
m_SendPointerHoverToParent: 1
m_TrackedDeviceDragThresholdMultiplier: 2
m_MaxTrackedDeviceRaycastDistance: 1000
m_EnableXRInput: 1
m_EnableMouseInput: 1
```

Those serialized fields identify it as **`XRUIInputModule`** from the XR Interaction Toolkit. That
GUID was resolved against every `.meta` file in `Assets/` and matches nothing — the package is gone,
so the component is a missing script.

With no live `BaseInputModule`, `EventSystem.currentInputModule` is `null` and `EventSystem.Update()`
processes nothing. **No pointer event, hover, click or navigation event is generated at all** — not
just for the mouse, for any device. Note the same dead module is present in all four scenes
(`LabScene.unity:73610`, `LabAssistantScene.unity:10443`, `TestingPhaseLab.unity:41053`).

**Fault B — `DesktopBootstrap` deliberately skipped the scene.**

```csharp
if (scene.name == "MainMenuScene")
{
    FirstPersonController.SetCursorLock(false);
    return;          // <- nothing else was ever built for the menu
}
```

So even the runtime repair path that rescues the lab scenes never ran for the menu.

**Why the lab scenes still work despite the identical dead EventSystem.** `ObjectInteraction`
bypasses the input module entirely: `ObjectInteraction.cs` calls
`EventSystem.current.RaycastAll(pointerData, results)` and then dispatches `pointerDown` /
`pointerUp` / `pointerClick` by hand through `ExecuteEvents`. `RaycastAll` walks the registered
`BaseRaycaster` list directly and never touches the input module, so the crosshair path is immune to
this bug. The menu has no `ObjectInteraction`, so it had nothing.

### 3.2 What was **not** wrong (checked, so it did not get "fixed" needlessly)

| Checked | Result |
|---|---|
| Canvas render mode | `Menu Canvas` (`MainMenuScene.unity:27226`) is World Space (`m_RenderMode: 2`) at (4.84, 1.36, 7.38) — correct for the authored look |
| `GraphicRaycaster` | **Present** on both canvases (`guid: dc42784cf147c0c48a680349fa168899`) |
| Canvas event camera | `m_Camera: {fileID: 1581865591}` — correctly wired to the scene's `Main Camera`, 1.11 m in front of the canvas |
| Camera setup | Exactly **one** camera, tagged `MainCamera`, enabled — no camera conflict |
| AudioListener | Exactly **one**, on `Main Camera` — no duplicate-listener risk |
| Button wiring | All 6 buttons and 2 toggles resolve to public `MainMenu.cs` methods (see table below) |
| Legacy input axes | `Horizontal`, `Vertical`, `Submit`, `Cancel`, `Mouse X/Y/ScrollWheel` all exist in `InputManager.asset` |
| `activeInputHandler` | `2` (Both), so the legacy `Input` class that `StandaloneInputModule` uses is active |

Verified button wiring:

| Control | Interactable | Handler |
|---|---|---|
| `LabSceneButton` | yes | `MainMenu.OpenLabScene` |
| `TestingPhaseLabButton` | yes | `MainMenu.OpenTestingPhaseLabScene` |
| `LabAssistantButton` | yes | `MainMenu.OpenLabAssistantScene` |
| `SettingsButton` | yes | `MainMenu.OpenSettingsPanel` |
| `CloseButton` | yes | `MainMenu.CloseSettingsPanel` |
| `ExitButton` | yes | `MainMenu.QuitApplication` |
| `Practice` / `Theory` toggles | yes | `MainMenu.UpdateIncludedTaskTypes` |

So the menu was fully functional apart from having nothing to deliver events to it. The fix is
correspondingly small.

### 3.3 How mouse and keyboard were hooked up

**New component `DesktopUIInput.cs`**, created from code by `DesktopBootstrap` and attached to the
scene's **existing** EventSystem GameObject (never a second one):

1. **`EnsureInputModule()`** — attaches a `StandaloneInputModule` *only if* `GetComponents<BaseInputModule>()`
   returns no live module. A missing script is not returned by `GetComponents<T>()`, which is exactly
   what makes this both effective today and inert under XR tomorrow.
2. **`RepairCanvases()`** (menu mode only) — guarantees each root canvas belonging to this scene has a
   `GraphicRaycaster`, and assigns `Camera.main` to any world-space canvas whose `worldCamera` is
   null. Both were already correct in `MainMenuScene`; this is defensive, and it is scoped to the
   active scene so the DontDestroyOnLoad panels from Tasks 2 and 4 are left to configure themselves.
3. **`SelectFirstControl()`** — selects the top-most interactable `Selectable` so keyboard navigation
   has a starting point (the authored `m_FirstSelected` is `{fileID: 0}`).
4. **`TryCloseOpenPanel()`** — Escape closes an open settings panel via `MainMenu.CloseSettingsPanel()`.

Resulting menu controls:

| Input | Action |
|---|---|
| **Mouse move** | Hover / highlight buttons and toggles |
| **Left click** | Activate button, flip toggle |
| **Arrow keys / WASD** | Navigate between controls (uGUI automatic navigation, driven by the `Horizontal`/`Vertical` axes) |
| **Enter / Space** | Activate the selected control (`Submit` axis) |
| **Escape** | Close the settings panel if it is open |

### 3.4 Cursor state and the handoff into the lab

`DesktopBootstrap.SetupScene` now branches explicitly:

```csharp
if (scene.name == MainMenuSceneName)
{
    SetupMainMenu();          // cursor unlocked + visible, DesktopUIInput in menu mode
    return;
}

FirstPersonController.SetCursorLock(true);   // deterministic lock before anything Start()s
SetupDesktopPlayer();                        // adds FirstPersonController + ObjectInteraction
SetupInteractables(scene);
SetupHelpUi(scene);
SetupCrosshairUi(scene);
SetupUiInput(false);                         // DesktopUIInput in gameplay mode
SetupLabBoundary(scene);                     // Task 2
```

The full cursor lifecycle:

| Moment | Cursor | Who sets it |
|---|---|---|
| Game starts in `MainMenuScene` | **Unlocked, visible** | `SetupMainMenu()`, then re-asserted every frame by `DesktopUIInput.ApplyMode()` |
| `MainMenu.OpenLabScene()` → `LoadScene("LabScene")` | **Locked, hidden** | `SetupScene` locks it at `AfterSceneLoad`, `FirstPersonController.Start()` locks it again — both agree |
| In the lab, player presses **Esc** | Unlocked | `FirstPersonController.Update()` |
| `ExitMenu.ReturnMainMenuScene()` → back to the menu | **Unlocked, visible** | `ExitMenu` unlocks, then `SetupMainMenu()` holds it there |

**No duplicate EventSystems.** `DesktopUIInput.Ensure()` reuses `EventSystem.current`, falling back
to `FindFirstObjectByType<EventSystem>`, and only constructs one if the scene genuinely has none —
which none of the four scenes do. A grep confirms no other script in the project creates an
`EventSystem`, a `StandaloneInputModule` or an `AudioListener`.

**No competition with the crosshair in the lab.** In gameplay mode `DesktopUIInput` keeps the input
module **disabled while the cursor is locked**, and sets `EventSystem.sendNavigationEvents = false`
so movement keys (Space, Enter) can never activate a UI control. `ObjectInteraction` owns clicks
while the cursor is locked; when the player presses Esc, `ObjectInteraction.CanProcessWorldInteraction()`
returns false and the input module takes over. The two are never live at the same time.

---

## 4. Task 2 Resolution — Lab Boundaries

### 4.1 Exact root cause

**The desktop player had no collision at all.** `FirstPersonController.useGravity` defaults to
`false`, and that branch of `HandleMovement()` moved the player like this:

```csharp
Vector3 flyMotion = (motion + (Vector3.up * verticalAxis * verticalMoveSpeed)) * Time.deltaTime;
transform.position += flyMotion;      // <- the bug
```

A `CharacterController` only sweeps and resolves collisions inside `Move()`. Assigning
`transform.position` teleports the capsule, so walls, table edges, benches and the outside of the
building were all passable. Combined with `enableVerticalFlyMovement = true` (Space/Ctrl), the
player was in free noclip flight — which is exactly the reported symptom set.

**And the room shell genuinely has no collider.** All four scenes share an identical
`Environment/Laboratory` subtree. Auditing every collider in `LabScene` (67 `BoxCollider`s, 11
`MeshCollider`s, resolved through their prefabs to world space):

| Object | Components | Verdict |
|---|---|---|
| `Environment/Laboratory/Cube` | `MeshFilter`, `MeshRenderer` | **the wall/floor shell — no collider** |
| `Environment/Laboratory/Cube.001` (`LabScene.unity:134181`) | `MeshFilter`, `MeshRenderer`, `BoxCollider` | a thin horizontal slab at y ≈ 4.47 — a ceiling, not walls |
| `Environment/Plane (1)` | `MeshCollider` | one floor/panel plane |
| `Environment/Periodic-table` | `MeshCollider` | wall decoration |
| the other 65 `BoxCollider`s | — | glassware, benches, shelves, containers, drop-zone triggers |

So there was nothing to stop the player horizontally even if movement *had* collided.

### 4.2 Collision strategy chosen

Per your decision, the movement model is **collide-aware fly**: the feel is unchanged (no gravity,
Space/Ctrl to rise and descend), but the motion now resolves against geometry.

Three layers, so no single failure lets the player out:

**Layer 1 — real character collision.** One line in `FirstPersonController.HandleMovement()`:

```csharp
- transform.position += flyMotion;
+ characterController.Move(flyMotion);
```

The `CharacterController` is a capsule of height 1.8 m, radius 0.3 m, `stepOffset` 0.35 m, centred
0.85 m below the eye — i.e. it already existed and was configured correctly by
`ConfigureCharacterController()`; it simply was never being used. This alone makes every existing
collider in the lab solid: walls where they exist, benches, desks, shelves and the ceiling slab.

**Layer 2 — an invisible boundary cage.** New `LabBoundary` component builds **six box colliders**
around the measured interior:

| Slab | Position | Size |
|---|---|---|
| `Boundary_Wall_NegX` | `x = min.x − t/2`, centred in y/z | `(t, height + 2t, depth + 2t)` |
| `Boundary_Wall_PosX` | `x = max.x + t/2`, centred in y/z | `(t, height + 2t, depth + 2t)` |
| `Boundary_Wall_NegZ` | `z = min.z − t/2`, centred in x/y | `(width + 2t, height + 2t, t)` |
| `Boundary_Wall_PosZ` | `z = max.z + t/2`, centred in x/y | `(width + 2t, height + 2t, t)` |
| `Boundary_Floor` | `y = min.y − t/2`, centred in x/z | `(width + 2t, t, depth + 2t)` |
| `Boundary_Ceiling` | `y = max.y + t/2`, centred in x/z | `(width + 2t, t, depth + 2t)` |

with `t = wallThickness = 0.5 m`. Every slab sits **just outside** the interior, so the whole
playable volume stays free, and the slabs overlap at the corners (hence the `+ 2t`) so there is no
seam to squeeze through. They are non-trigger, have no renderer, and carry no `Rigidbody`.

The floor slab matters as much as the walls: it closes the "fly downwards through the floor" hole
that free-fly opened, and it also catches any released physics object that would otherwise fall out
of the world.

**Layer 3 — a per-frame clamp.** `LabBoundary.LateUpdate()` hard-clamps the player position into
the interior regardless of how it got there. This is the backstop for anything physics does not
catch — a teleport, a scripted reposition, or a fast frame that tunnels a thin collider. It is
normally a no-op because layers 1 and 2 stop the player first.

**Layer assignment.** `ProjectSettings/TagManager.asset` gains a user layer:

```yaml
  layers:
  - Default          # 0
  - TransparentFX    # 1
  - Ignore Raycast   # 2
  -                  # 3
  - Water            # 4
  - UI               # 5
  -                  # 6
  -                  # 7
- -                  # 8
+ - LabBoundary      # 8
```

Layer **8** is the first free *user* layer (0–7 are Unity's built-ins and are not editable from the
Layers UI). The default physics collision matrix has layer 8 colliding with everything, so the cage
stops the `CharacterController` and any loose rigidbody. `LabBoundary.ExcludeBoundaryFromInteractionRaycasts()`
then clears bit 8 from every `ObjectInteraction.interactableLayer` so the cage can never intercept a
crosshair grab aimed at a beaker standing near a wall. If the layer is ever removed from the project,
the code falls back to the boundary object's own layer and still works.

### 4.3 Boundary extents

**The extents are measured at runtime, not hard-coded.** `LabBoundary.ResolveInterior()`:

1. Finds the room root by name (`"Laboratory"` — present at identical transforms in all four scenes,
   at world (4.66, 1.04, 6.26)).
2. Takes the **single largest-volume `MeshRenderer`** under it — deliberately not the union, because
   the room root also contains `Window_Group`, `Door_Group`, two lights and a ceiling lamp
   (`Cylinder`, at y ≈ 8.6) that would otherwise inflate the box.
3. Insets that shell by `wallInset` = 0.35 m in x and z, and drops `ceilingHeadroom` = 0.30 m off
   the top.
4. Sanity-checks the result (`IsPlausible`: each axis between 2 m and 200 m) and compares its floor
   area against the fallback (`IsComparableTo`: within a factor of 2.5), so a shell mesh that turns
   out to be the whole building rather than the room is rejected rather than silently producing a
   useless boundary.
5. Logs the result, so the actual numbers are visible in the Console on every load:

   ```
   [LabBoundary] Measured lab interior from 'Laboratory': min (x, y, z) max (x, y, z)
   ```

**Fallback extents** (used if detection fails or is rejected), identical for all three lab scenes
because they share the same room:

| | X | Y | Z |
|---|---|---|---|
| **min** | 1.35 | 0.00 | 2.90 |
| **max** | 8.00 | 4.40 | 9.30 |
| **span** | 6.65 m | 4.40 m | 6.40 m |

These were derived from the scene YAML by resolving every collider through its prefab into world
space: the `Cube.001` ceiling collider spans x ∈ [1.35, 7.97]; the furthest lab props are
`desk (1)` at x ≈ 1.61, the window wall at x ≈ 7.87, the blackboard/`Sponge` at z ≈ 3.18 and the
test-tube racks at z ≈ 9.02. The desktop spawn point is (5.33, 2.26, 8.03), which sits inside the
box with margins of 3.98 m (−X), 2.67 m (+X), 5.13 m (−Z) and **1.27 m (+Z)** — the player spawns
with their back near the +Z wall, facing into the room, which matches the authored view.

**Containment check.** Every `BoxCollider` in all three lab scenes was resolved to world space and
tested against this box:

| Scene | Box colliders inside the boundary |
|---|---|
| `LabScene` | **62 of 66** |
| `TestingPhaseLab` | **51 of 55** |
| `LabAssistantScene` | **18 of 24** |

Every exception is accounted for and none of them is lab furniture: the `Cube.001` ceiling slab, the
`TubeDropZone` / `BalonDropZone` **triggers**, and (in `LabAssistantScene`) a few loose `eprubeta`
test tubes. The offline YAML resolver reports those positions unreliably because their parents are
stripped prefab children under the room's negative-scale `Environment` node — which is precisely why
the shipped component measures the room with `Renderer.bounds` at runtime instead of trusting numbers
derived this way. No bench, glassware, container, shelf or experiment sheet is left outside the
boundary.

> An earlier pass of this analysis appeared to show the eight `ExperimentSheet` objects sitting
> outside the room at x = 0. That was an artefact of reading `m_LocalPosition` from a `RectTransform`,
> where x/y are driven by `m_AnchoredPosition`. Corrected, `ExperimentSheet1` resolves to
> **(4.00, 2.00, 6.31)** — on the lab bench, comfortably inside the boundary.

### 4.4 Held objects

`ObjectInteraction` moves a held object by direct transform assignment, so it would have passed
through a wall even with the player contained. All three places that compute the hold position
(`MoveHeldObject`, `TryGrabObject`, `ResetHeldObjectPose`) now go through one helper:

```csharp
Vector3 ConfinedHoldPosition()
{
    Vector3 position = transform.position + (transform.forward * holdDistance);
    LabBoundary boundary = LabBoundary.Active;
    if (boundary != null)
    {
        position = boundary.ClampHeldObjectPosition(position, heldObjectBoundaryMargin);
    }
    return position;
}
```

`heldObjectBoundaryMargin` defaults to 0.2 m and is an Inspector field. Because the object is
clamped rather than the player, a beaker held against a wall simply stops moving forward — it can
never push the player out of bounds, and it can never be released on the far side of a wall.

### 4.5 Both locomotion modes

| Mode | How it is confined |
|---|---|
| **Desktop WASD + fly** | `CharacterController.Move()` resolves against the cage (layer 1 + 2), and `LabBoundary.LateUpdate` clamps the camera transform (layer 3) |
| **VR continuous locomotion** | The cage is plain physics geometry, so any `CharacterController`- or `Rigidbody`-driven XR locomotion provider collides with it identically. For the clamp, `LabBoundary` resolves the **XR rig root** (`"XR Rig"`, `"XR Origin"`, …) rather than the camera when no `FirstPersonController` is present, and clamps only its footprint (`ClampHorizontally`) — clamping the camera in VR would fight the tracked pose driver and cause sickness |
| **VR teleportation** | Nothing to restrict: there are **no teleportation areas or anchors in any of the four scenes**. The `Locomotion System` GameObject exists but every script on it is a missing script (the XRI package is gone). If teleportation is ever restored, its `TeleportationArea` colliders would need to be placed on the lab floor inside these same extents — the cage does not by itself stop a teleport, which is why the layer-3 clamp exists |

`LabBoundary.ConfineTransform(Transform)` is exposed so a restored XR rig can be registered
explicitly rather than relying on name matching.

---

## 5. Edge Cases & Verification

### 5.1 Compilation

The **actual** `Assembly-CSharp` assembly was compiled with the compiler Unity ships
(`Editor/Data/DotNetSdkRoslyn/csc.dll` via Unity's own .NET runtime), against the project's real
Unity 6000.3.7f1 reference set taken from `Assembly-CSharp.csproj`:

```
sources: 112   references: 342 (0 hint paths missing on disk)
exit=0  errors=0  warnings=69
```

- **0 errors.**
- **0 warnings in any file this task touched** — verified by filtering the warning list for
  `LabBoundary`, `DesktopUIInput`, `FirstPersonController`, `ObjectInteraction` and
  `DesktopBootstrap`; the match count is zero.
- All 69 warnings are pre-existing `CS0649` / `CS0414` / one `CS0618` in `ReactionLearningController`,
  and the count is unchanged from before these edits.

Note the checked-in `Assembly-CSharp.csproj` remains stale (it lists 110 compile items and still
references files deleted in Task 3). The type-check regenerates the source list from disk and reuses
only the csproj's reference paths, all 342 of which resolved. Unity regenerates the csproj on open.

### 5.2 Scene integrity

Verified by parsing the scene YAML and resolving every `m_Script` GUID against the project's
`.meta` files:

| Check | Result |
|---|---|
| Scene files modified | **None** — all four `.unity` files untouched |
| EventSystems per scene | 1 (unchanged; no new one is ever created) |
| Cameras per scene | 1, tagged `MainCamera`, enabled — no camera conflict on transition |
| AudioListeners per scene | 1, on `Main Camera` — no duplicate-listener warning possible |
| Other scripts creating an EventSystem / AudioListener | none (grepped) |
| Menu button references | all 8 controls resolve to existing public `MainMenu.cs` methods |
| Orphaned components | none created; `LabBoundary` and its six slabs are scene-local children of one `LabBoundary` GameObject and die with the scene |
| DontDestroyOnLoad objects | unchanged set (`DesktopBootstrap`, `ExperimentHistoryManager`, `InLabAssistantController`, `LabEffectsInitializer`, `ReactionGraphUI`) — `DesktopUIInput` and `LabBoundary` are deliberately scene-local |

### 5.3 Backwards compatibility with VR

| Concern | Handling |
|---|---|
| Would the input module fight `XRUIInputModule`? | No — `DesktopUIInput` adds nothing if any live `BaseInputModule` is present. Reinstalling XRI makes the XR module live and the desktop module is never created |
| Does anything reference `UnityEngine.XR`? | No. The VR engine module is not in this manifest either, so an XR API check would not compile. The "is another module live?" test is used instead |
| Is the authored VR setup damaged? | No. No scene was edited, so `XR Rig`, `Camera Offset`, the hand controllers, `Locomotion System`, `XR Interaction Manager` and the canvas's `TrackedDeviceGraphicRaycaster` all remain exactly as serialized and will re-resolve the moment the packages come back |
| Would the boundary break VR? | The cage is ordinary physics geometry that any locomotion provider respects. The clamp targets the rig root, not the head, so tracked head motion is never fought |

### 5.4 Regression risks considered and closed

| Risk | Resolution |
|---|---|
| A `StandaloneInputModule` in the lab would double-fire clicks against the crosshair | The module is **disabled while the cursor is locked**, which is exactly when `ObjectInteraction` is active. `CanProcessWorldInteraction()` returns `IsCursorLocked`, so the two are mutually exclusive by construction |
| Navigation events letting Space (fly up) or Enter activate a lab UI button | `EventSystem.sendNavigationEvents = false` in gameplay mode; enabled only in the menu, where keyboard navigation is wanted |
| `RepairCanvases` adding a `GraphicRaycaster` to a lab canvas that deliberately has none, which would start swallowing grabs via `HasUiInteractableAtCrosshair()` | `RepairCanvases` runs in **menu mode only**, and only over canvases belonging to the active scene |
| The boundary cage intercepting a crosshair grab near a wall | Bit 8 is cleared from every `ObjectInteraction.interactableLayer` at boundary start-up |
| Auto-detection measuring the wrong mesh and producing a uselessly large boundary | `IsPlausible` + `IsComparableTo` reject it and fall back to the verified constants, with a `Debug.LogWarning` naming the ratio |
| The player spawning inside a collider now that collision is on | Spawn is (5.33, 2.26, 8.03); the capsule occupies y ∈ [0.51, 2.31] at radius 0.3. The nearest colliders are the bench at z ≤ 7.10 and the racks at x ≥ 7.16 — clear on every axis |
| Setting `isStatic` at runtime logging warnings | Removed; the slabs are ordinary runtime objects |
| Duplicate `LabBoundary` on scene reload | `SetupLabBoundary` returns early if one already exists in that scene |

### 5.5 Not verified — needs a Play-mode pass

I was not able to run the Unity Editor from this session (no Unity MCP server is connected), so the
following are reasoned from the scene data and the code, but untested in-game:

1. **The measured interior versus the fallback.** `LabBoundary` logs
   `[LabBoundary] Measured lab interior from 'Laboratory': min … max …` on every lab load. **This is
   the first thing worth checking.** If the line instead says `Fallback lab interior`, auto-detection
   was rejected and the constants in the Inspector are in use — still correct, but tune
   `fallbackMin`/`fallbackMax` if a corner of the room feels unreachable. Setting
   `debugDrawSeconds` above 0 draws the box in the Scene view.
2. **Whether any bench collider now feels "sticky".** Turning collision on means desks and shelves
   stop the player for the first time. That is the requested behaviour ("step over table edges" was
   listed as a bug), but if a particular prop's collider is oversized it will be obvious immediately.
   `characterRadius` and `stepOffset` are Inspector fields.
3. **Menu click accuracy at your resolution.** The world-space canvas maps mouse position through
   `Camera.main`; the geometry is correct on paper but only a Play-mode click confirms the hit area.
4. **Whether the keyboard auto-selection is wanted.** `selectFirstControlInMenu` starts with the
   top-most button selected so arrows/Enter work. Setting it to `false` makes the menu mouse-only.

### 5.6 Two things worth knowing beyond these tasks

1. **The XR packages need reinstalling before this project can run in a headset again.**
   Nothing in this change depends on them being absent, and nothing here blocks their return — but
   as the project stands today "VR mode" cannot be built or tested, because `com.unity.xr.interaction.toolkit`,
   `com.unity.xr.openxr`, `com.unity.xr.management` and `com.unity.inputsystem` are all missing from
   the manifest while `Assets/XR/` and the XRI sample folder are still checked in. Reinstalling them
   will resolve the missing scripts in all four scenes in one step.
2. **`LabAssistantScene` still contains the inert Inworld GameObjects** noted in Task 3 §8. They are
   unrelated to this work and were left alone.

---

*Generated on completing the Main Menu Desktop Input and Lab Boundary tasks.*
