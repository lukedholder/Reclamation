// Root GameObject for every construct in the scene.
//
// Position of this GameObject = the construct's world-space grid origin
// (the world point that corresponds to GridPos(0,0,0)).
// All block GameObjects (BlockView) are direct children.
//
// Setup: created automatically by BlockPlacer when a new construct is started.
//        Never move this transform after creation — block local positions depend on it.

using UnityEngine;

public class ConstructView : MonoBehaviour
{
    public Construct Construct { get; private set; }

    public void Init(Construct construct)
    {
        Construct = construct;
        name      = $"Construct_{construct.Id}";
    }

    // Adds or removes a Rigidbody depending on whether the construct is grounded.
    // Grounded (IsAnchored) → static collider, no Rigidbody.
    // Floating              → Rigidbody with gravity enabled, mass from block sum.
    public void ApplyPhysics()
    {
        var rb = GetComponent<Rigidbody>();

        if (Construct.IsAnchored)
        {
            if (rb != null) Destroy(rb);
            return;
        }

        float totalMass = 0f;
        var sim = GameManager.Instance?.Simulation;
        if (sim != null)
        {
            foreach (int bid in Construct.BlockIds)
                if (sim.Blocks.ById.TryGetValue(bid, out var b))
                    totalMass += b.Definition.Mass;
        }
        if (totalMass <= 0f) totalMass = 1f;

        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
        rb.mass                   = totalMass;
        rb.isKinematic            = false;
        rb.useGravity             = true;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.drag                   = 0.1f;
        rb.angularDrag            = 0.5f;
    }
}
