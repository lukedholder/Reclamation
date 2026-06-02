// Press E while aiming at a Storage Chest to open its contents panel.
// Shows total capacity, item types sorted by quantity (up to 8 rows), and an
// overflow indicator when more than 8 item types are stored.
// Press E or Escape to close the panel.
//
// The panel refreshes every frame while open — no polling delay.
//
// Setup: attach to the Player GameObject alongside Hotbar and Raycaster.
//        UIRoot must be in the scene (provides the Canvas and Font).

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ChestInteractor : MonoBehaviour
{
    // ── Layout constants (Canvas pixels at 1920×1080 reference) ──────────────

    private const float PanelW    = 280f;
    private const float TitleH    =  30f;
    private const float BarH      =  14f;   // capacity fill bar
    private const float ItemRowH  =  22f;
    private const float RowGap    =   2f;
    private const float OverflowH =  18f;
    private const float SepGap    =   6f;
    private const float PadV      =   8f;
    private const float PadH      =  10f;
    private const int   MaxRows   =   8;

    // Fixed panel height — all item rows are always present, shown/hidden as needed.
    // PadV + TitleH + SepGap + 1 + SepGap + BarH + SepGap + 1 + SepGap
    //   + MaxRows*ItemRowH + (MaxRows-1)*RowGap + SepGap + OverflowH + PadV
    private static readonly float PanelH =
        PadV + TitleH + SepGap + 1f + SepGap
        + BarH + SepGap + 1f + SepGap
        + MaxRows * ItemRowH + (MaxRows - 1) * RowGap
        + SepGap + OverflowH + PadV;

    // ── Colours ───────────────────────────────────────────────────────────────

    private static readonly Color ColPanel   = new Color(0.10f, 0.10f, 0.10f, 0.95f);
    private static readonly Color ColSep     = new Color(0.35f, 0.35f, 0.35f, 1.00f);
    private static readonly Color ColBarBg   = new Color(0.20f, 0.20f, 0.20f, 1.00f);
    private static readonly Color ColBarFill = new Color(0.20f, 0.65f, 0.25f, 1.00f);
    private static readonly Color ColRowEven = new Color(0.14f, 0.14f, 0.14f, 0.60f);
    private static readonly Color ColRowOdd  = new Color(0.18f, 0.18f, 0.18f, 0.60f);
    private static readonly Color ColDim     = new Color(0.60f, 0.60f, 0.60f, 1.00f);

    // ── Component references ──────────────────────────────────────────────────

    private Raycaster _raycaster;
    private Hotbar    _hotbar;

    private Simulation Sim => GameManager.Instance.Simulation;

    // ── Panel UI ──────────────────────────────────────────────────────────────

    private GameObject      _panel;
    private TextMeshProUGUI _titleText;
    private RectTransform   _barFillRT;     // width driven by fill ratio each frame
    private TextMeshProUGUI _capText;       // "X / 2000"

    // Pre-allocated item rows — shown/hidden, never rebuilt.
    private readonly GameObject[]      _rowObjects = new GameObject[MaxRows];
    private readonly TextMeshProUGUI[] _rowNames   = new TextMeshProUGUI[MaxRows];
    private readonly TextMeshProUGUI[] _rowCounts  = new TextMeshProUGUI[MaxRows];

    private TextMeshProUGUI _overflowText;

    // ── Interaction state ─────────────────────────────────────────────────────

    private bool                _open;
    private Block               _targetBlock;
    private StorageChestMachine _targetChest;

    // Sort buffer — reused every frame to avoid GC pressure.
    private readonly List<KeyValuePair<string, int>> _sortBuf =
        new List<KeyValuePair<string, int>>(64);

    // Exposed so MenuManager can close the panel via ESC routing.
    public bool IsOpen => _open;
    public void Close() => ClosePanel();

    // ── Unity ─────────────────────────────────────────────────────────────────

    private void Awake()
    {
        _raycaster = GetComponent<Raycaster>();
        _hotbar    = GetComponent<Hotbar>();
    }

    private void Start()
    {
        BuildPanel();
    }

    private void Update()
    {
        // Pause/settings menu takes priority — close chest panel if it sneaks in.
        if (MenuManager.IsOpen)
        {
            if (_open) ClosePanel();
            return;
        }

        // Auto-close if the chest was dismantled while the panel was open.
        if (_open && (_targetBlock == null || !Sim.Blocks.ById.ContainsKey(_targetBlock.Id)))
        {
            ClosePanel();
            return;
        }

        // ESC is handled by MenuManager; only E closes the panel here.
        bool eKey = Input.GetKeyDown(KeyCode.E);

        if (_open)
        {
            RefreshContents();
            if (eKey) ClosePanel();
        }
        else
        {
            if (eKey && !_hotbar.IsToolMode) TryOpen();
        }
    }

    // ── Panel lifecycle ───────────────────────────────────────────────────────

    private void TryOpen()
    {
        if (!_raycaster.HasHit) return;

        var bv = _raycaster.Hit.collider.GetComponent<BlockView>();
        if (bv == null) return;

        if (bv.Block.Definition.FunctionalType != FunctionalType.Storage) return;

        var chest = Sim.Machines.Get(bv.Block.Id) as StorageChestMachine;
        if (chest == null) return;

        _targetBlock = bv.Block;
        _targetChest = chest;

        _titleText.text = _targetBlock.Definition.DisplayName;
        RefreshContents();

        _open = true;
        _panel.SetActive(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
    }

    private void ClosePanel()
    {
        _open          = false;
        _targetBlock   = null;
        _targetChest   = null;
        _panel.SetActive(false);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;
    }

    // ── Contents refresh (called every frame while open) ─────────────────────

    private void RefreshContents()
    {
        // ── Capacity bar ──────────────────────────────────────────────────────
        int   stored   = _targetChest.TotalStored;
        int   capacity = StorageChestMachine.TotalCapacity;
        float ratio    = capacity > 0 ? Mathf.Clamp01((float)stored / capacity) : 0f;
        float barW     = PanelW - PadH * 2f;
        _barFillRT.sizeDelta = new Vector2(ratio * barW, 0f);
        _capText.text = $"{stored:N0} / {capacity:N0}";

        // ── Sort items by quantity descending ─────────────────────────────────
        _sortBuf.Clear();
        foreach (var kv in _targetChest.Contents)
            if (kv.Value > 0) _sortBuf.Add(kv);
        _sortBuf.Sort((a, b) => b.Value.CompareTo(a.Value));

        int visibleCount  = Mathf.Min(_sortBuf.Count, MaxRows);
        int overflowCount = _sortBuf.Count - visibleCount;

        // ── Item rows ─────────────────────────────────────────────────────────
        for (int i = 0; i < MaxRows; i++)
        {
            if (i < visibleCount)
            {
                _rowNames[i].text  = ToDisplayName(_sortBuf[i].Key);
                _rowCounts[i].text = _sortBuf[i].Value.ToString("N0");
                _rowObjects[i].SetActive(true);
            }
            else
            {
                _rowObjects[i].SetActive(false);
            }
        }

        // ── Overflow indicator ────────────────────────────────────────────────
        if (overflowCount > 0)
        {
            _overflowText.text = $"+ {overflowCount} more type{(overflowCount == 1 ? "" : "s")}…";
            _overflowText.gameObject.SetActive(true);
        }
        else
        {
            _overflowText.gameObject.SetActive(false);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    // "iron_ore" → "Iron Ore"
    private static string ToDisplayName(string id)
    {
        if (string.IsNullOrEmpty(id)) return id;
        var parts = id.Split('_');
        for (int i = 0; i < parts.Length; i++)
            if (parts[i].Length > 0)
                parts[i] = char.ToUpper(parts[i][0]) + parts[i].Substring(1);
        return string.Join(" ", parts);
    }

    // ── Panel construction ────────────────────────────────────────────────────

    private void BuildPanel()
    {
        // Root panel — centred on screen.
        _panel = new GameObject("ChestContentsPanel");
        _panel.transform.SetParent(UIRoot.Canvas.transform, false);
        var panelRT = _panel.AddComponent<RectTransform>();
        panelRT.anchorMin        =
        panelRT.anchorMax        =
        panelRT.pivot            = new Vector2(0.5f, 0.5f);
        panelRT.anchoredPosition = Vector2.zero;
        panelRT.sizeDelta        = new Vector2(PanelW, PanelH);

        // Opaque background — raycastTarget=true blocks clicks reaching the world.
        var bg = _panel.AddComponent<Image>();
        bg.color         = ColPanel;
        bg.raycastTarget = true;

        float yOff = PadV;   // Y from panel top, increasing downward

        // ── Title ─────────────────────────────────────────────────────────────
        _titleText = UIRoot.MakeText(_panel.transform, "Title", 14, TextAlignmentOptions.Left);
        _titleText.fontStyle = FontStyles.Bold;
        var titleRT = _titleText.GetComponent<RectTransform>();
        titleRT.anchorMin        = new Vector2(0f, 1f);
        titleRT.anchorMax        = new Vector2(1f, 1f);
        titleRT.pivot            = new Vector2(0f, 1f);
        titleRT.anchoredPosition = new Vector2(PadH, -yOff);
        titleRT.sizeDelta        = new Vector2(-PadH * 2f, TitleH);
        yOff += TitleH + SepGap;

        // ── Separator 1 ───────────────────────────────────────────────────────
        MakeSeparator(yOff);
        yOff += 1f + SepGap;

        // ── Capacity bar ──────────────────────────────────────────────────────
        float barW = PanelW - PadH * 2f;

        var barBgGO = new GameObject("CapBarBg");
        barBgGO.transform.SetParent(_panel.transform, false);
        var barBgRT = barBgGO.AddComponent<RectTransform>();
        barBgRT.anchorMin        = new Vector2(0f, 1f);
        barBgRT.anchorMax        = new Vector2(0f, 1f);
        barBgRT.pivot            = new Vector2(0f, 1f);
        barBgRT.anchoredPosition = new Vector2(PadH, -yOff);
        barBgRT.sizeDelta        = new Vector2(barW, BarH);
        var barBgImg = barBgGO.AddComponent<Image>();
        barBgImg.color         = ColBarBg;
        barBgImg.raycastTarget = false;

        // Fill — child of background; width set each frame via _barFillRT.sizeDelta.x.
        var barFillGO = new GameObject("CapBarFill");
        barFillGO.transform.SetParent(barBgGO.transform, false);
        _barFillRT = barFillGO.AddComponent<RectTransform>();
        _barFillRT.anchorMin        = new Vector2(0f, 0f);
        _barFillRT.anchorMax        = new Vector2(0f, 1f);   // stretch vertically, fixed width
        _barFillRT.pivot            = new Vector2(0f, 0.5f);
        _barFillRT.anchoredPosition = Vector2.zero;
        _barFillRT.sizeDelta        = Vector2.zero;           // width written each frame
        var barFillImg = barFillGO.AddComponent<Image>();
        barFillImg.color         = ColBarFill;
        barFillImg.raycastTarget = false;

        // Capacity text — overlaid on bar, right-aligned.
        _capText = UIRoot.MakeText(barBgGO.transform, "CapText", 11, TextAlignmentOptions.Right);
        var capRT = _capText.GetComponent<RectTransform>();
        UIRoot.StretchToParent(capRT);
        capRT.offsetMin = new Vector2(4f,  0f);
        capRT.offsetMax = new Vector2(-4f, 0f);
        _capText.raycastTarget = false;

        yOff += BarH + SepGap;

        // ── Separator 2 ───────────────────────────────────────────────────────
        MakeSeparator(yOff);
        yOff += 1f + SepGap;

        // ── Item rows ─────────────────────────────────────────────────────────
        for (int i = 0; i < MaxRows; i++)
        {
            var rowGO = new GameObject($"Row_{i}");
            rowGO.transform.SetParent(_panel.transform, false);
            var rowRT = rowGO.AddComponent<RectTransform>();
            rowRT.anchorMin        = new Vector2(0f, 1f);
            rowRT.anchorMax        = new Vector2(1f, 1f);   // full panel width
            rowRT.pivot            = new Vector2(0f, 1f);
            rowRT.anchoredPosition = new Vector2(0f, -yOff);
            rowRT.sizeDelta        = new Vector2(0f, ItemRowH);

            var rowBg = rowGO.AddComponent<Image>();
            rowBg.color         = (i % 2 == 0) ? ColRowEven : ColRowOdd;
            rowBg.raycastTarget = false;

            // Item name — left ~65% of row with left padding.
            var nameText = UIRoot.MakeText(rowGO.transform, "Name", 12, TextAlignmentOptions.Left);
            var nameRT   = nameText.GetComponent<RectTransform>();
            nameRT.anchorMin = new Vector2(0f, 0f);
            nameRT.anchorMax = new Vector2(0.65f, 1f);
            nameRT.offsetMin = new Vector2(PadH, 0f);
            nameRT.offsetMax = Vector2.zero;

            // Item count — right ~35% of row with right padding.
            var countText = UIRoot.MakeText(rowGO.transform, "Count", 12, TextAlignmentOptions.Right);
            var countRT   = countText.GetComponent<RectTransform>();
            countRT.anchorMin = new Vector2(0.65f, 0f);
            countRT.anchorMax = new Vector2(1f, 1f);
            countRT.offsetMin = Vector2.zero;
            countRT.offsetMax = new Vector2(-PadH, 0f);

            _rowObjects[i] = rowGO;
            _rowNames[i]   = nameText;
            _rowCounts[i]  = countText;

            yOff += ItemRowH + (i < MaxRows - 1 ? RowGap : 0f);
        }

        yOff += SepGap;

        // ── Overflow text ─────────────────────────────────────────────────────
        _overflowText = UIRoot.MakeText(_panel.transform, "Overflow", 11, TextAlignmentOptions.Center);
        _overflowText.color = ColDim;
        var overRT = _overflowText.GetComponent<RectTransform>();
        overRT.anchorMin        = new Vector2(0f, 1f);
        overRT.anchorMax        = new Vector2(1f, 1f);
        overRT.pivot            = new Vector2(0f, 1f);
        overRT.anchoredPosition = new Vector2(0f, -yOff);
        overRT.sizeDelta        = new Vector2(0f, OverflowH);
        _overflowText.gameObject.SetActive(false);

        _panel.SetActive(false);
    }

    private void MakeSeparator(float yOff)
    {
        var sepGO = new GameObject("Separator");
        sepGO.transform.SetParent(_panel.transform, false);
        var sepRT = sepGO.AddComponent<RectTransform>();
        sepRT.anchorMin        = new Vector2(0f, 1f);
        sepRT.anchorMax        = new Vector2(1f, 1f);
        sepRT.pivot            = new Vector2(0f, 1f);
        sepRT.anchoredPosition = new Vector2(0f, -yOff);
        sepRT.sizeDelta        = new Vector2(0f, 1f);
        var sepImg = sepGO.AddComponent<Image>();
        sepImg.color         = ColSep;
        sepImg.raycastTarget = false;
    }
}
