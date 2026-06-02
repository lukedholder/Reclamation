// A fixed-capacity ordered collection of item stacks used as the internal
// input or output buffer of a functional machine block.
//
// Slots are configured by SetRecipe() — one slot per recipe input/output.
// LogisticsSystem moves items between buffers and conveyor belts via inserters.
// MachineSystem reads and writes quantities directly during production.
//
// AcceptsAny mode (used by StorageChestMachine):
//   When AcceptsAny = true, Add() dynamically creates a new slot for any unknown
//   item type as long as Slots.Count < MaxSlots.  All other operations are unchanged.

using System.Collections.Generic;

public class ItemBuffer
{
    // Maximum number of distinct item stacks this buffer can hold.
    public int MaxSlots;

    // How many of one item type a single slot can hold.
    public int CapacityPerSlot = 100;

    // When true, Add() creates slots on demand for unknown item types (up to MaxSlots).
    // Used by StorageChestMachine input staging.  False for all recipe-based machines.
    public bool AcceptsAny = false;

    // Current contents. Each entry is one stack (item type + quantity).
    // Indexed by recipe input/output order. Configured by SetRecipe.
    public List<ItemStack> Slots = new List<ItemStack>();

    // How many of a given item are currently buffered (0 if not present).
    public int CountOf(string itemId)
    {
        foreach (var s in Slots)
            if (s.ItemId == itemId) return s.Quantity;
        return 0;
    }

    // Prepare slot at index to accept a specific item type.
    // Existing quantity is preserved if the item matches; reset to 0 if item changes.
    // Called by BaseMachine.SetRecipe() to configure the buffer layout.
    public void ConfigureSlot(int index, string itemId)
    {
        while (Slots.Count <= index) Slots.Add(new ItemStack("", 0));
        var existing = Slots[index];
        Slots[index] = new ItemStack(itemId, existing.ItemId == itemId ? existing.Quantity : 0);
    }

    // Add items to the first slot configured for this item type.
    // Returns the number actually added (capped by CapacityPerSlot).
    // In AcceptsAny mode a new slot is created for unknown item types when
    // Slots.Count < MaxSlots; otherwise returns 0 (all slots occupied by other types).
    public int Add(string itemId, int quantity)
    {
        for (int i = 0; i < Slots.Count; i++)
        {
            if (Slots[i].ItemId != itemId) continue;
            int space = CapacityPerSlot - Slots[i].Quantity;
            int added = quantity < space ? quantity : space;
            if (added > 0) Slots[i] = new ItemStack(itemId, Slots[i].Quantity + added);
            return added;
        }

        // AcceptsAny: dynamically create a slot for this unknown item type.
        if (AcceptsAny && Slots.Count < MaxSlots)
        {
            int added = quantity < CapacityPerSlot ? quantity : CapacityPerSlot;
            Slots.Add(new ItemStack(itemId, added));
            return added;
        }

        return 0; // no slot configured for this item
    }

    // True if any slot holds at least one item.
    public bool HasItems()
    {
        foreach (var s in Slots) if (s.Quantity > 0) return true;
        return false;
    }

    // Remove exactly `quantity` of `itemId`. Returns true if the removal succeeded.
    // Does nothing and returns false if the slot doesn't have enough.
    public bool TryRemove(string itemId, int quantity)
    {
        for (int i = 0; i < Slots.Count; i++)
        {
            if (Slots[i].ItemId != itemId) continue;
            if (Slots[i].Quantity < quantity) return false;
            Slots[i] = new ItemStack(itemId, Slots[i].Quantity - quantity);
            return true;
        }
        return false;
    }

    // Zero the quantity in every slot (preserves slot configuration).
    public void Clear()
    {
        for (int i = 0; i < Slots.Count; i++)
            Slots[i] = new ItemStack(Slots[i].ItemId, 0);
    }
}
