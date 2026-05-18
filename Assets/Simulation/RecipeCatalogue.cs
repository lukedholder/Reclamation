// All production recipes available in the game.
// Same pattern as BlockCatalogue — static shared instances, never mutated.
//
// Naming convention: ItemIds use snake_case and must match entries in the
// item data table (e.g. "iron_ore", "iron_plate", "copper_wire").
//
// Furnace recipes  — FunctionalType.Furnace   — smelting, 1-in 1-out
// Assembler recipes— FunctionalType.Assembler  — crafting, N-in 1-out
// Miner recipes    — not stored here; MinerMachine.SetResourceNode() builds
//                    a synthetic Recipe at runtime from the resource node.

using System.Collections.Generic;

public static class RecipeCatalogue
{
    // ── Furnace ──────────────────────────────────────────────────────────────

    public static readonly Recipe SmeltIron = new Recipe
    {
        Id          = "smelt_iron",
        DisplayName = "Smelt Iron",
        MachineType = FunctionalType.Furnace,
        CycleTime   = 3.5f,
        Inputs      = new List<ItemStack> { new ItemStack("iron_ore",    1) },
        Outputs     = new List<ItemStack> { new ItemStack("iron_plate",  1) },
    };

    public static readonly Recipe SmeltCopper = new Recipe
    {
        Id          = "smelt_copper",
        DisplayName = "Smelt Copper",
        MachineType = FunctionalType.Furnace,
        CycleTime   = 3.5f,
        Inputs      = new List<ItemStack> { new ItemStack("copper_ore",   1) },
        Outputs     = new List<ItemStack> { new ItemStack("copper_plate", 1) },
    };

    // ── Assembler ─────────────────────────────────────────────────────────────

    public static readonly Recipe IronGearWheel = new Recipe
    {
        Id          = "iron_gear_wheel",
        DisplayName = "Iron Gear Wheel",
        MachineType = FunctionalType.Assembler,
        CycleTime   = 0.5f,
        Inputs      = new List<ItemStack> { new ItemStack("iron_plate", 2) },
        Outputs     = new List<ItemStack> { new ItemStack("iron_gear",  1) },
    };

    public static readonly Recipe CopperWire = new Recipe
    {
        Id          = "copper_wire",
        DisplayName = "Copper Wire",
        MachineType = FunctionalType.Assembler,
        CycleTime   = 0.5f,
        Inputs      = new List<ItemStack> { new ItemStack("copper_plate", 1) },
        Outputs     = new List<ItemStack> { new ItemStack("copper_wire",  2) },
    };

    public static readonly Recipe CircuitBoard = new Recipe
    {
        Id          = "circuit_board",
        DisplayName = "Circuit Board",
        MachineType = FunctionalType.Assembler,
        CycleTime   = 1.0f,
        Inputs      = new List<ItemStack>
        {
            new ItemStack("iron_plate",  1),
            new ItemStack("copper_wire", 3),
        },
        Outputs     = new List<ItemStack> { new ItemStack("circuit_board", 1) },
    };
}
