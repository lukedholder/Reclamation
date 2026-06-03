// A refined material or crafted part produced by furnaces, assemblers, or other machines.
//
// Components serve two roles:
//   1. Production chain  — recipe inputs and outputs flowing on belts.
//   2. Construction cost — items consumed from inventory when placing blocks.
//
// ComponentItems are belt-transportable.
// They are not equippable to any gear slot.
//
// Instances are defined in ItemCatalogue.

public class ComponentItem : Item
{
    // ── Properties ────────────────────────────────────────────────────────────

    // Progression tier indicating how far into the tech tree this part sits.
    //   0 — Basic      : iron_plate, copper_plate, iron_gear, copper_wire
    //   1 — Intermediate: circuit_board
    //   2+ — Advanced  : reserved for future tiers
    public int Tier;

    // Id of the Recipe in RecipeCatalogue that produces this component.
    // Informational — used for cross-reference in crafting guides and tooltips.
    // Null for items with no single canonical recipe (obtained multiple ways).
    public string CraftedByRecipeId;

    // ── Constructor ───────────────────────────────────────────────────────────

    public ComponentItem()
    {
        Category     = ItemCategory.Component;
        MaxStackSize = 50;
        Weight       = 0.2f;
    }

    // ── Overrides ─────────────────────────────────────────────────────────────

    public override string TypeLabel => Tier switch
    {
        0 => "Basic Component",
        1 => "Intermediate Component",
        _ => "Advanced Component",
    };
}
