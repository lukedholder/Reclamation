// Shows a small info panel in the top-left corner for the block the crosshair is aimed at.
// Displays block name, grid position, construct ID, and any runtime state
// (machine mode, generator output, battery charge).
//
// Setup: attach to the Player GameObject alongside Raycaster.

using System.Text;
using UnityEngine;

public class BlockInfoHUD : MonoBehaviour
{
    private const float PanelX    = 8f;
    private const float PanelY    = 8f;
    private const float PanelW    = 240f;
    private const float LineH     = 18f;
    private const float PadX      = 6f;
    private const float PadY      = 4f;

    private Raycaster _raycaster;
    private StringBuilder _sb = new StringBuilder();

    private void Awake()
    {
        _raycaster = GetComponent<Raycaster>();
    }

    private void OnGUI()
    {
        if (!_raycaster.HasHit) return;

        var blockView = _raycaster.Hit.collider.GetComponent<BlockView>();
        if (blockView == null) return;

        var block = blockView.Block;
        var def   = block.Definition;

        _sb.Clear();
        _sb.AppendLine(def.DisplayName);
        _sb.AppendLine($"Grid       {block.GridPosition}");
        _sb.AppendLine($"Construct  #{block.ConstructId}");

        if (block.MachineState != null)
            _sb.AppendLine($"Mode       {block.MachineState.Mode}");

        if (block.GeneratorState != null)
        {
            var gs = block.GeneratorState;
            _sb.AppendLine(gs.IsRunning
                ? $"Generator  {gs.CurrentOutputKW:F1} kW"
                : "Generator  Off");
        }

        if (block.BatteryState != null)
        {
            var bs = block.BatteryState;
            _sb.AppendLine($"Battery    {bs.ChargePercent * 100f:F0}%  ({bs.StoredKJ:F0} / {bs.CapacityKJ:F0} kJ)");
        }

        var text  = _sb.ToString().TrimEnd();
        var lines = text.Split('\n').Length;
        float panelH = lines * LineH + PadY * 2f;

        // Background
        GUI.color = new Color(0f, 0f, 0f, 0.55f);
        GUI.DrawTexture(new Rect(PanelX, PanelY, PanelW, panelH), Texture2D.whiteTexture);

        // Text
        GUI.color = Color.white;
        GUI.Label(new Rect(PanelX + PadX, PanelY + PadY, PanelW - PadX * 2f, panelH), text);
    }
}
