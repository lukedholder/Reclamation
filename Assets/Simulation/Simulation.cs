// Core simulation. All positions are integer GridPos local to each construct.
// No Unity dependencies — pure C#.
//
// Placement flow:
//   Terrain click  → CreateConstruct() then PlaceBlock(def, constructId, GridPos.Zero, rot)
//   Block-on-block → PlaceBlock(def, existingConstructId, computedGridPos, rot)
//
// RemoveBlock returns any new construct IDs produced by a connectivity split,
// so the view layer can spawn ConstructView objects for them.

using System.Collections.Generic;

public class Simulation
{
    public int Tick { get; private set; }

    public readonly BlockTable      Blocks     = new BlockTable();
    public readonly ConstructTable  Constructs = new ConstructTable();
    public readonly PowerSystem     Power      = new PowerSystem();
    public readonly MachineSystem   Machines   = new MachineSystem();
    public readonly LogisticsSystem Logistics  = new LogisticsSystem();

    private int _nextBlockId     = 1;
    private int _nextConstructId = 1;

    public void Update()
    {
        Tick++;
        Power.Tick(MachineSystem.TickDelta, Blocks);        // 1. set OperatingRate on all consumers
        Machines.Tick();                                    // 2. advance production at throttled rate
        Logistics.Tick(MachineSystem.TickDelta, Blocks);   // 3. move items between machines
    }

    // Creates an empty construct and registers it. Called before placing the first block.
    public Construct CreateConstruct()
    {
        var c = new Construct { Id = _nextConstructId++ };
        Constructs.ById[c.Id] = c;
        return c;
    }

    // Places a block into an existing construct at a grid-space position.
    // GridPos is the block's minimum corner (bottom-left-back) in the construct's local grid.
    // Merges any other constructs the new block touches into the target construct.
    // Returns the placed Block.
    public Block PlaceBlock(BlockDefinition definition, int constructId, GridPos gridPos, int rotSteps = 0)
    {
        var block = new Block
        {
            Id           = _nextBlockId++,
            Definition   = definition,
            ConstructId  = constructId,
            GridPosition = gridPos,
            RotationSteps = rotSteps,
            Durability   = definition.MaxDurability,
        };

        Blocks.ById[block.Id] = block;
        IndexBlockToConstruct(block.Id, constructId);
        Power.Register(block);
        Machines.Register(block);

        var construct = Constructs.ById[constructId];
        construct.BlockIds.Add(block.Id);

        // Construct merging is deferred to the docking system.
        // AreAdjacent compares GridPos values, which are construct-local — calling it
        // across constructs would compare unrelated coordinate spaces and trigger false
        // merges.  MergeInto remains available for the docking implementation.

        return block;
    }

    // Removes a block. Returns IDs of any new constructs created by a split
    // (empty if the construct was destroyed or remained connected).
    public List<int> RemoveBlock(int blockId)
    {
        var newConstructIds = new List<int>();

        if (!Blocks.ById.TryGetValue(blockId, out var block)) return newConstructIds;
        Power.Unregister(block);
        Machines.Unregister(blockId);
        Logistics.DisconnectBlock(blockId);

        int constructId = block.ConstructId;
        var construct   = Constructs.ById[constructId];

        Blocks.ById.Remove(blockId);
        RemoveFromConstructIndex(blockId, constructId);
        construct.BlockIds.Remove(blockId);

        if (construct.BlockIds.Count == 0)
        {
            Constructs.ById.Remove(constructId);
            return newConstructIds;
        }

        // Flood-fill to find connected components
        var remaining  = new HashSet<int>(construct.BlockIds);
        var components = new List<HashSet<int>>();

        while (remaining.Count > 0)
        {
            var component = new HashSet<int>();
            var queue     = new Queue<int>();

            int startId = -1;
            foreach (var bid in remaining) { startId = bid; break; }
            remaining.Remove(startId);
            queue.Enqueue(startId);

            while (queue.Count > 0)
            {
                int cur      = queue.Dequeue();
                var curBlock = Blocks.ById[cur];
                component.Add(cur);

                foreach (var other in Blocks.ById.Values)
                {
                    if (!remaining.Contains(other.Id)) continue;
                    if (!AreAdjacent(curBlock, other)) continue;
                    remaining.Remove(other.Id);
                    queue.Enqueue(other.Id);
                }
            }

            components.Add(component);
        }

        // First component keeps the original construct.
        // Each additional component becomes a new construct.
        for (int i = 1; i < components.Count; i++)
        {
            var split = CreateConstruct();
            newConstructIds.Add(split.Id);

            foreach (var bid in components[i])
            {
                var b = Blocks.ById[bid];
                RemoveFromConstructIndex(bid, constructId);
                construct.BlockIds.Remove(bid);

                b.ConstructId = split.Id;
                split.BlockIds.Add(bid);
                IndexBlockToConstruct(bid, split.Id);
            }
        }

        return newConstructIds;
    }

    // Two blocks (minimum-corner GridPos, integer sizes) are face-adjacent when
    // they share exactly one face: touching on one axis, overlapping on the other two.
    private static bool AreAdjacent(Block a, Block b)
    {
        GridPos ap = a.GridPosition;
        GridPos bp = b.GridPosition;
        int asx = a.Definition.SizeX, asy = a.Definition.SizeY, asz = a.Definition.SizeZ;
        int bsx = b.Definition.SizeX, bsy = b.Definition.SizeY, bsz = b.Definition.SizeZ;

        bool touchX = ap.X + asx == bp.X || bp.X + bsx == ap.X;
        bool touchY = ap.Y + asy == bp.Y || bp.Y + bsy == ap.Y;
        bool touchZ = ap.Z + asz == bp.Z || bp.Z + bsz == ap.Z;

        bool overlapX = ap.X < bp.X + bsx && bp.X < ap.X + asx;
        bool overlapY = ap.Y < bp.Y + bsy && bp.Y < ap.Y + asy;
        bool overlapZ = ap.Z < bp.Z + bsz && bp.Z < ap.Z + asz;

        return (touchX && overlapY && overlapZ)
            || (touchY && overlapX && overlapZ)
            || (touchZ && overlapX && overlapY);
    }

    private void MergeInto(int survivorId, int dissolvedId)
    {
        var survivor = Constructs.ById[survivorId];
        var dissolved = Constructs.ById[dissolvedId];

        foreach (var bid in dissolved.BlockIds)
        {
            var b = Blocks.ById[bid];
            b.ConstructId = survivorId;
            survivor.BlockIds.Add(bid);
            RemoveFromConstructIndex(bid, dissolvedId);
            IndexBlockToConstruct(bid, survivorId);
        }

        Constructs.ById.Remove(dissolvedId);
    }

    private void IndexBlockToConstruct(int blockId, int constructId)
    {
        if (!Blocks.ByConstruct.ContainsKey(constructId))
            Blocks.ByConstruct[constructId] = new List<int>();
        Blocks.ByConstruct[constructId].Add(blockId);
    }

    private void RemoveFromConstructIndex(int blockId, int constructId)
    {
        if (Blocks.ByConstruct.TryGetValue(constructId, out var list))
            list.Remove(blockId);
    }
}
