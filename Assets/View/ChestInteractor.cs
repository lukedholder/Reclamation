// Press E while aiming at a Storage Chest to open its contents panel.
//
// Shows a 5-column slot grid with all stored item types (sorted by quantity,
// up to MaxSlots = 30).  Items can be dragged to/from the player inventory.
// A capacity fill bar and optional "N more types" overflow line give at-a-glance info.
//
// In dual-panel mode the panel anchors to DualLeftX = -340 and the player
// inventory opens alongside it on the right.
//
// Setup: attach to the Player GameObject alongside Hotbar, Raycaster, and PlayerInventory.
//        UIRoot and DragDropController must be in the scene.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ChestInteractor : MonoBehaviour
{
    // ── Layout constants ──────────────────────────────────────────────────────

    private const int   SlotCols   =  5;
    private const int   SlotRows   =  6;
    private const int   MaxSlots   = SlotCols * SlotRows;    // 30

    private const float DualLeftX  = -340f;
    private const float PadH       =  12f;
    private const float PadV       =  10f;
    private const float TitleH     =  30f;
    private const float BarH       =  14f;
    private const float OverflowH  =  18f;
    private const float SepGap     =   6f;

    // Grid pixel dimensions
    private static readonly float GridW =
        SlotCols * ItemSlotWidget.Size + (SlotCols - 1) * ItemSlotWidget.SlotGap;  // 276
    private static readonly float GridH =
        SlotRows * ItemSlotWidget.Size + (SlotRows - 1) * ItemSlotWidget.SlotGap;  // 332

    // Panel width = PadH + GridW + PadH
    private static readonly float PanelW = PadH + GridW + PadH;  // 300

    // Y offset from panel top where the slot grid starts
    // PadV + TitleH + SepGap + 1 + SepGap + BarH + SepGap + 1 + SepGap
    private static readonly float GridTop =
        PadV + TitleH + SepGap + 1f + SepGap + BarH + SepGap + 1f + SepGap;  // 80

    // Fixed panel height (grid + overflow label)
    private static readonly float PanelH = GridTop + GridH + SepGap + OverflowH + PadV;

    // ── Colours ───────────────────────────────────────────────────────────────

    private static readonly Color ColPanel   = new Color(0.10f, 0.10f, 0.10f, 0.95f);
    private static readonly Color ColSep     = new Color(0.35f, 0.35f, 0.35f, 1.00f);
    private static readonly Color ColBarBg   = new Color(0.20f, 0.20f, 0.20f, 1.00f);
    private static readonly Color ColBarFill = new Color(0.20f, 0.65f, 0.25f, 1.00f);
    private static readonly Color ColDim     = new Color(0.60f, 0.60f, 0.60f, 1.00f);

    // ── Component references ──────────────────────────────────────────────────

    private Raycaster       _raycaster;
    private Hotbar          _hotbar;
    private PlayerInventory _playerInventory;

    private Simulation Sim => GameManager.Instance.Simulation;

    // ── Panel UI ──────────────────────────────────────────────────────────────

    private GameObject         _panel;
    private RectTransform      _panelRT;
    private TextMeshProUGUI    _titleText;
    private RectTransform      _barFillRT;
    private TextMeshProUGUI    _capText;
    private TextMeshProUGUI    _overflowText;

    // Pre-allocated slot widgets — shown/hidden, never rebuilt
    private readonly ItemSlotWidget[] _slotWidgets = new ItemSlotWidget[MaxSlots];

    // ── Interaction state ─────────────────────────────────────────────────────

    private bool                _open;
    private Block               _targetBlock;
    private StorageChestMachine _targetChest;

    // Sort buffer — reused each frame to avoid allocation
    private readonly List<KeyValuePair<string, int>> _sortBuf = new List<KeyValuePair<string, int>>(64);

    public bool IsOpen => _open;
    public void Close() => ClosePanel();

    // ── Unity ─────────────────────────────────────────────────────────────────

    private void Awake()
    {
        _raycaster       = GetComponent<Raycaster>();
        _hotbar          = GetComponent<Hotbar>();
        _playerInventory = GetComponent<PlayerInventory>();
    }

    private void Start()
    {
        BuildPanel();
    }

    private void Update()
    {
        if (MenuManager.IsOpen)
        {
            if (_open) ClosePanel();
            return;
        }

        if (_open && (_targetBlock == null || !Sim.Blocks.ById.ContainsKey(_targetBlock.Id)))
        {
            ClosePanel();
            return;
        }

        bool eKey = GameInput.InteractDown;

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
        _playerInventory?.ShowForMachine();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
    }

    private void ClosePanel()
    {
        DragDropController.Instance?.CancelDrag();
        _playerInventory?.HideIfMachine();

        _open        = false;
        _targetBlock = null;
        _targetChest = null;
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

        int visible  = Mathf.Min(_sortBuf.Count, MaxSlots);
        int overflow = _sortBuf.Count - visible;

        // ── Slot widgets ──────────────────────────────────────────────────────
        for (int i = 0; i < MaxSlots; i++)
        {
            if (i < visible)
            {
                var kv = _sortBuf[i];
                _slotWidgets[i].Refresh(new ItemStack(kv.Key, kv.Value));
                _slotWidgets[i].Root.SetActive(true);
            }
            else
            {
                _slotWidgets[i].Root.SetActive(false);
            }
        }

        // ── Overflow indicator ────────────────────────────────────────────────
        if (overflow > 0)
        {
            _overflowText.text = $"+ {overflow} more type{(overflow == 1 ? "" : "s")}…";
            _overflowText.gameObject.SetActive(true);
        }
        else
        {
            _overflowText.gameObject.SetActive(false);
        }
    }

    // ── Transfer callbacks ────────────────────────────────────────────────────

    /// <summary>Accepts items from the inventory into this chest.</summary>
    public int TryAddToChest(ItemStack stack)
    {
        if (_targetChest == null) return 0;
        return _targetChest.GiveToStorage(stack.ItemId, stack.Quantity);
    }

    private void TransferChestSlotToInventory(ItemSlotWidget w)
    {
        if (w.Current.IsEmpty || _targetChest == null || _playerInventory == null) return;
        string id     = w.Current.ItemId;
        int    maxQty = ItemCatalogue.Get(id)?.MaxStackSize ?? 50;
        var    taken  = _targetChest.TakeFromStorage(id, maxQty);
        if (taken.IsEmpty) return;

        int leftover = _playerInventory.TryAddItem(taken.ItemId, taken.Quantity);
        if (leftover > 0)
            _targetChest.GiveToStorage(taken.ItemId, leftover);
    }

    // ── Panel construction (built once in Start) ──────────────────────────────

    private void BuildPanel()
    {
        _panel = new GameObject("ChestContentsPanel");
        _panel.transform.SetParent(UIRoot.Canvas.transform, false);
        _panelRT = _panel.AddComponent<RectTransform>();
        _panelRT.anchorMin        =
        _panelRT.anchorMax        =
        _panelRT.pivot            = new Vector2(0.5f, 0.5f);
        _panelRT.anchoredPosition = new Vector2(DualLeftX, 0f);
        _panelRT.sizeDelta        = new Vector2(PanelW, PanelH);

        var bg = _panel.AddComponent<Image>();
        bg.color         = ColPanel;
        bg.raycastTarget = true;

        float yOff = PadV;

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

        MakeSeparator(yOff);
        yOff += 1f + SepGap;

        // ── Capacity bar ──────────────────────────────────────────────────────
        float barW = PanelW - PadH * 2f;

        var barBgGO  = new GameObject("CapBarBg");
        barBgGO.transform.SetParent(_panel.transform, false);
        var barBgRT  = barBgGO.AddComponent<RectTransform>();
        barBgRT.anchorMin        = new Vector2(0f, 1f);
        barBgRT.anchorMax        = new Vector2(0f, 1f);
        barBgRT.pivot            = new Vector2(0f, 1f);
        barBgRT.anchoredPosition = new Vector2(PadH, -yOff);
        barBgRT.sizeDelta        = new Vector2(barW, BarH);
        barBgGO.AddComponent<Image>().color = ColBarBg;

        var fillGO = new GameObject("CapBarFill");
        fillGO.transform.SetParent(barBgGO.transform, false);
        _barFillRT            = fillGO.AddComponent<RectTransform>();
        _barFillRT.anchorMin  = new Vector2(0f, 0f);
        _barFillRT.anchorMax  = new Vector2(0f, 1f);
        _barFillRT.pivot      = new Vector2(0f, 0.5f);
        _barFillRT.sizeDelta  = Vector2.zero;
        fillGO.AddComponent<Image>().color = ColBarFill;

        _capText = UIRoot.MakeText(barBgGO.transform, "CapText", 10, TextAlignmentOptions.Right);
        var capRT = _capText.GetComponent<RectTransform>();
        UIRoot.StretchToParent(capRT);
        capRT.offsetMin = new Vector2(4f, 0f);
        capRT.offsetMax = new Vector2(-4f, 0f);
        yOff += BarH + SepGap;

        MakeSeparator(yOff);
        yOff += 1f + SepGap;

        // ── Slot grid ─────────────────────────────────────────────────────────
        for (int i = 0; i < MaxSlots; i++)
        {
            int   col = i % SlotCols;
            int   row = i / SlotCols;
            float x   = PadH + col * (ItemSlotWidget.Size + ItemSlotWidget.SlotGap);
            float y   = yOff + row * (ItemSlotWidget.Size + ItemSlotWidget.SlotGap);

            var w = ItemSlotWidget.Create(_panel.transform, $"ChestSlot_{i}");
            w.SetOffset(x, y);
            w.Refresh(default);

            // Double-click transfers this slot's item to the player inventory.
            var captured = w;
            w.OnSlotDoubleClicked = _ => TransferChestSlotToInventory(captured);

            _slotWidgets[i] = w;
        }

        yOff += GridH + SepGap;

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
        var go = new GameObject("Sep");
        go.transform.SetParent(_panel.transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0f, 1f);
        rt.anchorMax        = new Vector2(1f, 1f);
        rt.pivot            = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(0f, -yOff);
        rt.sizeDelta        = new Vector2(0f, 1f);
        var img = go.AddComponent<Image>();
        img.color         = ColSep;
        img.raycastTarget = false;
    }
}
