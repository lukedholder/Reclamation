// Shows a small info panel in the top-left corner for the block the crosshair is aimed at.
// Displays block name, grid position, construct ID, and any runtime state
// (machine mode, generator output, battery charge).
//
// Setup: attach to the Player GameObject alongside Raycaster.
//        UIRoot must be in the scene.

using System.Text;
using UnityEngine;
using UnityEngine.UI;

public class BlockInfoHUD : MonoBehaviour
{
    private const float PanelW  = 240f;
    private const float LineH   =  18f;
    private const float PadX    =   6f;
    private const float PadY    =   4f;

    private Raycaster     _raycaster;
    private GameObject    _panel;
    private RectTransform _panelRT;
    private Text          _text;
    private StringBuilder _sb = new StringBuilder();

    // ── Unity ─────────────────────────────────────────────────────────────────

    private void Awake()
    {
        _raycaster = GetComponent<Raycaster>();
    }

    private void Start()
    {
        BuildPanel();
    }

    private void Update()
    {
        RefreshPanel();
    }

    // ── Build ─────────────────────────────────────────────────────────────────

    private void BuildPanel()
    {
        // Root panel — anchored top-left.
        _panel = new GameObject("BlockInfoHUD");
        _panel.transform.SetParent(UIRoot.Canvas.transform, false);
        _panelRT = _panel.AddComponent<RectTransform>();
        _panelRT.anchorMin        = new Vector2(0f, 1f);
        _panelRT.anchorMax        = new Vector2(0f, 1f);
        _panelRT.pivot            = new Vector2(0f, 1f);
        _panelRT.anchoredPosition = new Vector2(8f, -8f);
        _panelRT.sizeDelta        = new Vector2(PanelW, LineH + PadY * 2f);

        var bg = _panel.AddComponent<Image>();
        bg.color         = new Color(0f, 0f, 0f, 0.55f);
        bg.raycastTarget = false;

        // Text sits inside the panel with padding.
        var textGO = new GameObject("Text");
        textGO.transform.SetParent(_panel.transform, false);
        var textRT = textGO.AddComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = new Vector2( PadX,  PadY);
        textRT.offsetMax = new Vector2(-PadX, -PadY);

        _text = textGO.AddComponent<Text>();
        if (UIRoot.Font != null) _text.font = UIRoot.Font;
        _text.fontSize      = 12;
        _text.color         = Color.white;
        _text.alignment     = TextAnchor.UpperLeft;
        _text.raycastTarget = false;

        _panel.SetActive(false);
    }

    // ── Update ────────────────────────────────────────────────────────────────

    private void RefreshPanel()
    {
        if (!_raycaster.HasHit) { _panel.SetActive(false); return; }

        var blockView = _raycaster.Hit.collider.GetComponent<BlockView>();
        if (blockView == null) { _panel.SetActive(false); return; }

        _panel.SetActive(true);

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
            _sb.AppendLine($"Battery    {bs.ChargePercent * 100f:F0}%  " +
                           $"({bs.StoredKJ:F0} / {bs.CapacityKJ:F0} kJ)");
        }

        var content = _sb.ToString().TrimEnd();
        var lines   = content.Split('\n').Length;

        // Resize panel to exactly fit the content.
        _panelRT.sizeDelta = new Vector2(PanelW, lines * LineH + PadY * 2f);
        _text.text         = content;
    }
}
