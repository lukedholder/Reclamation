// A naturally occurring raw material extracted from the world
// (ore nodes, coal deposits, etc.).
//
// ResourceItems are belt-transportable: miners output them, furnaces consume them.
// They are not equippable to any gear slot.
//
// Instances are defined in ItemCatalogue.

public class ResourceItem : Item
{
    // ── Properties ────────────────────────────────────────────────────────────

    // True if this item is extracted directly by a miner from a resource node.
    // False for gathered or scavenged materials that don't come from ore nodes.
    public bool IsRawOre;

    // Id of the item this resource directly refines or smelts into (1-to-1 relationship).
    // Informational — the actual conversion is defined by a Recipe in RecipeCatalogue.
    // Null if this material has no direct 1:1 refined form (e.g. coal is burned, not refined).
    public string RefinedOutputId;

    // ── Constructor ───────────────────────────────────────────────────────────

    public ResourceItem()
    {
        Category     = ItemCategory.Resource;
        MaxStackSize = 100;    // raw materials stack in large quantities
        Weight       = 0.5f;
    }

    // ── Overrides ─────────────────────────────────────────────────────────────

    public override string TypeLabel => "Resource";
}
