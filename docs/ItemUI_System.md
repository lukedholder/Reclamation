# Item UI System — Design Specification

Satisfactory-style dual-panel item management for the Reclamation factory game.

---

## Architecture Overview

```
Simulation layer (plain C#)          View layer (Unity)
──────────────────────────           ──────────────────
ItemCatalogue                        ItemLibrary (ScriptableObject)
  ResourceItem                         ItemDefinitionSO (per item)
  ComponentItem                        ItemLibraryLoader (MonoBehaviour)
  WeaponItem
  ItemStack                          ItemSlotWidget (plain C# helper)
  ItemBuffer                         DragDropController (MonoBehaviour singleton)
  StorageChestMachine
                                     PlayerInventory  ──┐
                                     MachineInteractor  ├── dual-panel
                                     ChestInteractor  ──┘
```

---

## Key Files

| File | Layer | Purpose |
|------|-------|---------|
| `Assets/Simulation/Items/ItemCatalogue.cs` | Sim | Static item definitions (logic, stats) |
| `Assets/View/ItemDefinitionSO.cs` | View | Per-item icon + UI tint (ScriptableObject) |
| `Assets/View/ItemLibrary.cs` | View | Catalogue of all ItemDefinitionSO assets |
| `Assets/View/ItemLibraryLoader.cs` | View | Registers ItemLibrary at startup (on GameManager) |
| `Assets/View/ItemSlotWidget.cs` | View | Reusable slot widget (background, icon, qty badge, button) |
| `Assets/View/DragDropController.cs` | View | Click-to-hold drag-drop singleton (on GameManager) |
| `Assets/View/PlayerInventory.cs` | View | Player inventory + gear slots |
| `Assets/View/MachineInteractor.cs` | View | Machine config panel (buffer slots + recipe selection) |
| `Assets/View/ChestInteractor.cs` | View | Chest contents panel (5×6 slot grid) |

---

## Dual-Panel Layout

When a machine or chest is opened, both panels appear side by side:

```
  ←380px→           ←660px→
 ┌──────────┐       ┌───────────────┐
 │ Machine  │  20px │  Inventory    │
 │ Panel    │ gap   │  Panel        │
 │ @-340    │       │  @+200        │
 └──────────┘       └───────────────┘
```

- Machine/chest panel: `anchoredPosition.x = -340` (DualLeftX)
- Inventory panel: `anchoredPosition.x = +200` (DualRightX)
- Standalone inventory (I key): `anchoredPosition.x = 0`

---

## Drag-Drop Pattern (Click-to-Hold)

DragDropController manages state:

```
Nothing held:
  Click slot → take() → hold the stack + show ghost

Something held:
  Click slot → place(_held) → returns leftover
    leftover.IsEmpty  → fully placed → clear state
    leftover == _held → fully rejected → cancel if same slot
    else              → partially placed / swap → update ghost
  
  Right-click → cancel → source return callback → item back to origin
  ESC → MenuManager closes panels → ClosePanel() → CancelDrag()
```

### Callback Signatures

```csharp
// Called by each slot on click
DragDropController.Instance?.HandleClick(
    widget,                              // null OK for gear/non-widget slots
    () => TakeFromSlot(),                // Func<ItemStack>
    s  => PlaceIntoSlot(s));             // Func<ItemStack, ItemStack>
```

---

## Machine Panel Sections

Dynamic layout built per machine open (and rebuilt on recipe change):

```
[Title: "Iron Smelter"]
[─────────────────────]
INPUT                     (if InputBuffer.Slots.Count > 0)
[ore slot] [...]
[Smelt Iron ]             recipe / resource selection buttons
[Smelt Copper]
[─────────────────────]
OUTPUT                    (if OutputBuffer.Slots.Count > 0)
[plate slot] [...]
[─────────────────────]
[— Clear Recipe —   ]
```

- Selecting a recipe rebuilds the panel immediately (no close)
- Clicking clear closes the panel

---

## Chest Panel

```
[Storage Chest]
[─────────────]
[████░░░░░] 450 / 2000   capacity bar
[─────────────]
[□][□][□][□][□]           5-column × 6-row slot grid
[□][□][□][□][□]           30 slots max; sorted by qty desc
 ...
[+ 5 more types…]        overflow indicator
```

- Items are sorted by quantity descending each frame
- Slots are pre-allocated (30 widgets, shown/hidden)
- Click slot → takes up to MaxStackSize into cursor
- Place into chest → adds to storage (rejects if full)

---

## Setup Checklist

1. **GameManager GameObject** — add components:
   - `UIRoot` (existing)
   - `ItemLibraryLoader` → assign your `ItemLibrary` asset
   - `DragDropController`

2. **Player GameObject** — already has:
   - `MachineInteractor`
   - `ChestInteractor`
   - `PlayerInventory`
   - `Raycaster`, `Hotbar`

3. **Item Library** — right-click in Project:
   - Create → Reclamation → Item Library → name it `ItemLibrary`
   - For each item: Create → Reclamation → Item Definition → set `Id` to match `ItemCatalogue` (e.g. `iron_ore`)
   - Assign icon sprites and tint colours per item
   - Add all ItemDefinitionSO assets to the library's `Items` array

---

## Item Slot Sizes

| Widget | Size |
|--------|------|
| `ItemSlotWidget.Size` | 52 × 52 px |
| `ItemSlotWidget.SlotGap` | 4 px |
| Gear row height | 40 px |
| Chest grid | 5 cols × 6 rows = 300 px wide |

---

## Cursor State

| Who manages cursor | Condition |
|-------------------|-----------|
| `MachineInteractor` | machine panel open (dual mode) |
| `ChestInteractor` | chest panel open (dual mode) |
| `PlayerInventory` | standalone inventory (I key) |
| `MenuManager` | pause/settings menu |

Inventory never touches cursor in machine/chest mode — the left-panel interactor owns it.

---

## StorageChestMachine API Additions

```csharp
// Take up to qty of itemId. Returns what was actually taken.
public ItemStack TakeFromStorage(string itemId, int qty)

// Add up to qty of itemId. Returns amount actually added.
public int GiveToStorage(string itemId, int qty)
```
