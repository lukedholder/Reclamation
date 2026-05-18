// Manages all power networks in the simulation.
// Each construct starts with one default network. Multiple independent networks per construct
// are possible once pole wiring is implemented — disconnected pole islands produce separate graphs.
//
// TICK LOGIC (per network):
//   1. Sum generator supply (GeneratorState.IsRunning && CurrentOutputKW).
//   2. Sum consumer demand (BlockDefinition.PowerDrawKW for every registered consumer).
//   3. Balance:
//       surplus  → charge batteries up to MaxChargeRateKW each.
//       deficit  → discharge batteries up to MaxDischargeRateKW each, then throttle consumers.
//   4. Write OperatingRate [0,1] to every consumer's MachineState.
//      PowerSystem.Tick() must run before MachineSystem.Tick() so machines see the
//      updated rate before advancing their production cycles.
//
// STATES (written to PowerNetwork.State each tick):
//   Nominal      — supply ≥ demand. Batteries charging. All consumers at 1.0.
//   BatteryAssist— supply < demand but batteries cover the gap. Consumers at 1.0.
//   Deficit      — supply + battery discharge < demand. Consumers throttled proportionally.
//   Dead         — no effective supply at all. All consumers at 0.0.
//
// V1 SIMPLIFICATION: generators run unconditionally (IsRunning = true, no fuel consumption).
// Fuel consumption will be handled by a future FuelSystem / GeneratorMachine subclass.

using System.Collections.Generic;

public class PowerSystem
{
    private readonly PowerNetworkTable _table = new PowerNetworkTable();
    private int _nextNetworkId = 1;

    // Expose networks for debug GUI
    public IReadOnlyDictionary<int, PowerNetwork> Networks => _table.ById;

    // ── Network management ────────────────────────────────────────────────────

    // Create a new empty network belonging to a construct.
    public PowerNetwork CreateNetwork(int constructId)
    {
        var network = new PowerNetwork { Id = _nextNetworkId++ };
        _table.ById[network.Id] = network;

        if (!_table.ByConstruct.ContainsKey(constructId))
            _table.ByConstruct[constructId] = new List<int>();
        _table.ByConstruct[constructId].Add(network.Id);

        return network;
    }

    // Return the first (default) network ID for a construct, creating one if none exists.
    public int GetOrCreateNetworkId(int constructId)
    {
        if (_table.ByConstruct.TryGetValue(constructId, out var ids) && ids.Count > 0)
            return ids[0];
        return CreateNetwork(constructId).Id;
    }

    // ── Block registration ────────────────────────────────────────────────────

    // Called by Simulation.PlaceBlock() for every placed block.
    // Blocks with PowerInterface.None are silently ignored.
    public void Register(Block block)
    {
        if (block.Definition.PowerInterface == PowerInterface.None) return;

        int networkId = GetOrCreateNetworkId(block.ConstructId);
        block.PowerNetworkId = networkId;
        var network = _table.ById[networkId];

        // Battery
        if (block.Definition.FunctionalType == FunctionalType.Battery)
        {
            var p = (BatteryParams)block.Definition.Params;
            block.BatteryState = new BatteryState
            {
                CapacityKJ         = p.CapacityKJ,
                MaxChargeRateKW    = p.MaxChargeRateKW,
                MaxDischargeRateKW = p.MaxDischargeRateKW,
                StoredKJ           = p.CapacityKJ,   // start fully charged
            };
            network.BatteryIds.Add(block.Id);
        }

        // Generator (V1: always running, no fuel)
        if (block.Definition.PowerOutputKW > 0f)
        {
            block.GeneratorState = new GeneratorState
            {
                IsRunning      = true,
                CurrentOutputKW = block.Definition.PowerOutputKW,
                FuelRemaining  = float.MaxValue,   // V1 placeholder — infinite fuel
            };
            network.GeneratorIds.Add(block.Id);
        }

        // Consumer (machines, turrets, lights — anything with a power draw)
        if (block.Definition.PowerDrawKW > 0f)
            network.ConsumerIds.Add(block.Id);

        // Pole
        if (block.Definition.PowerInterface == PowerInterface.WireEndpoint)
            network.PoleIds.Add(block.Id);
    }

    // Called by Simulation.RemoveBlock() before the block is deleted.
    public void Unregister(Block block)
    {
        if (block.PowerNetworkId < 0) return;
        if (!_table.ById.TryGetValue(block.PowerNetworkId, out var network)) return;

        network.GeneratorIds.Remove(block.Id);
        network.BatteryIds.Remove(block.Id);
        network.ConsumerIds.Remove(block.Id);
        network.PoleIds.Remove(block.Id);
        block.PowerNetworkId = -1;
    }

    // ── Tick ─────────────────────────────────────────────────────────────────

    public void Tick(float tickDelta, BlockTable blocks)
    {
        foreach (var network in _table.ById.Values)
            TickNetwork(network, tickDelta, blocks);
    }

    private void TickNetwork(PowerNetwork network, float tickDelta, BlockTable blocks)
    {
        // 1. Sum supply
        float supply = 0f;
        foreach (var id in network.GeneratorIds)
            if (blocks.ById.TryGetValue(id, out var b) && b.GeneratorState?.IsRunning == true)
                supply += b.GeneratorState.CurrentOutputKW;
        network.TotalSupplyKW = supply;

        // 2. Sum demand, scaling by machine mode where applicable.
        //    OperationMode drives effective draw (matches Vol2 §4.1):
        //      Idle / NoPower → 0 kW   (no recipe set, or grid dead last tick)
        //      Waiting        → 25 %   (stalled: inputs missing or output full)
        //      Operating      → 100 %  (actively producing)
        //    Blocks without a MachineState (future turrets, lights, etc.) always draw 100%.
        float demand = 0f;
        foreach (var id in network.ConsumerIds)
        {
            if (!blocks.ById.TryGetValue(id, out var b)) continue;
            float drawKW = b.Definition.PowerDrawKW;
            var mode = b.MachineState?.Mode;
            if (mode.HasValue)
                drawKW = mode.Value switch
                {
                    OperationMode.Idle    => 0f,
                    OperationMode.NoPower => 0f,
                    OperationMode.Waiting => drawKW * 0.25f,
                    _                     => drawKW,   // Operating
                };
            demand += drawKW;
        }
        network.TotalDemandKW = demand;

        // 3. Balance
        float balance = supply - demand;

        if (balance >= 0f)
        {
            ChargeBatteries(network, balance, tickDelta, blocks);
            network.State = PowerState.Nominal;
            SetOperatingRates(network, 1f, blocks);
            return;
        }

        // Deficit — try to cover with batteries
        float deficit      = -balance;
        float batteryKW    = DischargeBatteries(network, deficit, tickDelta, blocks);
        float effectiveKW  = supply + batteryKW;

        if (effectiveKW >= demand)
        {
            network.State = PowerState.BatteryAssist;
            SetOperatingRates(network, 1f, blocks);
        }
        else if (effectiveKW <= 0f)
        {
            network.State = PowerState.Dead;
            SetOperatingRates(network, 0f, blocks);
        }
        else
        {
            network.State = PowerState.Deficit;
            SetOperatingRates(network, demand > 0f ? effectiveKW / demand : 0f, blocks);
        }
    }

    // Absorb surplus kW into batteries, respecting MaxChargeRateKW and capacity.
    private void ChargeBatteries(PowerNetwork network, float surplusKW, float tickDelta, BlockTable blocks)
    {
        foreach (var id in network.BatteryIds)
        {
            if (surplusKW <= 0f) break;
            if (!blocks.ById.TryGetValue(id, out var b)) continue;
            var bat = b.BatteryState;

            float chargeKW = System.Math.Min(surplusKW, bat.MaxChargeRateKW);
            float chargeKJ = chargeKW * tickDelta;
            bat.StoredKJ   = System.Math.Min(bat.StoredKJ + chargeKJ, bat.CapacityKJ);
            surplusKW     -= chargeKW;
        }
    }

    // Draw up to deficitKW from batteries. Returns actual kW supplied.
    private float DischargeBatteries(PowerNetwork network, float deficitKW, float tickDelta, BlockTable blocks)
    {
        float supplied = 0f;
        foreach (var id in network.BatteryIds)
        {
            if (supplied >= deficitKW) break;
            if (!blocks.ById.TryGetValue(id, out var b)) continue;
            var bat = b.BatteryState;

            float need       = deficitKW - supplied;
            float maxDrawKW  = System.Math.Min(need, bat.MaxDischargeRateKW);
            float maxKWFromKJ = bat.StoredKJ / tickDelta;           // kJ ÷ s = kW
            float drawKW     = System.Math.Min(maxDrawKW, maxKWFromKJ);

            bat.StoredKJ = System.Math.Max(0f, bat.StoredKJ - drawKW * tickDelta);
            supplied    += drawKW;
        }
        return supplied;
    }

    // Write OperatingRate to each consumer's MachineState (if it has one).
    private void SetOperatingRates(PowerNetwork network, float rate, BlockTable blocks)
    {
        foreach (var id in network.ConsumerIds)
        {
            if (!blocks.ById.TryGetValue(id, out var b)) continue;
            if (b.MachineState != null)
                b.MachineState.OperatingRate = rate;
        }
    }
}
