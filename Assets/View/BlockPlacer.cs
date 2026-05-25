// Places blocks in the simulation on left-click and spawns a matching cube as a
// temporary visual stand-in (proper block rendering comes in a later step).
//
// Setup: attach to the Player GameObject alongside PlayerController,
//        Hotbar, and Raycaster.
//
// Controls:
//   Left-click   — place selected block where the ray hits

using UnityEngine;
using static ViewConstants;

public class BlockPlacer : MonoBehaviour
{

    private Raycaster  _raycaster;
    private Hotbar     _hotbar;

    // Always reads the current Simulation so a save-load reset doesn't leave a stale reference.
    private Simulation Sim => GameManager.Instance.Simulation;

    private void Awake()
    {
        _raycaster = GetComponent<Raycaster>();
        _hotbar    = GetComponent<Hotbar>();
    }

    private void Update()
    {
        if (_hotbar.NoBlockActive) return;  // Wire tool or cancelled — no placement
        if (Input.GetMouseButtonDown(0) && _raycaster.HasHit)
            TryPlace();
    }

    private void TryPlace()
    {
        var def       = _hotbar.SelectedDefinition;
        int rot       = _hotbar.RotationSteps;
        var hit       = _raycaster.Hit;
        var blockView = hit.collider.GetComponent<BlockView>();

        Block         block;
        ConstructView constructView;
        Vector3       localPos;

        if (blockView != null)
            PlaceOnBlock(def, rot, hit, blockView, out block, out constructView, out localPos);
        else
            PlaceOnTerrain(def, rot, hit, out block, out constructView, out localPos);

        bool swap = (rot & 1) == 1;
        int sx = swap ? def.SizeZ : def.SizeX;
        int sy = def.SizeY;
        int sz = swap ? def.SizeX : def.SizeZ;

        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = def.DisplayName;
        go.transform.SetParent(constructView.transform, worldPositionStays: false);
        go.transform.localPosition = localPos;
        go.transform.localScale    = new Vector3(sx * CellSize, sy * CellSize, sz * CellSize);
        go.AddComponent<BlockView>().Init(block);
    }

    // ── Placement modes ───────────────────────────────────────────────────────

    private void PlaceOnTerrain(BlockDefinition def, int rot, RaycastHit hit,
                                out Block block, out ConstructView constructView, out Vector3 localPos)
    {
        bool swap = (rot & 1) == 1;
        int sx = swap ? def.SizeZ : def.SizeX;
        int sy = def.SizeY;
        int sz = swap ? def.SizeX : def.SizeZ;

        var simConstruct = Sim.CreateConstruct();
        block = Sim.PlaceBlock(def, simConstruct.Id, GridPos.Zero, rot);

        // Construct origin = world position of GridPos(0,0,0), which is the minimum
        // corner of the first block. The ghost centers the block on hit.point (X/Z),
        // so the origin is half a footprint behind that.
        var cvGO = new GameObject();
        cvGO.transform.position = new Vector3(
            hit.point.x - sx * 0.5f * CellSize,
            hit.point.y,
            hit.point.z - sz * 0.5f * CellSize);
        constructView = cvGO.AddComponent<ConstructView>();
        constructView.Init(simConstruct);

        // First block sits at GridPos(0,0,0) — its center is half a block up from the origin.
        localPos = new Vector3(sx * 0.5f * CellSize, sy * 0.5f * CellSize, sz * 0.5f * CellSize);
    }

    private void PlaceOnBlock(BlockDefinition def, int rot, RaycastHit hit, BlockView blockView,
                              out Block block, out ConstructView constructView, out Vector3 localPos)
    {
        var hitBlock = blockView.Block;
        var hitDef   = hitBlock.Definition;

        // Construct origin lives on the parent transform — no need to rederive it.
        constructView = blockView.GetComponentInParent<ConstructView>();
        Vector3 origin = constructView.transform.position;

        // Identify the dominant axis of the hit normal.
        var   n  = hit.normal;
        float ax = Mathf.Abs(n.x), ay = Mathf.Abs(n.y), az = Mathf.Abs(n.z);
        int   axis = (ax >= ay && ax >= az) ? 0 : (ay >= az ? 1 : 2);
        float sign = axis == 0 ? n.x : (axis == 1 ? n.y : n.z);

        int[] hGrid = { hitBlock.GridPosition.X, hitBlock.GridPosition.Y, hitBlock.GridPosition.Z };
        int[] hSize = { hitDef.SizeX,            hitDef.SizeY,            hitDef.SizeZ            };

        // Effective new-block cell size after rotation (X/Z swap on odd rotSteps).
        bool swap = (rot & 1) == 1;
        int[] nSize = { swap ? def.SizeZ : def.SizeX, def.SizeY, swap ? def.SizeX : def.SizeZ };

        // Hit point in construct-local space, used to snap the free axes.
        Vector3 local = hit.point - origin;

        // Constrained axis: new block butts up against the hit face.
        int constrained = sign > 0
            ? hGrid[axis] + hSize[axis]
            : hGrid[axis] - nSize[axis];

        // Free axes: snap to the nearest grid cell.
        int Snap(int i) => Mathf.RoundToInt(local[i] / CellSize - nSize[i] * 0.5f);

        int gx, gy, gz;
        if      (axis == 0) { gx = constrained; gy = Snap(1);     gz = Snap(2); }
        else if (axis == 1) { gx = Snap(0);     gy = constrained; gz = Snap(2); }
        else                { gx = Snap(0);     gy = Snap(1);     gz = constrained; }

        block = Sim.PlaceBlock(def, hitBlock.ConstructId, new GridPos(gx, gy, gz), rot);

        // Local position relative to the construct origin — matches the ghost exactly.
        localPos = new Vector3(
            (gx + nSize[0] * 0.5f) * CellSize,
            (gy + nSize[1] * 0.5f) * CellSize,
            (gz + nSize[2] * 0.5f) * CellSize);
    }
}
