// Visual and logic bridge for a placed Gun Turret block.
//
// Responsibilities:
//   - Builds the barrel + pivot child objects on Init().
//   - Each frame: finds the nearest live enemy within range, smoothly rotates the
//     barrel pivot to face it, and fires when TurretMachine.TryFire() succeeds.
//   - On a successful shot: calls EnemySystem.Damage() and briefly enables a
//     muzzle-flash point light.
//
// The turret fire rate / ammo tracking are owned by TurretMachine (simulation).
// This class only handles spatial queries and visual feedback.
//
// Setup: BlockPlacer calls Init(block, machine) after creating the block cube.

using UnityEngine;

public class TurretView : MonoBehaviour
{
    // ── Barrel geometry (built in Init) ──────────────────────────────────────

    private GameObject _pivot;       // rotates in Y to track target
    private Light      _muzzleLight; // brief flash on fire

    // ── Machine refs ──────────────────────────────────────────────────────────

    private Block         _block;
    private TurretMachine _machine;

    // ── Flash ─────────────────────────────────────────────────────────────────

    private float _flashTimer;
    private const float FlashDuration = 0.06f;

    // ── Rotation ─────────────────────────────────────────────────────────────

    private const float TurnSpeed = 120f;   // degrees / second

    private Simulation Sim => GameManager.Instance.Simulation;

    // ── Init ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Wires this view to its simulation data and builds the barrel geometry.
    /// Call from BlockPlacer immediately after AddComponent.
    /// </summary>
    public void Init(Block block, TurretMachine machine)
    {
        _block   = block;
        _machine = machine;
        BuildBarrel();
    }

    // ── Unity ─────────────────────────────────────────────────────────────────

    private void Update()
    {
        if (_machine == null) return;

        // ── Muzzle flash decay ────────────────────────────────────────────────
        if (_flashTimer > 0f)
        {
            _flashTimer -= Time.deltaTime;
            if (_flashTimer <= 0f && _muzzleLight != null)
                _muzzleLight.enabled = false;
        }

        // ── Target acquisition ────────────────────────────────────────────────
        Enemy target = FindNearestEnemy();

        if (target != null)
        {
            RotateToward(target);

            // Fire as soon as the machine is ready
            if (_machine.TryFire())
            {
                Sim.Enemies.Damage(target.Id, _machine.Damage);
                TriggerMuzzleFlash();
            }
        }
    }

    // ── Targeting ─────────────────────────────────────────────────────────────

    private Enemy FindNearestEnemy()
    {
        float range   = _machine.Range;
        float rangeSq = range * range;
        float myX     = transform.position.x;
        float myZ     = transform.position.z;

        Enemy  best     = null;
        float  bestDist = float.MaxValue;

        foreach (var e in Sim.Enemies.All)
        {
            if (e.IsDead) continue;
            float dx = e.WorldX - myX;
            float dz = e.WorldZ - myZ;
            float dSq = dx * dx + dz * dz;
            if (dSq < rangeSq && dSq < bestDist)
            {
                best     = e;
                bestDist = dSq;
            }
        }

        return best;
    }

    private void RotateToward(Enemy target)
    {
        if (_pivot == null) return;

        Vector3 targetWorld = new Vector3(target.WorldX, _pivot.transform.position.y, target.WorldZ);
        Vector3 dir         = targetWorld - _pivot.transform.position;

        if (dir.sqrMagnitude < 0.001f) return;

        Quaternion desired  = Quaternion.LookRotation(dir.normalized, Vector3.up);
        _pivot.transform.rotation = Quaternion.RotateTowards(
            _pivot.transform.rotation, desired, TurnSpeed * Time.deltaTime);
    }

    // ── Muzzle flash ──────────────────────────────────────────────────────────

    private void TriggerMuzzleFlash()
    {
        if (_muzzleLight == null) return;
        _muzzleLight.enabled = true;
        _flashTimer = FlashDuration;
    }

    // ── Barrel construction ───────────────────────────────────────────────────
    //
    // Block cube for a 1×2×1-cell turret has world scale (0.5, 1.0, 0.5).
    // All child local sizes are chosen to produce the desired world dimensions
    // when multiplied by the inherited parent scale.

    private void BuildBarrel()
    {
        // Pivot: sits near the top of the base cube, inherits parent scale.
        // localPosition (0, 0.45, 0) → world offset (0, 0.45, 0) given Y-scale = 1.
        _pivot = new GameObject("TurretPivot");
        _pivot.transform.SetParent(transform, worldPositionStays: false);
        _pivot.transform.localPosition = new Vector3(0f, 0.45f, 0f);

        // Barrel cube.
        // Desired world size ≈ (0.08, 0.08, 0.40).
        // Parent world scale = (0.5, 1.0, 0.5), so local scale = desired / parent:
        //   X: 0.08 / 0.5 = 0.16   Y: 0.08 / 1.0 = 0.08   Z: 0.40 / 0.5 = 0.80
        // Center the barrel so it extends forward from the pivot:
        //   local Z = (0.40/2) / 0.5 = 0.40
        var barrel = GameObject.CreatePrimitive(PrimitiveType.Cube);
        barrel.name = "Barrel";
        barrel.transform.SetParent(_pivot.transform, worldPositionStays: false);
        barrel.transform.localPosition = new Vector3(0f, 0f, 0.40f);
        barrel.transform.localScale    = new Vector3(0.16f, 0.08f, 0.80f);

        var barrelRend  = barrel.GetComponent<Renderer>();
        var barrelMat   = barrelRend.material;
        barrelMat.color = new Color(0.18f, 0.18f, 0.18f);
        Destroy(barrel.GetComponent<Collider>());   // no collision on barrel

        // Muzzle flash light — parented to barrel, at its local +Z tip.
        var flashGO = new GameObject("MuzzleFlash");
        flashGO.transform.SetParent(barrel.transform, worldPositionStays: false);
        flashGO.transform.localPosition = new Vector3(0f, 0f, 0.5f);   // tip of barrel cube

        _muzzleLight           = flashGO.AddComponent<Light>();
        _muzzleLight.type      = LightType.Point;
        _muzzleLight.color     = new Color(1.0f, 0.75f, 0.25f);
        _muzzleLight.intensity = 3f;
        _muzzleLight.range     = 5f;
        _muzzleLight.enabled   = false;
    }
}
