// Passive item buffer — accepts any item type on its input belt, stores them
// in an internal dictionary, and exposes them on its output belt.
//
// No recipe, no power draw. Internal capacity: TotalCapacity items total.
//
// Belt interface:
//   BeltSegment.TryDeliver → InputBuffer.Add(itemId, 1)
//     Works because InputBuffer.AcceptsAny = true — slots are created on demand.
//   BeltSegment.TryPull   → OutputBuffer.Slots[0]
//     Chest Tick() keeps slot 0 of OutputBuffer topped-up from _storage so the
//     output belt always finds something to pull.
//
// Backpressure:
//   When _storage is full, Tick() stops draining InputBuffer.
//   InputBuffer staging slots fill to CapacityPerSlot.
//   TryDeliver sees CountOf(itemId) >= CapacityPerSlot → stops delivering.
//   Input belt stalls, propagating back to the upstream machine. ✓
//
// Output item selection:
//   The output slot keeps its current item type while any of that type remains
//   in _storage. Only switches to a new type when the current type is exhausted
//   and the output slot has been fully pulled empty by the belt.
//
// Tick order (Power → Machines → Logistics):
//   Machines runs before Logistics in each simulation step.
//   Chest refills OutputBuffer → belt pulls from it — all in one tick. ✓

using System.Collections.Generic;

public class StorageChestMachine : BaseMachine
{
    // Maximum total items stored across all item types.
    public const int TotalCapacity = 2000;

    // Small output staging buffer — keeps this many items ready for the output belt.
    private const int OutputStageSize = 10;

    // Internal storage: itemId → quantity.
    private readonly Dictionary<string, int> _storage = new Dictionary<string, int>();

    // ── Public accessors (for HUD / debug / UI) ──────────────────────────────

    public int TotalStored
    {
        get { int n = 0; foreach (var v in _storage.Values) n += v; return n; }
    }

    public IReadOnlyDictionary<string, int> Contents => _storage;

    // ── Player interaction (drag-drop UI) ─────────────────────────────────────

    /// <summary>
    /// Removes up to <paramref name="qty"/> of <paramref name="itemId"/> from storage.
    /// Returns what was actually taken (quantity may be less if fewer were stored).
    /// </summary>
    public ItemStack TakeFromStorage(string itemId, int qty)
    {
        if (!_storage.TryGetValue(itemId, out int stored) || stored <= 0)
            return default;

        int taken     = qty < stored ? qty : stored;
        int remaining = stored - taken;
        if (remaining <= 0) _storage.Remove(itemId);
        else                _storage[itemId] = remaining;

        return new ItemStack(itemId, taken);
    }

    /// <summary>
    /// Adds up to <paramref name="qty"/> of <paramref name="itemId"/> to storage.
    /// Returns the number actually added (capped by remaining capacity).
    /// </summary>
    public int GiveToStorage(string itemId, int qty)
    {
        if (qty <= 0) return 0;

        int space = TotalCapacity - TotalStored;
        if (space <= 0) return 0;

        int added = qty < space ? qty : space;
        _storage.TryGetValue(itemId, out int existing);
        _storage[itemId] = existing + added;
        return added;
    }

    // ── Construction ──────────────────────────────────────────────────────────

    public StorageChestMachine(Block block) : base(block)
    {
        // Input staging: any item type, up to 50 distinct types, 100 items/type.
        // Chest Tick() drains this each tick so the slots rarely fill.
        State.InputBuffer.AcceptsAny      = true;
        State.InputBuffer.MaxSlots        = 50;
        State.InputBuffer.CapacityPerSlot = 100;

        // Output staging: one slot — configured and topped-up by Tick().
        // CapacityPerSlot controls how many items are pre-staged for the belt.
        State.OutputBuffer.MaxSlots        = 1;
        State.OutputBuffer.CapacityPerSlot = OutputStageSize;
    }

    // Chests are not configurable with recipes.
    public override bool SetRecipe(Recipe recipe) => false;

    // ── Tick ──────────────────────────────────────────────────────────────────

    public override void Tick(float tickDelta)
    {
        DrainInputToStorage();
        RefillOutputFromStorage();
        UpdateMode();
    }

    // ── Step 1: InputBuffer → _storage ───────────────────────────────────────

    private void DrainInputToStorage()
    {
        int space = TotalCapacity - TotalStored;
        if (space <= 0) return;

        // Iterate a snapshot count; InputBuffer.Slots can only shrink in AcceptsAny
        // mode if we ever remove slots (we don't here), so iterating by index is safe.
        for (int i = 0; i < State.InputBuffer.Slots.Count; i++)
        {
            if (space <= 0) break;
            var slot = State.InputBuffer.Slots[i];
            if (string.IsNullOrEmpty(slot.ItemId) || slot.Quantity <= 0) continue;

            int toMove = slot.Quantity < space ? slot.Quantity : space;
            _storage.TryGetValue(slot.ItemId, out int existing);
            _storage[slot.ItemId] = existing + toMove;
            State.InputBuffer.TryRemove(slot.ItemId, toMove);
            space -= toMove;
        }
    }

    // ── Step 2: _storage → OutputBuffer slot 0 ───────────────────────────────

    private void RefillOutputFromStorage()
    {
        string currentItemId = State.OutputBuffer.Slots.Count > 0
                             ? State.OutputBuffer.Slots[0].ItemId : null;
        int    currentQty    = State.OutputBuffer.Slots.Count > 0
                             ? State.OutputBuffer.Slots[0].Quantity : 0;

        if (!string.IsNullOrEmpty(currentItemId) && currentQty > 0)
        {
            // Output slot has items — keep the same type and top it up.
            if (_storage.TryGetValue(currentItemId, out int available) && available > 0)
            {
                int topUp = OutputStageSize - currentQty;
                topUp = topUp < available ? topUp : available;
                if (topUp > 0)
                {
                    State.OutputBuffer.Add(currentItemId, topUp);
                    _storage[currentItemId] -= topUp;
                }
            }
        }
        else
        {
            // Output slot is empty — pick the first available item type from _storage.
            // Iterating a Dictionary is not guaranteed order, but in practice items
            // come out in insertion order on modern .NET, which is FIFO enough for V1.
            foreach (var kv in _storage)
            {
                if (kv.Value <= 0) continue;

                int toStage = kv.Value < OutputStageSize ? kv.Value : OutputStageSize;
                State.OutputBuffer.ConfigureSlot(0, kv.Key);
                State.OutputBuffer.Add(kv.Key, toStage);
                _storage[kv.Key] -= toStage;
                break;
            }
        }
    }

    // ── Step 3: update visible mode ───────────────────────────────────────────

    private void UpdateMode()
    {
        bool hasContent = TotalStored > 0
                       || State.InputBuffer.HasItems()
                       || State.OutputBuffer.HasItems();

        State.Mode = hasContent ? OperationMode.Operating : OperationMode.Idle;
    }
}
