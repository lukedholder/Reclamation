// Shows a transparent cube at the raycast hit position, sized to match
// the currently selected block in the Hotbar.
//
// Setup: attach to the Player GameObject alongside Raycaster and Hotbar.
//        Assign a semi-transparent material to the Ghost Material slot in the Inspector.

using UnityEngine;
using static ViewConstants;

public class GhostBlock : MonoBehaviour
{

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
        if (!_raycaster.HasHit || _hotbar.NoBlockActive)
        {
            _cube.SetActive(false);
            return;
        }

        var  def       = _hotbar.SelectedDefinition;
        var  blockView = _raycaster.Hit.collider.GetComponent<BlockView>();
        bool onBlock   = blockView != null;

        _cube.SetActive(true);

        if (onBlock)
        {
            // Construct grid: snap to 90° steps, swap X/Z on odd steps.
            // Ghost orientation = construct's world rotation × the player's relative 90° step.
            int rot          = _hotbar.RotationSteps;
            var (sx, sy, sz) = EffectiveSize(def, rot);
            _cube.transform.localScale = new Vector3(sx * CellSize, sy * CellSize, sz * CellSize);
            _cube.transform.rotation   = blockView.transform.parent.rotation
                                         * Quaternion.Euler(0f, rot * 90f, 0f);
        }
        else
        {
            // Terrain: arbitrary 15° rotation — apply as transform Y, no size swap.
            _cube.transform.localScale = new Vector3(def.SizeX * CellSize, def.SizeY * CellSize, def.SizeZ * CellSize);
            _cube.transform.rotation   = Quaternion.Euler(0f, _hotbar.RotationAngleY, 0f);
        }

        // ComputeGhostCenter uses RotationSteps for block-face snapping; terrain path ignores it.
        _cube.transform.position = ComputeGhostCenter(def, _hotbar.RotationSteps);
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
            return new Vector3(hit.point.x, hit.point.y + sy * 0.5f * CellSize, hit.point.z);

        // ── Block face hit — all maths in construct-local space ───────────────
        // Using InverseTransformPoint/Direction means this works for rotated constructs
        // (e.g. a construct placed on terrain at a 15° or 30° Y-angle).
        Transform constructTF = blockView.transform.parent;

        // Face normal in construct-local space — identifies which face was hit.
        Vector3 localNorm = constructTF.InverseTransformDirection(hit.normal);
        float   lnx = Mathf.Abs(localNorm.x), lny = Mathf.Abs(localNorm.y), lnz = Mathf.Abs(localNorm.z);
        int     axis = (lnx >= lny && lnx >= lnz) ? 0 : (lny >= lnz ? 1 : 2);
        float   normalDir = axis == 0 ? localNorm.x : (axis == 1 ? localNorm.y : localNorm.z);

        var   hitDef = blockView.Block.Definition;
        int[] hGrid  = { blockView.Block.GridPosition.X, blockView.Block.GridPosition.Y, blockView.Block.GridPosition.Z };
        int[] hSize  = { hitDef.SizeX, hitDef.SizeY, hitDef.SizeZ };
        int[] nSize  = { sx, sy, sz };
        float hitHalf = hSize[axis] * 0.5f * CellSize;
        float newHalf = nSize[axis] * 0.5f * CellSize;

        // Hit block's centre and hit point, both in construct-local space.
        Vector3 localHitCenter = new Vector3(
            (hGrid[0] + hSize[0] * 0.5f) * CellSize,
            (hGrid[1] + hSize[1] * 0.5f) * CellSize,
            (hGrid[2] + hSize[2] * 0.5f) * CellSize);
        Vector3 localHit = constructTF.InverseTransformPoint(hit.point);

        float Constrain(float center, float sign) => center + sign * (hitHalf + newHalf);
        float SnapLocal(float coord, int cells)
        {
            int g = Mathf.RoundToInt(coord / CellSize - cells * 0.5f);
            return (g + cells * 0.5f) * CellSize;
        }

        Vector3 localResult = axis switch
        {
            0 => new Vector3(Constrain(localHitCenter.x, Mathf.Sign(normalDir)), SnapLocal(localHit.y, sy), SnapLocal(localHit.z, sz)),
            1 => new Vector3(SnapLocal(localHit.x, sx), Constrain(localHitCenter.y, Mathf.Sign(normalDir)), SnapLocal(localHit.z, sz)),
            _ => new Vector3(SnapLocal(localHit.x, sx), SnapLocal(localHit.y, sy), Constrain(localHitCenter.z, Mathf.Sign(normalDir))),
        };

        return constructTF.TransformPoint(localResult);
    }
}
