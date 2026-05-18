// General-purpose crafting machine. Consumes N input items, produces M output items.
// All logic is handled by BaseMachine — this class exists as a named type so
// MachineSystem can instantiate the right subclass, and for future Assembler-specific
// overrides (e.g. speed multiplier from AssemblerParams, recipe category filtering).

public class AssemblerMachine : BaseMachine
{
    public AssemblerMachine(Block block) : base(block) { }

    // Future: apply AssemblerParams.SpeedMultiplier to OperatingRate here.
    // Future: filter SetRecipe() by AssemblerParams.AllowedRecipeCategories.
}
