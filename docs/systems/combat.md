# Combat System — Design Specification

Factory-integrated auto-turret defence and enemy placeholder.

---

## Architecture

```
Simulation layer (plain C#)          View layer (Unity)
──────────────────────────           ──────────────────
Enemy                                EnemyView  (capsule, movement, death)
EnemySystem                          EnemySpawner  (wave management)
TurretMachine  ──────────────────►  TurretView  (barrel, targeting, flash)
TurretParams
ItemCatalogue  (turret_round)
RecipeCatalogue (craft_turret_rounds)
BlockCatalogue (gun_turret)
```

### Sim/View split for spatial queries

Enemy world positions are stored on `Enemy.WorldX / WorldZ` (simulation layer) but
**written by `EnemyView`** each frame.  This keeps spatial data accessible to
`TurretView` without a `FindObjectsOfType` call, while letting movement run at
frame rate for smooth animation.

---

## Auto-Turret

### Block definition  (`BlockCatalogue.GunTurret`)

| Field | Value |
|-------|-------|
| Id | `gun_turret` |
| Size | 1 × 2 × 1 cells |
| Power draw | 20 kW |
| Input port | face NegZ (ammo belt) |
| Construction cost | 4 iron_plate, 2 iron_gear, 1 circuit_board |

### TurretParams

| Field | Default |
|-------|---------|
| Range | 15 world units |
| ShotsPerSecond | 1 |
| DamagePerShot | 25 |

### TurretMachine (simulation)

- Input buffer: 1 slot, pre-configured for `turret_round`
- No recipe, no output buffer
- `CanFire` → powered + ammo present + cooldown elapsed
- `TryFire()` → removes 1 round, resets cooldown (1 / ShotsPerSecond sec)
- Tick: decrements cooldown; sets `Operating` (ammo present) or `Waiting` (empty)

### TurretView (Unity)

- Built on top of the block cube via `BlockPlacer` after placement
- Barrel pivot child rotates in world Y toward the nearest in-range enemy (120°/s)
- Calls `TryFire()` each frame — fires the moment the machine is ready
- Calls `EnemySystem.Damage(id, damage)` on a successful shot
- Muzzle flash: point light enabled for 0.06 s

---

## Ammo

### Items

| Id | DisplayName | Stack | Recipe |
|----|-------------|-------|--------|
| `turret_round` | Turret Round | 200 | `craft_turret_rounds` |
| `rifle_round` | Rifle Round | 200 | `craft_rifle_rounds` |

### Recipes (both Assembler)

| Id | Inputs | Outputs | Cycle |
|----|--------|---------|-------|
| `craft_turret_rounds` | 2 iron_plate | 20 turret_round | 1.0 s |
| `craft_rifle_rounds` | 1 iron_plate | 10 rifle_round | 1.0 s |

---

## Enemy Placeholder

### EnemyView

- Spawned as a red capsule (`PrimitiveType.Capsule`)
- Moves toward `Camera.main` at `Enemy.Speed` units/sec (stops 1.2 m away)
- Colour fades from bright red → dark red as health decreases
- On death: white flash → scale to zero over 0.35 s → removes self from EnemySystem

### EnemySpawner

Attach to any persistent GameObject (e.g. GameManager).

| Inspector field | Default | Meaning |
|----------------|---------|---------|
| SpawnInterval | 8 s | Seconds between waves |
| SpawnsPerWave | 1 | Enemies per wave |
| MaxEnemies | 12 | Live enemy cap |
| MinSpawnRadius | 12 m | Inner ring boundary |
| MaxSpawnRadius | 28 m | Outer ring boundary |
| SpawnY | 1.0 | Capsule centre Y (feet at Y=0) |
| EnemyHealth | 100 | HP per enemy |
| EnemySpeed | 3.5 m/s | Movement speed |

---

## Factory Loop Integration

```
[Assembler: Turret Rounds]
        ↓ belt
[Gun Turret] ──► fires at enemy ──► EnemySystem.Damage()
        |
    (needs power from grid)
```

The turret draws 20 kW.  Its `OperatingRate` is set by `PowerSystem` just like
any other consumer — a power deficit throttles fire rate.  If ammo runs out the
block shows yellow (Waiting); if unpowered it shows red (NoPower).

---

## Setup Checklist

1. **EnemySpawner** — add component to GameManager (or a CombatManager object).
   Tune SpawnInterval and MaxEnemies to taste.

2. **Gun Turret block** — add `BlockCatalogue.GunTurret` to your Hotbar definition
   so it can be selected and placed.

3. **Ammo supply** — place an Assembler, select "Turret Rounds", run a belt from its
   output into the turret's back face (NegZ port).

4. **Power** — connect the turret to the power grid; it draws 20 kW.

5. **ItemDefinitionSO** — create a `turret_round` and `rifle_round` definition in
   the ItemLibrary so ammo shows correctly in the inventory and chest UI.
