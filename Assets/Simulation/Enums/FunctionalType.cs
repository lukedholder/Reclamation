// The specific function a block performs within its category.
// Drives which IFunctionalParams subclass is expected on BlockDefinition.Params,
// which simulation systems process this block each tick, and what UI is shown
// when the player inspects or configures the block.
// Blocks with FunctionalType.None are purely structural — no systems touch them.

public enum FunctionalType
{
    None,          // Purely structural or decorative. No simulation processing.
    Foundation,    // Anchors a construct to terrain. Sets the local grid origin for the construct.
    Miner,         // Extracts a resource from a node beneath it. No input items; one output face.
    Assembler,     // Crafts items from inputs using a player-selected recipe.
    Storage,       // Passive item storage container. Inserters move items in and out.
    Generator,     // Burns fuel items to produce electrical power (kW).
    Battery,       // Stores electrical energy (kJ). Charges on surplus; discharges on deficit.
    Pole,          // Power distribution node. Auto-connects via wires to nearby poles and blocks.
    Turret,        // Automated weapon. Always receives full power before other consumers.
    Seat,          // Allows a player to pilot the construct as a vehicle.
    Propulsion,    // Provides movement force. Required for Vehicle construct classification.
    Pump,          // Actively moves fluids through pipe networks.
    Pipe,          // Passive fluid conduit. No power draw.
    DockingPort,   // Interface point linking a vehicle's networks to a base's networks.
    Fabricator,    // High-tier production block with access to recipes beyond the Assembler.
}
