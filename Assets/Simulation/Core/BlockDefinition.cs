namespace Reclamation.Simulation
{
    // ── Enums ────────────────────────────────────────────────────────────────

    public enum BlockCategory
    {
        Structural,   // Walls, floors, foundations — merged into chunk mesh
        Production,   // Assemblers, furnaces, refineries — GPU instanced
        Power,        // Generators, batteries, conduits
        Logistics,    // Belts, inserters, chests
        Defense,      // Turrets, walls — protected from power throttle
        Utility,      // Lights, signs, misc
    }

    public enum FunctionalType
    {
        None,
        Foundation,   // Anchors a construct to the terrain
        Seat,         // Allows a construct to be piloted
        Propulsion,   // Provides movement capability
    }

    /// <summary>
    /// Describes how a block participates in a power network.
    /// Populated when the block is registered with PowerSystem.
    /// </summary>
    public enum PowerInterface
    {
        None,       // Not part of any power network
        Consumer,   // Draws power (machines, lights, turrets)
        Generator,  // Produces power (engines, solar panels)
        Battery,    // Stores power, acts as buffer
        Conduit,    // Passes power without consuming it (cables, pylons)
    }

    // ── BlockDefinition ──────────────────────────────────────────────────────

    /// <summary>
    /// Immutable data record for a block type. One instance exists per block type
    /// and is shared by every placed Block of that type.
    ///
    /// In the view layer a Unity ScriptableObject wraps this and provides Mesh /
    /// Material references. The simulation layer only sees this pure-C# class.
    /// </summary>
    public class BlockDefinition
    {
        // ── Identity ─────────────────────────────────────────────────────────

        /// <summary>Stable string key used for serialisation (e.g. "wall_steel").</summary>
        public string Id;

        /// <summary>Human-readable label shown in the build menu.</summary>
        public string DisplayName;

        // ── Classification ───────────────────────────────────────────────────

        public BlockCategory  Category;
        public FunctionalType FunctionalType;
        public PowerInterface PowerInterface;

        // ── Spatial ──────────────────────────────────────────────────────────

        /// <summary>
        /// How many grid cells this block occupies along each axis when facing North.
        /// Most blocks are (1, 1, 1). A 2×1 conveyor is (2, 1, 1).
        /// BlockTable rotates these offsets when placing.
        /// </summary>
        public Int3 Size = new(1, 1, 1);

        // ── Simulation stats ─────────────────────────────────────────────────

        /// <summary>Hit points at full health.</summary>
        public int MaxDurability = 100;

        /// <summary>Mass in kg. Used for vehicle physics budgets.</summary>
        public float Mass;

        /// <summary>Steady-state power draw in kilowatts (0 if not a consumer).</summary>
        public float PowerDrawKW;

        /// <summary>Steady-state power output in kilowatts (0 if not a generator).</summary>
        public float PowerOutputKW;

        // ── Progression ──────────────────────────────────────────────────────

        /// <summary>Minimum research tier before this block can be placed.</summary>
        public int TierRequired;

        // ── Placement rules ──────────────────────────────────────────────────

        /// <summary>
        /// Optional extra placement constraints evaluated by BlockTable.CanPlace.
        /// Supported tokens (future):
        ///   "requires_foundation" — must rest on a Foundation block.
        ///   "outdoor_only"        — cannot be placed inside a sealed room.
        ///   "ground_level"        — Y must equal terrain height at that cell.
        /// Leave null or empty for the default rule (adjacent to any existing block).
        /// </summary>
        public string[] PlacementRules;
    }
}
