// The catalogue of all block types available in the game.
// Each entry is a BlockDefinition instance shared across all placed blocks of that type.
// Add new block types here as they are designed and balanced.
//
// V1 block set (from design docs):
//   Structural: SmallCube, LargeCube, Plank, Foundation, Wall
//   Power:      SteamGenerator, SolarPanel, SmallBattery, SmallPowerPole
//   Production: BasicMiner, ElectricFurnace, AssemblerMk1
//   Storage:    StorageChest
//
// Currently only the three prototype blocks are fully defined.
// Full V1 block set will be added once the simulation systems that use them are implemented.

public static class BlockCatalogue
{
    // --- Prototype / Test Blocks ---
    // Used during early development before full V1 blocks are implemented.

    public static readonly BlockDefinition SmallCube = new BlockDefinition
    {
        Id               = "small_cube",
        DisplayName      = "Small Cube",
        Category         = BlockCategory.Structural,
        FunctionalType   = FunctionalType.None,
        TierRequired     = 0,
        SizeX            = 1,
        SizeY            = 1,
        SizeZ            = 1,
        MaxDurability    = 200,
        Mass             = 5f,
        PowerDrawKW      = 0f,
        PowerOutputKW    = 0f,
        PowerInterface   = PowerInterface.None,
        ConstructionCost = new[] { new ItemStack("iron_plate", 1) },
        Params           = null,
    };

    public static readonly BlockDefinition LargeCube = new BlockDefinition
    {
        Id               = "large_cube",
        DisplayName      = "Large Cube",
        Category         = BlockCategory.Structural,
        FunctionalType   = FunctionalType.None,
        TierRequired     = 0,
        SizeX            = 2,
        SizeY            = 2,
        SizeZ            = 2,
        MaxDurability    = 400,
        Mass             = 20f,
        PowerDrawKW      = 0f,
        PowerOutputKW    = 0f,
        PowerInterface   = PowerInterface.None,
        ConstructionCost = new[] { new ItemStack("iron_plate", 4) },
        Params           = null,
    };

    public static readonly BlockDefinition Plank = new BlockDefinition
    {
        Id               = "plank",
        DisplayName      = "Plank",
        Category         = BlockCategory.Structural,
        FunctionalType   = FunctionalType.None,
        TierRequired     = 0,
        SizeX            = 4,
        SizeY            = 1,
        SizeZ            = 1,
        MaxDurability    = 150,
        Mass             = 10f,
        PowerDrawKW      = 0f,
        PowerOutputKW    = 0f,
        PowerInterface   = PowerInterface.None,
        ConstructionCost = new[] { new ItemStack("iron_plate", 2) },
        Params           = null,
    };

    // --- V1 Power Blocks (params only — simulation logic not yet implemented) ---

    public static readonly BlockDefinition SteamGenerator = new BlockDefinition
    {
        Id               = "steam_generator",
        DisplayName      = "Steam Generator",
        Category         = BlockCategory.Power,
        FunctionalType   = FunctionalType.Generator,
        TierRequired     = 0,
        SizeX            = 2,
        SizeY            = 2,
        SizeZ            = 2,
        MaxDurability    = 300,
        Mass             = 80f,
        PowerDrawKW      = 0f,
        PowerOutputKW    = 120f,
        PowerInterface   = PowerInterface.Node,
        ConstructionCost = new[] { new ItemStack("iron_plate", 8), new ItemStack("copper_plate", 4) },
        Params           = new GeneratorParams
        {
            OutputKW            = 120f,
            FuelConsumptionRate = 0.25f, // 1 coal per 4 seconds
            FuelInputFace       = FaceDir.NegY,
        },
    };

    public static readonly BlockDefinition SmallBattery = new BlockDefinition
    {
        Id               = "small_battery",
        DisplayName      = "Small Battery",
        Category         = BlockCategory.Power,
        FunctionalType   = FunctionalType.Battery,
        TierRequired     = 0,
        SizeX            = 1,
        SizeY            = 2,
        SizeZ            = 1,
        MaxDurability    = 150,
        Mass             = 30f,
        PowerDrawKW      = 0f,
        PowerOutputKW    = 0f,
        PowerInterface   = PowerInterface.Node,
        ConstructionCost = new[] { new ItemStack("copper_plate", 4), new ItemStack("circuit_board", 2) },
        Params           = new BatteryParams
        {
            CapacityKJ         = 500f,
            MaxChargeRateKW    = 50f,
            MaxDischargeRateKW = 100f,
        },
    };

    public static readonly BlockDefinition SmallPowerPole = new BlockDefinition
    {
        Id               = "small_power_pole",
        DisplayName      = "Power Pole",
        Category         = BlockCategory.Power,
        FunctionalType   = FunctionalType.Pole,
        TierRequired     = 0,
        SizeX            = 1,
        SizeY            = 1,
        SizeZ            = 1,
        MaxDurability    = 100,
        Mass             = 5f,
        PowerDrawKW      = 0f,
        PowerOutputKW    = 0f,
        PowerInterface   = PowerInterface.WireEndpoint,
        ConstructionCost = new[] { new ItemStack("copper_wire", 2), new ItemStack("iron_plate", 1) },
        Params           = new PoleParams
        {
            WireRangeUnits = 8f,
            MaxConnections = 4,
        },
    };
}
