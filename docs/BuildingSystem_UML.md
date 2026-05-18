# Building System — UML Class Diagrams

Generated from Space_Crusade design documents (Vol1–5, Requirements v4, SDD).

---

## Architecture Overview

The building system is split into two layers:

**Simulation layer (pure C#, no Unity dependency)**
`Block`, `Construct`, `BlockDefinition`, `PowerNetwork`, tables, state, params.
Contains all game logic. Runs at a fixed 20 Hz tick rate via the accumulator pattern.

**View layer (Unity MonoBehaviours)**
`ConstructView`, `BlockView`, `BuildController`, `GhostBlock`, `GameManager`.
Reads simulation state and drives Unity transforms, renderers, colliders, and Rigidbodies.
Never writes to simulation state directly — calls simulation methods instead.

### Unity Scene Structure

Each construct is **one parent GameObject** (`ConstructView`) with a `Rigidbody`.
Each block is a **child GameObject** (`BlockView`) under its construct's parent.

```
Scene
└── Construct_42  (ConstructView + Rigidbody)
    ├── Block_1   (BlockView + MeshRenderer + BoxCollider)
    ├── Block_2   (BlockView + MeshRenderer + BoxCollider)
    └── Block_3   (BlockView + MeshRenderer + BoxCollider)
```

Unity automatically compounds all child `BoxCollider`s onto the parent `Rigidbody`,
giving each construct a single physics body with no manual compound collider setup.

**Block local position** = `block.GridPos * CellSize` (set once on spawn, or when the
block's GridPos changes). The Transform is the authoritative world position — the
simulation layer holds no world-space coordinates.

**Rigidbody mode:**
- `IsAnchored == true`  → `Rigidbody.isKinematic = true` (fixed base, no physics movement)
- `IsAnchored == false` → `Rigidbody.isKinematic = false` (vehicle, debris — full physics)

**Construct split:** instantiate a new parent GameObject, call
`blockView.transform.SetParent(newParent, worldPositionStays: true)` for each
block in the split component. Unity preserves world positions automatically.

---

## Cell Size

**1 cell = 0.5 m in world units.**
This constant is named `CellSize` and must be defined in exactly one place,
shared between the simulation and view layers. A 1×1×1 block is 0.5×0.5×0.5 m.
A 2×2×2 block is 1×1×1 m.

---

## 1 — Core Data Model (Simulation Layer)

```mermaid
classDiagram
    class BlockDefinition {
        +string Id
        +string DisplayName
        +BlockCategory Category
        +FunctionalType FunctionalType
        +int TierRequired
        +int SizeX, SizeY, SizeZ
        +int MaxDurability
        +float Mass
        +float PowerDrawKW
        +float PowerOutputKW
        +PowerInterface PowerInterface
        +ItemStack[] ConstructionCost
        +IFunctionalParams Params
    }

    class Block {
        +int Id
        +BlockDefinition Definition
        +int ConstructId
        +int PowerNetworkId
        +int LogisticsNetworkId
        +GridPos GridPosition
        +int RotationSteps
        +int Durability
        +MachineState MachineState
        +GeneratorState GeneratorState
        +BatteryState BatteryState
    }

    class Construct {
        +int Id
        +string Name
        +ConstructType Type
        +bool IsAnchored
        +bool IsPilotable
        +List~int~ BlockIds
        +List~int~ PowerNetworkIds
    }

    class GridPos {
        +int X, Y, Z
        +GridPos[] Neighbours
    }

    class BlockCategory {
        <<enumeration>>
        Structural / Production / Storage
        Power / Logistics / FluidLogistics
        Defense / Vehicle / Orbital
    }

    class FunctionalType {
        <<enumeration>>
        None / Foundation / Miner
        Assembler / Storage / Generator
        Battery / Pole / Turret / Seat
        Propulsion / Pump / Pipe
        DockingPort / Fabricator
    }

    class ConstructType {
        <<enumeration>>
        Structure / Base / Outpost
        Vehicle / Droid / OrbitalPlatform
    }

    BlockDefinition --> BlockCategory
    BlockDefinition --> FunctionalType
    Block --> BlockDefinition : type
    Block --> GridPos : position
    Block "*" --> "1" Construct : belongs to
    Construct --> ConstructType
```

---

## 2 — View Layer (Unity MonoBehaviours)

```mermaid
classDiagram
    class ConstructView {
        <<MonoBehaviour>>
        +int ConstructId
        +Rigidbody Rigidbody
        +SyncFromSimulation(Construct)
        +SetKinematic(bool isAnchored)
    }

    class BlockView {
        <<MonoBehaviour>>
        +int BlockId
        +MeshRenderer Renderer
        +BoxCollider Collider
        +SyncFromSimulation(Block, float cellSize)
    }

    class GameManager {
        <<MonoBehaviour>>
        -Simulation _simulation
        -BuildController _build
        -CameraController _camera
        -Dictionary~int_ConstructView~ _constructViews
        -Dictionary~int_BlockView~ _blockViews
        +Update()
        -SpawnConstruct(Construct) ConstructView
        -SpawnBlock(Block, ConstructView) BlockView
        -DestroyBlock(int blockId)
    }

    class BuildController {
        -BlockDefinition _selectedDef
        -GhostBlock _ghost
        -BuildMode _mode
        -int _rotationSteps
        +Update()
        -GetPlacementTarget() PlacementResult
        -ValidatePlacement(PlacementResult) ValidationResult
        -ConfirmPlacement()
        -ConfirmRemoval()
        -ConfirmRepair()
    }

    class GhostBlock {
        -GameObject _go
        -Material _validMaterial
        -Material _invalidMaterial
        +Show(Vector3 localPos, int rotSteps, BlockDefinition def, bool isValid)
        +Hide()
    }

    class CameraController {
        <<MonoBehaviour>>
        -float _yaw
        -float _pitch
        +Update()
    }

    GameManager --> BuildController
    GameManager --> CameraController
    GameManager "1" *-- "*" ConstructView : spawns and owns
    ConstructView "1" *-- "*" BlockView : parent transform
    BuildController --> GhostBlock
```

**Key rules:**
- `ConstructView` is the parent GameObject. Its `transform.position` is the construct's world origin.
- `BlockView` is a child of `ConstructView`. Its `transform.localPosition = block.GridPos * CellSize`.
- `Rigidbody` lives only on `ConstructView`. Child `BoxCollider`s are compounded onto it automatically.
- When GameManager receives a `ConstructSplitEvent`, it instantiates a new `ConstructView` and calls `SetParent(newParent, worldPositionStays: true)` on each affected `BlockView`.

---

## 3 — Simulation Architecture

### What gets ticked vs. what is event-driven

**Blocks are spatial data, not simulation actors.**
A wall, a foundation, a plank — nothing about a structural block changes between ticks.
Blocks are placed and removed as events; only systems with time-evolving state are ticked.

**Construct connectivity is event-driven, not ticked.**
The flood-fill connectivity check runs *once* when a block is placed or removed,
updates construct membership, then stops. There is no reason to recheck every tick.

**Only three systems are ticked at 20 Hz:**

| System | Why it ticks |
|--------|-------------|
| `PowerSystem` | Battery charge changes every tick; supply/demand balance must be recalculated |
| `LogisticsSystem` | Items physically advance along belts every tick |
| `MachineSystem` | Production cycle progress advances every tick |

**Functional blocks register with systems at placement and unregister at removal.**
A generator block placed on a construct calls `PowerSystem.Register(block)` — it does not
get processed by the simulation loop itself. The simulation loop only processes networks
and machine states, not individual blocks.

```mermaid
classDiagram
    class Simulation {
        +int Tick
        +BlockTable Blocks
        +ConstructTable Constructs
        +PowerNetworkTable PowerNetworks
        +LogisticsNetworkTable LogisticsNetworks
        +EventBus Events
        +Tick()
        +PlaceBlock(def, construct, gridPos, rot) Block
        +RemoveBlock(blockId)
    }

    class ConstructSystem {
        <<event-driven, not ticked>>
        +OnBlockPlaced(Block)
        +OnBlockRemoved(Block)
        -FloodFill(startId) HashSet~int~
        -MergeConstructs(int[] ids)
        -SplitConstruct(components)
    }

    class PowerSystem {
        <<ticked 20Hz>>
        +Register(Block)
        +Unregister(Block)
        +Tick(PowerNetworkTable)
        -BalanceNetwork(PowerNetwork)
        -ChargeBatteries(surplus, batteries)
        -ThrottleConsumers(deficit, consumers)
    }

    class MachineSystem {
        <<ticked 20Hz>>
        +Register(Block)
        +Unregister(Block)
        +Tick(float deltaTime)
        -AdvanceCycle(MachineState, Recipe)
        -ConsumeInputs(MachineState)
        -ProduceOutputs(MachineState)
    }

    class LogisticsSystem {
        <<ticked 20Hz>>
        +Register(Block)
        +Unregister(Block)
        +Tick(LogisticsNetworkTable)
        -AdvanceBelts()
        -RunInserters()
    }

    class EventBus {
        +Publish~T~(T event)
        +Subscribe~T~(Action~T~ handler)
    }

    class ConstructSplitEvent {
        +Construct Original
        +Construct[] NewConstructs
    }

    Simulation --> PowerSystem : ticks
    Simulation --> MachineSystem : ticks
    Simulation --> LogisticsSystem : ticks
    Simulation --> ConstructSystem : calls on place/remove
    Simulation --> EventBus
    EventBus --> ConstructSplitEvent
```

---

## 4 — Power System

Power balance per tick:
- `surplus = totalSupply - totalDemand`
- Surplus → charge batteries up to `MaxChargeRateKW`
- Deficit → batteries discharge first; if still short → throttle non-turret consumers proportionally
- Turrets always receive full power before other consumers are throttled

```mermaid
classDiagram
    class PowerNetwork {
        +int Id
        +List~int~ GeneratorIds
        +List~int~ BatteryIds
        +List~int~ ConsumerIds
        +List~int~ PoleIds
        +float TotalSupplyKW
        +float TotalDemandKW
        +PowerState State
    }

    class PowerState {
        <<enumeration>>
        Nominal
        BatteryAssist
        Deficit
        Dead
    }

    class GeneratorState {
        +float CurrentOutputKW
        +bool IsRunning
        +float FuelRemaining
    }

    class BatteryState {
        +float StoredKJ
        +float CapacityKJ
        +float MaxChargeRateKW
        +float MaxDischargeRateKW
        +float ChargePercent
    }

    class MachineState {
        +OperationMode Mode
        +Recipe ActiveRecipe
        +float CycleProgress
        +float OperatingRate
        +ItemBuffer InputBuffer
        +ItemBuffer OutputBuffer
    }

    class OperationMode {
        <<enumeration>>
        Idle / Waiting / Operating / NoPower
    }

    PowerNetwork --> PowerState
    PowerNetwork "1" --> "*" GeneratorState
    PowerNetwork "1" --> "*" BatteryState
    MachineState --> OperationMode
```

---

## 5 — Block Functional Parameters

```mermaid
classDiagram
    class IFunctionalParams {
        <<interface>>
    }

    class MinerParams {
        +float ExtractRatePerSecond
        +string[] ResourceTypes
        +FaceDir OutputFace
    }

    class GeneratorParams {
        +float OutputKW
        +float FuelConsumptionRate
        +FaceDir FuelInputFace
    }

    class AssemblerParams {
        +float SpeedMultiplier
        +string[] AllowedRecipeCategories
    }

    class PoleParams {
        +float WireRangeUnits
        +int MaxConnections
    }

    class BatteryParams {
        +float CapacityKJ
        +float MaxChargeRateKW
        +float MaxDischargeRateKW
    }

    class DockingPortParams {
        +DockingPortType PortType
    }

    class DockingPortType {
        <<enumeration>>
        Logistics / Power / FullInterface
    }

    IFunctionalParams <|-- MinerParams
    IFunctionalParams <|-- GeneratorParams
    IFunctionalParams <|-- AssemblerParams
    IFunctionalParams <|-- PoleParams
    IFunctionalParams <|-- BatteryParams
    IFunctionalParams <|-- DockingPortParams
    DockingPortParams --> DockingPortType
    BlockDefinition --> IFunctionalParams
```

---

## 6 — Build Mode & Placement

```mermaid
classDiagram
    class BuildController {
        -BlockDefinition _selectedDef
        -GhostBlock _ghost
        -BuildMode _mode
        -int _rotationSteps
        +Update()
        -GetPlacementTarget() PlacementResult
        -ValidatePlacement(PlacementResult) ValidationResult
        -ConfirmPlacement()
        -ConfirmRemoval()
        -ConfirmRepair()
    }

    class PlacementResult {
        +bool IsValid
        +bool IsOnBlock
        +bool IsOnTerrain
        +BlockView HitBlockView
        +Construct HitConstruct
        +GridPos GridPos
        +Vector3 WorldPos
        +Vector3 Normal
    }

    class BuildMode {
        <<enumeration>>
        Explore / Build / Dismantle / Repair / Blueprint
    }

    class BlueprintSystem {
        +List~Blueprint~ Library
        +Capture(Bounds bounds) Blueprint
        +Place(Blueprint bp, GridPos origin, Construct target)
        +Save(Blueprint bp)
        +Delete(string name)
    }

    class Blueprint {
        +string Name
        +BlueprintBlock[] Blocks
    }

    class BlueprintBlock {
        +string DefinitionId
        +GridPos LocalPosition
        +int RotationSteps
        +PlacementState State
    }

    BuildController --> BuildMode
    BuildController --> PlacementResult
    BuildController --> BlueprintSystem
    BlueprintSystem "1" --> "*" Blueprint
    Blueprint "1" --> "*" BlueprintBlock
```

---

## Data Tables (Simulation Stores)

```mermaid
classDiagram
    class BlockTable {
        +Dictionary~int_Block~ ById
        +Dictionary~int_List_int~ ByConstruct
        +Dictionary~int_List_int~ ByPowerNetwork
    }

    class ConstructTable {
        +Dictionary~int_Construct~ ById
    }

    class PowerNetworkTable {
        +Dictionary~int_PowerNetwork~ ById
        +Dictionary~int_List_int~ ByConstruct
    }

    class LogisticsNetworkTable {
        +Dictionary~int_LogisticsNetwork~ ById
        +Dictionary~int_List_int~ ByConstruct
    }
```

---

## 7 — Machine System

```mermaid
classDiagram
    class BaseMachine {
        <<abstract>>
        #Block _block
        #MachineState State
        +SetRecipe(Recipe) bool
        +Tick(float tickDelta)
        #HasInputsForOneCycle() bool
        #HasOutputSpace() bool
        #ConsumeInputs()
        #ProduceOutputs()
    }

    class MinerMachine {
        +SetResourceNode(itemId, cycleTime, amount)
        #HasInputsForOneCycle() bool
        #ConsumeInputs()
    }

    class AssemblerMachine {
    }

    class FurnaceMachine {
    }

    class MachineSystem {
        +const TickDelta = 0.05s
        +Count int
        +Register(Block)
        +Unregister(int blockId)
        +Get(int blockId) BaseMachine
        +Get~T~(int blockId) T
        +Tick()
        -Dictionary~int_BaseMachine~ _machines
    }

    BaseMachine <|-- MinerMachine
    BaseMachine <|-- AssemblerMachine
    BaseMachine <|-- FurnaceMachine
    MachineSystem "1" *-- "*" BaseMachine : owns
    BaseMachine --> MachineState : reads and writes
```

**Key rules:**
- `MachineSystem.Register(block)` is called by `Simulation.PlaceBlock()` — never manually.
- Only `Miner`, `Assembler`, and `Furnace` functional types receive a machine instance. Structural/power/logistics blocks are ignored.
- All machine state (`Mode`, `CycleProgress`, `InputBuffer`, `OutputBuffer`) lives on `Block.MachineState` so the view layer can read it without touching the machine object.
- `CycleProgress` is normalised [0, 1]. Overshoot is carried over (`-= 1f`) so fast machines don't lose fractional time.
- `SetRecipe()` in base validates `recipe.MachineType == block.Definition.FunctionalType`. Returns `false` on mismatch — wrong machine type silently rejects incompatible recipes.
- `MinerMachine.SetResourceNode()` builds a synthetic recipe with empty `Inputs` list, reusing the base Tick loop without modification.

---

## 8 — Power System

```mermaid
classDiagram
    class PowerSystem {
        +Networks IReadOnlyDictionary
        +CreateNetwork(constructId) PowerNetwork
        +GetOrCreateNetworkId(constructId) int
        +Register(Block)
        +Unregister(Block)
        +Tick(tickDelta, BlockTable)
        -TickNetwork(PowerNetwork, tickDelta, BlockTable)
        -ChargeBatteries(network, surplusKW, tickDelta, BlockTable)
        -DischargeBatteries(network, deficitKW, tickDelta, BlockTable) float
        -SetOperatingRates(network, rate, BlockTable)
        -PowerNetworkTable _table
    }

    class PowerNetwork {
        +int Id
        +List~int~ GeneratorIds
        +List~int~ BatteryIds
        +List~int~ ConsumerIds
        +List~int~ PoleIds
        +float TotalSupplyKW
        +float TotalDemandKW
        +PowerState State
    }

    PowerSystem "1" *-- "*" PowerNetwork
    PowerNetwork --> PowerState
```

**Tick order (per simulation step):**
1. `Power.Tick()` — sets `OperatingRate` on all consumer `MachineState`s
2. `Machines.Tick()` — advances production at throttled rate
3. `Logistics.Tick()` — moves items between machines

**Balance algorithm:**
- `supply = Σ GeneratorState.CurrentOutputKW` (running generators only)
- `demand = Σ BlockDefinition.PowerDrawKW` (all registered consumers)
- surplus → charge batteries up to `MaxChargeRateKW` each → `Nominal`
- deficit → discharge batteries up to `MaxDischargeRateKW` each → `BatteryAssist` if covered, `Deficit` if partial, `Dead` if effectiveSupply ≤ 0
- `OperatingRate = effectiveSupply / demand` in Deficit state (proportional throttle)

**V1 simplification:** generators run unconditionally (`IsRunning = true`, `FuelRemaining = ∞`). Fuel consumption is a future `FuelSystem` concern.

---

## 9 — Logistics System

```mermaid
classDiagram
    class LogisticsSystem {
        +Count int
        +Belts IReadOnlyDictionary
        +Connect(srcBlockId, srcPort, dstBlockId, dstPort, lengthInCells, throughput) BeltSegment
        +Disconnect(beltId)
        +DisconnectBlock(blockId)
        +Tick(tickDelta, BlockTable)
        -Dictionary~int_BeltSegment~ _belts
    }

    class BeltSegment {
        +int Id
        +int SourceBlockId
        +int SourcePortIndex
        +int DestBlockId
        +int DestPortIndex
        +float ThroughputPerMin
        +int LengthInCells
        +string[] Slots
        +int ItemCount
        +float StepProgress
        +Init()
        +Tick(tickDelta, BlockTable)
        -Step(BlockTable)
        -TryDeliver(BlockTable, string) bool
        -TryPull(BlockTable)
    }

    LogisticsSystem "1" *-- "*" BeltSegment
```

**Physical model (shift register):**
- The belt has `LengthInCells` item slots (`string[]`), one per 0.5 m cell. `null` = empty.
- `Slot[0]` = entry (source side). `Slot[LengthInCells-1]` = exit (dest side).
- Items advance together by one slot on each belt step — not individually.
- An item that cannot advance (blocked exit) stalls in place; items behind it also stall, backing the belt up naturally.
- Transit time for an item on an empty belt = `LengthInCells` steps.

**Step order (per belt step):**
1. Try to deliver exit slot to destination machine input buffer (`TryDeliver`). Clears the slot on success.
2. Advance items toward the exit, exit-side first so nothing moves twice (`Slots[i+1] = Slots[i]` when ahead is empty).
3. Pull one item from source machine output buffer into the entry slot if it is empty (`TryPull`).

**Timing:** `StepProgress += ThroughputPerMin / 60f * tickDelta`. One step fires each time it crosses 1.0.
At 60 items/min (Mk1 belt) and 20 Hz ticks: one step every 20 ticks (1 second).

**Key rules:**
- `Machines.Tick()` runs before `Logistics.Tick()` each step — machines produce outputs first, belts pull from them second.
- `SourcePortIndex` / `DestPortIndex` are `PortDefinition.Index` values on the respective blocks, which map 1:1 to `OutputBuffer` / `InputBuffer` slot indices.
- `LengthInCells = floor(splineArcLength / CellSize)` — NOT the straight-line grid distance. The view layer computes arc length from the belt spline and passes it to `Connect()`.
- `LogisticsSystem.DisconnectBlock(id)` is called by `Simulation.RemoveBlock()` before the block is deleted, ensuring no dangling belt references.
- Belt segments are direct connections for V1 (no belt blocks in the world yet). When belt blocks are added, each placed belt creates a `BeltSegment` here.

---

## Confirmed Design Decisions

| Decision | Detail |
|----------|--------|
| **Cell size** | **0.5 m per cell.** One constant named `CellSize`, shared everywhere. |
| **Construct = parent GameObject** | `ConstructView` MonoBehaviour + `Rigidbody`. All blocks are child GameObjects. |
| **Block positions** | Stored as integer `GridPos` in simulation. `localPosition = GridPos * CellSize` on the view. |
| **Rigidbody** | One per construct, on the parent. Kinematic for anchored bases, non-kinematic for vehicles. |
| **Construct split** | Spawn new parent GameObject, `SetParent(newParent, worldPositionStays: true)` on affected blocks. |
| **Colliders** | `BoxCollider` on each `BlockView` child. Compounded automatically onto parent `Rigidbody`. |
| **Grid model** | Bases/Outposts use integer grid (Foundation sets origin). Vehicles are freeform (any rotation). |
| **Structural integrity** | Connectivity-only for V1 — no stress or load simulation. |
| **Power wiring** | Auto-connects within pole range — no manual wire drawing. |
| **Construct classification** | Recalculated on block place/remove events, not every tick. |
| **Adjacency** | Face-adjacency only (6 directions). O(1) via `occupancy[gridPos + neighbour]` dictionary lookup. |
| **What the simulation ticks** | Only PowerSystem, LogisticsSystem, MachineSystem. Blocks and constructs are spatial data — updated on place/remove events, never ticked. |
| **System registration** | Functional blocks (generators, machines, belts) register with their system on placement and unregister on removal. The tick loop processes networks and states, not individual blocks. |
| **Dismantle recovery** | 100% material return in V1. |
| **Resource node depletion** | Infinite in V1. |
| **Camera** | First-person (not top-down as written in design docs). Player moves and looks with standard FPS controls. |
| **Terrain placement** | Blocks can be placed freely on terrain (no global grid constraint). Snapping only occurs when placing against an existing block face. |
| **Block-to-block snapping** | Face-plane snap: new block aligns to the face plane of the hit block, with valid centers determined by `hitFaceLeft + Round((cursor - hitFaceLeft - newHalf) / CellSize) * CellSize + newHalf`. Not a global-grid snap. |
| **Simulation layer** | Pure C# — zero Unity dependencies. No `UnityEngine` imports, no MonoBehaviours, no Vector3. All Unity coupling lives in the view layer only. |
| **Rotation — grid constructs** | 90° increments around Y-axis only (RotationSteps 0–3). |
| **Rotation — vehicles/freeform** | Continuous any-angle rotation (not yet implemented). |
| **Machine base class** | Abstract `BaseMachine` (pure C#). Template method pattern: `Tick()` in base, virtual `HasInputsForOneCycle`, `ConsumeInputs`, `ProduceOutputs` overridden per machine type. |
| **Machine state ownership** | All state (`Mode`, `CycleProgress`, `InputBuffer`, `OutputBuffer`) lives on `Block.MachineState`, not on the machine object. Machine objects are stateless logic carriers. |
| **Miner synthetic recipe** | `MinerMachine.SetResourceNode()` constructs a `Recipe` with empty `Inputs` so the base `Tick()` loop runs unmodified. `HasInputsForOneCycle()` is overridden to always return true. |
| **CycleProgress normalisation** | `[0, 1]` float. Resets via `-= 1f` (not `= 0f`) to carry over overshoot at high speed. |
| **Machine registration** | `Simulation.PlaceBlock()` calls `MachineSystem.Register(block)`. `Simulation.RemoveBlock()` calls `MachineSystem.Unregister(blockId)`. No self-registration in constructors. |
| **FunctionalType: Furnace** | Added to enum. Smelting recipes use `FunctionalType.Furnace`; they are rejected by Assembler and vice versa. |
| **Recipe — pure C# not ScriptableObject** | `Recipe` lives in the simulation layer (pure C#). ScriptableObject would import Unity and break sim isolation. Future designer editing uses a `RecipeAsset` ScriptableObject in the view layer that converts to `Recipe` at startup. |
| **RecipeCatalogue** | Static class of shared immutable `Recipe` instances, same pattern as `BlockCatalogue`. Never mutated at runtime. |
| **Miner recipes** | Not stored in `RecipeCatalogue`. `MinerMachine.SetResourceNode()` constructs a synthetic `Recipe` at runtime from the node's item/rate/amount. |
| **Belt throughput** | Mk1 conveyor: 60 items/min. `StepProgress += throughput/60 * tickDelta`; one belt step fires per crossing of 1.0. Each step shifts the entire shift-register by one slot. |
| **Belt stall behaviour** | Exit slot blocked → item stays in place; items behind it stall naturally (no special cap needed — the shift only moves items into empty slots ahead). Throughput is not wasted because no phantom items are generated. |
| **Tick order** | `MachineSystem.Tick()` → `LogisticsSystem.Tick()`. Machines produce first, belts distribute second within the same simulation step. |
| **Power network per construct** | `CreateConstruct()` does not auto-create a network. `PowerSystem.Register(block)` calls `GetOrCreateNetworkId()` which lazily creates the first network for the construct on demand. |
| **Multiple networks per construct** | Supported in the table (`ByConstruct` maps to a list). Pole wiring will split/merge networks when implemented. For V1, all blocks in a construct share one network. |
| **Waiting-state power draw** | Machines in `Waiting` mode draw **25% of their operating power** (Vol2 §4.1: *"Power continues to be drawn at idle rate while stalled"*). `PowerSystem` reads `MachineState.Mode` each tick: `Idle`/`NoPower` → 0 kW, `Waiting` → `PowerDrawKW * 0.25f`, `Operating` → `PowerDrawKW`. Blocks without a `MachineState` (future turrets, lights) always draw 100%. |
| **Consumer throttling** | `OperatingRate = effectiveSupply / demand` (where demand already reflects per-machine mode scaling). Written to `MachineState.OperatingRate` on all consumers. MachineSystem reads this when advancing `CycleProgress`. Turret priority (turrets always full power before others throttled) is a future addition. |
| **Tick order** | Power → Machines → Logistics. Power sets rates first so machines see the correct throttle in the same tick. |
| **Belt V1 scope** | Belt segments are logical connections (no belt blocks in the world). `LogisticsSystem.Connect()` wires two machine slots directly. Belt block placement comes later. |
| **Port definitions** | Each `BlockDefinition` carries a `PortDefinition[]`. Each port has `Index` (maps 1:1 to buffer slot), `Type` (Input/Output), and `Face` (which block face the spline attaches to in the view layer). |
| **Belt length source** | `LengthInCells = floor(splineArcLength / CellSize)`. NOT the straight-line grid distance between machines. The view layer computes arc length from the spline fitted through port anchors and belt-stand waypoints, then passes it to `LogisticsSystem.Connect()`. |
| **Belt stands** | Future waypoint blocks. Placed freely (not grid-constrained); act as intermediate spline anchors for routing belts around obstacles. Each stand has one input port and one output port. |
| **Belt grid rule** | Belts are NOT grid-constrained. They follow splines between port anchor points. Only machines and blocks snap to the integer grid. |
