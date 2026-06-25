# Vehicle System — Design & Roadmap

Goal: turn a built construct into a **pilotable vehicle** the player can drive/fly. Builds directly on the [PhysicsSystem](PhysicsSystem.md): a vehicle is a non-anchored construct (Rigidbody + gravity) that converts pilot input into thrust forces.

## Guiding principles

- **Sim/View split stays intact.** Simulation decides *what a construct is* (`IsPilotable`, available thrust per axis after power throttling). View applies Unity forces, runs the pilot camera, reads input. Simulation has no Unity dependency.
- **Reuse power throttling.** A thruster is a power **consumer**. `PowerSystem` already computes `MachineState.OperatingRate` (0..1) per consumer from supply/demand. Available thrust = `ThrustKN × OperatingRate` — power deficit weakens the vehicle for free, no new power code.
- **Reuse the physics anchor model.** Anchored construct = static (no Rigidbody). A vehicle must be *unanchored*. We add a manual override so a craft built on the ground can be released.

## Recommended MVP scope: thrust-based flight

Thrusters apply `Rigidbody.AddForce` directly — they reuse the physics from this branch with almost no new view code. Ground vehicles with `WheelCollider` suspension are a much larger, separate effort. **Recommendation: ship thruster flight first; revisit wheels later.** (Decision point — flagged for the user.)

---

## New simulation pieces

### Params
```csharp
// Assets/Simulation/Params/ThrusterParams.cs
public class ThrusterParams : IFunctionalParams
{
    public float   ThrustKN    = 50f;       // max force at full power
    public FaceDir ExhaustDir  = FaceDir.NegZ; // thrust pushes opposite the exhaust
    // PowerDrawKW lives on BlockDefinition (consumer), as with other machines.
}

// Assets/Simulation/Params/SeatParams.cs (optional for MVP)
public class SeatParams : IFunctionalParams
{
    public FaceDir Facing = FaceDir.PosZ;   // pilot look direction / camera forward
}
```

### Blocks (BlockCatalogue, BlockCategory.Vehicle)
| Block | FunctionalType | Notes |
|---|---|---|
| Pilot Seat | `Seat` | 1×1×1. Entry point. MVP uses the first seat in the construct. |
| Thruster | `Propulsion` | e.g. 1×1×2. Power consumer. `ThrusterParams`. Thrust dir = opposite `ExhaustDir`, rotated by block + construct rotation. |
| Gyroscope (later) | `Propulsion` | Provides clean rotational torque + angular damping. |
| Landing Gear (later) | new type | Toggles anchor lock (grounded ↔ free), SE-style. |

Add construction costs (iron_plate / circuit_board / etc.), register in `BlockCatalogue.All()`, and add any new ammo/parts to `ItemCatalogue` + `RecipeCatalogue` as needed.

### Construct classification — `ConstructSystem` (NEW; currently referenced in comments but does not exist)
```csharp
// Assets/Simulation/Systems/ConstructSystem.cs
//   Reclassify(construct):
//     hasSeat, hasPropulsion, hasPower (generator/battery present)
//     anchored = construct.IsAnchored
//     IsPilotable = hasSeat && hasPropulsion && !anchored
//     Type = Vehicle if (hasSeat && hasPropulsion && !anchored)
//            else Base/Outpost if anchored
//            else Structure
```
Called from `Simulation.Update()` over all constructs (counts are small). Sets `Construct.Type` and `Construct.IsPilotable` (both already exist on `Construct`).

### Anchor override
Add `Construct.ManualUnanchored` (bool). `Simulation.RecalcAnchor()` becomes:
`IsAnchored = !ManualUnanchored && (any member block IsOnTerrain)`.
Persist `ManualUnanchored` in the save file. This is how a ground-built craft becomes free-floating.

---

## New view pieces

### `VehicleController` (MonoBehaviour on ConstructView)
- `FixedUpdate` while piloted: read input axes → for each axis sum thrust from thrusters whose world-space thrust direction matches, each scaled by its block's `OperatingRate` → `rb.AddForce` (and `AddTorque` for rotation).
- Thruster world direction = `constructTransform.rotation * FaceToVector(thrust dir)`.
- Optional flight assist: damp lateral velocity / angular velocity when no input (Gyro-style stability).

### Pilot flow (extend Raycaster/interaction)
- **F** while aiming at a Seat block → enter pilot mode (E is taken by machine/chest panels).
- Enter: disable `PlayerController` + `CharacterController`; set `construct.ManualUnanchored = true` → `ConstructView.ApplyPhysics()`; reparent the camera to a seat anchor (offset up/back); set `VehicleController.Piloted = true`.
- Exit (F again): re-enable player beside the seat; reparent camera back. Vehicle stays free (parked on its colliders) — re-lock via Landing Gear later.

### Camera
MVP: reuse the single player camera. On enter, detach from player and parent to the seat anchor; on exit, reattach. (A dedicated vehicle camera with smoothing is a later polish.)

---

## Milestones (dependency-ordered)

**M0 — Physics foundation** ✅ done (this branch). Floating constructs get a gravity Rigidbody.

**M1 — Vehicle blocks + classification (sim only)**
1. `ThrusterParams` (+ optional `SeatParams`).
2. Pilot Seat + Thruster in `BlockCatalogue` (+ costs, recipes, `All()`).
3. `ConstructSystem.Reclassify()` wired into `Simulation.Update()`.
4. `Construct.ManualUnanchored` + `RecalcAnchor()` honoring it; persist it.
- **Exit check:** debug HUD shows a built craft as `IsPilotable` once seat+thruster present and released.

**M2 — Release & physics validation**
5. Release/lock action (key on the looked-at construct now; Landing Gear block later) toggling `ManualUnanchored` → `ApplyPhysics()`.
- **Exit check:** build a small craft on the ground, release it, it falls and rests as a Rigidbody.

**M3 — Pilot mode + control (view)**
6. Seat F-to-enter/exit; camera swap; disable PlayerController.
7. `VehicleController`: input → per-axis available thrust (Σ thrusters × OperatingRate) → AddForce/AddTorque.
- **Exit check:** fly the craft with WASD + Space/Ctrl; cutting power reduces thrust.

**M4 — Polish & integration**
8. Gyroscope block + flight assist/damping.
9. Vehicle HUD (speed, power %, throttle, "F to exit").
10. Thruster draw model (idle vs active draw).
11. Thruster exhaust VFX.
12. (Stretch) Docking ports (`FunctionalType.DockingPort` already enumerated) to bridge vehicle↔base power/items.

## Risks / open questions
- **Center of mass:** Unity's auto COM may make craft tumble; set `rb.centerOfMass` from block mass distribution.
- **Thrust/mass tuning:** `Mass` is in `BlockDefinition`; thruster `ThrustKN` must out-muscle gravity on `Σ mass`. Needs balancing.
- **Player on a moving vehicle:** CharacterController vs moving compound collider — keep player parented to the vehicle while piloting to avoid sliding off.
- **Decision:** thrusters (flight) for MVP vs wheels (ground). Wheels = WheelCollider suspension, a separate track.
- **Power draw model:** treat thruster as constant consumer (simplest, reuses OperatingRate) vs draw scaled by throttle.

---

## Implementation status (basic prototype shipped)

A flyable basic prototype is implemented. What landed and where it deviates from
the spec above:

**Done**

- **Params:** `ThrusterParams { ThrustKN, ExhaustDir }`, `SeatParams { Facing }`
  (`Assets/Simulation/Params/`).
- **Blocks:** `PilotSeat` (Seat) and `Thruster` (Propulsion), `BlockCategory.Vehicle`,
  in `BlockCatalogue` + `All()`. A **Vehicle** tab was added to `BuildMenu`.
- **Classification:** `ConstructSystem.Reclassify()` sets `IsPilotable`
  (`hasSeat && hasPropulsion`) and `Type`; ticked from `Simulation.Update()` (step 5).
- **Release:** `Construct.ManualUnanchored` overrides `RecalcAnchor()`; persisted in
  saves (`SaveLoadManager`). Entering a seat releases the craft and
  `ConstructView.ApplyPhysics()` gives it a Rigidbody.
- **Pilot mode:** `VehiclePilot` (on Player) — **F** to enter/exit a seat, disables
  PlayerController + CharacterController + build/interact components, parents the
  player to the construct, manages cursor, draws a control hint.
- **Driving:** `VehicleController` (on ConstructView) — WASD translate, Space/Ctrl
  up/down, Q/E roll, mouse yaw/pitch as torque; `_hoverAssist` cancels gravity for
  easy flight; piloted drag/angular-drag for arcade control.

**Deliberate simplifications vs spec (revisit later)**

- **Thrust is NOT power-gated.** Thrusters draw no power and stay off the grid
  (`PowerInterface.None`, `PowerDrawKW = 0`). The `OperatingRate` hook is marked
  TODO in `VehicleController.TotalThrustForce()`. (Was spec M3/M4.)
- **Total-thrust model**, not directional summing: total force = Σ `ThrustKN`,
  applied in the construct frame. `ExhaustDir`/`SeatParams.Facing` are unused so far.
- **Mouse steers via torque** (no gyroscope block yet). Tuning lives on
  `VehicleController` serialized fields (`_forceScale`, torques, drags).
- **Save** stores only Y rotation, so a craft saved mid-flight loses pitch/roll.

**Scene setup required:** add the `VehiclePilot` component to the Player GameObject
(same object that has `PlayerController`, `Raycaster`, `Hotbar`, etc.).

**Next:** power-gated thrust (make Thruster a consumer, scale by `OperatingRate`),
directional thruster summing, gyroscope + flight-assist toggle, vehicle HUD.
