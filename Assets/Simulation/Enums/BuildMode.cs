// The player's current interaction mode with the world.
// Only one mode is active at a time. BuildController manages transitions.
// The active mode is always shown on the HUD.
// Switching modes changes which inputs are consumed and what the cursor highlights.

public enum BuildMode
{
    Explore,    // Default. Normal movement and interaction. No placement, removal, or repair.
    Build,      // Ghost preview active. Left-click places. R rotates. Right-click exits.
    Dismantle,  // Hover highlights a block. Left-click removes it and returns 100% of materials.
    Repair,     // Hover highlights a damaged block. Left-click spends materials to restore durability.
    Blueprint,  // Entered via Shift+B from Build. Drag-select to capture; click to paste from library.
}
