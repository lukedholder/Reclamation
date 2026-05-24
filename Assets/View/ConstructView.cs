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
}
