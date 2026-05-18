// Runtime state for a Generator block (FunctionalType.Generator).
// PowerSystem reads CurrentOutputKW each tick when summing network supply.
// MachineSystem is responsible for consuming fuel and updating IsRunning and FuelRemaining.

public class GeneratorState
{
    // Kilowatts actually being produced this tick.
    // 0 if IsRunning is false (no fuel, no network, or block destroyed).
    public float CurrentOutputKW;

    // True while the generator has fuel loaded and is actively producing power.
    public bool IsRunning;

    // Quantity of fuel item currently loaded in the generator's internal tank.
    // Decremented by GeneratorParams.FuelConsumptionRate × deltaTime each tick while running.
    // When this reaches 0, IsRunning becomes false and CurrentOutputKW drops to 0.
    public float FuelRemaining;
}
