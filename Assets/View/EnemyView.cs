// Visual and movement controller for one enemy unit.
//
// Spawned by EnemySpawner.  Each frame it:
//   1. Writes its world XZ position back to Enemy.WorldX/Z (read by TurretView for range checks).
//   2. Moves the capsule toward the player at Enemy.Speed units/second.
//   3. Tints the capsule from red → dark red as health falls.
//   4. On IsDead: triggers a quick scale-down death animation, then removes itself
//      from EnemySystem and destroys the GameObject.
//
// Setup: call Init(enemy) immediately after AddComponent.

using UnityEngine;

public class EnemyView : MonoBehaviour
{
    // ── Colours ───────────────────────────────────────────────────────────────

    private static readonly Color ColHealthy = new Color(0.90f, 0.15f, 0.15f);
    private static readonly Color ColLow     = new Color(0.45f, 0.05f, 0.05f);
    private static readonly Color ColFlash   = new Color(1.00f, 1.00f, 1.00f);

    // ── State ─────────────────────────────────────────────────────────────────

    private Enemy    _enemy;
    private Material _material;

    private bool  _dying;
    private float _deathTimer;
    private const float DeathDuration = 0.35f;

    private Simulation Sim => GameManager.Instance.Simulation;

    // ── Init ──────────────────────────────────────────────────────────────────

    /// <summary>Link this view to its simulation data.  Call immediately after AddComponent.</summary>
    public void Init(Enemy enemy)
    {
        _enemy    = enemy;
        _material = GetComponent<Renderer>().material;
        _material.color = ColHealthy;

        // Seed starting position so turrets can range-check before the first Update.
        _enemy.WorldX = transform.position.x;
        _enemy.WorldZ = transform.position.z;
    }

    // ── Unity ─────────────────────────────────────────────────────────────────

    private void Update()
    {
        if (_enemy == null) return;

        // Keep sim position in sync.
        _enemy.WorldX = transform.position.x;
        _enemy.WorldZ = transform.position.z;

        // ── Death animation ───────────────────────────────────────────────────
        if (_dying)
        {
            _deathTimer -= Time.deltaTime;
            float t = 1f - Mathf.Clamp01(_deathTimer / DeathDuration);
            transform.localScale = Vector3.Lerp(Vector3.one, Vector3.zero, t);

            if (_deathTimer <= 0f)
            {
                Sim.Enemies.Remove(_enemy.Id);
                Destroy(gameObject);
            }
            return;
        }

        if (_enemy.IsDead)
        {
            _dying      = true;
            _deathTimer = DeathDuration;
            _material.color = ColFlash;
            return;
        }

        // ── Movement toward player ────────────────────────────────────────────
        var cam = Camera.main;
        if (cam != null)
        {
            Vector3 toPlayer = cam.transform.position - transform.position;
            toPlayer.y = 0f;
            float dist = toPlayer.magnitude;

            if (dist > 1.2f)   // stop just outside melee range
            {
                Vector3 move = toPlayer.normalized * (_enemy.Speed * Time.deltaTime);
                transform.position += move;
                transform.rotation  = Quaternion.LookRotation(toPlayer.normalized, Vector3.up);
            }
        }

        // ── Health tint ───────────────────────────────────────────────────────
        float hp = _enemy.MaxHealth > 0f ? _enemy.Health / _enemy.MaxHealth : 0f;
        _material.color = Color.Lerp(ColLow, ColHealthy, hp);
    }
}
