// Runtime state for a Battery block (FunctionalType.Battery).
// PowerSystem charges this when the network has a surplus and discharges it during a deficit.
// StoredKJ is clamped to [0, CapacityKJ] after every tick.
// Values are initialised from BatteryParams when the block is registered with a network.

public class BatteryState
{
    // Current stored energy in kilojoules. Written by PowerSystem each tick.
    public float StoredKJ;

    // Maximum storable energy in kilojoules. Copied from BatteryParams at registration.
    public float CapacityKJ;

    // Maximum rate at which this battery absorbs energy during a network surplus (kW).
    public float MaxChargeRateKW;

    // Maximum rate at which this battery supplies energy to cover a network deficit (kW).
    public float MaxDischargeRateKW;

    // Charge level as a fraction [0, 1]. Used by the UI battery indicator.
    public float ChargePercent => CapacityKJ > 0f ? StoredKJ / CapacityKJ : 0f;
}
