// Describes a type of block — its name, size, category, costs, and behaviour parameters.
// One BlockDefinition instance exists per block type (defined in BlockCatalogue).
// Every placed Block holds a reference to its definition; the definition is never mutated.
//
// In a future Unity integration step this becomes a ScriptableObject so block types
// can be authored in the Inspector and loaded from the Resources/Data folder.
// For now it is a plain C# class to keep the simulation layer Unity-free.

public class BlockDefinition
{
    // --- Identity ---

    // Unique string key used in save files, recipes, and blueprints (e.g. "steam_generator").
    public string Id;

    // Player-facing name shown in the build menu and block inspector.
    public string DisplayName;


    // --- Classification ---

    // Broad role this block plays — used to filter blocks in simulation systems and UI.
    public BlockCategory Category;

    // Specific function this block performs. Determines which Params subclass to expect
    // and which simulation systems process this block each tick.
    // None = purely structural; no systems process it beyond ConstructSystem membership.
    public FunctionalType FunctionalType;

    // Tier at which this block becomes available. 0 = always available.
    // Higher tiers require progression milestones to unlock.
    public int TierRequired;


    // --- Size ---

    // Number of grid cells this block occupies in each axis (e.g. 2,2,2 = 2x2x2 cell cube).
    // One cell = CellSize world units (currently 0.5 m).
    public int SizeX;
    public int SizeY;
    public int SizeZ;


    // --- Durability & Physics ---

    // Maximum hit points. Block is destroyed and removed when Durability reaches 0.
    public int MaxDurability;

    // Mass in kg. Ignored for terrain-anchored constructs.
    // Used for vehicle physics, orbital calculations, and droid locomotion.
    public float Mass;


    // --- Power ---

    // Kilowatts this block draws from its power network when Operating.
    // 0 for structural blocks and passive logistics blocks.
    public float PowerDrawKW;

    // Kilowatts this block produces when running (generators only).
    // 0 for everything else.
    public float PowerOutputKW;

    // How this block participates in power network graph construction.
    // None = invisible to the power system.
    public PowerInterface PowerInterface;


    // --- Construction Cost ---

    // Items consumed from the player's inventory or connected storage when placing this block.
    // 100% of these items are returned when the block is dismantled (V1 policy).
    public ItemStack[] ConstructionCost;


    // --- Logistics Ports ---

    // All logistics connection points on this block.
    // Indexed by PortDefinition.Index. Input ports map to InputBuffer slots;
    // output ports map to OutputBuffer slots.
    // Empty for blocks with no logistics role (structural, power-only, etc.).
    public PortDefinition[] Ports = System.Array.Empty<PortDefinition>();


    // --- Functional Parameters ---

    // Type-specific configuration data. Cast to the appropriate subclass based on FunctionalType:
    //   Generator   → GeneratorParams
    //   Battery     → BatteryParams
    //   Miner       → MinerParams
    //   Assembler   → AssemblerParams
    //   Pole        → PoleParams
    //   DockingPort → DockingPortParams
    // Null for blocks with FunctionalType.None.
    public IFunctionalParams Params;
}
