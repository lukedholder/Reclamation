// Classifies each construct from its current block composition.
// Pure C# — no Unity dependency.
//
// Runs every tick (construct counts are small). Sets:
//   Construct.IsPilotable — has at least one Seat and one Propulsion block.
//   Construct.Type        — Vehicle if pilotable, else Base (anchored) or Structure.
//
// IsPilotable deliberately ignores the anchored state: a vehicle may be built on
// terrain and is only released (unanchored) at the moment the player pilots it.
// Power presence is computed for future gating but does not block piloting yet.

public class ConstructSystem
{
    public void Tick(ConstructTable constructs, BlockTable blocks)
    {
        foreach (var construct in constructs.ById.Values)
            Reclassify(construct, blocks);
    }

    public void Reclassify(Construct construct, BlockTable blocks)
    {
        bool hasSeat = false, hasPropulsion = false, hasPower = false;

        foreach (int bid in construct.BlockIds)
        {
            if (!blocks.ById.TryGetValue(bid, out var b)) continue;
            var def = b.Definition;

            switch (def.FunctionalType)
            {
                case FunctionalType.Seat:       hasSeat       = true; break;
                case FunctionalType.Propulsion: hasPropulsion = true; break;
                case FunctionalType.Battery:    hasPower      = true; break;
            }
            if (def.PowerOutputKW > 0f) hasPower = true;
        }

        // hasPower is reserved for future thrust power-gating; unused in the basic prototype.
        _ = hasPower;

        construct.IsPilotable = hasSeat && hasPropulsion;

        construct.Type = construct.IsPilotable ? ConstructType.Vehicle
                       : construct.IsAnchored  ? ConstructType.Base
                       :                         ConstructType.Structure;
    }
}
