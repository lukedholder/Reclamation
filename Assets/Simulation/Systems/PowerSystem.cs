// Manages all power networks in the simulation.
// Networks are defined purely by wire connections — two blocks joined by a wire
// belong to the same network.  Construct membership does NOT automatically merge power;
// wiring is always required to distribute power across constructs.
//
// Network topology is rebuilt lazily (once per tick if the graph has changed) using a
// flood-fill over the explicit wire connection set.  Each connected component of power
// blocks becomes one PowerNetwork.  Isolated blocks get their own Dead network.
// The network's ID equals the minimum block ID in the component, so IDs are stable
// across reconnects as long as the lowest-ID block in each component isn't removed.
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
    private readonly PowerNetworkTable      _table       = new PowerNetworkTable();
    private readonly HashSet<(int, int)>    _connections = new HashSet<(int, int)>();
    private bool _dirty = false;

    // Expose networks for debug/view
    public IReadOnlyDictionary<int, PowerNetwork>   Networks        => _table.ById;
    // Explicit user-placed wire connections between block IDs (order-normalised: lower ID first).
    public IReadOnlyCollection<(int, int)>          WireConnections => _connections;

    // ── Wire connections ──────────────────────────────────────────────────────

    // Adds an explicit wire between two power blocks.  Returns false if already connected.
    public bool ConnectBlocks(int blockA, int blockB)
    {
        bool added = _connections.Add(MakeKey(blockA, blockB));
        if (added) _dirty = true;
        return added;
    }

    // Removes an explicit wire.  Returns false if no connection existed.
    public bool DisconnectBlocks(int blockA, int blockB)
    {
        bool removed = _connections.Remove(MakeKey(blockA, blockB));
        if (removed) _dirty = true;
        return removed;
    }

    public bool HasConnection(int blockA, int blockB)
        => _connections.Contains(MakeKey(blockA, blockB));

    // Number of wires currently attached to a given block.
    public int ConnectionCount(int blockId)
    {
        int n = 0;
        foreach (var (a, b) in _connections)
            if (a == blockId || b == blockId) n++;
        return n;
    }

    // Normalises so the lower ID is always first, making the pair order-independent.
    private static (int, int) MakeKey(int a, int b) => a < b ? (a, b) : (b, a);

    // ── Block registration ────────────────────────────────────────────────────

    // Called by Simulation.PlaceBlock() for every placed block.
    // Initialises per-block power state; network assignment happens lazily in Tick().
    // Blocks with PowerInterface.None are silently ignored.
    public void Register(Block block)
    {
        if (block.Definition.PowerInterface == PowerInterface.None) return;

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
        }

        // Generator (V1: always running, no fuel)
        if (block.Definition.PowerOutputKW > 0f)
        {
            block.GeneratorState = new GeneratorState
            {
                IsRunning       = true,
                CurrentOutputKW = block.Definition.PowerOutputKW,
                FuelRemaining   = float.MaxValue,   // V1 placeholder — infinite fuel
            };
        }

        _dirty = true;
    }

    // Called by Simulation.RemoveBlock() before the block is deleted.
    public void Unregister(Block block)
    {
        if (block.Definition.PowerInterface == PowerInterface.None) return;

        // Drop every wire attached to this block.
        if (block.Definition.MaxWireConnections > 0)
        {
            var toRemove = new List<(int, int)>();
            foreach (var (a, b) in _connections)
                if (a == block.Id || b == block.Id)
                    toRemove.Add((a, b));
            foreach (var key in toRemove)
                _connections.Remove(key);
        }

        _dirty = true;
    }

    // ── Tick ─────────────────────────────────────────────────────────────────

    public void Tick(float tickDelta, BlockTable blocks)
    {
        if (_dirty) RebuildNetworks(blocks);

        foreach (var network in _table.ById.Values)
            TickNetwork(network, tickDelta, blocks);
    }

    // ── Network rebuild ───────────────────────────────────────────────────────

    // Flood-fills _connections to group all power blocks into connected components.
    // Each component becomes a PowerNetwork; isolated blocks each get their own Dead network.
    private void RebuildNetworks(BlockTable blocks)
    {
        _dirty = false;
        _table.ById.Clear();

        // Reset all network assignments.
        foreach (var b in blocks.ById.Values)
            b.PowerNetworkId = -1;

        // Collect all power-participating blocks.
        var wireable = new HashSet<int>();
        foreach (var b in blocks.ById.Values)
            if (b.Definition.PowerInterface != PowerInterface.None)
                wireable.Add(b.Id);

        // Flood-fill connected components via explicit wire connections.
        var visited = new HashSet<int>();
        foreach (int startId in wireable)
        {
            if (visited.Contains(startId)) continue;

            var component = new List<int>();
            var queue     = new Queue<int>();
            queue.Enqueue(startId);
            visited.Add(startId);

            while (queue.Count > 0)
            {
                int id = queue.Dequeue();
                component.Add(id);

                foreach (var (a, b) in _connections)
                {
                    int neighbor = (a == id) ? b : (b == id) ? a : -1;
                    if (neighbor < 0 || visited.Contains(neighbor) || !wireable.Contains(neighbor))
                        continue;
                    visited.Add(neighbor);
                    queue.Enqueue(neighbor);
                }
            }

            // Canonical network ID = minimum block ID in the component (stable for reconnects).
            int netId = int.MaxValue;
            foreach (int id in component) if (id < netId) netId = id;

            var network = new PowerNetwork { Id = netId };
            _table.ById[netId] = network;

            foreach (int id in component)
            {
                if (!blocks.ById.TryGetValue(id, out var b)) continue;
                b.PowerNetworkId = netId;

                if (b.Definition.FunctionalType == FunctionalType.Battery)
                    network.BatteryIds.Add(id);
                if (b.Definition.PowerOutputKW > 0f)
                    network.GeneratorIds.Add(id);
                if (b.Definition.PowerDrawKW > 0f)
                    network.ConsumerIds.Add(id);
                if (b.Definition.PowerInterface == PowerInterface.WireEndpoint)
                    network.PoleIds.Add(id);
            }
        }
    }

    // ── Per-network tick ──────────────────────────────────────────────────────

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
        float deficit     = -balance;
        float batteryKW   = DischargeBatteries(network, deficit, tickDelta, blocks);
        float effectiveKW = supply + batteryKW;

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

    // Draw up to deficitKW from batteries.  Returns actual kW supplied.
    private float DischargeBatteries(PowerNetwork network, float deficitKW, float tickDelta, BlockTable blocks)
    {
        float supplied = 0f;
        foreach (var id in network.BatteryIds)
        {
            if (supplied >= deficitKW) break;
            if (!blocks.ById.TryGetValue(id, out var b)) continue;
            var bat = b.BatteryState;

            float need        = deficitKW - supplied;
            float maxDrawKW   = System.Math.Min(need, bat.MaxDischargeRateKW);
            float maxKWFromKJ = bat.StoredKJ / tickDelta;   // kJ ÷ s = kW
            float drawKW      = System.Math.Min(maxDrawKW, maxKWFromKJ);

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
