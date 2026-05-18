// A group of physically connected blocks that act as one simulation unit.
// Power networks, vehicle physics, and logistics networks all belong to a construct —
// not to individual blocks.
//
// A construct is created when any isolated block is placed and destroyed when its
// last block is removed. Constructs split when a removed block severs connectivity,
// and merge when a new block bridges two previously separate constructs.
//
// UNITY SCENE STRUCTURE:
//   Each construct maps to one parent GameObject (ConstructView MonoBehaviour).
//   Every block in the construct is a child GameObject (BlockView MonoBehaviour).
//   The Rigidbody lives on the parent — Unity automatically compounds all child
//   colliders into one physics body. World position and rotation are entirely owned
//   by the parent's Transform; this class holds no world-space coordinates.
//
// Type is recalculated every tick by ConstructSystem.Reclassify() — it is not persisted
// because it derives entirely from the current block composition.

using System.Collections.Generic;

public class Construct
{
    // --- Identity ---

    // Unique ID assigned sequentially by Simulation. Never reused.
    public int Id;

    // Optional player-given name (e.g. "Iron Mine Alpha"). Empty by default.
    public string Name = "";


    // --- Classification ---

    // Derived from block composition. Recalculated each tick by ConstructSystem.Reclassify().
    // Determines which simulation systems apply and what the HUD displays.
    public ConstructType Type = ConstructType.Structure;

    // True if this construct contains at least one Foundation block touching terrain.
    // ConstructView uses this to set Rigidbody.isKinematic = IsAnchored,
    // preventing anchored bases from falling or being pushed by physics.
    public bool IsAnchored;

    // True if the construct has a Seat, at least one Propulsion block, and a power source.
    // Gates the ability for a player to enter pilot mode on this construct.
    public bool IsPilotable;


    // --- Block Membership ---

    // IDs of all blocks belonging to this construct.
    // Maintained by ConstructSystem — do not modify directly from outside that system.
    public List<int> BlockIds = new List<int>();

    // IDs of all power networks owned by this construct.
    // A single construct may have multiple independent networks.
    // Maintained by PowerNetworkManager.
    public List<int> PowerNetworkIds = new List<int>();
}
