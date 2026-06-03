// Broad classification of an item type.
// Used by Item subclass constructors to set defaults and by the inventory UI
// to group and colour items.

public enum ItemCategory
{
    Resource,    // naturally occurring raw materials  (ores, coal, wood)
    Component,   // refined materials and crafted parts (plates, gears, wire, boards)
    Weapon,      // combat equipment                   (melee and ranged weapons)
}
