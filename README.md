# Reclamation

A factory-building and base-defense game built in Unity, designed from the ground up
for high simulation throughput — targeting smooth gameplay with hundreds of concurrent
factory machines, logistics belts, power networks, and enemy units.

---

## Technical Overview

The core design challenge: factory games become unplayable at scale when simulation
logic is naively coupled to Unity's update loop. Reclamation separates simulation from
rendering entirely and only ticks what actually needs to change each frame.

### Simulation / View Separation

The simulation runs as **pure C# with zero Unity dependencies**. No `MonoBehaviour`,
no `Vector3`, no `UnityEngine` imports. This means:

- Simulation logic is independently testable without spinning up a Unity scene
- The view layer (MonoBehaviours, renderers, colliders) only reads simulation state —
  it never writes to it
- Future headless simulation and multiplayer authority become straightforward

### Fixed-Timestep Tick at 20 Hz

All time-evolving simulation runs through an accumulator pattern:

```csharp
_accumulator += Time.deltaTime;
while (_accumulator >= TickRate)
{
    _simulation.Update();   // pure C#, no Unity
    _accumulator -= TickRate;
}
```

Simulation rate is fully decoupled from render rate. Heavy factory loads don't cause
cascading frame drops — the simulation catches up in fixed increments.

### Only Ticking What Changes

Most game objects do not need to be evaluated every tick. Reclamation divides all
systems into two categories:

| System | Update strategy | Reason |
|---|---|---|
| `PowerSystem` | Ticked at 20 Hz | Battery charge changes continuously |
| `LogisticsSystem` | Ticked at 20 Hz | Items advance along belts every tick |
| `MachineSystem` | Ticked at 20 Hz | Production cycle progress advances every tick |
| `ConstructSystem` | Event-driven | Flood-fill runs once on block place/remove, then stops |
| Structural blocks | Never ticked | Spatial data — walls, foundations, planks don't change |

A construct with 500 structural blocks costs nothing per tick. Only active machines,
generators, and belt segments burn tick budget.

### Registration Pattern — No Per-Tick Scans

Functional blocks (generators, machines, belts) call `System.Register(block)` the
moment they're placed and `System.Unregister(block)` when removed. The 20 Hz tick
loops over only the registered set — never the full block list.

### O(1) Data Access

Block and construct lookups use dictionary tables, not lists:

```csharp
public class BlockTable {
    public Dictionary<int, Block>        ById;          // primary lookup
    public Dictionary<int, List<int>>    ByConstruct;   // fast per-construct iteration
    public Dictionary<int, List<int>>    ByPowerNetwork;
}
```

Before this structure, connectivity and power checks used `List<Block>.Find()` —
O(n) per lookup, which compounds badly across hundreds of blocks. Every hot path is
now O(1).

### Integer Grid — No Float Drift

Block positions are stored as integer `GridPos(x, y, z)` local to each construct,
not as world-space floats. Adjacency checks are exact integer range comparisons:

```csharp
bool touchX = a.GridPosition.X + a.Definition.SizeX == b.GridPosition.X
           || b.GridPosition.X + b.Definition.SizeX == a.GridPosition.X;
```

Float-based adjacency required epsilon tolerances and still produced incorrect
connectivity at scale. Integer adjacency is exact and branchless.

### Construct Connectivity — Flood-Fill on Events

When a block is placed or removed, a flood-fill determines connected components once.
If a removal splits a construct, new constructs are created for each disconnected
island. This runs exactly once per placement event — not every tick — regardless of
how many blocks are in the construct.

---

## Game Systems (In Progress)

- **Building system** — free placement on terrain, integer grid snapping to existing
  blocks, multi-size blocks, 90° rotation
- **Power network** — auto-wiring within pole range, battery buffering, deficit
  throttling with turret priority
- **Logistics** — conveyor belts, inserters, per-machine item buffers
- **Machine production** — recipe-driven cycles, operating rate, input/output buffers
- **Blueprints** — capture, save, and replay block layouts
- **Combat** — turret defense, enemy units (droids, vehicles)

---

## Architecture at a Glance

```
Assets/
├── Simulation/          # Pure C# — no Unity dependency
│   ├── Simulation.cs    # Tick loop, PlaceBlock, RemoveBlock, flood-fill
│   ├── Block.cs         # Placed block instance (GridPos, state, membership)
│   ├── Construct.cs     # Group of connected blocks (one physics body)
│   ├── Tables/          # O(1) data stores (BlockTable, ConstructTable, ...)
│   ├── Systems/         # PowerSystem, MachineSystem, LogisticsSystem
│   ├── Params/          # Per-block-type configuration (GeneratorParams, ...)
│   ├── State/           # Runtime state (MachineState, BatteryState, ...)
│   └── Support/         # GridPos, ItemStack, Recipe, ItemBuffer
└── View/                # Unity MonoBehaviours — reads sim state, drives Unity
    ├── GameManager.cs   # Tick driver, view spawning
    └── CameraController.cs
```

---

## Stack

- **Engine**: Unity (C#)
- **Simulation**: Pure C#, no Unity dependency
- **Language**: C# 9
```

---

The README leads with the *why* (factory games fall apart at scale), then shows the *how* with enough code to be credible in a resume context. Adjust the "In Progress" section as systems get built out.
