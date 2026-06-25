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

    public readonly BlockTable      Blocks         = new BlockTable();
    public readonly ConstructTable  Constructs     = new ConstructTable();
    public readonly PowerSystem     Power          = new PowerSystem();
    public readonly MachineSystem   Machines       = new MachineSystem();
    public readonly LogisticsSystem Logistics      = new LogisticsSystem();
    public readonly EnemySystem     Enemies        = new EnemySystem();
    public readonly ConstructSystem ConstructTypes = new ConstructSystem();

    private int _nextBlockId     = 1;
    private int _nextConstructId = 1;

    public void Update()
    {
        Tick++;
        Power.Tick(MachineSystem.TickDelta, Blocks);        // 1. set OperatingRate on all consumers
        Machines.Tick();                                    // 2. advance production at throttled rate
        Logistics.Tick(MachineSystem.TickDelta, Blocks);   // 3. move items between machines
        Enemies.Tick(MachineSystem.TickDelta);             // 4. enemy AI tick (placeholder)
        ConstructTypes.Tick(Constructs, Blocks);           // 5. classify constructs (vehicle / base / structure)
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
    // Returns the placed Block, or null if the construct doesn't exist or the target
    // cells are already occupied (overlap rejected via the occupancy map).
    public Block PlaceBlock(BlockDefinition definition, int constructId, GridPos gridPos,
                            int rotSteps = 0, bool isOnTerrain = false)
    {
        if (!Constructs.ById.TryGetValue(constructId, out var construct)) return null;
        if (!AreCellsFree(construct, definition, gridPos, rotSteps))      return null;

        var block = new Block
        {
            Id            = _nextBlockId++,
            Definition    = definition,
            ConstructId   = constructId,
            GridPosition  = gridPos,
            RotationSteps = rotSteps,
            Durability    = definition.MaxDurability,
            IsOnTerrain   = isOnTerrain,
        };

        Blocks.ById[block.Id] = block;
        IndexBlockToConstruct(block.Id, constructId);
        Power.Register(block);
        Machines.Register(block);

        construct.BlockIds.Add(block.Id);
        RegisterCells(construct, block);

        RecalcAnchor(constructId);

        // Construct merging is deferred to the docking system — MergeInto remains
        // available for the docking implementation.

        return block;
    }

    // True if the target footprint is unoccupied — exposed so the view can validate
    // a ghost before committing a placement.
    public bool CanPlace(int constructId, BlockDefinition def, GridPos gridPos, int rotSteps = 0)
        => Constructs.ById.TryGetValue(constructId, out var c) && AreCellsFree(c, def, gridPos, rotSteps);

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
        UnregisterCells(construct, block);

        if (construct.BlockIds.Count == 0)
        {
            Constructs.ById.Remove(constructId);
            return newConstructIds;
        }

        // Flood-fill to find connected components, using the occupancy map for O(1)
        // neighbour lookups (O(total cells) overall instead of O(blocks²)).
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
                int cur = queue.Dequeue();
                component.Add(cur);
                EnqueueAdjacentBlocks(construct, Blocks.ById[cur], cur, remaining, queue);
            }

            components.Add(component);
        }

        // First component keeps the original construct.
        // Each additional component becomes a new construct, taking its cells with it.
        for (int i = 1; i < components.Count; i++)
        {
            var split = CreateConstruct();
            newConstructIds.Add(split.Id);

            foreach (var bid in components[i])
            {
                var b = Blocks.ById[bid];
                UnregisterCells(construct, b);
                RemoveFromConstructIndex(bid, constructId);
                construct.BlockIds.Remove(bid);

                b.ConstructId = split.Id;
                split.BlockIds.Add(bid);
                IndexBlockToConstruct(bid, split.Id);
                RegisterCells(split, b);
            }
        }

        // Recalculate which constructs still touch terrain after the removal/split.
        RecalcAnchor(constructId);
        foreach (int id in newConstructIds)
            RecalcAnchor(id);

        return newConstructIds;
    }

    // Recalculates Construct.IsAnchored by scanning whether any member block is on terrain.
    // Call after any block removal or split.
    public void RecalcAnchor(int constructId)
    {
        if (!Constructs.ById.TryGetValue(constructId, out var construct)) return;

        // A manually-released construct (a flying vehicle) is never anchored,
        // regardless of any remaining terrain-touching blocks.
        if (construct.ManualUnanchored)
        {
            construct.IsAnchored = false;
            return;
        }

        bool anchored = false;
        foreach (int bid in construct.BlockIds)
        {
            if (Blocks.ById.TryGetValue(bid, out var b) && b.IsOnTerrain)
            {
                anchored = true;
                break;
            }
        }
        construct.IsAnchored = anchored;
    }

    // ── Occupancy map ──────────────────────────────────────────────────────────

    // Footprint dimensions after the X/Z swap that an odd rotation step applies.
    private static void GetFootprint(BlockDefinition def, int rotSteps, out int sx, out int sy, out int sz)
    {
        bool swap = (rotSteps & 1) == 1;
        sx = swap ? def.SizeZ : def.SizeX;
        sy = def.SizeY;
        sz = swap ? def.SizeX : def.SizeZ;
    }

    // True if every cell the block would occupy is currently empty in this construct.
    private static bool AreCellsFree(Construct c, BlockDefinition def, GridPos g, int rotSteps)
    {
        GetFootprint(def, rotSteps, out int sx, out int sy, out int sz);
        for (int x = 0; x < sx; x++)
        for (int y = 0; y < sy; y++)
        for (int z = 0; z < sz; z++)
            if (c.Cells.ContainsKey(new GridPos(g.X + x, g.Y + y, g.Z + z)))
                return false;
        return true;
    }

    private static void RegisterCells(Construct c, Block b)
    {
        GetFootprint(b.Definition, b.RotationSteps, out int sx, out int sy, out int sz);
        var g = b.GridPosition;
        for (int x = 0; x < sx; x++)
        for (int y = 0; y < sy; y++)
        for (int z = 0; z < sz; z++)
            c.Cells[new GridPos(g.X + x, g.Y + y, g.Z + z)] = b.Id;
    }

    private static void UnregisterCells(Construct c, Block b)
    {
        GetFootprint(b.Definition, b.RotationSteps, out int sx, out int sy, out int sz);
        var g = b.GridPosition;
        for (int x = 0; x < sx; x++)
        for (int y = 0; y < sy; y++)
        for (int z = 0; z < sz; z++)
        {
            var cell = new GridPos(g.X + x, g.Y + y, g.Z + z);
            if (c.Cells.TryGetValue(cell, out int id) && id == b.Id)
                c.Cells.Remove(cell);
        }
    }

    // Enqueues every not-yet-visited block face-adjacent to `block`, found via the
    // occupancy map: scan the block's cells, probe the six neighbours of each.
    private static void EnqueueAdjacentBlocks(Construct c, Block block, int blockId,
                                              HashSet<int> remaining, Queue<int> queue)
    {
        GetFootprint(block.Definition, block.RotationSteps, out int sx, out int sy, out int sz);
        var g = block.GridPosition;
        for (int x = 0; x < sx; x++)
        for (int y = 0; y < sy; y++)
        for (int z = 0; z < sz; z++)
        {
            var cell = new GridPos(g.X + x, g.Y + y, g.Z + z);
            foreach (var n in GridPos.Neighbours)
            {
                if (c.Cells.TryGetValue(cell + n, out int nbId)
                    && nbId != blockId && remaining.Remove(nbId))
                    queue.Enqueue(nbId);
            }
        }
    }

    private void MergeInto(int survivorId, int dissolvedId)
    {
        var survivor = Constructs.ById[survivorId];
        var dissolved = Constructs.ById[dissolvedId];

        foreach (var bid in dissolved.BlockIds)
        {
            var b = Blocks.ById[bid];
            UnregisterCells(dissolved, b);
            b.ConstructId = survivorId;
            survivor.BlockIds.Add(bid);
            RemoveFromConstructIndex(bid, dissolvedId);
            IndexBlockToConstruct(bid, survivorId);
            RegisterCells(survivor, b);
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
