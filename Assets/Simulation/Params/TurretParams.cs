// Configuration parameters for an auto-targeting turret block.
// Stored on BlockDefinition.Params; shared and immutable across all placed turrets of this type.

public class TurretParams : IFunctionalParams
{
    // Detection and engagement range in world units.
    public float Range = 15f;

    // How many rounds are fired per second when a target is in range.
    public float ShotsPerSecond = 1f;

    // Hit-point damage applied to the target per round.
    public float DamagePerShot = 25f;
}
