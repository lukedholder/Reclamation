# Building & Vehicles — Design & Status

**Authoritative source of truth** for constructs — the block-grid building system, its
physics/anchoring, and vehicles. Consolidates the prior per-system building/vehicle notes
and supersedes the legacy [`BuildingSystem_UML.md`](../archive/BuildingSystem_UML.md).

Status is marked **(current)** — implemented and in the codebase — or **(planned)** —
designed here, not yet built.

---

## 1. Scope & principles

- **Constructs unify bases and vehicles.** A construct is a group of face-connected blocks.
  A terrain-anchored construct is a base; a released one with a seat + propulsion is a
  vehicle. Same data model, same physics, same building tools.
- **Strict sim/view split.** All logic is pure C# in `Assets/Simulation/` (no `UnityEngine`);
  `Assets/View/` renders it and feeds input. Deterministic integer grid.
- **Cell = 0.5 m** today (`ViewConstants.CellSize`); vehicles will get a finer *per-construct*
  grid (planned, §6C).

---

## 2. Core model (current)

| Type | Key fields |
|---|---|
| `BlockDefinition` (immutable) | Id, Category, FunctionalType, SizeX/Y/Z (cells), Mass, power, cost, Ports, Params |
| `Block` (instance) | Definition, ConstructId, `GridPosition` (integer), `RotationSteps` (0–3, 90° about Y), `IsOnTerrain`, Durability, runtime state |
| `Construct` | `BlockIds`, `Cells` (occupancy map), `IsAnchored`, `ManualUnanchored`, `IsPilotable`, `Type` |

- **Occupancy map** — `Construct.Cells : Dictionary<GridPos,int>` maps every filled cell → its
  block id (a multi-cell block registers one entry per cell). Maintained by `Simulation` on
  place/remove/split/merge. Gives O(1) overlap + adjacency, so connectivity flood-fill is
  O(cells) not O(blocks²).
- **Overlap rejection** — `Simulation.PlaceBlock` returns **null** if the footprint is occupied;
  `Simulation.CanPlace(...)` exposes the same test. *No ghost-validity feedback yet — the click
  just no-ops; wiring `CanPlace` into `GhostBlock` is a follow-up.*
- **Connectivity** — placement joins a block's construct; removal (`RemoveBlock`) flood-fills the
  remainder and splits disconnected components into new constructs (returns the new ids).
- **Unity structure** — one `ConstructView` parent per construct (holds the `Rigidbody` when
  floating); `BlockView` children hold colliders. Unity compounds child colliders automatically.

---

## 3. Placement, rotation & content (current)

- **Placement** — `BlockPlacer`: terrain click (free XZ snap, new construct) vs block-on-block
  (snap to the hit face plane, join that construct). `BlockDismantler`: right-click removes
  (100% material return).
- **Rotation** — 90° about Y only (`RotationSteps` 0–3); odd steps swap the X/Z footprint.
  *(Expanded to 24 orientations in the redesign, §6A.)*
- **Designer blocks** — author blocks in the Editor without code:

  | File | Role |
  |---|---|
  | `BlockDefinitionSO` (view) | Inspector block → `ToDefinition()` pure-C# `BlockDefinition` + a `Prefab` |
  | `BlockLibrary` / `BlockLibraryLoader` | SO array; registered on GameManager `Awake()` |
  | `BlockRegistry` (static) | Merges code `BlockCatalogue.All()` + SO blocks; `All`/`GetById`/`GetPrefab` |
  | `BlockViewFactory` (static) | Spawns the designer prefab for a block id, else a sized primitive cube |

  `BuildMenu` lists `BlockRegistry.All()`; `SaveLoadManager` resolves defs via it. Zero-setup
  safe (no library → code blocks only). **Today each block is its own GameObject** (prefab or
  cube) — this is what the procedural mesher replaces (§6G).

---

## 4. Physics & anchoring (current)

Space Engineers-style: grounded = static, cut loose = dynamic.

- `Block.IsOnTerrain` is set when a block is placed on terrain (terrain click, or a physical
  `Physics.CheckBox` against `BlockPlacer._terrainMask` so a base built into a hillside also
  anchors). `Construct.IsAnchored` is recomputed by `Simulation.RecalcAnchor()`:
  `IsAnchored = !ManualUnanchored && (any member block IsOnTerrain)`.
- `ConstructView.ApplyPhysics()`: **anchored** → no Rigidbody (static compound collider);
  **floating** → Rigidbody (mass = Σ `BlockDefinition.Mass`, gravity on, `Continuous`, drag 0.1,
  angularDrag 0.5). Called after removal/split and per construct on load.
- Save stores `isOnTerrain` and `manualUnanchored` per construct; physics is rebuilt on load.

---

## 5. Vehicles (current: basic prototype)

A vehicle is a released (non-anchored) construct the player pilots.

- **Blocks** — `PilotSeat` (`Seat`) and `Thruster` (`Propulsion`), `BlockCategory.Vehicle`
  (a **Vehicle** tab in `BuildMenu`). Params: `ThrusterParams { ThrustKN, ExhaustDir }`,
  `SeatParams { Facing }`.
- **Classification** — `ConstructSystem.Reclassify()` (ticked from `Simulation.Update()`) sets
  `IsPilotable = hasSeat && hasPropulsion` and `Type`.
- **Release** — `Construct.ManualUnanchored` overrides anchoring; entering a seat releases the
  craft and `ApplyPhysics()` gives it a Rigidbody. Persisted in saves.
- **Pilot** — `VehiclePilot` (on Player): **F** enters/exits a seat, disables PlayerController +
  build/interact input, parents the player to the construct, swaps the camera, draws a hint.
- **Drive** — `VehicleController` (on ConstructView): WASD translate, Space/Ctrl up/down, Q/E
  roll, mouse yaw/pitch as torque; `_hoverAssist` cancels gravity for easy flight.

**Deliberate simplifications (revisit):** thrust is **not power-gated** (thrusters draw no
power; the `OperatingRate` hook is a TODO); **total-thrust** model, not directional summing
(`ExhaustDir`/`Facing` unused); mouse-torque steering (no gyroscope); save stores only Y
rotation (a craft saved mid-flight loses pitch/roll). Setup: add `VehiclePilot` to the Player.

---

## 6. Redesign — Expressive Grid + Parts (planned)

**Why.** The 0.5 m axis-aligned cuboid grid with 90°-only rotation can't express detailed
builds — the driving example is a ~1.5 m sportscar with curves, angled panels, headlights,
mirrors, and **suspension**. Keep the grid for structure; add expressiveness + a parts layer.
Preserve occupancy, connectivity, physics, functional blocks, the designer pipeline, save/load.

Two problems, layered: **body resolution & shape** (finer grid + shaped blocks + full
orientation) and **functional/cosmetic parts** (an attachment layer; wheels/suspension are
articulated — jointed parts, never grid cells).

### A. Full 24-orientation rotation *(foundational)*
Replace `Block.RotationSteps` (0–3) with `Block.Orientation` (0–23, the cube rotations) so
slopes/corners face any way. Generalise the occupancy footprint (AABB of the rotated size box;
a 1×1×1 detail block is always 1 cell). Touches sim footprint, view, placement controls
(add pitch/roll), and save/load.

### B. Shaped blocks (procedural)
Slope, wedge, inner/outer corner, rounded, half-slab, etc. — occupy their cell(s), render a
sub-shape (SE armor model). **Generated procedurally, not prefabs** (§G): each shape is a
geometry template + a 6-face solidity mask, both rotated by (A); blocks carry a material id.
**Split:** structural/armor/shaped blocks are procedural; **functional** blocks (thrusters,
machines, seats, turrets) and **parts** keep authored prefabs. `BlockDefinition` gains a render
mode — `Procedural(shape, material)` or `Prefab`.

### C. Small grid (per-construct cell size)
Add `Construct.CellSize` (0.5 m default; e.g. 0.125 m "small"). Block `GridPos`/size stay
integer cells, so occupancy/connectivity are unchanged — only the world scale differs. View
scales by `construct.CellSize`. A construct is large- or small-grid at creation; true SE mixed
grids (small welded onto large) deferred.

### D. Part layer + surface fixtures
- `PartDefinition` (sim): Id, category (Cosmetic / Light / Wheel / …), mass, params
  (e.g. `WheelParams`), + a view prefab. `Part` (sim): Id, DefId, `HostBlockId`, local pose.
  Parts do **not** occupy cells; they live on the construct and are removed with their host.
- **Wheels/suspension** = a `WheelCollider` (or Rigidbody + `ConfigurableJoint`) on the
  construct's Rigidbody at the part pose; drive/steer/brake from a `VehicleWheelController`.
  This is what makes it an actual car (thruster flight is already done, §5).
- **Functional surface fixtures** — parts placed **anywhere on a block's surface** (off-grid,
  free rotation), e.g. a control panel. Each carries a **collider hitbox**; placement respects
  it via two paths: block↔block uses the integer occupancy map; anything involving a fixture
  uses a physics `OverlapBox`. Fixture overlap is validated in the **view** (physics), not the
  sim — fixtures are detail, not structure. Save/load parts as `(defId, hostRef, localPose)`.

### E. Symmetry tool
A mirror plane across the construct centreline; placing a block/part also places the mirrored
counterpart (mirrored pose + orientation; left slope → right slope). Near-mandatory for cars.

### F. Fine-build tooling (making a small grid usable)
**Decouple grid resolution from block size** — a 10 cm grid means you *can* place at 10 cm, not
that you place 10 cm blocks (most of a body is a few big panels). Plus **drag tools** (line /
face / box, filled/hollow; batched place/remove), **blueprints / copy-paste + stamps**, and a
later **surface brush**.

### G. Block meshing — procedural, face-culled *(perf gate)*
Structural blocks are not per-block prefab cubes. A **construct mesher** (the blocky analog of
the voxel Surface Nets mesher) emits geometry from the occupancy/block data:
- **Hidden-face culling (Minecraft-style):** per block, emit only faces the neighbour cell
  doesn't fully occlude (O(1) via `Construct.Cells`); each shape's 6-face solidity mask
  (rotated by orientation) makes shaped-block occlusion correct.
- **Chunked + incremental:** subdivide a construct into mesh chunks; an edit re-meshes only the
  affected chunk(s). Boundaries need neighbour solidity (as with voxels).
- **Materials → submeshes/atlas;** prerequisite for **greedy meshing** (later): merge coplanar
  same-material faces into large quads (mostly full/slab faces; slopes don't merge).
- **Two hard consequences:** (1) **render mesh ≠ physics collider** — a concave culled mesh
  can't go on a *moving* Rigidbody, so dynamic constructs use **compound box colliders** (per
  block / greedy-merged) while static bases may use a concave MeshCollider; (2) **block picking**
  — with no per-block GameObject, raycast the combined mesh and convert hit → cell → block
  (Minecraft-style), touching `BlockPlacer`/`BlockDismantler`/`BlockInfoHUD`.

---

## 7. Roadmap (planned, dependency-ordered)

1. **24-orientation rotation** (§6A) — sim footprint + view + placement + save.
2. **Construct mesher** (§6G) — face-culled full cubes; render mesh + compound box colliders +
   mesh-based picking. Replaces per-block prefab cubes for structural blocks.
3. **Shaped blocks** (§6B) — slope/corner/slab templates + occlusion masks, on (1)+(2).
4. **Small grid** (§6C) — per-construct cell size (the mesher absorbs the block count).
5. **Drag tools** (§6F) — line/face/box, filled/hollow.
6. **Part layer + surface fixtures** (§6D) — cosmetic/functional, collider hitbox, save.
7. **Wheels + suspension** (§6D) — `WheelCollider` + `VehicleWheelController`, into piloting.
8. **Greedy meshing** (§6G) — coplanar same-material merge.
9. **Blueprints / copy-paste** (§6F).
10. **Symmetry tool** (§6E).

Tracks: **[1→2→3→4]** body & rendering; **[6→7]** function (wheels); **[5, 9, 10]** tooling/UX;
**[8]** perf follow-up. Vehicle **thruster flight is already shipped** (§5); its pending items
(power-gated thrust, directional summing, gyroscope, HUD) fold in alongside.

---

## 8. Status at a glance

| Area | Status |
|---|---|
| Construct/Block model, occupancy map, connectivity split/merge | **current** |
| Placement (terrain / block-on-block), overlap rejection, dismantle | **current** |
| 90° Y rotation | **current** (→ 24-orientation planned) |
| Designer blocks (SO → registry → prefab/cube per block) | **current** (→ procedural mesher planned) |
| Terrain anchoring + grounded/floating physics | **current** |
| Vehicles: seat/thruster, classification, release, pilot, thruster flight | **current** (basic) |
| Power-gated thrust, gyroscope, vehicle HUD | **planned** |
| 24-orientation, procedural face-culled meshing, shaped blocks | **planned** |
| Small grid, drag tools, parts + surface fixtures, wheels/suspension | **planned** |
| Greedy meshing, blueprints, symmetry | **planned** |

---

## 9. Source map

| Area | Files |
|---|---|
| Core model | `Simulation/Block.cs`, `Construct.cs`, `BlockDefinition.cs`, `Support/GridPos.cs`, `Tables/*` |
| Placement/connectivity/occupancy | `Simulation/Simulation.cs`, `Systems/ConstructSystem.cs` |
| Build/place view | `View/BlockPlacer.cs`, `BlockDismantler.cs`, `Hotbar.cs`, `BuildMenu.cs`, `GhostBlock.cs`, `ConstructView.cs`, `BlockView.cs` |
| Designer blocks | `View/BlockDefinitionSO.cs`, `BlockLibrary.cs`, `BlockLibraryLoader.cs`, `BlockRegistry.cs`, `BlockViewFactory.cs` |
| Physics/anchoring | `View/ConstructView.cs`, `BlockPlacer.cs`, `BlockDismantler.cs`, `SaveLoadManager.cs` |
| Vehicles | `Simulation/Params/{Thruster,Seat}Params.cs`, `Systems/ConstructSystem.cs`, `View/VehiclePilot.cs`, `VehicleController.cs` |

---

## Appendix — Foundational decisions (carried forward, still binding)

| Decision | Detail |
|---|---|
| Cell size | 0.5 m; one shared `CellSize` constant (per-construct override planned). |
| Construct = parent GameObject | `ConstructView` parent + optional Rigidbody; blocks are child GameObjects; colliders compound. |
| Positions | Integer `GridPos` in sim; `localPosition = GridPos * CellSize` in view. |
| Simulation isolation | Pure C#, zero `UnityEngine`, no `Vector3` in sim. |
| Connectivity | Face-adjacency (6 dirs) via the occupancy map; event-driven on place/remove, not ticked. |
| Construct split | New parent GameObject; `SetParent(worldPositionStays:true)` per block; cells transferred. |
| Rotation | 90° steps about Y today (→ 24 orientations). Vehicles/freeform continuous is a separate future. |
| Terrain placement | Free-form, no global grid; snapping only against block faces. |
| Dismantle recovery | 100% material return (V1). |
| Save references | Wires/belts key blocks by `(constructIndex, gridPos)` — survives id reassignment. |
