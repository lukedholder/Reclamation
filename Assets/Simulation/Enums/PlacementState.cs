// The feasibility state of a single block within a Blueprint being pasted.
// Each BlueprintBlock is evaluated independently every frame while the blueprint ghost is shown.
// Ghost block colour in the preview: Ready = green, MissingMaterials = yellow, Blocked = red.
// Blocks in the Blocked state are skipped entirely when the player confirms placement.

public enum PlacementState
{
    Ready,            // Block can be placed and all required materials are available.
    MissingMaterials, // Block can be placed but required items are not in inventory or connected storage.
    Blocked,          // Block cannot be placed — cell is occupied or there is a terrain conflict.
}
