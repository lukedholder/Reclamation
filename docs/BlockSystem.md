# Block System — Occupancy, Designer Blocks, Terrain Anchoring

Three improvements adapted from a parallel grid-construct prototype, fitted to
Reclamation's sim/view split.

---

## 1. Per-construct occupancy map

Each `Construct` now owns a cell index:

```csharp
public readonly Dictionary<GridPos, int> Cells;   // every filled cell → block id
```

A multi-cell block registers one entry per occupied cell (footprint = size with the
X/Z swap an odd rotation applies). Maintained by `Simulation` on place / remove /
split / merge via `RegisterCells` / `UnregisterCells`.

**What it fixes.** `RemoveBlock`'s connectivity flood-fill previously compared every
block against every other with `AreAdjacent` — **O(blocks²)** per dismantle. It now
probes the six neighbours of each occupied cell through the map (`EnqueueAdjacentBlocks`),
so a split is **O(total cells)**. `AreAdjacent` was deleted. This is what the design
doc ([BuildingSystem_UML.md](BuildingSystem_UML.md)) always specified
(*"O(1) via occupancy[gridPos + neighbour] dictionary lookup"*) but the code didn't do.

**Overlap rejection.** `PlaceBlock` now returns **null** if the target footprint is
occupied (or the construct is missing), checked with `AreCellsFree`. `Simulation.CanPlace(...)`
exposes the same test for a future ghost-validity tint. Callers updated:
- `BlockPlacer.TryPlace` bails on null (no GameObject spawned).
- `SaveLoadManager.LoadGame` skips and warns on null.

> Behaviour change: placements that would overlap an existing block are now refused
> (previously they were allowed to visually overlap). There's no ghost feedback yet —
> the place click just no-ops. Wiring `CanPlace` into `GhostBlock` is a follow-up.

---

## 2. Designer-authored blocks (ScriptableObject → C#)

Blocks can now be authored in the Editor with real prefabs, without editing code —
mirroring the existing `ItemDefinitionSO` / `ItemLibrary` pattern and keeping the
simulation Unity-free.

| File | Role |
|---|---|
| `BlockDefinitionSO` (view) | Inspector-authored block; `ToDefinition()` → pure-C# `BlockDefinition` + a `Prefab` |
| `BlockLibrary` (view) | Array of `BlockDefinitionSO`; `RegisterAll()` |
| `BlockLibraryLoader` (view) | MonoBehaviour on GameManager; registers the library in `Awake()` |
| `BlockRegistry` (view, static) | Merges `BlockCatalogue.All()` (code blocks) + SO blocks; `All()`, `GetById`, `GetPrefab` |
| `BlockViewFactory` (view, static) | Spawns the prefab for a block id, else a sized cube placeholder |

Consumers now read the **registry**, so designer blocks are first-class:
- `BuildMenu` lists `BlockRegistry.All()` (appears under its category tab).
- `SaveLoadManager` resolves defs via `BlockRegistry.GetById` (saves/loads SO blocks).
- `BlockPlacer` and `SaveLoadManager` spawn visuals via `BlockViewFactory` — the
  designer prefab if registered, otherwise the same primitive cube as before.

Zero-setup safe: with no `BlockLibrary` assigned, only code blocks exist and the game
behaves exactly as before. `BlockDefinitionSO` is intended for structural/decorative
blocks (`FunctionalType.None`); functional blocks keep their `Params` in code but an
SO with a matching `Id` can still supply their prefab.

**Setup:** Project → right-click → *Reclamation → Block Definition* (one per block) and
*Reclamation → Block Library*; add the definitions to the library; put
`BlockLibraryLoader` on GameManager and assign the library.

---

## 3. Physical terrain anchoring

Previously only the first block of a terrain-click placement was flagged
`IsOnTerrain`. Now any placed block that physically touches terrain anchors its
construct — so a base built sideways into a hillside stays put.

`BlockPlacer` does a `Physics.CheckBox` of the placed block's footprint (plus a small
skin) against a serialized **`_terrainMask`**. On contact it sets `block.IsOnTerrain`,
re-runs `Simulation.RecalcAnchor`, and calls `ConstructView.ApplyPhysics()` (which can
re-ground a previously floating piece).

- **Setup:** assign `_terrainMask` on the Player's `BlockPlacer` to your ground
  layer(s). Left as *Nothing*, behaviour is unchanged (terrain-click anchoring only).
- The check runs only for blocks not already flagged on terrain, so it's a no-op for
  the common terrain-click case.

---

## What was deliberately not adopted from the prototype

- **ScriptableObject as the sim `BlockDefinition`** — would import Unity into the
  simulation. We keep the SO in the view and convert (as above).
- **Float grid coords + free rotation** — conflicts with the deterministic integer
  grid and 90° steps. Kept integer `GridPos`.
- **`localCells` arbitrary shapes / connection-point connectivity** — extra
  bookkeeping for L-shaped blocks and partial-face connections; not needed for boxy
  factory blocks. Face-adjacency via the occupancy map covers current needs.
