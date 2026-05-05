// The core simulation. Tracks all placed blocks and constructs.
// When a block is placed, it either starts a new construct or joins/merges
// existing ones. When removed, flood-fill checks if the remaining blocks
// are still connected, splitting into separate constructs if not.

using System.Collections.Generic;

public class Simulation
{
    public int Tick { get; private set; }

    public List<Block>     Blocks     = new List<Block>();
    public List<Construct> Constructs = new List<Construct>();

    private int _nextBlockId     = 1;
    private int _nextConstructId = 1;

    // How large one grid cell is in world units.
    // Used when checking whether two blocks are touching.
    private const float CellSize = 0.5f;

    public void Update()
    {
        Tick++;
    }

    public Block PlaceBlock(BlockDefinition definition,
                            float x, float y, float z,
                            float rotX = 0f, float rotY = 0f, float rotZ = 0f)
    {
        var block = new Block
        {
            Id         = _nextBlockId++,
            Definition = definition,
            X          = x,
            Y          = y,
            Z          = z,
            RotationX  = rotX,
            RotationY  = rotY,
            RotationZ  = rotZ,
        };

        Blocks.Add(block);

        // Find which constructs the new block is touching
        var touchedIds = new HashSet<int>();
        foreach (var other in Blocks)
        {
            if (other.Id == block.Id) continue;
            if (AreAdjacent(block, other))
                touchedIds.Add(other.ConstructId);
        }

        if (touchedIds.Count == 0)
        {
            // Not touching anything — start a new construct
            var c = new Construct { Id = _nextConstructId++ };
            c.BlockIds.Add(block.Id);
            block.ConstructId = c.Id;
            Constructs.Add(c);
        }
        else
        {
            // Touching one or more constructs — merge them all into one
            int survivorId = -1;
            foreach (var id in touchedIds) { survivorId = id; break; }
            var survivor = Constructs.Find(c => c.Id == survivorId);

            foreach (var id in touchedIds)
            {
                if (id == survivorId) continue;
                var other = Constructs.Find(c => c.Id == id);
                foreach (var bid in other.BlockIds)
                {
                    var b = Blocks.Find(bl => bl.Id == bid);
                    b.ConstructId = survivorId;
                    survivor.BlockIds.Add(bid);
                }
                Constructs.Remove(other);
            }

            block.ConstructId = survivorId;
            survivor.BlockIds.Add(block.Id);
        }

        return block;
    }

    public bool RemoveBlock(int id)
    {
        var block = Blocks.Find(b => b.Id == id);
        if (block == null) return false;

        var construct = Constructs.Find(c => c.Id == block.ConstructId);

        Blocks.Remove(block);
        construct.BlockIds.Remove(id);

        if (construct.BlockIds.Count == 0)
        {
            Constructs.Remove(construct);
            return true;
        }

        // Flood-fill to check if the remaining blocks are still connected.
        // If removing this block split the construct, we get multiple components.
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
                var current      = queue.Dequeue();
                var currentBlock = Blocks.Find(b => b.Id == current);
                component.Add(current);

                foreach (var other in Blocks)
                {
                    if (!remaining.Contains(other.Id)) continue;
                    if (!AreAdjacent(currentBlock, other)) continue;
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
            var newConstruct = new Construct { Id = _nextConstructId++ };
            foreach (var bid in components[i])
            {
                var b = Blocks.Find(bl => bl.Id == bid);
                b.ConstructId = newConstruct.Id;
                construct.BlockIds.Remove(bid);
                newConstruct.BlockIds.Add(bid);
            }
            Constructs.Add(newConstruct);
        }

        return true;
    }

    // Two blocks are adjacent if their bounding boxes are touching —
    // gap of ~0 in one axis and overlapping in the other two.
    private bool AreAdjacent(Block a, Block b)
    {
        const float epsilon = 0.01f;

        float aHX = a.Definition.SizeX * CellSize * 0.5f;
        float aHY = a.Definition.SizeY * CellSize * 0.5f;
        float aHZ = a.Definition.SizeZ * CellSize * 0.5f;

        float bHX = b.Definition.SizeX * CellSize * 0.5f;
        float bHY = b.Definition.SizeY * CellSize * 0.5f;
        float bHZ = b.Definition.SizeZ * CellSize * 0.5f;

        float dx = System.Math.Abs(a.X - b.X) - (aHX + bHX);
        float dy = System.Math.Abs(a.Y - b.Y) - (aHY + bHY);
        float dz = System.Math.Abs(a.Z - b.Z) - (aHZ + bHZ);

        bool touchX = dx > -epsilon && dx <= epsilon;
        bool touchY = dy > -epsilon && dy <= epsilon;
        bool touchZ = dz > -epsilon && dz <= epsilon;

        bool gapX = dx < epsilon;
        bool gapY = dy < epsilon;
        bool gapZ = dz < epsilon;

        return (touchX && gapY && gapZ)
            || (touchY && gapX && gapZ)
            || (touchZ && gapX && gapY);
    }
}
