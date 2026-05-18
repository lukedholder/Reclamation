// Marker interface for block-type-specific configuration data.
// Every BlockDefinition with FunctionalType != None holds one IFunctionalParams
// implementation describing that block type's static behaviour.
//
// Params are immutable per block type — one instance shared across all placed blocks
// of that type. Never write to a Params field at runtime; use State classes instead.
//
// To add a new functional block type:
//   1. Add a value to FunctionalType.
//   2. Create a new class implementing IFunctionalParams.
//   3. Add a BlockDefinition entry in BlockCatalogue with Params set to the new class.
//   4. Add processing logic to the appropriate simulation system.

public interface IFunctionalParams { }
