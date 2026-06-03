// All item type definitions available in the game.
// Same pattern as BlockCatalogue — one static readonly instance per item type, never mutated.
//
// Item IDs must exactly match the string literals used in:
//   RecipeCatalogue    (Inputs / Outputs)
//   BlockCatalogue     (ConstructionCost arrays)
//   PlayerInventory    (TryAddItem / TryRemoveItem)
//   MinerParams        (ResourceTypes)
//
// To add a new item:
//   1. Declare it here as a public static readonly field.
//   2. Add it to the array returned by All().
//   The Get() lookup dict rebuilds automatically on the next call.

using System.Collections.Generic;

public static class ItemCatalogue
{
    // ── Raw Resources ─────────────────────────────────────────────────────────

    public static readonly ResourceItem IronOre = new ResourceItem
    {
        Id              = "iron_ore",
        DisplayName     = "Iron Ore",
        Description     = "Raw iron ore extracted from surface deposits. Smelt into iron plates.",
        IsRawOre        = true,
        RefinedOutputId = "iron_plate",
        MaxStackSize    = 100,
        Weight          = 0.8f,
    };

    public static readonly ResourceItem CopperOre = new ResourceItem
    {
        Id              = "copper_ore",
        DisplayName     = "Copper Ore",
        Description     = "Raw copper ore extracted from surface deposits. Smelt into copper plates.",
        IsRawOre        = true,
        RefinedOutputId = "copper_plate",
        MaxStackSize    = 100,
        Weight          = 0.7f,
    };

    public static readonly ResourceItem Coal = new ResourceItem
    {
        Id              = "coal",
        DisplayName     = "Coal",
        Description     = "Combustible carbon fuel. Feeds steam generators.",
        IsRawOre        = true,
        RefinedOutputId = null,   // burned, not refined into another item
        MaxStackSize    = 100,
        Weight          = 0.4f,
    };

    // ── Basic Components (Tier 0) ─────────────────────────────────────────────

    public static readonly ComponentItem IronPlate = new ComponentItem
    {
        Id                = "iron_plate",
        DisplayName       = "Iron Plate",
        Description       = "Smelted iron sheet. Foundational material for construction and assemblies.",
        Tier              = 0,
        CraftedByRecipeId = "smelt_iron",
        MaxStackSize      = 100,
        Weight            = 0.5f,
    };

    public static readonly ComponentItem CopperPlate = new ComponentItem
    {
        Id                = "copper_plate",
        DisplayName       = "Copper Plate",
        Description       = "Smelted copper sheet. Used in electrical and wiring components.",
        Tier              = 0,
        CraftedByRecipeId = "smelt_copper",
        MaxStackSize      = 100,
        Weight            = 0.5f,
    };

    public static readonly ComponentItem IronGear = new ComponentItem
    {
        Id                = "iron_gear",
        DisplayName       = "Iron Gear",
        Description       = "Machined iron gear wheel. Required in mechanical assemblies and machines.",
        Tier              = 0,
        CraftedByRecipeId = "iron_gear_wheel",
        MaxStackSize      = 50,
        Weight            = 0.3f,
    };

    public static readonly ComponentItem CopperWire = new ComponentItem
    {
        Id                = "copper_wire",
        DisplayName       = "Copper Wire",
        Description       = "Fine copper conductor. Required in all electrical and electronic parts.",
        Tier              = 0,
        CraftedByRecipeId = "copper_wire",
        MaxStackSize      = 200,   // wire stacks generously — used in large quantities
        Weight            = 0.05f,
    };

    // ── Intermediate Components (Tier 1) ──────────────────────────────────────

    public static readonly ComponentItem CircuitBoard = new ComponentItem
    {
        Id                = "circuit_board",
        DisplayName       = "Circuit Board",
        Description       = "Basic control electronics. Required for mid-tier machines.",
        Tier              = 1,
        CraftedByRecipeId = "circuit_board",
        MaxStackSize      = 25,
        Weight            = 0.1f,
    };

    // ── Ammo ──────────────────────────────────────────────────────────────────

    public static readonly ComponentItem RifleRound = new ComponentItem
    {
        Id                = "rifle_round",
        DisplayName       = "Rifle Round",
        Description       = "5.56 mm cartridge for the Hunting Rifle. Craft in the Assembler.",
        Tier              = 0,
        CraftedByRecipeId = "craft_rifle_rounds",
        MaxStackSize      = 200,
        Weight            = 0.02f,
    };

    public static readonly ComponentItem TurretRound = new ComponentItem
    {
        Id                = "turret_round",
        DisplayName       = "Turret Round",
        Description       = "Heavy-calibre slug for auto-turrets. Craft in the Assembler.",
        Tier              = 0,
        CraftedByRecipeId = "craft_turret_rounds",
        MaxStackSize      = 200,
        Weight            = 0.05f,
    };

    // ── Weapons ───────────────────────────────────────────────────────────────

    public static readonly WeaponItem IronWrench = new WeaponItem
    {
        Id           = "iron_wrench",
        DisplayName  = "Iron Wrench",
        Description  = "Heavy maintenance tool that works just as well as a close-range weapon.",
        Kind         = WeaponType.Melee,
        Damage       = 18f,
        AttackRate   = 1.2f,   // 1.2 swings per second
        Range        = 1.8f,
        Weight       = 1.5f,
    };

    public static readonly WeaponItem HuntingRifle = new WeaponItem
    {
        Id           = "hunting_rifle",
        DisplayName  = "Hunting Rifle",
        Description  = "Bolt-action rifle. Accurate at long range but slow to reload.",
        Kind         = WeaponType.Ranged,
        Damage       = 55f,
        AttackRate   = 0.8f,   // 0.8 shots per second
        Range        = 120f,
        AmmoItemId   = "rifle_round",
        MagazineSize = 5,
        ReloadTime   = 2.5f,
        Weight       = 4.0f,
    };

    // ── Catalogue enumeration ─────────────────────────────────────────────────

    public static IReadOnlyList<Item> All() => _all;

    private static readonly Item[] _all =
    {
        // Resources
        IronOre, CopperOre, Coal,
        // Components
        IronPlate, CopperPlate, IronGear, CopperWire, CircuitBoard,
        // Ammo
        RifleRound, TurretRound,
        // Weapons
        IronWrench, HuntingRifle,
    };

    // ── Fast lookup by Id ─────────────────────────────────────────────────────

    // Populated lazily on first Get() call and reused thereafter.
    private static Dictionary<string, Item> _lookup;

    /// <summary>Returns the Item definition for <paramref name="itemId"/>, or null if not registered.</summary>
    public static Item Get(string itemId)
    {
        if (_lookup == null) BuildLookup();
        _lookup.TryGetValue(itemId, out var item);
        return item;
    }

    /// <summary>Typed convenience getter — returns null if the id is not found or is the wrong type.</summary>
    public static T Get<T>(string itemId) where T : Item => Get(itemId) as T;

    private static void BuildLookup()
    {
        _lookup = new Dictionary<string, Item>(_all.Length);
        foreach (var item in _all)
            _lookup[item.Id] = item;
    }
}
