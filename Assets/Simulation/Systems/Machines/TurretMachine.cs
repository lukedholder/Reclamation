// Auto-targeting turret machine.
// Consumes turret_round ammo from its input buffer; the view layer (TurretView)
// handles spatial targeting and calls TryFire() when an enemy is in range.
//
// Does NOT use the recipe system — no recipe selection in the UI.
// Input buffer is pre-configured with one ammo slot on construction.
//
// Fire rate is governed by a cooldown that decrements each simulation tick.
// TryFire() (called from TurretView at frame rate) safely returns false until
// the cooldown expires, then consumes one round and resets it.

public class TurretMachine : BaseMachine
{
    public const string AmmoId = "turret_round";

    private float _fireCooldown;

    private TurretParams TParams => _block.Definition.Params as TurretParams;

    /// <summary>Detection range in world units (from TurretParams, default 15).</summary>
    public float Range => TParams?.Range ?? 15f;

    /// <summary>Damage dealt per fired round (from TurretParams, default 25).</summary>
    public float Damage => TParams?.DamagePerShot ?? 25f;

    public TurretMachine(Block block) : base(block)
    {
        // One fixed input slot for ammo; turrets produce nothing.
        State.InputBuffer.MaxSlots = 1;
        State.InputBuffer.ConfigureSlot(0, AmmoId);
        State.OutputBuffer.MaxSlots = 0;
    }

    // Turrets don't use recipes — reject any attempt to assign one.
    public override bool SetRecipe(Recipe recipe) => false;

    public override void Tick(float tickDelta)
    {
        if (State.OperatingRate <= 0f)
        {
            State.Mode = OperationMode.NoPower;
            _fireCooldown = 0f;
            return;
        }

        if (_fireCooldown > 0f)
            _fireCooldown -= tickDelta;

        // Show "Waiting" (yellow) when the ammo belt is empty.
        State.Mode = State.InputBuffer.CountOf(AmmoId) > 0
            ? OperationMode.Operating
            : OperationMode.Waiting;
    }

    /// <summary>True when powered, loaded, and cooldown has elapsed.</summary>
    public bool CanFire =>
        _fireCooldown <= 0f &&
        State.OperatingRate > 0f &&
        State.InputBuffer.CountOf(AmmoId) > 0;

    /// <summary>
    /// Consumes one round and resets the fire cooldown.
    /// Returns false if not ready — check CanFire first or handle the false return.
    /// </summary>
    public bool TryFire()
    {
        if (!CanFire) return false;
        State.InputBuffer.TryRemove(AmmoId, 1);
        _fireCooldown = 1f / (TParams?.ShotsPerSecond ?? 1f);
        return true;
    }
}
