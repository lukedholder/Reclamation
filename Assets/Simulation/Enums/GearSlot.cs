// Equipment slots available on the player character.
// Shared between the simulation layer (Item.CanEquipToSlot) and the view layer
// (PlayerInventory gear panel).  Defined here so Item subclasses can reference it
// without creating a dependency on the View layer.

public enum GearSlot
{
    Head      = 0,
    Chest     = 1,
    Legs      = 2,
    Feet      = 3,
    Primary   = 4,   // primary weapon
    Secondary = 5,   // secondary weapon / off-hand
    Utility   = 6,   // consumable or tool slot
}
