// Shows a transparent cube at the raycast hit position, sized to match
// the currently selected block in the Hotbar.
//
// Setup: attach to the Player GameObject alongside Raycaster and Hotbar.
//        Assign a semi-transparent material to the Ghost Material slot in the Inspector.

using UnityEngine;

public class GhostBlock : MonoBehaviour
{
    private const float CellSize = 0.5f;

    [SerializeField] private Material _ghostMaterial;

    private Raycaster  _raycaster;
    private Hotbar     _hotbar;
    private GameObject _cube;

    private void Awake()
    {
        _raycaster = GetComponent<Raycaster>();
        _hotbar    = GetComponent<Hotbar>();

        _cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        _cube.name = "GhostBlock";

        // Remove the collider so the raycast passes straight through the ghost.
        Destroy(_cube.GetComponent<Collider>());

        _cube.GetComponent<Renderer>().sharedMaterial = _ghostMaterial;
        _cube.SetActive(false);
    }

    private void Update()
    {
        if (!_raycaster.HasHit)
        {
            _cube.SetActive(false);
            return;
        }

        var def = _hotbar.SelectedDefinition;
        int rot = _hotbar.RotationSteps;
        var (sx, sy, sz) = EffectiveSize(def, rot);

        _cube.SetActive(true);
        _cube.transform.position   = ComputeGhostCenter(def, rot);
        _cube.transform.localScale = new Vector3(sx * CellSize, sy * CellSize, sz * CellSize);
    }

    // Returns the block's world-space cell footprint after applying rotSteps 90° Y-axis turns.
    // Y never changes; X and Z swap on odd rotations (1 = 90°, 3 = 270°).
    private static (int sx, int sy, int sz) EffectiveSize(BlockDefinition def, int rot)
    {
        bool swap = (rot & 1) == 1;
        return swap ? (def.SizeZ, def.SizeY, def.SizeX)
                    : (def.SizeX, def.SizeY, def.SizeZ);
    }

    private Vector3 ComputeGhostCenter(BlockDefinition def, int rot)
    {
        var hit       = _raycaster.Hit;
        var blockView = hit.collider.GetComponent<BlockView>();

        var (sx, sy, sz) = EffectiveSize(def, rot);

        // ── Terrain hit ───────────────────────────────────────────────────────
        if (blockView == null)
        {
            return new Vector3(
                hit.point.x,
                hit.point.y + sy * 0.5f * CellSize,
                hit.point.z);
        }

        // ── Block face hit ────────────────────────────────────────────────────
        // Identify the dominant axis of the hit normal.
        var    n  = hit.normal;
        float  ax = Mathf.Abs(n.x), ay = Mathf.Abs(n.y), az = Mathf.Abs(n.z);
        int    axis = (ax >= ay && ax >= az) ? 0 : (ay >= az ? 1 : 2);

        var   hitDef    = blockView.Block.Definition;
        float hitHalf   = new float[] { hitDef.SizeX, hitDef.SizeY, hitDef.SizeZ }[axis] * 0.5f * CellSize;
        float newHalf   = new float[] { sx,            sy,            sz            }[axis] * 0.5f * CellSize;
        float normalDir = axis == 0 ? n.x : (axis == 1 ? n.y : n.z);

        Vector3 hitCenter = blockView.transform.position;

        // Construct grid origin — the world point that maps to GridPos(0,0,0).
        Vector3 origin = blockView.transform.parent.position;

        // Hit point in construct-local space.
        Vector3 local = hit.point - origin;

        // For each axis: constrain the face axis, snap the two free axes to the grid.
        float Constrain(float center, float sign) => center + sign * (hitHalf + newHalf);
        float Snap(float originCoord, float localCoord, int cells)
        {
            int grid = Mathf.RoundToInt(localCoord / CellSize - cells * 0.5f);
            return originCoord + (grid + cells * 0.5f) * CellSize;
        }

        return axis switch
        {
            0 => new Vector3(
                    Constrain(hitCenter.x, Mathf.Sign(normalDir)),
                    Snap(origin.y, local.y, sy),
                    Snap(origin.z, local.z, sz)),
            1 => new Vector3(
                    Snap(origin.x, local.x, sx),
                    Constrain(hitCenter.y, Mathf.Sign(normalDir)),
                    Snap(origin.z, local.z, sz)),
            _ => new Vector3(
                    Snap(origin.x, local.x, sx),
                    Snap(origin.y, local.y, sy),
                    Constrain(hitCenter.z, Mathf.Sign(normalDir))),
        };
    }
}
