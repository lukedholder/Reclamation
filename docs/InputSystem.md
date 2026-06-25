# Input System — Design Specification

All player controls live in one place: the static **`GameInput`** class
(`Assets/View/GameInput.cs`). No other script calls `UnityEngine.Input` directly
(the one exception is `CameraController`, a debug free-fly camera that is not part
of the player control scheme).

This was adapted from a centralized `PlayerInput` pattern in another project, with
three deliberate improvements for Reclamation:

1. **No hard-wiring to a single controller.** `GameInput` exposes semantic signals
   that *any* system reads, rather than calling one controller's methods. "Interact"
   means different things on a machine, a chest, or a seat — each system decides.
2. **A context model, not two action maps.** Reclamation has more than Player/UI:
   gameplay, piloting, a panel, the build menu, paused. `GameInput.Context` is the
   single definition of "what is the player doing," replacing the duplicated
   `MenuManager.IsOpen` / `BuildMenu.IsMenuOpen` / `VehiclePilot.IsPiloting` / cursor
   checks that used to be copy-pasted across components.
3. **Camera/look stays in the controllers**, not in the input layer.

## The context model

```
GameInput.Context (computed each access from existing global state):

  MenuManager.IsOpen            -> Paused      (pause / settings; cursor free)
  BuildMenu.IsMenuOpen          -> BuildMenu   (cursor free)
  VehiclePilot.IsPiloting       -> Piloting    (cursor locked; VehicleController drives)
  Cursor.lockState != Locked    -> Panel       (machine/chest/inventory; cursor free)
  otherwise                     -> Gameplay    (on foot; cursor locked)
```

Components gate with one check instead of several:

```csharp
// before — duplicated in many files:
if (MenuManager.IsOpen) return;
if (BuildMenu.IsMenuOpen) return;
if (Input.GetKeyDown(KeyCode.E)) TryOpen();

// after:
if (GameInput.Context != InputContext.Gameplay) return;   // or the relevant context
if (GameInput.InteractDown) TryOpen();
```

The "Q opens the build menu while seated" class of bug disappears: adding a new
context (e.g. Piloting) is a change in one place, not a hunt across components.

## API

| Signal | Binding | Notes |
|---|---|---|
| `Move` | WASD (Horizontal/Vertical, raw) | `Vector2` |
| `Look` | Mouse X/Y | `Vector2`; consumers apply their own sensitivity |
| `Scroll` | Mouse wheel | hotbar rotation |
| `MousePosition` | cursor screen pos | drag-drop ghost |
| `PrimaryDown` / `SecondaryDown` | LMB / RMB (down) | place/dismantle/wire/belt/drag-cancel |
| `InteractDown` | E | machine/chest open-close |
| `InventoryDown` | I | inventory toggle |
| `BuildMenuDown` | Q | build menu toggle |
| `PilotDown` | F | enter/exit vehicle |
| `PauseDown` | Esc | menu back/pause |
| `JumpDown` | Space (down) | jump on foot |
| `AscendHeld` / `DescendHeld` | Space / LeftCtrl | vehicle up/down |
| `RollLeftHeld` / `RollRightHeld` | Q / E | vehicle roll |
| `SaveDown` / `LoadDown` | F5 / F9 | save / load |
| `HotbarSlotDown` | 1–9, 0 | returns 0–9 or −1 |
| `Context` | — | `InputContext` enum |

## Bindings are rebindable

Keys are static **fields** (e.g. `GameInput.Interact = KeyCode.E`), not constants,
so a future settings/rebinding screen can reassign them at runtime with no other
code change.

## Behaviour change introduced by this refactor

- **On-foot movement and look are now gated to the Gameplay context.** Previously
  the character could still walk (though not look) while a machine/chest/inventory
  panel was open. Now both stop in any non-gameplay context, which is the intended
  behaviour for a build/management game.

## Migration path to Unity's Input System

If rebindable-by-default UI and gamepad support are wanted later, only `GameInput`
changes: it becomes the wrapper around a generated `GameControls` (from an
`.inputactions` asset), keeping the same public properties so the ~15 consuming
components are untouched. `CameraController` (debug free-cam) would be migrated or
removed at that time.

## Source map

| File | Role |
|---|---|
| `Assets/View/GameInput.cs` | All bindings, semantic signals, `InputContext` |
| `PlayerController`, `VehicleController` | read `Move`/`Look`, apply sensitivity |
| `Hotbar`, `BlockPlacer`, `BlockDismantler`, `WireConnector`, `BeltConnector` | gameplay-context actions |
| `MachineInteractor`, `ChestInteractor`, `PlayerInventory`, `DragDropController` | panel actions |
| `BuildMenu`, `MenuManager`, `SaveLoadManager`, `VehiclePilot` | menu / global actions |
| `CameraController` | **excluded** — debug free-fly camera |
