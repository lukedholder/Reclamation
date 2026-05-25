// Shows a slightly oversized transparent cube around whichever block the crosshair
// is aimed at, giving visual feedback before a dismantle or interaction.
//
// Setup: attach to the Player GameObject alongside Raycaster.
//        Assign a semi-transparent material to the Highlight Material slot in the Inspector.

using UnityEngine;
using static ViewConstants;

public class BlockHighlight : MonoBehaviour
{
    // Extra metres added to each axis so the highlight sits just outside the block.
    private const float Bias = 0.025f;

    [SerializeField] private Material _highlightMaterial;

    private Raycaster  _raycaster;
    private Hotbar     _hotbar;
    private GameObject _cube;

    private void Awake()
    {
        _raycaster = GetComponent<Raycaster>();
        _hotbar    = GetComponent<Hotbar>();

        _cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        _cube.name = "BlockHighlight";
        Destroy(_cube.GetComponent<Collider>());
        _cube.GetComponent<Renderer>().sharedMaterial = _highlightMaterial;
        _cube.SetActive(false);
    }

    private void Update()
    {
        // Wire/Belt tools draw their own highlights — suppress this one.
        if (!_raycaster.HasHit || _hotbar.IsToolMode)
        {
            _cube.SetActive(false);
            return;
        }

        var blockView = _raycaster.Hit.collider.GetComponent<BlockView>();
        if (blockView == null)
        {
            _cube.SetActive(false);
            return;
        }

        _cube.SetActive(true);
        _cube.transform.position   = blockView.transform.position;
        _cube.transform.localScale = blockView.transform.localScale + Vector3.one * Bias;
    }
}
