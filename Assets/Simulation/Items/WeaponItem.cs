// A weapon that can be equipped to the Primary or Secondary gear slot.
//
// WeaponItems are NOT belt-transportable — they are player-held equipment and
// are never produced by or transported through the factory logistics system.
//
// Melee weapons  : Kind = Melee,  AmmoItemId = null,  MagazineSize = 0
// Ranged weapons : Kind = Ranged, AmmoItemId = item id of the ammo consumed per shot
//
// Instances are defined in ItemCatalogue.

public class WeaponItem : Item
{
    // ── Properties ────────────────────────────────────────────────────────────

    // Melee or Ranged — determines which combat controller logic applies.
    // Named "Kind" to avoid shadowing the WeaponType enum in expression context.
    public WeaponType Kind;

    // Damage dealt per successful hit or projectile impact.
    public float Damage;

    // Attacks (or shots) per second at base speed — no modifiers applied here.
    public float AttackRate;

    // Effective range in metres.
    //   Melee  : typically 1.5 – 2.5 m
    //   Ranged : typically 30 – 200 m
    public float Range;

    // ItemId of the ammo item consumed per shot (e.g. "rifle_round").
    // Null for melee weapons.
    public string AmmoItemId;

    // Number of shots before a reload is required.
    // 0 for melee weapons (no magazine).
    public int MagazineSize;

    // Time in seconds to complete a reload animation.
    // 0 for melee weapons.
    public float ReloadTime;

    // ── Constructor ───────────────────────────────────────────────────────────

    public WeaponItem()
    {
        Category     = ItemCategory.Weapon;
        MaxStackSize = 1;       // weapons never stack in inventory
        Weight       = 2.5f;
    }

    // ── Overrides ─────────────────────────────────────────────────────────────

    // Weapons may only be placed in Primary or Secondary gear slots.
    public override bool CanEquipToSlot(GearSlot slot)
        => slot == GearSlot.Primary || slot == GearSlot.Secondary;

    // Weapons are not transported by belts or produced by machines.
    public override bool IsBeltTransportable => false;

    public override string TypeLabel => Kind == WeaponType.Melee ? "Melee Weapon"
                                                                  : "Ranged Weapon";

    // ── Helpers ───────────────────────────────────────────────────────────────

    public bool IsRanged => Kind == WeaponType.Ranged;
    public bool IsMelee  => Kind == WeaponType.Melee;
    public bool UsesAmmo => AmmoItemId != null;
}
