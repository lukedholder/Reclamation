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

    // Always reads the current Simulation so a save-load reset doesn't leave a stale reference.
    private Simulation Sim => GameManager.Instance.Simulation;

    private void Awake()
    {
        _raycaster = GetComponent<Raycaster>();
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

        var constructView = blockView.GetComponentInParent<ConstructView>();

        // Remove from simulation. May return new construct IDs if removal splits connectivity.
        var splitIds = Sim.RemoveBlock(blockView.Block.Id);

        if (constructView != null && constructView.transform.childCount == 1)
        {
            // Last block — destroy the whole construct GO (takes the block with it).
            Destroy(constructView.gameObject);
            return;
        }

        Destroy(blockView.gameObject);

        if (splitIds.Count > 0)
            HandleSplit(constructView, splitIds);
    }

    // When a removal severs a construct into pieces, the sim creates new construct IDs
    // for the disconnected components and updates each Block.ConstructId accordingly.
    // The view creates a new ConstructView for each piece and re-parents its block cubes.
    // Both the original and new ConstructViews share the same world origin — block GridPos
    // values are relative to that origin and are never changed by a split.
    private void HandleSplit(ConstructView originalCV, System.Collections.Generic.List<int> splitIds)
    {
        Vector3 origin = originalCV.transform.position;

        foreach (int newId in splitIds)
        {
            var cvGO = new GameObject();
            cvGO.transform.position = origin;
            var newCV = cvGO.AddComponent<ConstructView>();
            newCV.Init(Sim.Constructs.ById[newId]);

            // Collect children whose Block now belongs to this new construct.
            var toReparent = new System.Collections.Generic.List<Transform>();
            foreach (Transform child in originalCV.transform)
            {
                var bv = child.GetComponent<BlockView>();
                if (bv != null && bv.Block.ConstructId == newId)
                    toReparent.Add(child);
            }

            foreach (var t in toReparent)
                t.SetParent(newCV.transform, worldPositionStays: true);
        }
    }
}
