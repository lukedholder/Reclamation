// A conveyor belt modelled as a shift register.
//
// PHYSICAL MODEL (matches Satisfactory behaviour):
//   - The belt has LengthInCells slots, one per 0.5 m cell.
//   - Slot 0 = entry end (source side). Slot LengthInCells-1 = exit end (dest side).
//   - All items advance together by one slot on each belt step.
//   - An item that cannot advance (blocked exit, or gap ahead) stays in place —
//     items behind it also stall, backing up the belt naturally.
//   - Transit time for an item entering an empty belt = LengthInCells belt steps.
//
// TIMING:
//   ThroughputPerMin = 60  →  1 item/sec  →  belt steps every 20 simulation ticks (20 Hz).
//   A 4-cell belt at 60/min: max 4 items in flight, 4 s transit time.
//
// STEP ORDER (per step):
//   1. Try to deliver exit slot to destination machine input buffer.
//   2. Advance items from exit-1 down to entry (items only move into an empty slot ahead).
//   3. Pull one item from source machine output buffer into the entry slot if it is empty.

public class BeltSegment
{
    public int   Id;

    public int   SourceBlockId;
    public int   SourcePortIndex;   // PortDefinition.Index on source block (Output port); == OutputBuffer slot index
    public int   DestBlockId;
    public int   DestPortIndex;     // PortDefinition.Index on dest block (Input port); == InputBuffer slot index

    public float ThroughputPerMin;

    // Number of item slots = floor(splinePathLength / CellSize).
    // NOT the straight-line grid distance between machines. Belts follow curved
    // splines through port anchors and belt-stand waypoints, so the path can be
    // arbitrarily longer than a direct route. The view layer computes the arc
    // length and passes it to LogisticsSystem.Connect(); the simulation only
    // cares about the resulting slot count.
    public int   LengthInCells;

    // Item slots.  null = empty.  non-null = item ID occupying that slot.
    // Length == LengthInCells.  Slot 0 = entry, Slot[Length-1] = exit.
    public string[] Slots;

    // Items currently on the belt (for GUI / stats).
    public int ItemCount
    {
        get
        {
            int n = 0;
            foreach (var s in Slots) if (s != null) n++;
            return n;
        }
    }

    // [0,1) progress toward the next belt step.
    public float StepProgress { get; private set; }

    public void Init() => Slots = new string[LengthInCells];

    // Called once per simulation tick by LogisticsSystem.
    public void Tick(float tickDelta, BlockTable blocks)
    {
        StepProgress += ThroughputPerMin / 60f * tickDelta;
        if (StepProgress < 1f) return;
        StepProgress -= 1f;
        Step(blocks);
    }

    private void Step(BlockTable blocks)
    {
        int exit = LengthInCells - 1;

        // 1. Deliver exit slot to destination (clears the slot so items behind can advance).
        if (Slots[exit] != null && TryDeliver(blocks, Slots[exit]))
            Slots[exit] = null;

        // 2. Advance items toward the exit (process from exit-side so nothing moves twice).
        for (int i = exit - 1; i >= 0; i--)
        {
            if (Slots[i] != null && Slots[i + 1] == null)
            {
                Slots[i + 1] = Slots[i];
                Slots[i]     = null;
            }
        }

        // 3. Pull a new item from source into the entry slot if it is empty.
        if (Slots[0] == null)
            TryPull(blocks);
    }

    // Try to push the exit-slot item into the destination machine's input buffer.
    private bool TryDeliver(BlockTable blocks, string itemId)
    {
        if (!blocks.ById.TryGetValue(DestBlockId, out var dest)) return false;
        var buf = dest.MachineState?.InputBuffer;
        if (buf == null || DestPortIndex >= buf.Slots.Count) return false;

        var slot = buf.Slots[DestPortIndex];
        if (slot.ItemId != itemId)                              return false; // wrong item type
        if (buf.CountOf(itemId) >= buf.CapacityPerSlot)         return false; // dest full

        buf.Add(itemId, 1);
        return true;
    }

    // Try to pull one item from the source machine's output buffer into the entry slot.
    private void TryPull(BlockTable blocks)
    {
        if (!blocks.ById.TryGetValue(SourceBlockId, out var source)) return;
        var buf = source.MachineState?.OutputBuffer;
        if (buf == null || SourcePortIndex >= buf.Slots.Count) return;

        var slot = buf.Slots[SourcePortIndex];
        if (slot.Quantity <= 0) return;

        if (buf.TryRemove(slot.ItemId, 1))
            Slots[0] = slot.ItemId;
    }
}
