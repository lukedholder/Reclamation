using System.Collections.Generic;

public class Simulation
{
    public int Tick { get; private set; }

    public List<Block> Blocks { get; private set; } = new List<Block>();

    private int _nextId = 1;

    public void Update()
    {
        Tick++;
    }

    public Block PlaceBlock(float x, float y, float z, float rotationY = 0f)
    {
        var block = new Block
        {
            Id        = _nextId++,
            X         = x,
            Y         = y,
            Z         = z,
            RotationY = rotationY,
        };

        Blocks.Add(block);
        return block;
    }

    public bool RemoveBlock(int id)
    {
        return Blocks.RemoveAll(b => b.Id == id) > 0;
    }
}