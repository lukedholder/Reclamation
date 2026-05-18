// Broad classification of what role a block plays in the simulation.
// Used by the build menu to group block types, and by simulation systems
// to filter which blocks they need to process each tick
// (e.g. PowerSystem only touches blocks in the Power category or with a power draw).

public enum BlockCategory
{
    Structural,      // Physical structure only — no functional systems. High durability.
    Production,      // Transforms inputs into outputs. Requires power and logistics connections.
    Storage,         // Passively holds items or fluids. No power draw.
    Power,           // Generates, stores, or distributes electrical energy.
    Logistics,       // Moves solid items: conveyor belts, inserters, splitters.
    FluidLogistics,  // Moves fluids: pipes, pumps, valves.
    Defense,         // Automated weapons. Turrets are protected from power throttling.
    Vehicle,         // Enables movement and vehicle-specific functions (seat, propulsion, etc.).
    Orbital,         // High-tier blocks for orbital platforms and space construction.
}
