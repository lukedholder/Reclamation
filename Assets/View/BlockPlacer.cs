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
        var hit       = _raycaster.Hit;
        var blockView = hit.collider.GetComponent<BlockView>();

        Block         block;
        ConstructView constructView;
        Vector3       localPos;
        int           blockRot;   // rotation used only for the size-swap on construct grids

        if (blockView != null)
        {
            blockRot = _hotbar.RotationSteps;
            PlaceOnBlock(def, blockRot, hit, blockView, out block, out constructView, out localPos);
        }
        else
        {
            blockRot = 0;   // terrain: block has no rotation within its construct
            PlaceOnTerrain(def, hit, out block, out constructView, out localPos);
        }

        bool swap = (blockRot & 1) == 1;
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

    private void PlaceOnTerrain(BlockDefinition def, RaycastHit hit,
                                out Block block, out ConstructView constructView, out Vector3 localPos)
    {
        // Terrain blocks always use rot=0 within their construct.
        // The construct's Transform is rotated to carry the Y-orientation.
        var simConstruct = Sim.CreateConstruct();
        block = Sim.PlaceBlock(def, simConstruct.Id, GridPos.Zero, 0);

        // Block centre in world space: ray hit point on XZ, half-height up.
        Vector3 blockWorldCenter = new Vector3(
            hit.point.x,
            hit.point.y + def.SizeY * 0.5f * CellSize,
            hit.point.z);

        // The block's centre in construct-local space (rot=0, so no swap).
        localPos = new Vector3(
            def.SizeX * 0.5f * CellSize,
            def.SizeY * 0.5f * CellSize,
            def.SizeZ * 0.5f * CellSize);

        // Place and orient the construct so that (construct.rotation * localPos) lands on blockWorldCenter.
        var cvGO = new GameObject();
        cvGO.transform.rotation = Quaternion.Euler(0f, _hotbar.RotationAngleY, 0f);
        cvGO.transform.position = blockWorldCenter - cvGO.transform.rotation * localPos;
        constructView = cvGO.AddComponent<ConstructView>();
        constructView.Init(simConstruct);
    }

    private void PlaceOnBlock(BlockDefinition def, int rot, RaycastHit hit, BlockView blockView,
                              out Block block, out ConstructView constructView, out Vector3 localPos)
    {
        var hitBlock = blockView.Block;
        var hitDef   = hitBlock.Definition;

        constructView = blockView.GetComponentInParent<ConstructView>();
        Transform constructTF = constructView.transform;

        // Work in construct-local space so rotated constructs are handled correctly.
        Vector3 localNorm = constructTF.InverseTransformDirection(hit.normal);
        float   lnx = Mathf.Abs(localNorm.x), lny = Mathf.Abs(localNorm.y), lnz = Mathf.Abs(localNorm.z);
        int     axis = (lnx >= lny && lnx >= lnz) ? 0 : (lny >= lnz ? 1 : 2);
        float   sign = axis == 0 ? localNorm.x : (axis == 1 ? localNorm.y : localNorm.z);

        int[] hGrid = { hitBlock.GridPosition.X, hitBlock.GridPosition.Y, hitBlock.GridPosition.Z };
        int[] hSize = { hitDef.SizeX,            hitDef.SizeY,            hitDef.SizeZ            };

        // Effective new-block cell size after rotation (X/Z swap on odd rotSteps).
        bool swap = (rot & 1) == 1;
        int[] nSize = { swap ? def.SizeZ : def.SizeX, def.SizeY, swap ? def.SizeX : def.SizeZ };

        // Hit point in construct-local space — used to snap the free axes.
        Vector3 localHit = constructTF.InverseTransformPoint(hit.point);

        // Constrained axis: new block butts up against the hit face.
        int constrained = sign > 0
            ? hGrid[axis] + hSize[axis]
            : hGrid[axis] - nSize[axis];

        // Free axes: snap to the nearest grid cell.
        int Snap(int i) => Mathf.RoundToInt(localHit[i] / CellSize - nSize[i] * 0.5f);

        int gx, gy, gz;
        if      (axis == 0) { gx = constrained; gy = Snap(1); gz = Snap(2); }
        else if (axis == 1) { gx = Snap(0); gy = constrained; gz = Snap(2); }
        else                { gx = Snap(0); gy = Snap(1);     gz = constrained; }

        block = Sim.PlaceBlock(def, hitBlock.ConstructId, new GridPos(gx, gy, gz), rot);

        // Local position relative to the construct origin — matches the ghost exactly.
        localPos = new Vector3(
            (gx + nSize[0] * 0.5f) * CellSize,
            (gy + nSize[1] * 0.5f) * CellSize,
            (gz + nSize[2] * 0.5f) * CellSize);
    }
}
