// A single block entry within a saved Blueprint.
// Position and rotation are stored relative to the blueprint's local origin (0,0,0)
// so the blueprint can be pasted at any world location.
//
// PlacementState is NOT saved — it is recalculated every frame while the blueprint ghost
// is being positioned, based on current world occupancy and the player's inventory.

public class BlueprintBlock
{
    // Matches BlockDefinition.Id in BlockCatalogue (e.g. "foundation_basic").
    public string DefinitionId;

    // Integer offset from the blueprint origin. Resolved to a world GridPos at paste time.
    public GridPos LocalPosition;

    // 0–3, each step is a 90° Y-axis rotation applied at paste time.
    public int RotationSteps;

    // Evaluated at paste time — not serialised.
    // Ready = green ghost, MissingMaterials = yellow, Blocked = red.
    public PlacementState State;
}
