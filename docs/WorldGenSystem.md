# World Generation & Chunk Streaming — Design

Voxel, deformable, on a spherical planet, streamed around the player. This supersedes
the flat-2D world-streaming model in the SDD (`clean_SDD.txt` §7) and folds in the prior
`tempSpaceCrusade` cube-sphere planet work.

## Decisions (locked)

- **Voxel, smooth (Space Engineers feel).** Terrain is a signed density field meshed as a
  smooth isosurface. Mesher = **Naive Surface Nets** (dual-contouring-lite), chosen over
  Marching Cubes for the SE-like smoothness and because it avoids the error-prone 256-row
  MC tables. Swappable behind `SurfaceNets`.
- **Shell, not full-depth.** Only a band around the surface is voxel data: diggable rock
  to `ShellDepth` below the local surface (then a solid floor), open air above. Generous
  enough for terrain variation and **oceans** (surface dips below `SeaLevel`), with depth
  reserved for underground enemy-nest **tunnels** — caves are **deferred** (added/polished
  later).
- **Spherical planet, player-centric frame (M3).** Authoritative data lives in **planet
  space**; Unity space is a per-frame rotation+translation that keeps the player near the
  origin, upright (+Y). This is the Unity-native equivalent of SE's doubles + Havok physics
  clusters (Unity Transforms are float-only, so we can't copy SE literally). Per-object
  radial gravity; physics only near the player.
- **Determinism.** World = pure function of `(seed, position)` + saved edits. Noise is a
  pure-C# `PerlinNoise` (the prototype used `Mathf.PerlinNoise`, which is 2D-only and
  platform-dependent). Save = seed + voxel edit deltas + constructs.

## Reconciliation with prior material

| Prior | Verdict |
|---|---|
| SDD §7 flat 2D chunks (`Vector2Int`, 16 m, SIM/VIEW radii) | **Superseded** for addressing (sphere needs `(face,u,v,h)`); **kept**: 32-cell/16 m chunk, dual-radius streaming, "only loaded chunks simulate", save/evict. |
| Prototype noise (`PlanetShapeSettings` layers, `NoiseUtil.FBM`) | **Reused** — it's the density source. Ported to pure-C# `PerlinNoise.Fbm`. |
| Prototype `CubeSphere` math | **Reused** at M3 for chunk addressing. |
| Prototype cube-sphere **surface mesh** (`PlanetVisualizer`) | **Repurposed** as the cheap far-LOD shell behind near voxels. |
| `PlanetSettings` (seed, radius≈2 km, amplitude, sea level) | **Kept** as the planet config (→ a view-layer SO at M3, like our other SO→C# bridges). |

The key insight that makes these fit: the voxel **density is the heightmap read as a
field** — `density(p) = (surfaceHeight − distanceAlong) `, so the planet shape is identical
to the prototype's, just diggable. At M3 the flat height swaps for the radial one with no
change to the mesher.

## Milestones

- **M1 (done)** — single voxel region, Surface Nets mesh + collider, dig/add, from seeded
  noise; shell with ocean basins (caves deferred). Flat framing (sphere swapped in at M3).
- **M2 (done)** — streaming around a moving origin: XZ **column chunks** (32×N×32, full
  shell height — flat framing), view radius + unload hysteresis, GameObject pooling,
  per-frame build budget, dig/add rebuilds only affected chunks. Seam-free via
  **world-space vertices** (neighbours emit bit-identical coincident boundary geometry —
  no gaps, no z-fighting; single-ownership stitching is a later optimisation). The SDD's
  larger **simulation** radius and 3D cube-sphere chunks arrive with M3/M4.
- **M3** — planet frame: cube-sphere addressing + player-centric planet→Unity transform +
  radial gravity. Far-LOD via the cube-sphere surface mesh.
- **M4** — persistence (edit deltas), integrate constructs/ore/anchoring with voxel terrain.

## Performance

- **Heightmap caching (done).** `VoxelWorld.SampleDensity` evaluates the surface FBM once
  per XZ column, not per voxel (~90× fewer noise evals per chunk). This is the bulk of the
  cost and removes most of the streaming hitch.
- **Threading (next lever).** Density + Surface Nets + normals are pure C#/struct math and
  run off the main thread; only the `Mesh`/`MeshCollider` apply is main-thread (collider
  pre-bakeable via `Physics.BakeMesh`). The one hazard is reading `VoxelWorld._edits` while
  digging mutates it — snapshot per job. Cheap interim alternative: time-budget the build
  loop instead of a fixed `maxBuildsPerFrame`.
- **Burst + Jobs (planet scale, M3+).** The project already has burst/collections/
  mathematics. Porting the noise + mesher to Burst `IJob`s over `NativeArray` (+ `Mesh.MeshData`)
  is the endgame for a full planet — needs the voxel code in blittable form (no
  `Dictionary`/`List`/delegates; `NativeHashMap` for edits).

## M1 code map

| File | Layer | Role |
|---|---|---|
| `Assets/Simulation/World/PerlinNoise.cs` | Sim | Deterministic 3D Perlin + FBM (pure C#) |
| `Assets/Simulation/World/VoxelMaterial.cs` | Sim | Per-voxel material enum |
| `Assets/Simulation/World/VoxelWorld.cs` | Sim | Density field: shell, oceans, materials, dig/add edits |
| `Assets/View/World/SurfaceNets.cs` | View | Smooth isosurface mesher (robust winding) |
| `Assets/View/World/VoxelChunkView.cs` | View | Meshes a region → `Mesh` + `MeshCollider` (world-space verts) |
| `Assets/View/World/VoxelWorldDriver.cs` | View | M1 single-region harness: generate + mouse dig/add + sea plane |
| `Assets/View/World/VoxelChunkStreamer.cs` | View | M2: streams column chunks around a target — pool, budget, dig/add |

Density convention: `> 0` solid, `<= 0` air; surface at 0. 1 voxel = 1 cell = 0.5 m (shares
the block lattice). M1 simplifications to revisit: full-region rebuild per edit (M2 does
partial per-chunk), placeholder lit material (vertex-colour materials later), `float`
density (SE-style `sbyte` packing later).

## WorldGenTesting setup

1. On an empty GameObject **at the world origin (0,0,0)**, attach **`VoxelChunkStreamer`**
   (M2 streaming) — or **`VoxelWorldDriver`** for the M1 single-region test. Use one, not
   both. The streamer emits world-space geometry, so keep its GameObject at the origin.
2. Ensure the scene has a **Main Camera** (the stream target + dig aim) and a
   **Directional Light**.
3. Press Play: terrain streams in around the camera. **Move the camera/player to load and
   unload chunks; left-click digs, right-click adds.** Tune seed/amplitude/sea level and
   `viewRadius`/`maxBuildsPerFrame` in the Inspector; component menu **Rebuild World** after
   a seed change. (If terrain looks inside-out, set the material's Render Face to Both.)
