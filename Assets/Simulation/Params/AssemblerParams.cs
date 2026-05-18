// Configuration for Assembler and Fabricator blocks.
// SpeedMultiplier scales how fast production cycles complete relative to the recipe's base CycleTime.
// AllowedRecipeCategories controls which recipe categories appear in the recipe selection UI
// for this machine — a recipe whose category is not in this list cannot be selected.

public class AssemblerParams : IFunctionalParams
{
    // Production cycle speed multiplier (Assembler Mk1: 1.0, Mk2: 1.5).
    public float SpeedMultiplier;

    // Recipe category IDs accepted by this machine (e.g. "basic_parts", "electronics").
    public string[] AllowedRecipeCategories;
}
