// A quantity of one item type.
// Used in construction costs, recipe inputs/outputs, inventory slots,
// and logistics buffer contents.
// ItemId is a string key matching an entry in the item data table (not a Unity object).

[System.Serializable]
public struct ItemStack
{
    public string ItemId;    // matches an entry in the item catalogue (e.g. "iron_plate")
    public int    Quantity;

    public ItemStack(string itemId, int quantity)
    {
        ItemId   = itemId;
        Quantity = quantity;
    }

    public bool IsEmpty => Quantity <= 0;

    public override string ToString() => $"{Quantity}x {ItemId}";
}
