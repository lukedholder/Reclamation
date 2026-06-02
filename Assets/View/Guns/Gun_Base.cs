using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum FireMode
{
    Single,
    Auto
}

public abstract class Gun_Base : MonoBehaviour
{
    public FireMode fireMode;
    public int magSize;
    public int ammoInMag;
    public int reserveAmmo;
    public float fireRate;
    public float reloadTime;
    public float aimSpeed;

    public abstract void Shoot();
    public abstract void StopShoot();
    public abstract void Aim();
    public abstract void StopAim();
    public abstract void Reload();
    public abstract void PullOut();
    public abstract void Stow();
    public abstract void Melee();
    public abstract void ToggleFireMode();
}
