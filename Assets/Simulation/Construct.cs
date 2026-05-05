// A construct is a group of physically connected blocks that act as one unit.
// Power networks, vehicle physics, and logistics will all belong to a construct,
// not to individual blocks.

using System.Collections.Generic;

public class Construct
{
    public int       Id;
    public List<int> BlockIds = new List<int>();
}
