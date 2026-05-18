// A saved snapshot of a region of blocks captured by the player in Blueprint mode (Shift+B).
// Stores block types, local positions, and rotations relative to the blueprint's origin.
// Does NOT store runtime state — no machine progress, inventory contents, or power levels.
// Blueprints are saved persistently in the save file and restored on load.
// The same blueprint system is used for base sections and droid designs.

using System.Collections.Generic;

public class Blueprint
{
    // Player-given name, shown in the blueprint library UI.
    public string Name;

    // All block entries captured in this blueprint, in bottom-to-top layer order.
    // Placed in this order on confirmation so lower blocks are always present
    // before upper blocks try to snap to them.
    public List<BlueprintBlock> Blocks = new List<BlueprintBlock>();
}
