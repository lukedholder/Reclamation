// The core simulation. GameManager creates one of these and calls Update
// 20 times per second. All game logic will live in here, separate from Unity.

using System.Collections.Generic;

public class Simulation
{
    public int Tick { get; private set; }

    public List<Block> Blocks { get; private set; } = new List<Block>();

    private int _nextId = 1;

    public void Update()    // 20Hz
    {
        Tick++;
    }

    public Block PlaceBlock(int x, int y, int z)
    {
        var block = new Block
        {
            Id = _nextId++,
            X  = x,
            Y  = y,
            Z  = z,
        };

        Blocks.Add(block);
        return block;
    }
}