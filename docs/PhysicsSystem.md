# Physics System

Space Engineers-style construct physics: structures are static when grounded, dynamic when cut loose.

## Core concept

A **construct** (group of connected blocks) is **anchored** if any block in it was placed directly on terrain. Anchored constructs are static Unity colliders — no Rigidbody. When a block removal severs a construct and the resulting piece has no terrain contact, a Rigidbody is added and gravity takes over.

## Data model

| Field | Type | Where | Meaning |
|---|---|---|---|
| `Block.IsOnTerrain` | `bool` | Simulation | True for the first block of every terrain placement |
| `Construct.IsAnchored` | `bool` | Simulation | True if any member block has `IsOnTerrain` |

`IsAnchored` is never stored explicitly — it is recomputed by `Simulation.RecalcAnchor(constructId)` any time blocks are added or removed.

## Lifecycle

### Placement
- **Terrain click** (`BlockPlacer.PlaceOnTerrain`) → `PlaceBlock(..., isOnTerrain: true)` → `RecalcAnchor` → `IsAnchored = true` → no Rigidbody.
- **Block-on-block click** (`BlockPlacer.PlaceOnBlock`) → `isOnTerrain = false`. The construct already has its `IsAnchored` state and it doesn't change.

### Removal / split
`Simulation.RemoveBlock()` flood-fills for connectivity, reassigns disconnected blocks to new constructs, then calls `RecalcAnchor` on the original and every new construct. `BlockDismantler` then calls `ConstructView.ApplyPhysics()` on each, which:
- **Anchored** → destroys Rigidbody if present (static collider).
- **Floating** → adds/updates Rigidbody: mass = sum of `BlockDefinition.Mass` across all member blocks, gravity enabled, `CollisionDetectionMode.Continuous`.

### Save / load
`isOnTerrain` is stored per block in the save file. On load, `PlaceBlock` restores the flag, `RecalcAnchor` runs per block placed, and `ConstructView.ApplyPhysics()` is called once per construct after all its blocks are loaded.

## Rigidbody settings (floating constructs)

| Property | Value |
|---|---|
| `mass` | Sum of `BlockDefinition.Mass` for all blocks |
| `isKinematic` | false |
| `useGravity` | true |
| `collisionDetectionMode` | Continuous |
| `drag` | 0.1 |
| `angularDrag` | 0.5 |

Unity automatically compounds all child BoxColliders (block cubes) into the construct's Rigidbody. No extra collider setup needed.

## Files changed

| File | Change |
|---|---|
| `Simulation/Block.cs` | Added `IsOnTerrain` field |
| `Simulation/Simulation.cs` | `PlaceBlock` gains `isOnTerrain` param; `RecalcAnchor()` added; called from `PlaceBlock` and `RemoveBlock` |
| `View/ConstructView.cs` | Added `ApplyPhysics()` |
| `View/BlockPlacer.cs` | Passes `isOnTerrain: true` for terrain placements |
| `View/BlockDismantler.cs` | Calls `ApplyPhysics()` on original + split constructs; fixed missing rotation on split ConstructViews |
| `View/SaveLoadManager.cs` | `BlockData` gains `isOnTerrain`; `ApplyPhysics()` called after each construct loads |
