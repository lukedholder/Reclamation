// Attached to every placed block cube.
// Stores a reference back to the simulation Block so that raycasts can identify
// what they hit and retrieve block data.
// Tints the cube each frame to reflect the block's current OperationMode.

using UnityEngine;

public class BlockView : MonoBehaviour
{
    public Block Block { get; private set; }

    private Material _material;

    public void Init(Block block)
    {
        Block = block;
        // Force a per-instance material so tinting is independent per cube.
        _material = GetComponent<Renderer>().material;
    }

    private void Update()
    {
        _material.color = StateColor();
    }

    // ── Colour mapping ────────────────────────────────────────────────────────

    private static readonly Color ColStructural = Color.white;
    private static readonly Color ColIdle       = new Color(0.60f, 0.60f, 0.60f); // grey
    private static readonly Color ColOperating  = new Color(0.20f, 0.85f, 0.20f); // green
    private static readonly Color ColWaiting    = new Color(0.85f, 0.85f, 0.20f); // yellow
    private static readonly Color ColNoPower    = new Color(0.85f, 0.20f, 0.20f); // red

    private Color StateColor()
    {
        if (Block.MachineState == null) return ColStructural;

        return Block.MachineState.Mode switch
        {
            OperationMode.Operating => ColOperating,
            OperationMode.Waiting   => ColWaiting,
            OperationMode.NoPower   => ColNoPower,
            OperationMode.Idle      => ColIdle,
            _                       => ColStructural,
        };
    }
}
