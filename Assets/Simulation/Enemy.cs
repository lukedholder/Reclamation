// Simulation-layer data for one enemy unit.
// Pure C# — no Unity dependency.
//
// WorldX / WorldZ are written by EnemyView each frame so that the turret's
// range check (which runs in TurretView, also on the view layer) can read
// positions without going through FindObjectsOfType.
//
// Health is authoritative here; EnemyView reads it to trigger death visuals.

public class Enemy
{
    public int   Id;
    public float MaxHealth;
    public float Health;

    // Movement speed in world units per second (used by EnemyView).
    public float Speed;

    // World-space XZ position, kept in sync by EnemyView.Transform each frame.
    public float WorldX;
    public float WorldZ;

    public bool IsDead => Health <= 0f;

    public void TakeDamage(float amount)
    {
        Health -= amount;
        if (Health < 0f) Health = 0f;
    }
}
