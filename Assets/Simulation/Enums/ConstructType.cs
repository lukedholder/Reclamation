// The runtime classification of a construct, derived from its current block composition.
// Recalculated by ConstructSystem.Reclassify() every tick — not stored persistently.
// Determines which simulation systems apply (vehicle physics, orbital mechanics, etc.)
// and what information the HUD shows when the player looks at the construct.

public enum ConstructType
{
    Structure,       // Default fallback. Anything not meeting another classification.
    Base,            // Terrain-anchored. Has production machines, storage, or power generators.
    Outpost,         // Terrain-anchored. Primarily extraction or logistics; no significant production.
    Vehicle,         // Has Seat + Propulsion + Power source + NOT terrain-anchored.
    Droid,           // Has Locomotion + Power + at least one weapon or tool block.
    OrbitalPlatform, // Has StationCore block. Not terrain-anchored. Simulated in orbit.
}
