// Places blocks in the simulation on left-click and spawns a matching cube as a
// temporary visual stand-in (proper block rendering comes in a later step).
//
// Setup: attach to the Player GameObject alongside PlayerController,
//        Hotbar, and Raycaster.
//
// Controls:
//   Left-click   — place selected block where the ray hits

using UnityEngine;

public class BlockPlacer : MonoBehaviour
{
    // 1 grid cell = 0.5 m.  Matches the value used in the simulation docs.
    // Will move to a shared ViewConstants file once the view layer grows.
    private const float CellSize = 0.5f;

    private Raycaster  _raycaster;
    private Hotbar     _hotbar;
    private Simulation _sim;

    private void Awake()
    {
        _raycaster = GetComponent<Raycaster>();
        _hotbar    = GetComponent<Hotbar>();
    }

    private void Start()
    {
        _sim = GameManager.Instance.Simulation;
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0) && _raycaster.HasHit)
            TryPlace();
    }

    private void TryPlace()
    {
        var def       = _hotbar.SelectedDefinition;
        var hit       = _raycaster.Hit;
        var blockView = hit.collider.GetComponent<BlockView>();

        Block   block;
        Vector3 worldCenter;

        if (blockView != null)
            PlaceOnBlock(def, hit, blockView, out block, out worldCenter);
        else
            PlaceOnTerrain(def, hit, out block, out worldCenter);

        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = def.DisplayName;
        go.transform.position   = worldCenter;
        go.transform.localScale = new Vector3(
            def.SizeX * CellSize,
            def.SizeY * CellSize,
            def.SizeZ * CellSize);
        go.AddComponent<BlockView>().Init(block);
    }

    // ── Placement modes ───────────────────────────────────────────────────────

    private void PlaceOnTerrain(BlockDefinition def, RaycastHit hit,
                                out Block block, out Vector3 worldCenter)
    {
        var construct = _sim.CreateConstruct();
        block = _sim.PlaceBlock(def, construct.Id, GridPos.Zero);
        worldCenter = new Vector3(
            hit.point.x,
            hit.point.y + def.SizeY * 0.5f * CellSize,
            hit.point.z);
    }

    private void PlaceOnBlock(BlockDefinition def, RaycastHit hit, BlockView blockView,
                              out Block block, out Vector3 worldCenter)
    {
        var hitBlock = blockView.Block;
        var hitDef   = hitBlock.Definition;

        // Identify the dominant axis of the hit normal.
        var   n  = hit.normal;
        float ax = Mathf.Abs(n.x), ay = Mathf.Abs(n.y), az = Mathf.Abs(n.z);
        int   axis = (ax >= ay && ax >= az) ? 0 : (ay >= az ? 1 : 2);
        float sign = axis == 0 ? n.x : (axis == 1 ? n.y : n.z);

        int[] hGrid = { hitBlock.GridPosition.X, hitBlock.GridPosition.Y, hitBlock.GridPosition.Z };
        int[] hSize = { hitDef.SizeX,            hitDef.SizeY,            hitDef.SizeZ            };
        int[] nSize = { def.SizeX,               def.SizeY,               def.SizeZ               };

        // Derive the construct's world origin from the hit block's position and GridPos.
        Vector3 hitCenter = blockView.transform.position;
        Vector3 origin = hitCenter - new Vector3(
            (hGrid[0] + hSize[0] * 0.5f) * CellSize,
            (hGrid[1] + hSize[1] * 0.5f) * CellSize,
            (hGrid[2] + hSize[2] * 0.5f) * CellSize);

        // Hit point in construct-local space, used to snap the free axes.
        Vector3 local = hit.point - origin;

        // Constrained axis: new block butts up against the hit face.
        int constrained = sign > 0
            ? hGrid[axis] + hSize[axis]
            : hGrid[axis] - nSize[axis];

        // Free axes: snap to the nearest grid cell.
        int Snap(int i) => Mathf.RoundToInt(local[i] / CellSize - nSize[i] * 0.5f);

        int gx, gy, gz;
        if      (axis == 0) { gx = constrained; gy = Snap(1);       gz = Snap(2); }
        else if (axis == 1) { gx = Snap(0);     gy = constrained;   gz = Snap(2); }
        else                { gx = Snap(0);     gy = Snap(1);       gz = constrained; }

        block = _sim.PlaceBlock(def, hitBlock.ConstructId, new GridPos(gx, gy, gz));

        // World centre — same formula as the ghost so they always agree.
        worldCenter = origin + new Vector3(
            (gx + def.SizeX * 0.5f) * CellSize,
            (gy + def.SizeY * 0.5f) * CellSize,
            (gz + def.SizeZ * 0.5f) * CellSize);
    }
}
