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
        var hit = _raycaster.Hit;

        _cube.SetActive(true);
        _cube.transform.position = new Vector3(
            hit.point.x,
            hit.point.y + def.SizeY * 0.5f * CellSize,
            hit.point.z);
        _cube.transform.localScale = new Vector3(
            def.SizeX * CellSize,
            def.SizeY * CellSize,
            def.SizeZ * CellSize);
    }
}
