// Configuration for Generator blocks (FunctionalType.Generator).
// Fuel items enter through FuelInputFace and are consumed at FuelConsumptionRate per second.
// At full load the generator outputs OutputKW kilowatts to its power network.
// A generator with no fuel transitions its GeneratorState.IsRunning to false and outputs 0 kW.

public class GeneratorParams : IFunctionalParams
{
    // Kilowatts produced at full load (Steam Generator: 120, Large Generator T2: 400).
    public float OutputKW;

    // Fuel item quantity consumed per second at full load (Steam Generator: 0.25 = 1 coal per 4s).
    public float FuelConsumptionRate;

    // Which face of the generator block fuel items enter from.
    public FaceDir FuelInputFace;
}
