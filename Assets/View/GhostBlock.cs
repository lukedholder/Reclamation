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

        _cube.SetActive(true);
        _cube.transform.position = ComputeGhostCenter(def);
        _cube.transform.localScale = new Vector3(
            def.SizeX * CellSize,
            def.SizeY * CellSize,
            def.SizeZ * CellSize);
    }

    private Vector3 ComputeGhostCenter(BlockDefinition def)
    {
        var hit       = _raycaster.Hit;
        var blockView = hit.collider.GetComponent<BlockView>();

        // ── Terrain hit ───────────────────────────────────────────────────────
        if (blockView == null)
        {
            return new Vector3(
                hit.point.x,
                hit.point.y + def.SizeY * 0.5f * CellSize,
                hit.point.z);
        }

        // ── Block face hit ────────────────────────────────────────────────────
        // Identify the dominant axis of the hit normal.
        var    n  = hit.normal;
        float  ax = Mathf.Abs(n.x), ay = Mathf.Abs(n.y), az = Mathf.Abs(n.z);
        int    axis = (ax >= ay && ax >= az) ? 0 : (ay >= az ? 1 : 2);

        var   hitDef    = blockView.Block.Definition;
        float hitHalf   = new float[] { hitDef.SizeX, hitDef.SizeY, hitDef.SizeZ }[axis] * 0.5f * CellSize;
        float newHalf   = new float[] { def.SizeX,    def.SizeY,    def.SizeZ    }[axis] * 0.5f * CellSize;
        float normalDir = axis == 0 ? n.x : (axis == 1 ? n.y : n.z);

        // World-space corner of the hit block (minimum XYZ), used as the snap origin.
        Vector3 hitCenter = blockView.transform.position;
        Vector3 hitCorner = hitCenter - new Vector3(
            hitDef.SizeX * 0.5f * CellSize,
            hitDef.SizeY * 0.5f * CellSize,
            hitDef.SizeZ * 0.5f * CellSize);

        // Hit point in hit-block-local space.
        Vector3 local = hit.point - hitCorner;

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
                    Snap(hitCorner.y, local.y, def.SizeY),
                    Snap(hitCorner.z, local.z, def.SizeZ)),
            1 => new Vector3(
                    Snap(hitCorner.x, local.x, def.SizeX),
                    Constrain(hitCenter.y, Mathf.Sign(normalDir)),
                    Snap(hitCorner.z, local.z, def.SizeZ)),
            _ => new Vector3(
                    Snap(hitCorner.x, local.x, def.SizeX),
                    Snap(hitCorner.y, local.y, def.SizeY),
                    Constrain(hitCenter.z, Mathf.Sign(normalDir))),
        };
    }
}
