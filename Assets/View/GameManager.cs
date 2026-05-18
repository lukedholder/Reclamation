using System.Text;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    private Simulation _simulation;
    private float      _accumulator;

    private const float TickRate = 1f / 20f;

    private void Start()
    {
        _simulation = new Simulation();
        RunDebugScenario();
    }

    private void Update()
    {
        _accumulator += Time.deltaTime;
        while (_accumulator >= TickRate)
        {
            _simulation.Update();
            _accumulator -= TickRate;
        }
    }

    // ── Debug scenario ────────────────────────────────────────────────────────
    // Seeds the simulation with machines so the GUI has something to display.
    // Replace with real placement/logistics wiring once those systems exist.

    private void RunDebugScenario()
    {
        var construct = _simulation.CreateConstruct();

        // Miner — extracts iron ore every 2 seconds
        var minerBlock = _simulation.PlaceBlock(BlockCatalogue.BasicMiner, construct.Id, GridPos.Zero);
        _simulation.Machines.Get<MinerMachine>(minerBlock.Id)
            ?.SetResourceNode("iron_ore", cycleTime: 2f, amountPerCycle: 1);

        // Furnace — smelts iron ore → iron plate
        var furnaceBlock = _simulation.PlaceBlock(BlockCatalogue.ElectricFurnace, construct.Id,
            new GridPos(2, 0, 0));
        _simulation.Machines.Get<BaseMachine>(furnaceBlock.Id)
            ?.SetRecipe(RecipeCatalogue.SmeltIron);

        // Assembler — crafts iron gear wheels from iron plates
        var assemblerBlock = _simulation.PlaceBlock(BlockCatalogue.AssemblerMk1, construct.Id,
            new GridPos(4, 0, 0));
        _simulation.Machines.Get<BaseMachine>(assemblerBlock.Id)
            ?.SetRecipe(RecipeCatalogue.IronGearWheel);

        // Manually seed the furnace and assembler input buffers so they start working
        // (logistics system will do this automatically once implemented)
        furnaceBlock.MachineState.InputBuffer.Add("iron_ore",   50);
        assemblerBlock.MachineState.InputBuffer.Add("iron_plate", 50);
    }

    // ── Debug GUI ─────────────────────────────────────────────────────────────

    private readonly StringBuilder _sb = new StringBuilder();

    private void OnGUI()
    {
        _sb.Clear();
        _sb.AppendLine($"Tick: {_simulation.Tick}   " +
                       $"Blocks: {_simulation.Blocks.ById.Count}   " +
                       $"Constructs: {_simulation.Constructs.ById.Count}   " +
                       $"Machines: {_simulation.Machines.Count}");
        _sb.AppendLine();

        foreach (var block in _simulation.Blocks.ById.Values)
        {
            var ms = block.MachineState;
            if (ms == null) continue;

            string recipe  = ms.ActiveRecipe?.DisplayName ?? "—";
            int    pct     = Mathf.RoundToInt(ms.CycleProgress * 100f);
            string mode    = ms.Mode.ToString();
            string type    = block.Definition.FunctionalType.ToString();

            _sb.AppendLine($"[{block.Id}] {type,-12} | {mode,-10} | {recipe,-18} | {pct,3}%");

            // Input buffer
            foreach (var slot in ms.InputBuffer.Slots)
                if (!string.IsNullOrEmpty(slot.ItemId))
                    _sb.AppendLine($"        IN  {slot.Quantity,4}x {slot.ItemId}");

            // Output buffer
            foreach (var slot in ms.OutputBuffer.Slots)
                if (!string.IsNullOrEmpty(slot.ItemId))
                    _sb.AppendLine($"        OUT {slot.Quantity,4}x {slot.ItemId}");
        }

        GUI.Label(new Rect(10, 10, 500, 800), _sb.ToString());
    }
}
