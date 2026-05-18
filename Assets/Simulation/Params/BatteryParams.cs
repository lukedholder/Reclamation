// Static configuration for Battery blocks (FunctionalType.Battery).
// These values are copied into a BatteryState instance when the block is first
// registered with a power network. They do not change at runtime.
// V1 batteries: Small Battery (500 kJ / 50 kW charge / 100 kW discharge).
//               Large Battery (2000 kJ / 150 kW charge / 300 kW discharge).

public class BatteryParams : IFunctionalParams
{
    // Total storable energy in kilojoules.
    public float CapacityKJ;

    // Maximum rate at which this battery absorbs energy from a network surplus (kW).
    public float MaxChargeRateKW;

    // Maximum rate at which this battery supplies energy to cover a network deficit (kW).
    public float MaxDischargeRateKW;
}
