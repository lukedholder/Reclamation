// A fixed-capacity ordered collection of item stacks used as the internal
// input or output buffer of a functional machine block.
//
// Distinct from player inventory — it is not directly accessible to the player.
// LogisticsSystem moves items between buffers and conveyor belts via inserter blocks.
// Buffer size (MaxSlots) is set from the block's functional parameters at registration.

using System.Collections.Generic;

public class ItemBuffer
{
    // Maximum number of distinct item stacks this buffer can hold.
    public int MaxSlots;

    // Current contents. Each entry is one stack (item type + quantity).
    public List<ItemStack> Slots = new List<ItemStack>();
}
