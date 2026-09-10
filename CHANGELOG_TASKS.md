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

---

# Task Report — Testing Scene on the Lab's Reaction Logic

**Date:** 2026-09-07
**Branch:** `master`
**Status:** ✅ Built — 0 compile errors, 0 new warnings; **111/111 checks pass** running the real engine
**Scope:** `TestingPhaseLab` now judges experiments exactly as `LabScene` does, with the quantities hidden

---

## 6. Executive Summary

The testing scene's eight `*Test.cs` scripts were still on the **original pre-free-hand logic**: a plain
boolean check such as `if (salt.containsCuO && h2so4.containsHCL) -> task finished`. Quantity,
tolerance and procedure order were not considered at all, so any amount of anything passed.

All eight now run the same `FreeHandReactionEngine` as their lab counterparts, with the same targets,
tolerances, flow rates and chemistry-accurate failure reasons — **except that the target quantity and
the accepted range are never disclosed.** The student sees only how much of each chemical they have
actually used, which is the point of a test: knowing the right amount is the thing being examined.

| | Lab (`LabScene`) | Test (`TestingPhaseLab`) |
|---|---|---|
| Tracker line | `H2SO4: 12.5 / 20.0 ml  (accept 18.4-21.6)` | `H2SO4: 12.5 ml` |
| While pouring | `Adding H2SO4... stop between 18.4 and 21.6 ml.` | `Adding H2SO4...` |
| Failure headline | `FAILED: Too much H2SO4` + `21.6 ml (max 21.6)` | `FAILED: Too much H2SO4` + `21.6 ml used` |
| Failure detail | `H2SO4: 21.6 ml (expected ~20.0)` | `H2SO4: 21.6 ml used` |
| Closing line | `Ask your AI Lab Assistant...` | `Revisit this experiment in the Lab to see the correct quantities.` |
| Step-by-step guidance | yes ("Now you can add Copper oxide...") | none — only the task prompt |

### Architectural choices

**One `hideTargets` flag on the shared engine, defaulting to `false`.** Exam mode changes only what is
*said*, never how the experiment is *judged*. The same code path decides success and failure in both
scenes, so the two can never disagree about what a correct experiment is.

**One shared `ExamReactionRunner` instead of eight copies of the lab wiring.** Each lab reaction carries
roughly 150 lines of engine/tooltip/history plumbing. Copying that eight more times would have been
eight chances to introduce a bug and eight places to drift. The runner owns it once; each test script
gained about 25 lines.

**No scene or prefab was edited.** The tooltip anchors to the script's own transform (or an existing
`currentBerzelius` reference), so nothing new needs wiring in the Inspector.

---

## 7. File Manifest

```
NEW  VR/Assets/Scripts/ExamReactionRunner.cs   (+ .meta)  shared exam driver

MOD  VR/Assets/Scripts/FreeHandReactionEngine.cs      hideTargets + exam closing line
MOD  VR/Assets/Scripts/ExperimentHistoryManager.cs    ReactionHistoryRecorder.hideTargets
MOD  VR/Assets/Scripts/ExperimentHistoryUI.cs         blockedScenes gate

MOD  VR/Assets/Scripts/ReactionTest.cs                test task 1  (lab reaction 1)
MOD  VR/Assets/Scripts/ReactionTestHClNaOH.cs         test task 2  (lab reaction 3)
MOD  VR/Assets/Scripts/ReactionTestH2so4CuO.cs        test task 3  (lab reaction 2)
MOD  VR/Assets/Scripts/KOHReactionTest.cs             test task 4  (lab reaction 4)
MOD  VR/Assets/Scripts/ReactionAlI3Test.cs            test task 5  (lab reaction 5)
MOD  VR/Assets/Scripts/ReactionCaOHTest.cs            test task 6  (lab reaction 6)
MOD  VR/Assets/Scripts/CaCO3ReactionTest.cs           test task 7  (lab reaction 7)
MOD  VR/Assets/Scripts/FeSO4ReactionTest.cs           test task 8  (lab reaction 8)
```

**Note the numbering.** `Randomize` numbers its tasks differently from the book: test tasks 2 and 3 are
lab reactions 3 and 2. Each script records the **lab** reaction id so test attempts group with lab
attempts in the history, with `[Test]` appended to the display name so the two stay distinguishable.

Not modified: `ControlReactions.cs`, `PourSubstance.cs`, any pour script, `Randomize.cs`,
`CountdownTimer.cs`, `TheoreticalTasksManager.cs`, any lab reaction script, any scene, any prefab.

---

## 8. What each test experiment now enforces

Same numbers as the lab, taken from each lab script:

| Test task | Reagents | Targets | Tol. | Flow |
|---|---|---|---:|---|
| 1 | Water, Sodium | 50 ml, 5 g | ±5% | 10 ml/s, one measured block |
| 2 | HCl, NaHCO₃ | 15 ml, 12 g | ±10% | 5 ml/s, 3 g/s |
| 3 | H₂SO₄, CuO | 20 ml, 8 g | ±8% | 5 ml/s, 2 g/s |
| 4 | Water, Potassium | 50 ml, 3 g | ±5% | 10 ml/s, block + 1 g/s after a 2 s grace |
| 5 | Aluminium, Iodine, Water drops | 5 g, 15 g, 2 ml | ±10% | 1.25 g/s, 3.75 g/s, 0.7 ml/s |
| 6 | Water, CaO | 30 ml, 15 g | ±8% | 5 ml/s, 3 g/s |
| 7 | Heating | 10 s | ±15% | 1 s/s over a lit burner |
| 8 | Heating | `heatingDuration` | ±15% | 1 s/s over a lit burner |

Order is enforced everywhere it is in the lab (acid before oxide, water before metal, both powders
before the catalyst drops). Task 7 also fails if the tube is heated before the balloon is fitted.
Tasks 1 and 4 still require the indicator afterwards, and task 6 still requires the litmus check.

### Two decisions I made

| Question | Choice | Why |
|---|---|---|
| What happens when a task is failed? | Show the chemistry reason, **award no points**, move to the next task after 6 s | It mirrors the existing 60-second timeout path, so the student is never stuck. Scoring is skipped by setting `continua = false` *and* `wasScored = true`, the only combination `CountdownTimer` treats as "no score". |
| Can the student look the answer up mid-test? | No | The Tab history panel prints `(target 20.0)` on every step. `ReactionHistoryRecorder.hideTargets` now omits targets from test attempts, and `ExperimentHistoryUI.blockedScenes` closes the panel in `TestingPhaseLab`. Both are Inspector fields if you disagree. |

Reaction 1 is the one place the test is *stricter* than the lab: `Reaction.cs` tracks its quantities
inline and does not enforce order, so `ReactionTest` was modelled on `KOHReaction` — the same reaction
with potassium instead of sodium — which does. Sodium still arrives as one correctly-measured block,
exactly as in the lab.

---

## 9. Verification

### The outcome matrix — 111/111, running the shipped engine

`FreeHandReactionEngine`'s only Unity dependency is `Time.deltaTime`, so the engine was extracted
**verbatim** from the source file, given a one-field `Time` shim, and driven with the exact targets,
tolerances and flow rates the eight test scripts register. This is the shipped judging code, not a
re-implementation.

```
T3  H2SO4 + CuO (ReactionTestH2so4CuO)   (tolerance +/-8%)
  PASS correct amounts -> Success
  PASS no target/range wording (success)
  PASS overdose -> FailOverdose
  PASS underdose -> FailUnderdose
  PASS reversed order -> FailWrongOrder
  PASS pause mid-procedure -> still InProgress
  PASS   then finish -> Success
  PASS tracker shows the amount used ("H2SO4: 10.0")
  PASS tracker has no accepted range
  PASS readings have no (used / target) form
  PASS target value never printed
...
ALL 111 CHECKS PASSED
```

Every task was checked for: success on correct amounts, overdose, underdose, wrong order (where order
is enforced), **pausing mid-procedure must not fail you**, and — the point of this task — that the
tracker still shows the amount used while never disclosing a target or a range.

The no-leak check is both **structural** (no `accept`, `stop between`, `(max`, `(min`, `expected ~`,
and no `used / target` form) and **numeric**: with a measurement deliberately away from the target and
from both tolerance edges, every number the engine prints is parsed and compared, and none may equal a
target or a bound.

Actual exam-mode output, produced by running the code:

```
[Chemicals Used]                    Experiment Failed - too much H2SO4.
H2SO4: 12.5 ml                      H2SO4: 21.6 ml used
CuO: 0.0 g                          CuO: 0.0 g used

Adding H2SO4...                     Excess acid creates corrosive fumes ...

                                    Revisit this experiment in the Lab to see
                                    the correct quantities.
```

### Lab mode is provably unchanged

Seven regression checks run the same engine with `hideTargets = false` and assert the old wording is
still produced exactly:

```
  PASS lab tracker still shows 'used / target'
  PASS lab tracker still shows the accepted range
  PASS lab tracker still shows the stop-between hint
  PASS lab tooltip still shows the target
  PASS lab failure headline still shows the max
  PASS lab failure text still shows the expected value
  PASS lab failure text still points at the AI assistant
```

### Three harness bugs caught before they became false bug reports

1. **Reversed order looked broken on six tasks.** It is not: `IsOrderViolated` fires when an
   *earlier*-ordered reagent arrives *after* a later one, so adding only the second reagent is not yet
   a violation. The harness was adding one reagent and expecting a verdict; corrected, all six pass.
2. **The failure line looked like a leak.** On an overdose the engine reports the instant the maximum
   is crossed, so the measurement shown is approximately the upper bound. That number is the student's
   own reading, which is exactly what was asked for — the check now compares against measurements.
3. **`5.0` was "found" inside `15.0`.** The numeric check now parses numbers and compares them
   numerically instead of by substring.

### The scene assumption was verified, not assumed

Everything here depends on `Randomize` switching each experiment's GameObject on and off, since that is
what drives `OnEnable`/`OnDisable` and therefore the per-task reset. Each script's host was resolved
through its prefab instance in the scene YAML:

| Script | Sits on `Substance` under | `Randomize` shows it for |
|---|---|---|
| `ReactionTest` | `correct_berzelius-with-substance_NaOH` | task 1 ✓ |
| `ReactionTestHClNaOH` | `berzelius_hcl_nahco3` | task 2 ✓ |
| `ReactionTestH2so4CuO` | `correct_berzelius-with-substance` | task 3 ✓ |
| `KOHReactionTest` | `KOHContainer` | task 4 ✓ |
| `ReactionAlI3Test` | `small_vase_new` (own root) | task 5 ✓ |
| `ReactionCaOHTest` | `CaOH_beaker22` | task 6 ✓ |
| `CaCO3ReactionTest` | `TubeWithSubstance1` (own root) | task 7 ✓ |
| `FeSO4ReactionTest` | `TubeWithSubstance` (own root) | task 8 ✓ |

All eight match `Randomize`'s show-lists exactly, and every name it passes to `GameObject.Find` exists
in the scene.

### Compilation

```
sources: 113   references: 342
exit=0  errors=0  warnings=69
```

0 errors, and **0 warnings in any file touched by this task** — the 69 are the same pre-existing
`CS0649` / `CS0414` / `CS0618` set as before.

### No interference with the lab-only systems

`PostSuccessSequencer` and `ReactionGraphUI` both subscribe to `ReactionHistoryRecorder.Completed`,
which the test scripts now raise on success. Both check `IsEnabledScene()` **at event time** and are
gated to `LabScene`, so no molecular video and no graph panel can interrupt a timed test. Verified by
reading the call sites rather than assuming.

### Not verified — needs a Play-mode pass

I could not run the Unity Editor, so these are reasoned but untested:

- **Whether 60 seconds is still enough per task.** This is the one worth checking first: measuring by
  hand is slower than tripping a boolean, and the accepted pour windows are 0.5–1.0 s of careful
  pouring. If tasks start timing out, raise `startingTime` in `CountdownTimer`.
- Tooltip placement above each receptacle (`tooltipHeightOffset` is an Inspector field on all eight).
- That the failure-sound fields, which ship unassigned, are silently skipped as intended.
- The 6-second pause on a failed task before the next one is drawn.

---

# Task 7 — Enhancement Pack: Settings, Periodic Table, Achievements, Report Card

**Date:** 2026-09-07
**Branch:** `kushal`
**Status:** ✅ Built — 0 compile errors, 0 new warnings; **33/33 logic checks pass** running the real code
**Brief:** *"completely enhance my project and add all the new features you can think of — free hand"*

---

## 10. Executive Summary

Seven additions, chosen by looking for things the project genuinely lacked rather than things that
would be fun to build. Every one of them is **additive, built from code at runtime, and touches no
scene or prefab** — the same pattern Tasks 1–6 established, so all of it survives a Unity reimport
and none of it conflicts with the VR authoring.

| # | Feature | How you reach it | Why it was missing |
|---|---|---|---|
| 1 | **Settings** — sensitivity, invert look, move speed, FOV, volume, panel size, accessibility | `F1` | There were none. Sensitivity and FOV were compile-time constants. |
| 2 | **Pause menu** with Settings / Achievements / Controls tabs | `F1` | The only way out of the lab was the exit sign in the corner. |
| 3 | **Interactive periodic table** — 118 elements, category-coded, click for detail | `P` | The lab has a periodic table modelled on the wall that is a texture you cannot read. |
| 4 | **Achievements** — 13 milestones with HUD toasts | Pause menu | Nothing read the history back as progress. |
| 5 | **End-of-test report card** — per-task outcomes, score, grade, advice | automatic | The testing scene ended with one line of text. |
| 6 | **Lab report export** — Markdown + CSV | Pause menu / report card | The history was JSON only: for the game, not for a student or a marker. |
| 7 | **Fixed a real input bug** — `V` was bound to two things at once | — | Asking the assistant a question silently rolled whatever you were holding. |

### The bug worth calling out

`V` was **double-bound**: `InLabAssistantController.pushToTalkKey` and the roll axis in
`ObjectInteraction.RotateHeldObject`. Both are live in `LabScene` at the same time, so holding `V`
to talk to the assistant also spun the beaker in your hand.

Roll is now one key with a modifier — `C`, or `Shift+C` to reverse — which frees `V` completely.
While there, all five rotation keys became Inspector fields instead of hard-coded `KeyCode`
literals, so any of them can be rebound without touching code.

---

## 11. File Manifest

### Created

```
NEW  VR/Assets/Scripts/AtomixSettings.cs          244   persistent preference store + live apply
NEW  VR/Assets/Scripts/AtomixSettingsApplier.cs    68   pushes settings into each loaded scene
NEW  VR/Assets/Scripts/LabPanelBuilder.cs         342   shared world-space panel construction
NEW  VR/Assets/Scripts/PauseMenuUI.cs             432   F1 menu, three tabs
NEW  VR/Assets/Scripts/PeriodicTableData.cs       296   118 elements, IUPAC atomic weights
NEW  VR/Assets/Scripts/PeriodicTableUI.cs         376   P, interactive table
NEW  VR/Assets/Scripts/AchievementSystem.cs       343   13 milestones derived from history
NEW  VR/Assets/Scripts/LabReportExporter.cs       365   Markdown + CSV export, grading
NEW  VR/Assets/Scripts/TestResultsUI.cs           405   end-of-test report card
                                                 ----
                                                 2871   (+ 9 .meta files)
```

### Modified — 116 insertions, 27 deletions across 10 files

```
MOD  ObjectInteraction.cs          rotation keys made rebindable; V conflict fixed
MOD  FirstPersonController.cs      invertLook option
MOD  DesktopCrosshairUI.cs         honours the "show crosshair" setting
MOD  CountdownTimer.cs             exposed Score and TimeRemaining (read-only)
MOD  LabHudController.cs           ShowToast made public + a static Instance, so other
                                   systems reuse the toast instead of building their own
MOD  ExperimentHistoryManager.cs   bootstraps the four new components
MOD  ExperimentHistoryUI.cs        uses the shared panel-exclusion helper
MOD  ReactionGraphUI.cs            same, plus the "read the graphs" achievement
MOD  PostSuccessSequencer.cs       "watched the molecular video" achievement
MOD  ControlsHelpUI.cs             documents F1, P and the new roll binding
```

Not modified: any `.unity` scene, any prefab, any reaction script, `ControlReactions.cs`,
`Randomize.cs`, `PourSubstance.cs`, `FreeHandReactionEngine.cs`, `ExamReactionRunner.cs`.

---

## 12. Design notes worth recording

**Why `F1` and not `Esc`.** `Esc` is already load-bearing: it toggles the cursor lock in
`FirstPersonController` and closes the history, graph and video panels. Hanging a pause menu off it
would have meant unpicking all of that. `F1` was free, is the universal "help/menu" key, and is
documented in both the `H` overlay and the menu's own Controls tab.

**The pause menu does not touch `Time.timeScale`.** Several reaction scripts sequence their visuals
with `DateTime.Now`, which ignores the time scale — R2 changes the beaker material at 2 s, 4 s and
6 s, R6 holds its explosion to 6 s. Freezing time would desync every one of them from its own
effects. What *would* be unfair to leave running is the testing countdown, so that one clock is
paused directly and restored on close. The player is frozen by disabling the controller component,
which stops WASD and mouse-look in one step.

**Panels are mutually exclusive.** Every runtime panel sits at roughly the same distance in front of
the camera, so two open at once just stack and neither is readable. Previously the history and graph
panels closed each other by name; with three more panels that would have been a growing web of
pairwise calls, so it is now one helper — `LabPanelBuilder.CloseOtherPanels(this)` — that the five
panels all route through.

**The settings menu uses a real mouse pointer, not the crosshair.** It is a lot of small controls.
`DesktopUIInput` (Task 1 of the previous round) enables the UI input module exactly when the cursor
is unlocked, so releasing the cursor makes the buttons clickable — and disabling the controller stops
the player wandering off while it is released. The steppers use `◀ ▶` buttons rather than uGUI
Sliders, because a Slider needs a pointer *drag*, which never arrives while the crosshair is locked.

**Settings are reachable from the main menu too.** A student whose mouse is too fast needs to fix it
*before* walking into the lab. The panel is placed 0.85 m out there, in front of the authored menu
canvas at 1.11 m, and the "Main menu" button hides itself when you are already in it.

**Achievements are derived, never incremented.** `EvaluateHistory` re-reads the stored attempts
rather than keeping counters, so the state stays correct across sessions and after the history is
loaded back from disk. All 13 hang off `ReactionHistoryRecorder.Completed` — the same single funnel
the graphs and the video sequencer use — so no reaction script needed touching.

**The exam keeps its secrets.** The periodic table names every element, which is a hint during an
examination, so `P` is blocked in `TestingPhaseLab` — consistent with the Task-on-test-scene decision
to block the history panel there for the same reason.

---

## 13. Grading

The report card and the export share one grading routine, so the number on screen and the number in
the file cannot disagree.

| Success rate | Grade |
|---|---|
| ≥ 90% | A |
| ≥ 80% | B |
| ≥ 65% | C |
| ≥ 50% | D |
| below | F |

with a **coverage cap**, because finishing two experiments perfectly is not an A when there are
eight on the bench:

- fewer than 4 of 8 experiments completed → capped at **C**
- fewer than 6 of 8 completed → capped at **B**

Abandoned attempts are counted separately and excluded from the rate — walking away from an
experiment is not the same as getting it wrong.

---

## 14. Verification

### Compilation

```
sources: 123   references: 344
exit=0  errors=0  warnings=69
```

0 errors, and **0 warnings in any of the nine new files**. The 69 are the same pre-existing
`CS0649` / `CS0414` / `CS0618` set as before this task.

### The pure logic was executed, not just compiled

`PeriodicTableData` and the grading types were extracted verbatim from the shipped sources, given
shims for the handful of `UnityEngine` members they touch, and run:

```
PERIODIC TABLE
  PASS 118 elements parsed (got 118)
  PASS atomic numbers unique
  PASS symbols unique
  PASS every group/period/mass in range
  PASS atomic numbers 1-118 with no gaps
  PASS no two elements share a grid cell
  PASS H = 1.008 / C = 12.011 / Fe = 55.845 / U = 238.03
  PASS Og is element 118
  PASS Tc flagged as having no stable isotope
  PASS Au not flagged synthetic
  PASS Ce laid out on the lanthanide strip
  PASS U laid out on the actinide strip
  PASS Hf back in the main body at period 6
  PASS all 8 reactions map to real element symbols (26 references)

GRADING
  PASS 8/8 covered, no failures -> A
  PASS 8/8 covered, 88.9% -> B, just under the A line
  PASS 8/8 covered, 80% -> B      PASS 67% -> C      PASS 50% -> D      PASS 20% -> F
  PASS 2/8 covered, perfect -> capped at C
  PASS 5/8 covered, perfect -> capped at B
  PASS 6/8 covered, perfect -> A
  PASS empty history yields a placeholder grade, not a crash
  PASS abandoned attempts counted separately, not as failures
  PASS success rate ignores abandoned attempts

CSV ESCAPING
  PASS plain field unquoted      PASS comma field quoted
  PASS embedded quotes doubled   PASS empty field stays empty

ALL 33 CHECKS PASSED
```

The **"no two elements share a grid cell"** check is the one that matters most for the table: a
single wrong group or period number would silently stack two elements on top of each other, which is
easy to author and hard to spot by eye across 118 rows.

One check failed on the first run — `8 successes, 1 failure -> A`. That was the *test* being wrong:
8/9 is 88.9%, below the 90% A threshold, so B is correct. Expectation corrected rather than the code.

### Self-review found three real problems before they shipped

1. **The periodic table stacked a new "on the bench" label every time it was opened**, because it was
   created inside the refresh rather than once at build time. Now created once and re-texted.
2. **The pause menu left the player walking around.** Releasing the cursor for the mouse pointer does
   not stop movement — `FirstPersonController.allowMovementWhenCursorUnlocked` is true — so WASD still
   drove the camera behind the menu. The controller is now disabled while any full-screen panel is up.
3. **Panels could stack.** `F1` over an open periodic table would have drawn two unreadable panels at
   the same depth. All five now route through one exclusion helper.

### Not verified — needs a Play-mode pass

No Unity Editor was available this session (the `com.coplaydev.unity-mcp` package is in the manifest
but no MCP server is connected), so the following are reasoned but untested:

- **Panel proportions and legibility at 1.5–1.6 m.** All sizes are Inspector fields, and the panels
  use the same construction and distances as the history and graph panels, so if those read
  correctly these should too.
- **The periodic table's 118 cells at 66 px.** The layout arithmetic is checked (no collisions, all
  rows inside the grid rect), but the *visual* density is a judgement only Play mode can settle.
  `CellSize` and the canvas dimensions are the two numbers to nudge.
- **The main-menu pause panel drawing in front of the authored menu canvas.** It is placed nearer
  (0.85 m vs 1.11 m) and given a higher `sortingOrder`, which should settle it both ways, but the
  main menu is the one place two world-space canvases now share a view.
- **That `F1` is not swallowed by the OS or the editor** on your machine.

---

## 15. Round 2 — playtest fixes

Two problems reported from Play mode, with screenshots. Both reproduced from the layout arithmetic
before anything was changed, so the fixes are aimed at the actual cause rather than nudged by eye.

### 15.1 The element card covered four elements

**Reported:** *"in the periodic table image some elements are hidden by the information that is
shown when we click on the element"*

**Cause.** The detail card was pinned to the top-left corner of the panel — 40 px in from the left
edge, 190 px down from the top — which is where the s-block lives. Running the layout maths against
every element position confirmed exactly what the screenshot showed:

```
detail pane  x[-740..-320] y[185..335]
  Li  at ( -597.0,  287.0)  covered=True
  Be  at ( -527.0,  287.0)  covered=True
  Na  at ( -597.0,  217.0)  covered=True
  Mg  at ( -527.0,  217.0)  covered=True
  K   at ( -597.0,  147.0)  covered=False
```

Clicking any element hid Li, Be, Na and Mg.

**Fix.** A periodic table has a large piece of genuinely empty space built into it: **groups 3–12
of periods 1–3**, the notch between the s- and p-blocks. That is the conventional place to put a
key, and nothing can ever be drawn there. The card now lives in it.

The coordinates are **derived from the grid**, not hand-tuned — `blockLeft`, `blockRight`,
`blockTop` and `blockBottom` are computed from `CellSize`, `CellGap` and the grid origin — so
changing the cell size moves the card with the table instead of silently re-creating the overlap.
A shared `GridCentreY` constant replaced the magic `40.0f` that the grid and the card each had
their own copy of.

Re-checked against every one of the 118 element positions plus both f-block markers:

```
detail card  centre=(-142,287)  size=680x190
             x[-482..198] y[192..382]
elements covered by the card: 0
marker 57-71   at (-457,7)   covered=False
marker 89-103  at (-457,-63) covered=False
```

### 15.2 The pause menu was a postage stamp

**Reported:** *"for F1 settings menu it should be seen in full screen"*

**Cause.** It was built as a **world-space** panel like the history and graph panels — a plate
1120×800 units scaled by 0.001 and hung 1.5 m in front of the camera. That is right for something
you read beside the glassware, but a settings screen is a lot of small controls read head-on: at
1.5 m it has to be small enough to fit the field of view, which is why it floated in the middle of
the lab at about a third of the screen.

**Fix.** New `LabPanelBuilder.CreateFullScreenCanvas` builds a **Screen Space Overlay** canvas with
a `CanvasScaler` in `ScaleWithScreenSize` mode against a 1920×1080 design space, `matchWidthOrHeight
= 0.5` so the layout survives both 16:9 and wider desktops. The panel stretches to the full canvas
less a 70 px margin.

Two things this also buys:

- **Crisper text.** Overlay rasterises at screen pixels, instead of scaling glyphs down by 0.001 and
  back up again.
- **The backdrop now absorbs clicks** (`raycastTarget = true`, unlike the world-space one), so a
  stray click behind the menu can no longer grab a beaker you cannot see.

`Menu size` in Settings now drives `CanvasScaler.scaleFactor` rather than the world-space transform
scale, so it does what its name says at any resolution.

### 15.3 A second bug found while fixing the first

The screenshot also showed *"Colour-blind safe swaps the green/red result pair…"* printed straight
through the **Reset to defaults** button. That was not a rendering artefact — the two were 2 px
apart:

```
status line   y=-278
reset button  y=-276
overlap=True (gap 2 px, need ~50)
```

The layout had grown a row at a time with no check that the body still fitted above the footer. The
bands are now named constants (`TitleY`, `TabsY`, `BodyHeight`, `StatusY`, `FooterY`) and every gap
is verified:

```
title            bottom    372 -> subtitle         top    366  gap     6
subtitle         bottom    338 -> tabs             top    326  gap    12
tabs             bottom    274 -> settings row 1   top    262  gap    12
...
settings row 9   bottom   -230 -> reset button     top   -250  gap    20
reset button     bottom   -298 -> status           top   -326  gap    28
status           bottom   -378 -> footer           top   -395  gap    17
content -453..428 inside panel -470..470 : True
```

Retuning `TabsY` from 316 to 300 also closed a fresh 4 px collision between the subtitle and the tab
row that the first pass had introduced — caught by the same check before it shipped.

### 15.4 Verification

```
exit=0  errors=0  warnings=69      (0 in any changed file)
ALL 33 CHECKS PASSED               (periodic table data, grading, CSV escaping — unchanged)
```

Layout geometry for both panels is now asserted arithmetically rather than eyeballed: 0 of 118
elements covered by the detail card, and no overlapping bands in the pause menu with everything
inside the panel bounds.

**Still wants a Play-mode look:** the overlay menu at your actual window size and aspect ratio (the
scaler handles it in principle, but 21:9 is worth a glance), and whether the element card reads well
in the table's notch now that it is wider and shorter than before.

---

## 16. Round 3 — settings menu redesign

**Reported:** *"in the settings menu the text alignment is not proper and increase the size of text.
you can see in the screen shot that some of the text is overlaping. and also enhance the ui of the
settings menu"*

Three separate faults, all reproduced from the layout arithmetic before anything was changed.

### 16.1 The achievements header printed through the tab row

```
tabs       band 274 .. 326
ach header band 273 .. 311
OVERLAP: True   <- "5 of 13 unlocked" drawn over the Achievements tab button
```

**Cause, and the gap in my own check.** The round-2 verification walked the *Settings* tab bands
against the chrome, and for the other two tabs only asserted "last row is inside the body bottom".
It never checked either tab's **first** row against the tab buttons — so a header sitting at y = 292,
16 px above the tab row's lower edge, sailed through.

**Fix.** A named `BodyTop = 262` constant now defines the top of the usable content band, 12 px
below the tab row, and all three tab builders start from it. The verification walks every row of
all three tabs against both the tab row above and the status line below.

### 16.2 Check marks and arrows rendered as empty boxes

The earned achievements showed `□` rather than a tick, and the settings steppers showed `□ □`
rather than `◀ ▶`. Not a layout problem — **missing glyphs**.

The default TextMeshPro font atlas covers Latin-1 and General Punctuation. `U+2714 CHECK MARK`
(Dingbats) and `U+25C0`/`U+25B6` (Geometric Shapes) are in neither, so TMP drew the "no glyph" box.

**Fix.**

- Steppers use plain ASCII `-` and `+`.
- The achievement tick became `LabPanelBuilder.CreateStatusPip` — a small filled square for earned,
  a hollow outline for not earned. Drawn from two `Image` plates, so it cannot fail to render and
  it picks up the colour-blind-safe palette.
- `1 – 8` became `1 - 8`, `60°` became `60 deg`.
- A scan now asserts that **no built UI string contains a glyph outside the safe range**:

```
=== non-ASCII glyphs left in built UI strings ===
  none - every glyph in a built UI string is inside the default TMP atlas range
```

### 16.3 The redesign

| Before | After |
|---|---|
| 9 undifferentiated rows | Three labelled sections: **Controls**, **Display and sound**, **Accessibility** |
| Label and value floating on the panel | Every row on its own backing plate, 48 px tall |
| Columns drifting with label length | Label, value and buttons at fixed offsets from the row edges, so all rows line up |
| Label 22 pt / value 22 pt | Label and value **25 pt**, section headings 20 pt, title 42 pt |
| Achievements: glyph + two text columns | Status pip + title + description, alternating row tint |
| Controls: 21 rows in one column at 20 pt | **Two columns**, 11 + 10, at 23/21 pt with alternating tint |

Spacing is now three named constants — `HeaderStep` 42, `RowStep` 52, `SectionStep` 46 — rather
than arithmetic inline at each call site, which is how the 2 px collision in round 2 and the 1 px
one here both arose.

### 16.4 Verification

Every gap in every tab, checked by running the layout maths:

```
hdr Controls           bottom  229.0 ->   Controls 1   top  226.0  gap  3.0
  Controls 3           bottom   74.0 -> hdr Display    top   67.0  gap  7.0
  Accessibility 2      bottom -258.0 ->   Accessibility 3 top -262.0  gap  4.0

span -310.0 .. 259.0
first row top  259.0 vs tab row bottom 274.0 : clear
last row bottom -310.0 vs status top -326.0  : clear
inside body band -320..262 : True
ALL CLEAN: True

SETTINGS      rows=12  clean=True
ACHIEVEMENTS  rows=14  clean=True
CONTROLS      rows=11  clean=True
```

Horizontal columns too:

```
settings row (width 1100):
  label   -530 ..   30      value    180 ..  420
  [-]      428 ..  480      [+]      486 ..  538      toggle  412 .. 540
  label->value 150 | value->[-] 8 | [-]->[+] 6 | [+]->edge 12
achievement row: pip -721..-699  title -670..-270  desc -240..720  gap 30
controls col @-430: plate -830..-30  key -810..-510  desc -465..-35   inside=True
controls col @ 450: plate   50..850  key   70.. 370  desc  415..845   inside=True
```

```
exit=0  errors=0  warnings=69      (0 in any changed file)
ALL 33 CHECKS PASSED               (periodic table, grading, CSV escaping - unchanged)
```

### 16.5 A note on how these three rounds went

Each round fixed what was reported and then found something adjacent that the previous round's
checks had not covered — a 2 px collision, then a 1 px one, then a whole category of fault
(missing glyphs) that no amount of coordinate checking would have caught.

The checks are now structural rather than spot: every row of every tab against the chrome above and
below it, every column against its neighbours, and every string against the font's glyph coverage.
That is what should stop the next round of this, but layout at your actual resolution and aspect
ratio is still something only Play mode can confirm.

---

## 17. Round 4 — ungrabbable test tubes in the CaCO3 and FeSO4 test tasks

**Reported:** *"In the test scene the feso4 and caco3 reactions I am not able to grab some objects"*

### 17.1 Root cause: a race between two `Start`-order assumptions

Desktop grabbing is retrofitted. `DesktopBootstrap.SetupInteractables` reflects over every
MonoBehaviour in the scene, collects the `GameObject`-typed **fields** it finds, and adds an
`ObjectGrabbable` to each. An object nothing points at never becomes grabbable.

That scan runs from `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]` and `SceneManager.sceneLoaded`.
Unity's order is:

```
Awake -> OnEnable -> [AfterSceneLoad] / sceneLoaded -> Start
```

`Randomize` locates all ~30 pieces of testing-scene equipment with `GameObject.Find` — **in
`Start()`**, and into *private* fields, so they are also null in the scene file. The scan therefore
reflects over `Randomize` while every one of those fields is still `null`, and registers nothing.

The lab does not have this problem because `ControlReactions` holds the same equipment in
**serialized** fields, populated in the scene asset before anything runs:

```
=== LabScene.unity
    SuportEprubeta        <- ControlReactions.cs.testTubeSuport2
    SuportEprubeta (1)    <- ControlReactions.cs.testTubeSuport1
    TubeWithSubstance     <- ControlReactions.cs.testTube2
    TubeWithSubstance (1) <- ControlReactions.cs.testTube1
=== TestingPhaseLab.unity
    (no script field references any tube/stand/burner)
```

**Why only these two experiments.** Every other task's glassware is reachable from a pour script's
serialized field (`PourSubstance.SecondGlass` and friends), so it was registered anyway. The two
heating tasks are the only ones whose central object — the test tube — is referenced by nothing but
`Randomize`. The balloon was fine, because `CaCO3ReactionTest.balon` is a serialized field:

```
Balon                <- CaCO3ReactionTest.cs.balon
TubeWithSubstance    <- NOTHING (only Randomize, filled in Start)
TubeWithSubstance1   <- NOTHING (only Randomize, filled in Start)
BunsenBurner / BunsenBurner1 / SuportEprubeta / SuportEprubeta1  <- likewise
```

A first pass at this analysis appeared to show the tubes *were* referenced. They were not: the match
was on `m_GameObject`, which is Unity's component-to-owner link, not a script field, and
`RegisterGameObjectFields` never sees it. Worth recording, because it nearly closed the
investigation on the wrong answer.

### 17.2 Fix

`DesktopBootstrap` now re-scans for interactables after the frame has settled:

```csharp
IEnumerator RescanInteractables(Scene scene)
{
    yield return null;                       // Start() has now run
    if (scene.isLoaded) SetupInteractables(scene);

    yield return new WaitForSeconds(0.5f);   // and anything bound from a coroutine
    if (scene.isLoaded) SetupInteractables(scene);
}
```

Chosen over moving `Randomize.FindAllObjects()` into `Awake` because it is general: **any** script
that resolves references in `Start` is now covered, not just this one. It is safe to repeat —
`EnsureGrabbable` and `AttachInvokeAction` both no-op on anything already set up, and
`FindObjectsByType(FindObjectsInactive.Include)` still sees the equipment after `Randomize` hides it
on the first `Update`.

Verified against the grabbable filter:

| Object | Collider | Name passes filter | After fix |
|---|---|---|---|
| `TubeWithSubstance` (FeSO4) | yes | yes | **grabbable** |
| `TubeWithSubstance1` (CaCO3) | yes | yes | **grabbable** |
| `Balon` | yes | yes | grabbable (was already) |
| stands / burners | inside `.fbx`, not resolvable offline | yes | grabbable if they carry a collider, otherwise rejected exactly as before |

The burner is lit by clicking its button, which goes through `LightFire.burnerSupport` ->
`DesktopInvokeInteractable` — a separate path that was never affected.

### 17.3 A second bug, visible in the same screenshot

The HUD strip read `2Al + 3I2 -> 2AlI3 [Test] | Aluminium 0.0 g | Iodine 16.5 g | ... FAILED`
while the CaCO3 task was the one on screen.

`LabHudController` read `ReactionHistoryRecorder.Active`, which is documented as the *most recent*
experiment and is deliberately left in place after a verdict so the AI assistant can still be asked
what went wrong. The next task's recorder does not replace it until the student actually pours
something — so the previous task's reagents and its FAILED banner sat on screen through the whole of
the next one.

`ReactionHistoryRecorder` now also tracks **`Selected`** — the experiment whose equipment is on the
bench — set in `ResetForNewAttempt()` and cleared in `Abandon()`. Those two are called from every
`OnEnable`/`OnDisable` path in all sixteen reaction scripts (the eight lab ones via
`RestartAttemptIfRequested`, the eight test ones via `ExamReactionRunner`), so one edit in one file
covers every experiment in both scenes. The HUD prefers `Selected`, and falls back to `Active` only
while that attempt is still running — never to a finished one.

`Active` itself is untouched, so the AI assistant's context is unchanged.

### 17.4 Verification

```
exit=0  errors=0  warnings=69      (0 in any changed file)
```

Scene analysis confirmed the reference graph in both scenes, and the grabbable filter for every
affected object. **Not verified:** the actual grab in Play mode — the timing fix is reasoned from
Unity's documented callback order rather than observed, so it is worth confirming that the tube can
now be picked up in both heating tasks.

---

## 18. Round 5 — retrying a failed experiment

**Reported:** *"in the lab scene when i fail the reaction there is no way to restart the reaction and
try again I need to restart my entire game"*

Accurate, and there were two compounding reasons.

### 18.1 Re-selecting the same experiment did nothing

Every reaction resets itself in `OnEnable`, through `RestartAttemptIfRequested()`:

```csharp
recorder.Abandon();
recorder.ResetForNewAttempt();
engine.Reset();
failureReported = false;  oneReaction = false;  ...
```

And `ControlReactions.StartReaction2()` brings its equipment back with:

```csharp
h2so4Recipient.SetActive(true);
cuoRecipient.SetActive(true);
cuso4Recipient.SetActive(true);
```

`SetActive(true)` on an object that is **already active is a no-op** — `OnEnable` never fires. So
choosing the experiment you were already on left the engine sitting in `Failed` permanently, with no
route back. Only switching to a *different* experiment and returning fired the reset at all.

### 18.2 …and even then the bench was not reset

Task 2 recorded this as a known limitation; it is what makes the first problem terminal rather than
annoying. None of the following is cleared anywhere in the project:

| State | Where it lives |
|---|---|
| `containsWater`, `containsCuO`, `containsHCL`, `containsNahco3`, `containsNatrium`, `containsPhenolphthalein` | the eight pour scripts — set once, never cleared |
| The recipient's substance material (the CuSO4 blue, the NaCl white) | `transform.Find("Substance")` renderer |
| Poured containers | wherever the student dropped them |
| The CaCO3 balloon | snapped to the socket and `SetGrabbable(false)` |
| The FeSO4 tube colour | baked to the final gradient value |

So clearing the engine on its own would have produced a *half*-reset experiment: one that reports
`0.0 ml` while the beaker is still full and blue. That is worse than no retry at all, because it
looks like it worked.

### 18.3 Fix — reload the lab and re-select the experiment

New `LabRetryController` (154 lines), bootstrapped onto the same persistent object as the rest of
the runtime systems.

**F5** — or **Retry experiment** in the pause menu — remembers which experiment is on the bench,
closes the open attempt, reloads `LabScene`, waits for `ControlReactions.Start()` to run, and calls
the matching `StartReactionN()`. A toast confirms it: *"Bench reset — H2SO4 + CuO → CuSO4 + H2O -
ready to try again"*.

Chasing the state in §18.2 across eight reaction scripts, eight pour scripts, materials, transforms,
particle systems and the balloon snap is exactly where a subtle *"sometimes it does not reset"* bug
would live. Reloading is "wash up and start over" — what a real bench requires, and impossible to
get half-right. Nothing earned is lost: the history, achievements and settings all live on
DontDestroyOnLoad objects, and the failed attempt stays on the record rather than being erased.

`ControlReactions` itself is **not modified** — the numbered entry points are dispatched from a
switch in the new controller.

### 18.4 Making it findable

A key nobody knows about is not a fix, so it is advertised in three places:

- **The failure message itself.** `FreeHandReactionEngine.AssistantClosingLine` now reads:

  > Ask your AI Lab Assistant what went wrong and how to correct it.
  > **Press F5 to reset the bench and try this experiment again.**

  One constant, so it appears for all eight lab reactions — including Reaction 1, which composes its
  failure text through the same shared helper. The testing scene keeps its own closing line, because
  it advances by itself and has "Run the test again" on the results card.

- **The pause menu footer**, as a fourth button. It hides itself when there is nothing on the bench
  to retry, and says so if clicked in that state.

- **The `H` controls overlay** and the pause menu's Controls tab.

### 18.5 Verification

```
sources: 124   exit=0  errors=0  warnings=69      (0 in any new or changed file)
```

Footer re-spaced for the fourth button and checked:

```
Resume              -800 ..  -520
Retry experiment    -500 ..  -160   gap 20
Export lab report   -140 ..   180   gap 20
Main menu            210 ..   490   gap 30
span -800 .. 490 inside +-890 : True
```

**Not verified — needs Play mode:** the reload timing. The re-selection waits for
`ControlReactions` to appear and for at least two frames to pass, and retries for up to 120 frames
before warning, but "the scene is ready" is reasoned from Unity's callback order rather than
observed. If the experiment comes back unselected, `reselectAttempts` is an Inspector field.

`F5` is free across the project's bindings; it is a public `KeyCode` field if it clashes with
anything on your machine.

---

# Task 8 — Live Molecular Animation + Offline AI Assistant

**Date:** 2026-09-08
**Status:** ✅ Built — 0 compile errors, 0 new warnings, **167/167 checks pass** running the real code
**Full report:** [`TASK8_MOLECULAR_ANIMATION_AND_OFFLINE_AI.md`](TASK8_MOLECULAR_ANIMATION_AND_OFFLINE_AI.md)

## 19. Executive Summary

Mapped the project description's ten features against the code. Seven were done. Three were not,
and two of those were load-bearing:

| Gap | What was actually wrong |
|---|---|
| **Reactions 1 and 8 had no molecular visualisation** | `videoClip: {fileID: 0}` in `ReactionLearningVideoCatalog.asset`. Succeeding at sodium-and-water — the reaction the book opens on — produced the string "Molecular explanation video is unavailable." |
| **The whole AI pillar was one HTTP request from gone** | Four of the ten features route through `POST api.convai.com/character/getResponse`. No internet, no mic, an expired quota or a college firewall took out features 4, 5, 7 and 9 together. |
| **No way to ask about the molecular animation** | Feature 7 of the brief had no implementation anywhere. |

Six new files, 2790 lines. Everything additive, built from code at runtime — **no scene, prefab,
reaction script or ProjectSettings asset was touched**, same as Tasks 1–7.

| # | Feature | How you reach it |
|---|---|---|
| 1 | **Live molecular animation for all 8 reactions** — ball-and-stick, rendered in-engine into the existing video panel. Red = bond breaking, green = bond forming, yellow = electron. | Automatic for R1/R8; `3D VIEW` button elsewhere |
| 2 | **Ask about the step on screen** | `ASK AI` button on the video panel |
| 3 | **Offline AI brain** — answers "why did it fail", the graphs and the animation from the game's own live data when Convai is unreachable | automatic fallback |
| 4 | **Offline voice** — replies spoken via the Windows synthesiser | automatic |
| 5 | **Typed questions** — no microphone needed | `Enter` |
| 6 | **One-key "why did it go wrong?"** | `Y` |
| 7 | **Fixed a real input bug** — `T` was double-bound | — |

### The bug worth calling out

`T` was **double-bound**: it is `ObjectInteraction.resetHeldPoseKey`, so binding "type a question"
to it would have snapped whatever you were holding back to its default pose every time you asked
something. Same class of bug Task 7 found with `V`. The binding is now **Enter**.

### Design notes worth recording

**Why the molecular view is rendered live instead of two more MP4s.** An MP4 is a fixed asset that
has to be produced, imported and kept in sync with the chemistry. A live stage can be checked by
tests — and is: 167 assertions cover atom conservation, bond lengths, camera framing, NaN, and
whether each reaction's electron transfer matches its actual redox status. R2, R3 and R6 show *no*
electron transfer, because they are not redox reactions, and R2's caption says so explicitly.

**Why the stage sits 8000 units below the lab.** Its camera has a 60-unit far plane, so the
laboratory is simply not in the frustum. That is what avoids adding a culling layer, which would
have meant editing `TagManager.asset` — a project-wide change for one feature.

**Why typing needed a global gate.** The cursor is locked for the crosshair, so a uGUI InputField
cannot be focused, so keystrokes come from `Input.inputString`. But the lab binds nearly every
letter — typing "why did the sodium fail" would walk the player across the room and open three
panels. `LabTextInput.IsCapturing` is one flag that fifteen scripts check. Two deliberate
exceptions: `LabHudController` gates only its key read (its toast and measurement strip are display
work and freezing them would look like a hang), and **typing is refused mid-pour**, because gating
object interaction also freezes the tilt that controls pouring.

**Why the offline brain is a lookup, not a model.** It reads the live `FreeHandReactionEngine`
measurements, Task 4's thermodynamics and Task 2's history. Within these eight reactions that makes
it *more* accurate than a cloud model, not less — it can say "you poured 27.4 ml where the equation
needs 20.0, a 37% overdose" because it is reading the actual number.

**Why offline voice shells out to PowerShell.** Unity compiles against `netstandard2.1`, which does
not contain `System.Speech`, so the type cannot be referenced at compile time on any backend. The
reply text is piped through **stdin**, not embedded in `-Command`: an assistant reply contains
quotes, semicolons and newlines, and interpolating that would be both fragile and a
command-injection hole.

## 20. New controls

| Key | Does |
|---|---|
| **Enter** | Type a question to the lab assistant (Enter sends, Esc cancels) |
| **Y** | Ask why the last experiment went wrong |
| **3D VIEW** | Switch to the live molecular animation |
| **ASK AI** | Ask about the molecular step currently on screen |

---

# Task 9 — Coin Economy, Exam Records and a Downloadable Test Report

**Date:** 2026-09-08
**Status:** ✅ Built — 0 compile errors, 0 new warnings, **226/226 checks pass** running the real code
**Full report:** [`TASK9_COIN_ECONOMY_AND_EXAM_REPORT.md`](TASK9_COIN_ECONOMY_AND_EXAM_REPORT.md)

## 21. Executive Summary

| Gap | What was actually wrong |
|---|---|
| **The coin score reset every run** | `CountdownTimer` held it in a plain `int score` that `Start()` set to 0. The testing scene reloads on every "Run the test again", so nothing ever carried. |
| **Half the coin tiers were unreachable** | `if (currentTime >= 30) … else if (currentTime >= 30)` — the second test duplicates the first, so the 50-point tier was dead code and anything over 30 s left scored the maximum. |
| **The report card could not see theory questions** | `TheoreticalTasksManager` recorded nothing. A theory-only run — a real setting — ended with "No graded experiments were recorded in this run" after ten answered questions. |
| **Tasks that timed out vanished** | Nothing filed them, so the report card could say "passed 4 of 4" on a run of six tasks. |
| **No exam report, and the existing export was unfindable** | `LabReportExporter` writes the whole history to `AppData\LocalLow\…`. |

Four new files, 1708 lines. All additive, built from code at runtime — **no scene, prefab or
ProjectSettings asset touched**, same as Tasks 1–8.

| # | Feature | How you reach it |
|---|---|---|
| 1 | **Coins persist** — lifetime totals, best run, best streak, first clears | automatic |
| 2 | **Ranks** — Apprentice → Lab Technician → Chemist → Senior Chemist → Lab Master | report card, pause menu |
| 3 | **Spend coins during a test** — hint 40, +30 s 60, skip 100 | `F2` / `F3` / `F4` |
| 4 | **Bonuses** — first clear +25, streak x3 +30, streak x5 +60, unaided perfect run +50 | automatic |
| 5 | **Every task recorded** — practical, theory, skipped and timed-out | automatic |
| 6 | **Downloadable exam report** — styled HTML + CSV, written to your Desktop | report card |
| 7 | **Open folder** button | report card |

### Design notes worth recording

**Why the paid hint gives the procedure and not the quantity.** The testing scene exists to measure
whether the student knows the right amounts — `engine.hideTargets` is on for exactly that reason.
A hint that named a quantity would be selling the answer, so `ChemistryKnowledgeBase.ProcedureHint`
returns what to do and in what order and leaves the judgement alone. There is a test asserting no
hint contains "ml", "gram" or a bare " g ".

**Why the award maths was split into a pure function.** `ExamSession.ComputeAward` has no
PlayerPrefs in it so the tiers and bonus thresholds can be exercised directly. That is not
incidental: the original tier table shipped with an unreachable branch precisely because nothing
could ever run it. The suite now pins every tier boundary on both sides, asserts all seven tiers
are reachable, and asserts the curve is monotonic.

**Why the report goes to the Desktop.** "Download" has to produce a file the student can find.
Desktop first, then Documents, then persistentDataPath — and each candidate is probed for
*writability*, not just existence, because a lab machine can have a redirected or read-only
Desktop and the alternative is failing at the last step with an error nobody can act on.

**Why the HTML is escaped.** Failure reasons and answer text contain `<`, `>` and `&` in chemical
notation, and one unescaped `<` silently swallows the rest of a table cell.

## 22. New controls

| Key | Does |
|---|---|
| **F2** | Buy a hint — 40 coins (practical experiments only) |
| **F3** | Buy 30 more seconds — 60 coins |
| **F4** | Skip the current task — 100 coins |

---

# Task 10 — Game Feel, Audio Feedback and Presentation

**Date:** 2026-09-09
**Status:** ✅ Built — 0 errors, 68 warnings (identical to baseline; no new warnings)

Full report: `TASK10_GAME_FEEL_AUDIO_AND_PRESENTATION.md`

## 23. Executive summary

The audit verified Tasks 1–9 against the implementation and found the chemistry in good shape. The
gap was the presentation layer, which had never been built: the simulation knew things the game was
not communicating. Picking up a beaker was silent, a locked piece of equipment gave no feedback at
all, a scene change froze the window, and succeeding at an experiment changed one word in a HUD
strip from blue to green.

Three systems the project already paid for were unused: `com.unity.postprocessing` was a dependency
with no volume in any scene; there was no reflection probe, so a windowless laboratory's glassware
mirrored a procedural blue sky; and there were no camera effects at all.

Ten new scripts, no new binary assets, **no `.unity` or `.prefab` file touched**.

| Area | Added |
|---|---|
| Audio | 12 synthesised interaction cues, laboratory room tone, menu pad, pour loops, three volume buses |
| Camera | Walk bob, sprint field-of-view kick, screen shake, verdict hit-stop — all scaling to zero |
| Visuals | Post-processing (bloom, ACES grade, vignette, AO), SMAA/FXAA, a room reflection probe, emission-based object highlighting |
| Flow | Async scene loading behind a fade with tip cards, first-run onboarding, a live objective line |
| Settings | Music/SFX/ambience buses, camera shake, head bob, graphics quality, visual effects, fullscreen, frame cap — laid out in two columns |
| Feedback | Verdict sting + shake + edge flash, audible coin awards, and a reason given when equipment is locked |

## 24. Defects fixed

1. `RemoveHighlight` could stamp a stale colour onto a material a reaction script had swapped
   mid-experiment.
2. The highlight overwrote albedo outright, flattening every material to the same yellow.
3. `MainMenu.Start()` dereferenced `settingsPanel` unguarded — one lost reference killed every
   menu button, because the throw skipped `InitializeToggles`.
4. Held-object and UI smoothing were frame-rate dependent (`Lerp(a, b, dt * k)`).
5. Locked equipment gave no feedback of any kind, which is indistinguishable from a missed click.
6. Synchronous scene loads froze the window long enough for Windows to offer to close it.
7. *(prevented)* A runtime-added `PostProcessLayer` null-references every rendered frame unless it
   is handed its resources asset explicitly.
8. *(prevented)* Both audio beds had a 4 ms hole at the loop point — the anti-click guard fade was
   undoing the seamless-loop construction. Caught by measurement, not review.
9. *(prevented)* Head bob applied in `LateUpdate` would have fought `LabBoundary`'s position clamp.

## 25. Deliberately not changed

- **`productName` / `companyName`.** The window title says "UnityLab". Renaming discards every
  player's PlayerPrefs and `persistentDataPath` on Windows; it needs a migration, not an edit.
- **Ambient lighting mode.** Strong evidence of a latent bug (interior trilight colours authored
  but unused because the mode is Skybox), but changing it alters room brightness and wants an
  eyes-on pass.

## 26. Verification

Compiled against the real Unity 6000.3.7f1 assemblies after every change: **0 errors, 68 warnings,
zero new**. Every remaining warning is pre-existing `CS0649`/`CS0414` on scene-assigned fields in
the original reaction scripts; none is in a file this task created or touched.

The audio synthesis was additionally extracted verbatim and exercised outside Unity: **20/20 checks
pass**, covering cue level, silent endpoints, and loop-seam continuity measured against the mean
sample step inside each clip.

A Play-mode pass was not possible — the Unity Editor holds the project lock.


## 27. Round 2 — playtest fixes

Four issues reported from a play session. All four were mine.

| Report | Cause | Fix |
|---|---|---|
| Objects inside the table, and sometimes falling below it | The settler cast downward from `bounds.center`. On an object slightly sunk into the bench that ray starts *below* the bench top, misses it, and hits the **floor** - so releasing a slightly-embedded beaker teleported it to the floor. A negative gap was rejected outright, so an embedded object could never be rescued. Separately, held objects were positioned with no collision sweep at all, so walking into the bench pushed them through it | Cast from `bounds.max.y` and take the **highest** surface, not the nearest; a negative gap now lifts the object out, capped, and only off static geometry. Held objects now sphere-cast and stop short of anything solid |
| Walking motion caused nausea | Head bob, plus a sprint field-of-view kick doing the same thing behind a different switch | Bob ships **off**, ceiling halved, roll cut by two-thirds; the sprint kick moved behind the same control, relabelled **Walking motion** |
| "Colour contrast was perfect before, now it is looking dull" | ACES tonemapping an already-authored image, a shadow lift that removes contrast by definition, and a bloom thresholded at 1.10 that hazed a white-walled room instead of catching the flame | Colour grading and vignette **off entirely** - the pass no longer touches a colour value. Bloom raised to 1.35 and cut to 0.75. Only antialiasing and ambient occlusion remain |
| Water tap sound on every scene load | **(a)** My room tone was low-passed white noise, which is acoustically what running water is. **(b)** A real pre-existing bug: `DesktopBootstrap` made objects grabbable *before* claiming actuators, so the tap handle became grabbable, the settler moved it, and `RotateButton` toggles the water whenever its handle stops moving | Room tone rebuilt as pure mains hum and fan beat, nothing above 200 Hz, measured 840,000x less spectrally flat - and it now ships **off**, with a settings version bump so existing installs receive the new default. Actuators are now claimed before anything is made grabbable; the settler skips controls; `RotateButton` ignores movement for two seconds after load |

Compile unchanged: 0 errors, 68 warnings, zero new. Audio suite now 22 checks, all passing.


## 28. Round 3 — the Bunsen burner

| Report | Cause | Fix |
|---|---|---|
| Burner alight before the player lights it | `LightFire` has the same movement-toggle bug fixed in `RotateButton` in Round 2 and missed here - it lights the flame whenever `burnerSupport` stops moving, and the desktop rig moves objects during startup. The flame is also authored active with `playOnAwake`, and `LightFire.Start()` never runs while ControlReactions has the burner disabled | The logic now lives once in a new `MovementLatch` with a two-second settle-in window, shared by all **four** scripts that had it duplicated - `RotateButton`, `LightFire`, and the sodium and potassium lids, the last two carrying the same latent misfire. The flame is put out in `Awake`, and `LabEffectsInitializer` now sweeps it |
| Burner still standing inside the table | **My Round 2 fix caused this.** `LightFire.burnerSupport` is the burner's own GameObject, so the "never settle a control" rule excluded it - and because `EnsureGrabbable` skips actuators, it had no `ObjectGrabbable` either, dropping it out of the settle scan entirely | The rule is now directional: controls are never *lowered* (a guess, and the movement can actuate them) but are always *lifted* out of solid geometry (never correct, control or not). The settle scan now collects `DesktopInteractable` alongside `ObjectGrabbable` |

Compile unchanged: 0 errors, 68 warnings, zero new. Audio suite 22/22.

### Round 3 follow-up

`LightFire.burnerSupport` resolves to a **child inside the burner prefab**, not the burner root
(GameObject 802329528 is a stripped entry of prefab instance 802329525 in `LabScene.unity`). The
burner root has neither an `ObjectGrabbable` nor a `DesktopInteractable`, so it was never in the
settle scan - which is exactly why every other object settled correctly and the burner did not.

The burner is now scanned as a whole object via `burnerSupport.transform.root`, guarded by
`AddIfNotNested` (no double-settling a parent and its own child), a 1.5 m footprint ceiling, and a
lift-only rule for anything *containing* a control (`isControl` now looks at children as well as
parents). Bounds now ignore inactive geometry, so the hidden flame no longer inflates the burner's
measured height, and the movement latch re-arms in `OnEnable` so equipment revealed later cannot
actuate itself when placed.

## 29. Round 4 — the learning video panel went through the table

`ReactionLearningController.PositionUiInFrontOfCamera` placed the panel 1.5 m along
`camera.forward` with the camera's full rotation. The video opens when an experiment succeeds, which
is exactly when the student is looking down at the bench - so the panel was put inside the table,
tilted face-up, and nothing ever moved it again.

- The panel now uses only the horizontal part of the view direction, so it opens upright at eye
  level whatever the pitch. Looking straight down falls back to the top edge of the view.
- A forward raycast stops it short of a wall; a downward one keeps its bottom edge above the bench
  for a player who has flown down low with Ctrl.
- **O** brings the panel back in front of the player at any time while it is open. `O` was
  checked to be unbound everywhere else in the project.

Only this method, one `Update` branch and one line of `ControlsHelpUI` changed. Line endings
preserved (CRLF). Compile unchanged: 0 errors, 68 warnings.

## 30. Round 5 — elastic held objects, and the assistant inside a table

**Held objects moved elastically.** `ObjectInteraction.MoveHeldObject` lerped the object in world
space towards a point in front of the camera. That point moves with the camera, so whenever the
player started moving, stopped or turned, the object fell behind and sprang back. It also ran in
`Update` in an undefined order relative to `FirstPersonController`, so it was often chasing the
camera's previous-frame pose.

The object is now carried rigidly by the camera and only its camera-relative offset is smoothed:
walking and turning have no lag, while the pickup glide and the pull-in short of the bench still
ease. Positioning moved to `LateUpdate`, and `[DefaultExecutionOrder(100)]` puts it after both the
controller's movement and `LabBoundary`'s clamp. Rotation behaviour is unchanged.

**The assistant stood inside a table when entering from the main menu.** `InLabAssistantController`
spawns the character on `activeSceneChanged`, which fires *before* `sceneLoaded` - so on that route
the spot was chosen before `DesktopBootstrap` had reset the camera's XR offset and rotation, and
before the room's floor collider existed. Played directly, `Start` ran after the rig was built,
which is why only the menu route went wrong. The placement also never checked the spot was free,
and measured the floor at a different point from where the model was then stood.

`LabAssistantCharacter` now hides the model until the rig and boundary are ready, then picks the
first of 25 candidate spots (authored spot first) where a body-sized capsule touches nothing, the
spot is inside the room, and the player can see the assistant's head. If none qualifies it falls
back to the original spot. The floor is measured directly beneath the player.

Compile unchanged: 0 errors, 68 warnings. `LabAssistantCharacter.cs` keeps its CRLF endings.

## 31. Round 6 — two regressions from Round 5

**Balloon and tube moving at different speeds.** Round 5 moved held-object positioning into a
`LateUpdate` at execution order +100. `CaCO3Reaction` (and `CaCO3ReactionTest`) snap the balloon onto
the tube's socket in their own `LateUpdate` at the default order 0 - so every frame the balloon
snapped to where the tube *had been*, and then the tube moved. Positioning is back in `Update`, now
at -50, with `FirstPersonController` pinned to -100. The frame runs: `CameraJuice` restores the
camera (-200), the player moves (-100), the held object is placed (-50), then every follower at the
default order sees its current pose. The rigid camera-relative carry from Round 5 is unchanged, so
the elastic motion stays fixed.

**Assistant standing in mid-air.** Round 5 measured the floor with a ray straight down from the
player's eyes, on the assumption that nothing lies between a player's head and the floor. In this rig
that is false: the disabled XR rig's hand controllers stay parked beneath the camera at spawn (the
desktop controller moves the camera, not the rig), and the camera is lifted 0.35 m on start. The ray
found those first. *(Correction, Round 7: the hand models carry no colliders - their only
added component is `HandAnimationController` - so they were not what the ray hit. The fix
below does not depend on the cause.)* The floor is now probed *at each candidate spot*, starting 25 cm above the
player's feet (the bottom of their CharacterController) - below every table top, and far below the
parked hands - so a table on the spot is passed under and then rejected by the clearance capsule.

Compile unchanged: 0 errors, 68 warnings.

## 32. Round 7 — the player started low and rose on the first step

The start height was a fixed guess: the XR rig's authored height (y = 1.91 in all three labs) plus
`desktopStartHeightOffset` (0.35). The collision capsule's bottom is always `eyeHeight` (1.75) below
the eyes, and nothing checked where the floor actually was - so the capsule began partly inside the
floor. A `CharacterController` does not push itself out while standing still, only when given real
movement, so the player started a little low and rose suddenly on the first step.

`FirstPersonController` now rests the capsule on the floor before any movement: it probes straight
down from the eyes for the nearest upward-facing surface that is not the player, and lifts the
player so the capsule bottom sits at `floor + skinWidth` - the height walking would have produced.
The lab has no floor collider of its own (the floor is `LabBoundary`'s slab, built during scene
setup), so it retries each frame for up to 1.5 s. It only ever lifts, by at most 1 m, and calls
`Physics.SyncTransforms()` because auto-sync is off in this project.

Checked along the way: nothing under the XR rig has a collider, so nothing else can be inside the
capsule at spawn.

Compile unchanged: 0 errors, 68 warnings.
