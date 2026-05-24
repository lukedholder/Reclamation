// Removes a placed block on right-click.
//
// Setup: attach to the Player GameObject alongside Raycaster.
//
// Controls:
//   Right-click   — dismantle the block the crosshair is aimed at

using UnityEngine;

public class BlockDismantler : MonoBehaviour
{
    private Raycaster  _raycaster;
    private Simulation _sim;

    private void Awake()
    {
        _raycaster = GetComponent<Raycaster>();
    }

    private void Start()
    {
        _sim = GameManager.Instance.Simulation;
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(1) && _raycaster.HasHit)
            TryDismantle();
    }

    private void TryDismantle()
    {
        var blockView = _raycaster.Hit.collider.GetComponent<BlockView>();
        if (blockView == null) return;

        _sim.RemoveBlock(blockView.Block.Id);

        var constructView = blockView.GetComponentInParent<ConstructView>();
        if (constructView != null && constructView.transform.childCount == 1)
        {
            // Last block — destroy the whole construct GO (takes the block with it).
            Destroy(constructView.gameObject);
        }
        else
        {
            Destroy(blockView.gameObject);
        }
    }
}
