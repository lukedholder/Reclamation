// Abstract base class for all item type definitions.
//
// One Item instance exists per item type (defined in ItemCatalogue).
// Runtime quantity is tracked by ItemStack { ItemId, Quantity } — not by this class.
// Item instances are never mutated after construction.
//
// Hierarchy:
//   Item  (this file)
//   ├── ResourceItem  — raw natural materials  (ores, coal)
//   ├── ComponentItem — refined parts & crafted intermediates (plates, gears, circuit boards)
//   └── WeaponItem    — equippable combat weapons
//
// Future: this class and its subclasses will migrate to ScriptableObject so item types
// can be authored in the Inspector and loaded from Resources/Data without code changes.

public abstract class Item
{
    // ── Identity ──────────────────────────────────────────────────────────────

    // Unique string key — must match ItemStack.ItemId, recipe IDs, and construction cost arrays.
    // Convention: snake_case  e.g. "iron_ore", "circuit_board", "hunting_rifle".
    public string Id;

    // Player-visible name shown in inventory slots and tooltips.
    public string DisplayName;

    // Short flavour / tooltip text.
    public string Description;

    // Which broad category this item belongs to.
    public ItemCategory Category;

    // ── Stack behaviour ───────────────────────────────────────────────────────

    // Maximum quantity that fits in a single inventory slot.
    // Subclass constructors set type-appropriate defaults:
    //   ResourceItem  → 100
    //   ComponentItem → 50
    //   WeaponItem    → 1   (weapons never stack)
    public int MaxStackSize = 50;

    // ── Physical properties ───────────────────────────────────────────────────

    // Mass in kg per unit.  Reserved for future encumbrance / vehicle load calculations.
    public float Weight = 0.1f;

    // ── Logistics behaviour ───────────────────────────────────────────────────

    // True if this item can be output by machines and carried on production belts.
    // Weapons return false — they are not produced on assembly lines.
    public virtual bool IsBeltTransportable => true;

    // ── Equipment behaviour ───────────────────────────────────────────────────

    // Returns true if this item type may be placed in the given gear slot.
    // Base implementation: no item is equippable.  Subclasses override as needed.
    public virtual bool CanEquipToSlot(GearSlot slot) => false;

    // ── Display ───────────────────────────────────────────────────────────────

    // Short human-readable type descriptor for tooltips ("Resource", "Basic Component", etc.).
    // Each subclass provides its own implementation.
    public abstract string TypeLabel { get; }

    // ── Helpers ───────────────────────────────────────────────────────────────

    public override string ToString() => $"{DisplayName} ({Id})";
}
