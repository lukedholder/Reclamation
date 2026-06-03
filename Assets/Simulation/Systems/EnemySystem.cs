// Manages all active enemy units for the simulation.
// Pure C# — no Unity dependency.
//
// Enemies are spawned and removed via this system; their visual representation
// lives in EnemyView (view layer).  EnemyView writes WorldX/Z back to the
// Enemy data each frame so turret range checks can use it.
//
// Tick() is a no-op for now — enemy movement is intentionally handled in the
// view layer (EnemyView) so it runs at render frame rate rather than the 20 Hz
// sim tick, which gives smoother motion without sim changes.

using System.Collections.Generic;

public class EnemySystem
{
    private readonly List<Enemy> _enemies = new List<Enemy>();
    private int _nextId;

    public IReadOnlyList<Enemy> All   => _enemies;
    public int                  Count => _enemies.Count;

    /// <summary>Creates a new enemy entry with the given stats.  EnemySpawner creates the view.</summary>
    public Enemy Spawn(float health = 100f, float speed = 3f)
    {
        var e = new Enemy
        {
            Id        = _nextId++,
            MaxHealth = health,
            Health    = health,
            Speed     = speed,
        };
        _enemies.Add(e);
        return e;
    }

    /// <summary>Deals damage to the enemy with the given ID.  Called by TurretView on a successful shot.</summary>
    public void Damage(int enemyId, float amount)
    {
        foreach (var e in _enemies)
            if (e.Id == enemyId) { e.TakeDamage(amount); return; }
    }

    /// <summary>Removes the enemy by ID.  Call from EnemyView after the death animation finishes.</summary>
    public void Remove(int enemyId)
    {
        for (int i = _enemies.Count - 1; i >= 0; i--)
            if (_enemies[i].Id == enemyId) { _enemies.RemoveAt(i); return; }
    }

    // Called by Simulation.Update(). No pure-sim enemy logic yet — placeholder for future AI.
    public void Tick(float tickDelta) { }
}
