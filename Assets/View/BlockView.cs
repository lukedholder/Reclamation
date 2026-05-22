// Attached to every placed block cube.
// Stores a reference back to the simulation Block so that raycasts
// can identify what they hit and retrieve the block's data.

using UnityEngine;

public class BlockView : MonoBehaviour
{
    public Block Block { get; private set; }

    public void Init(Block block)
    {
        Block = block;
    }
}
