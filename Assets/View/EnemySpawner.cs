// Periodically spawns enemy placeholder units around the player.
//
// Attach to any persistent GameObject (e.g. GameManager or a dedicated
// "CombatManager" object in the scene).
//
// Spawn ring: enemies appear at a random angle, random radius between
// MinSpawnRadius and MaxSpawnRadius, centred on the player camera.
// Y is fixed to 1 so capsules (height = 2) stand on a flat ground plane.
// Adjust SpawnY if your terrain is not at Y = 0.
//
// Inspector knobs:
//   SpawnInterval  — seconds between waves
//   SpawnsPerWave  — how many enemies each wave
//   MaxEnemies     — hard cap; no spawn if live count >= this
//   MinSpawnRadius — inner exclusion zone (keep enemies out of arm's reach at spawn)
//   MaxSpawnRadius — outer boundary of the spawn ring
//   SpawnY         — ground height for new enemies
//   EnemyHealth    — HP of each spawned enemy
//   EnemySpeed     — movement speed in world units / second

using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    [Header("Timing")]
    [SerializeField] private float _spawnInterval  = 8f;
    [SerializeField] private int   _spawnsPerWave  = 1;

    [Header("Limits")]
    [SerializeField] private int   _maxEnemies     = 12;

    [Header("Placement")]
    [SerializeField] private float _minSpawnRadius = 12f;
    [SerializeField] private float _maxSpawnRadius = 28f;
    [SerializeField] private float _spawnY         = 1f;   // capsule center (height=2, so feet at y=0)

    [Header("Enemy Stats")]
    [SerializeField] private float _enemyHealth    = 100f;
    [SerializeField] private float _enemySpeed     = 3.5f;

    private float _timer;

    private Simulation Sim => GameManager.Instance.Simulation;

    // ── Unity ─────────────────────────────────────────────────────────────────

    private void Update()
    {
        if (Sim.Enemies.Count >= _maxEnemies) return;

        _timer += Time.deltaTime;
        if (_timer >= _spawnInterval)
        {
            _timer -= _spawnInterval;
            SpawnWave();
        }
    }

    // ── Spawning ──────────────────────────────────────────────────────────────

    private void SpawnWave()
    {
        int toSpawn = Mathf.Min(_spawnsPerWave, _maxEnemies - Sim.Enemies.Count);
        for (int i = 0; i < toSpawn; i++)
            SpawnOne();
    }

    private void SpawnOne()
    {
        var cam = Camera.main;
        Vector3 origin = cam != null ? cam.transform.position : Vector3.zero;

        float angle  = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        float radius = Random.Range(_minSpawnRadius, _maxSpawnRadius);
        float x = origin.x + Mathf.Cos(angle) * radius;
        float z = origin.z + Mathf.Sin(angle) * radius;

        var enemy = Sim.Enemies.Spawn(_enemyHealth, _enemySpeed);

        // Build capsule visual
        var go  = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        go.name = $"Enemy_{enemy.Id}";
        go.transform.position = new Vector3(x, _spawnY, z);

        var ev = go.AddComponent<EnemyView>();
        ev.Init(enemy);
    }
}
