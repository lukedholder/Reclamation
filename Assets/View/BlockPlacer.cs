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
        var def = _hotbar.SelectedDefinition;
        var hit = _raycaster.Hit;

        // Each terrain click starts a new construct at GridPos.Zero.
        // Placing on existing blocks (face-snap into the same construct)
        // comes in a later step once blocks have proper view objects.
        var construct = _sim.CreateConstruct();
        _sim.PlaceBlock(def, construct.Id, GridPos.Zero);

        // Spawn a primitive cube as a temporary visual.
        // The block's bottom face sits on the terrain at the hit point.
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = $"{def.DisplayName}";
        go.transform.position = new Vector3(
            hit.point.x,
            hit.point.y + def.SizeY * 0.5f * CellSize,
            hit.point.z);
        go.transform.localScale = new Vector3(
            def.SizeX * CellSize,
            def.SizeY * CellSize,
            def.SizeZ * CellSize);
    }
}
