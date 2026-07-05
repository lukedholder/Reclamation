# Input System — Design Specification

All player controls live in one place: the static **`GameInput`** class
(`Assets/View/GameInput.cs`). No other script calls `UnityEngine.Input` directly.

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
`.inputactions` asset), keeping the same public properties so the consuming
components are untouched.

## Planned: Build & Combat modes — the R-key scheme

*Design intent (not yet implemented; combat/weapons don't exist yet). Folded in from the
original controls design doc. Bindings here will be reconciled with the live `GameInput`
table above when weapons land — some differ (e.g. `Q` currently toggles the build menu;
this scheme uses `Q` to deselect and `R` for weapon handling).*

Reclamation has two primary on-foot modes with fast, deliberate transitions — the guiding
principle is **no mode should feel like a trap**: you can always reach for a weapon, and
always get back to building.

**Build Mode (default).** Cursor locked; crosshair drives placement/interaction.

| Input | Action |
|---|---|
| 1–7 | Select block type (ghost shown) |
| 8 / 9 | Wire Tool / Belt Tool (click endpoints; right-click cancels the pending link) |
| Q | Deselect / cancel current slot |
| Scroll | Rotate block (Y-axis, four 90° stops) |
| LMB / RMB | Place / dismantle |
| E | Interact with machine (opens config panel) |
| R (tap) | **Draw last weapon → enter Combat Mode** |

**Combat Mode.** Entered by drawing a weapon; cursor stays locked; raycast targets.

| Input | Action |
|---|---|
| 1 / 2 | Primary / secondary weapon |
| 3–5 | Heavy weapon (rocket, LMG, …) — excluded from the R quick-draw |
| LMB / RMB | Fire / ADS or alt-fire |
| R (tap) | **Reload** |
| R (hold) | **Holster → return to Build Mode** |
| Shift / WASD / Mouse | Sprint / move / aim |

**The R key is the bridge between modes** — one button, three context-sensitive actions:

| Context | Input | Result |
|---|---|---|
| Build | tap R | draw last weapon → Combat |
| Combat | tap R | reload |
| Combat | hold R | holster → Build |

Rationale: short press = offensive (draw/reload), long press = defensive (holster), so
"R = weapon management" holds in both modes. Heavy weapons require deliberate selection
(1–5) and **don't** update the last-weapon memory, keeping the quick-draw fast and light.
Walkthroughs of this in play are in [use-cases](../use-cases.md) (scenarios 1–4).

**Open questions:** Alt+Scroll for multi-axis block rotation; auto-restore the Belt/Wire
tool slot after holstering vs. return to last block slot; tap/hold thresholds (~0.3 s / 0.5 s);
sprint-while-firing (disable vs. accuracy penalty); configuring machines (E) without holstering.

## Source map

| File | Role |
|---|---|
| `Assets/View/GameInput.cs` | All bindings, semantic signals, `InputContext` |
| `PlayerController`, `VehicleController` | read `Move`/`Look`, apply sensitivity |
| `Hotbar`, `BlockPlacer`, `BlockDismantler`, `WireConnector`, `BeltConnector` | gameplay-context actions |
| `MachineInteractor`, `ChestInteractor`, `PlayerInventory`, `DragDropController` | panel actions |
| `BuildMenu`, `MenuManager`, `SaveLoadManager`, `VehiclePilot` | menu / global actions |
