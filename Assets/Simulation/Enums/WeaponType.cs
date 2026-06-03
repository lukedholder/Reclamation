// Determines the combat behaviour model of a WeaponItem.

public enum WeaponType
{
    Melee,   // close-range; no ammo; attack rate limited by swing speed
    Ranged,  // projectile; consumes AmmoItemId per shot; supports magazine and reload
}
