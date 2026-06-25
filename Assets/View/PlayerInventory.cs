// Player inventory and equipment panel.
//
// General slots — count is set in the Inspector (Range 8–80).
// Changing SlotCount at runtime (Inspector or code) resizes the grid automatically;
// item data is preserved up to the new limit.
//
// Gear slots — one per GearSlot enum value (Head…Utility).
// Gear rows show an icon + type label + item name.
//
// Dual-panel mode (Satisfactory-style):
//   ShowForMachine()   — panel slides to DualRightX and opens alongside a machine panel.
//                        Cursor state is managed by the machine interactor.
//   HideIfMachine()    — hides the panel when opened-by-machine closes.
//   ShowStandalone()   — panel centred at screen centre (I key).
//
// Controls:
//   I  — toggle standalone inventory
//   ESC — handled by MenuManager (closes if open)
//
// Setup: attach to the Player GameObject alongside MachineInteractor/ChestInteractor.
//        UIRoot and DragDropController must be in the scene.

using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerInventory : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [Tooltip("Number of general inventory slots (8–80).")]
    [Range(8, 80)]
    [SerializeField] private int _slotCount = 32;

    // ── Layout constants ──────────────────────────────────────────────────────

    private const int   GridCols   = 8;
    private const float GearRowH   = 40f;
    private const float GearRowGap =  4f;
    private const float TitleH     = 24f;
    private const float SepH       =  1f;
    private const float PadH       = 16f;
    private const float PadV       = 12f;
    private const float InnerGap   = 16f;
    private const float GearColW   = 200f;

    // X position of the inventory panel in canvas units.
    private const float DualRightX  = 200f;   // beside a left machine panel
    private const float StandaloneX =   0f;   // centred on screen

    // Pre-computed grid area width.
    private static readonly float GridW =
        GridCols * ItemSlotWidget.Size + (GridCols - 1) * ItemSlotWidget.SlotGap;   // ~412 px

    // ── Gear slot display names ───────────────────────────────────────────────

    private static readonly string[] GearSlotNames =
    {
        "Head", "Chest", "Legs", "Feet", "Primary", "Secondary", "Utility"
    };

    // ── Colours ───────────────────────────────────────────────────────────────

    private static readonly Color ColPanel    = new Color(0.10f, 0.10f, 0.10f, 0.97f);
    private static readonly Color ColSep      = new Color(0.30f, 0.30f, 0.30f, 1.00f);
    private static readonly Color ColGearBg   = new Color(0.18f, 0.18f, 0.26f, 1.00f);
    private static readonly Color ColGearOcc  = new Color(0.16f, 0.28f, 0.40f, 1.00f);
    private static readonly Color ColGearHov  = new Color(0.22f, 0.30f, 0.42f, 1.00f);
    private static readonly Color ColTypeHint = new Color(0.55f, 0.55f, 0.55f, 1.00f);

    // ── Cross-panel references (same GameObject) ──────────────────────────────

    private MachineInteractor _machineInteractor;
    private ChestInteractor   _chestInteractor;

    // ── Data ──────────────────────────────────────────────────────────────────

    private ItemStack[] _items;   // _slotCount entries
    private ItemStack[] _gear;    // GearSlotNames.Length entries (fixed)

    // ── UI references ─────────────────────────────────────────────────────────

    private bool          _open;
    private bool          _openedByMachine;
    private GameObject    _panel;
    private RectTransform _panelRT;

    // Inventory grid (rebuilt when slot count changes)
    private ItemSlotWidget[] _invWidgets;
    private Transform        _gridContainer;

    // Gear column (built once, refreshed on change)
    private readonly Image[]           _gearBgs    = new Image[GearSlotNames.Length];
    private readonly Image[]           _gearIcons  = new Image[GearSlotNames.Length];
    private readonly TextMeshProUGUI[] _gearLabels = new TextMeshProUGUI[GearSlotNames.Length];

    // ── MenuManager ESC interface ─────────────────────────────────────────────

    public bool IsOpen => _open;
    public void Close()
    {
        ClosePanel();
        // Ensure cursor is locked even if this was machine-mode (ESC closes all)
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;
    }

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    private void Awake()
    {
        _items             = new ItemStack[_slotCount];
        _gear              = new ItemStack[GearSlotNames.Length];
        _machineInteractor = GetComponent<MachineInteractor>();
        _chestInteractor   = GetComponent<ChestInteractor>();
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

        if (GameInput.InventoryDown)
        {
            if (_open && !_openedByMachine) ClosePanel();
            else if (!_open)               ShowStandalone();
        }

        if (_open) RefreshWidgets();
    }

    // Keeps slot widgets in sync with _items[].
    // Called every frame while open so external modifications (e.g. TryAddItem
    // called from MachineInteractor/ChestInteractor) are reflected immediately.
    private void RefreshWidgets()
    {
        if (_invWidgets == null) return;
        for (int i = 0; i < _invWidgets.Length && i < _items.Length; i++)
            _invWidgets[i].Refresh(_items[i]);
    }

    private void OnValidate()
    {
        _slotCount = Mathf.Clamp(_slotCount, 8, 80);
        if (_items != null && _items.Length != _slotCount) ResizeItemArray();
        if (_panel != null) RebuildInventoryGrid();
    }

    // ── Dual-panel API (called by MachineInteractor / ChestInteractor) ────────

    /// <summary>Shows the inventory at the right side of a dual-panel layout.</summary>
    public void ShowForMachine()
    {
        _openedByMachine = true;
        _open            = true;
        _panelRT.anchoredPosition = new Vector2(DualRightX, 0f);
        _panel.SetActive(true);
        // Cursor state managed by the caller (machine/chest)
    }

    /// <summary>Shows the inventory centred (standalone I-key mode).</summary>
    public void ShowStandalone()
    {
        _openedByMachine = false;
        _open            = true;
        _panelRT.anchoredPosition = new Vector2(StandaloneX, 0f);
        _panel.SetActive(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
    }

    /// <summary>Hides the inventory when the paired machine/chest panel closes.</summary>
    public void HideIfMachine()
    {
        if (!_openedByMachine) return;
        DragDropController.Instance?.CancelDrag();
        _open = false;
        _panel.SetActive(false);
        // Cursor state managed by the caller
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Stacks into existing slots then fills empty slots.  Returns leftover (0 = success).</summary>
    public int TryAddItem(string itemId, int quantity)
    {
        for (int i = 0; i < _items.Length && quantity > 0; i++)
            if (!_items[i].IsEmpty && _items[i].ItemId == itemId)
            {
                _items[i] = new ItemStack(itemId, _items[i].Quantity + quantity);
                quantity  = 0;
            }
        for (int i = 0; i < _items.Length && quantity > 0; i++)
            if (_items[i].IsEmpty)
            {
                _items[i] = new ItemStack(itemId, quantity);
                quantity  = 0;
            }
        return quantity;
    }

    /// <summary>Removes up to <paramref name="quantity"/> of <paramref name="itemId"/>.  Returns amount removed.</summary>
    public int TryRemoveItem(string itemId, int quantity)
    {
        int removed = 0;
        for (int i = 0; i < _items.Length && removed < quantity; i++)
        {
            if (_items[i].IsEmpty || _items[i].ItemId != itemId) continue;
            int take   = Mathf.Min(_items[i].Quantity, quantity - removed);
            _items[i]  = _items[i].Quantity - take > 0
                         ? new ItemStack(itemId, _items[i].Quantity - take)
                         : default;
            removed   += take;
        }
        return removed;
    }

    /// <summary>Total of <paramref name="itemId"/> across all inventory slots.</summary>
    public int CountItem(string itemId)
    {
        int n = 0;
        for (int i = 0; i < _items.Length; i++)
            if (!_items[i].IsEmpty && _items[i].ItemId == itemId)
                n += _items[i].Quantity;
        return n;
    }

    /// <summary>Equips an item to a gear slot.  Returns what was previously equipped (may be empty).</summary>
    public ItemStack SetGear(GearSlot slot, ItemStack item) { var old = _gear[(int)slot]; _gear[(int)slot] = item; return old; }
    public ItemStack GetGear(GearSlot slot)   => _gear[(int)slot];
    public void      ClearGear(GearSlot slot)  => _gear[(int)slot] = default;

    // ── Panel lifecycle ───────────────────────────────────────────────────────

    private void ClosePanel()
    {
        bool wasMachine  = _openedByMachine;
        DragDropController.Instance?.CancelDrag();
        _open            = false;
        _openedByMachine = false;
        _panel.SetActive(false);

        // Only lock cursor when we were managing it (standalone mode).
        // In machine mode the machine/chest interactor owns the cursor.
        if (!wasMachine)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible   = false;
        }
    }

    // ── Drag-drop callbacks ───────────────────────────────────────────────────

    // Double-click: transfer the item at slot i to whichever machine/chest is open.
    private void TransferFromInv(int i)
    {
        var stack = _items[i];
        if (stack.IsEmpty) return;

        int transferred = 0;

        if (_machineInteractor != null && _machineInteractor.IsOpen)
            transferred = _machineInteractor.TryAddToInput(stack);
        else if (_chestInteractor != null && _chestInteractor.IsOpen)
            transferred = _chestInteractor.TryAddToChest(stack);

        if (transferred <= 0) return;

        int remaining = stack.Quantity - transferred;
        _items[i] = remaining > 0 ? new ItemStack(stack.ItemId, remaining) : default;
        _invWidgets[i].Refresh(_items[i]);
    }

    private ItemStack TakeFromInv(int i)
    {
        var stack = _items[i];
        if (stack.IsEmpty) return default;
        _items[i] = default;
        _invWidgets[i].Refresh(default);
        return stack;
    }

    private ItemStack PlaceIntoInv(int i, ItemStack held)
    {
        if (held.IsEmpty) return default;

        var slot = _items[i];

        if (slot.IsEmpty)
        {
            _items[i] = held;
            _invWidgets[i].Refresh(held);
            return default;
        }

        if (slot.ItemId == held.ItemId)
        {
            // Merge stacks up to MaxStackSize
            int maxStack = ItemCatalogue.Get(held.ItemId)?.MaxStackSize ?? 50;
            int canAdd   = maxStack - slot.Quantity;
            if (canAdd <= 0) return held;
            int added    = held.Quantity < canAdd ? held.Quantity : canAdd;
            _items[i]    = new ItemStack(slot.ItemId, slot.Quantity + added);
            _invWidgets[i].Refresh(_items[i]);
            int leftover = held.Quantity - added;
            return leftover > 0 ? new ItemStack(held.ItemId, leftover) : default;
        }

        // Different items — swap
        _items[i] = held;
        _invWidgets[i].Refresh(held);
        return slot;
    }

    private ItemStack TakeFromGear(int i)
    {
        var stack = _gear[i];
        if (stack.IsEmpty) return default;
        _gear[i] = default;
        RefreshGearRow(i);
        return stack;
    }

    private ItemStack PlaceIntoGear(int i, ItemStack held)
    {
        if (held.IsEmpty) return default;

        var item = ItemCatalogue.Get(held.ItemId);
        if (item == null || !item.CanEquipToSlot((GearSlot)i)) return held;

        var prev  = _gear[i];
        _gear[i]  = new ItemStack(held.ItemId, 1);   // gear never stacks
        RefreshGearRow(i);
        return prev.IsEmpty ? default : prev;
    }

    private void RefreshGearRow(int i)
    {
        bool occ            = !_gear[i].IsEmpty;
        _gearBgs[i].color   = occ ? ColGearOcc : ColGearBg;
        _gearLabels[i].text = occ ? ToDisplayName(_gear[i].ItemId) : "—";

        if (occ)
        {
            var def = ItemLibrary.Get(_gear[i].ItemId);
            if (def?.Icon != null)
            {
                _gearIcons[i].sprite = def.Icon;
                _gearIcons[i].gameObject.SetActive(true);
            }
            else
            {
                _gearIcons[i].gameObject.SetActive(false);
            }
        }
        else
        {
            _gearIcons[i].gameObject.SetActive(false);
        }
    }

    // ── Panel construction ────────────────────────────────────────────────────

    private void BuildPanel()
    {
        int   rows   = RowsForSlotCount(_slotCount);
        float gridH  = GridHeight(rows);
        float gearH  = GearColumnHeight();
        float panelH = PanelHeight(gridH, gearH);
        float panelW = PadH + GridW + InnerGap + GearColW + PadH;

        _panel = new GameObject("InventoryPanel");
        _panel.transform.SetParent(UIRoot.Canvas.transform, false);
        _panelRT = _panel.AddComponent<RectTransform>();
        _panelRT.anchorMin        =
        _panelRT.anchorMax        =
        _panelRT.pivot            = new Vector2(0.5f, 0.5f);
        _panelRT.anchoredPosition = Vector2.zero;
        _panelRT.sizeDelta        = new Vector2(panelW, panelH);

        var panelBg = _panel.AddComponent<Image>();
        panelBg.color         = ColPanel;
        panelBg.raycastTarget = true;

        // ── Left column: inventory grid ───────────────────────────────────────
        BuildColumnHeader(_panel.transform, "Inventory", PadH);
        BuildSeparator(_panel.transform, PadH, GridW);

        var gridGO = new GameObject("InvGrid");
        gridGO.transform.SetParent(_panel.transform, false);
        var gridRT = gridGO.AddComponent<RectTransform>();
        gridRT.anchorMin        = new Vector2(0f, 1f);
        gridRT.anchorMax        = new Vector2(0f, 1f);
        gridRT.pivot            = new Vector2(0f, 1f);
        gridRT.anchoredPosition = new Vector2(PadH, -GridContentTop());
        gridRT.sizeDelta        = new Vector2(GridW, gridH);
        _gridContainer = gridGO.transform;

        PopulateGrid(rows);

        // ── Right column: gear ────────────────────────────────────────────────
        float gearX = PadH + GridW + InnerGap;
        BuildColumnHeader(_panel.transform, "Equipment", gearX);
        BuildSeparator(_panel.transform, gearX, GearColW);
        BuildGearRows(gearX);

        _panel.SetActive(false);
    }

    private void RebuildInventoryGrid()
    {
        int   rows  = RowsForSlotCount(_slotCount);
        float gridH = GridHeight(rows);
        float gearH = GearColumnHeight();

        var gridRT = _gridContainer.GetComponent<RectTransform>();
        gridRT.sizeDelta = new Vector2(GridW, gridH);

        var panelRT = _panel.GetComponent<RectTransform>();
        panelRT.sizeDelta = new Vector2(panelRT.sizeDelta.x, PanelHeight(gridH, gearH));

        PopulateGrid(rows);
    }

    private void PopulateGrid(int rows)
    {
        if (_invWidgets != null)
            foreach (var w in _invWidgets) w?.Destroy();

        _invWidgets = new ItemSlotWidget[_slotCount];

        for (int i = 0; i < _slotCount; i++)
        {
            int cap = i;
            var w   = ItemSlotWidget.Create(_gridContainer, $"Slot_{i}");
            w.SetGridCell(i % GridCols, i / GridCols);
            w.Refresh(_items[i]);
            w.OnSlotDoubleClicked = _ => TransferFromInv(cap);
            _invWidgets[i] = w;
        }
    }

    private void BuildGearRows(float xPos)
    {
        float yStart = GridContentTop();

        for (int i = 0; i < GearSlotNames.Length; i++)
        {
            int   gcap = i;
            float yOff = yStart + i * (GearRowH + GearRowGap);

            var rowGO = new GameObject($"GearRow_{GearSlotNames[i]}");
            rowGO.transform.SetParent(_panel.transform, false);
            var rowRT = rowGO.AddComponent<RectTransform>();
            rowRT.anchorMin        = new Vector2(0f, 1f);
            rowRT.anchorMax        = new Vector2(0f, 1f);
            rowRT.pivot            = new Vector2(0f, 1f);
            rowRT.anchoredPosition = new Vector2(xPos, -yOff);
            rowRT.sizeDelta        = new Vector2(GearColW, GearRowH);

            _gearBgs[i]       = rowGO.AddComponent<Image>();
            _gearBgs[i].color = ColGearBg;

            // Button for drag-drop interaction
            var rowBtn         = rowGO.AddComponent<Button>();
            rowBtn.image         = _gearBgs[i];
            rowBtn.targetGraphic = _gearBgs[i];
            var cb2 = rowBtn.colors;
            cb2.normalColor      = ColGearBg;
            cb2.highlightedColor = ColGearHov;
            cb2.pressedColor     = ColGearOcc;
            cb2.selectedColor    = ColGearBg;
            cb2.fadeDuration     = 0.05f;
            rowBtn.colors        = cb2;
            rowBtn.onClick.AddListener(() =>
                DragDropController.Instance?.HandleClick(
                    null,
                    () => TakeFromGear(gcap),
                    s  => PlaceIntoGear(gcap, s)));

            // Icon (24×24, anchored left)
            var iconGO = new GameObject("Icon");
            iconGO.transform.SetParent(rowGO.transform, false);
            _gearIcons[i]               = iconGO.AddComponent<Image>();
            _gearIcons[i].raycastTarget  = false;
            _gearIcons[i].preserveAspect = true;
            var iconRT = _gearIcons[i].GetComponent<RectTransform>();
            iconRT.anchorMin        = new Vector2(0f, 0.5f);
            iconRT.anchorMax        = new Vector2(0f, 0.5f);
            iconRT.pivot            = new Vector2(0f, 0.5f);
            iconRT.anchoredPosition = new Vector2(8f, 0f);
            iconRT.sizeDelta        = new Vector2(24f, 24f);
            iconGO.SetActive(false);

            // Slot type label — dim, left portion after icon
            var typeLabel = UIRoot.MakeText(rowGO.transform, "Type", 10, TextAlignmentOptions.Left);
            typeLabel.color = ColTypeHint;
            typeLabel.text  = GearSlotNames[i];
            var tlRT = typeLabel.GetComponent<RectTransform>();
            tlRT.anchorMin = new Vector2(0f, 0f);
            tlRT.anchorMax = new Vector2(0f, 1f);
            tlRT.offsetMin = new Vector2(36f, 0f);
            tlRT.offsetMax = new Vector2(90f, 0f);

            // Item name — right portion
            var itemLabel = UIRoot.MakeText(rowGO.transform, "Item", 11, TextAlignmentOptions.Right);
            var ilRT      = itemLabel.GetComponent<RectTransform>();
            ilRT.anchorMin = new Vector2(0f, 0f);
            ilRT.anchorMax = new Vector2(1f, 1f);
            ilRT.offsetMin = new Vector2(94f,  0f);
            ilRT.offsetMax = new Vector2(-8f,  0f);
            itemLabel.text = "—";
            _gearLabels[i] = itemLabel;
        }
    }

    // ── Shared helpers ────────────────────────────────────────────────────────

    private void BuildColumnHeader(Transform parent, string title, float xPos)
    {
        float colW = (xPos == PadH) ? GridW : GearColW;
        var lbl = UIRoot.MakeText(parent, title + "Title", 14, TextAlignmentOptions.Left);
        lbl.fontStyle = FontStyles.Bold;
        lbl.text      = title;
        var rt = lbl.GetComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0f, 1f);
        rt.anchorMax        = new Vector2(0f, 1f);
        rt.pivot            = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(xPos, -PadV);
        rt.sizeDelta        = new Vector2(colW, TitleH);
    }

    private void BuildSeparator(Transform parent, float xPos, float width)
    {
        var sep   = UIRoot.MakeImage(parent, "Sep", ColSep);
        var sepRT = sep.GetComponent<RectTransform>();
        sepRT.anchorMin        = new Vector2(0f, 1f);
        sepRT.anchorMax        = new Vector2(0f, 1f);
        sepRT.pivot            = new Vector2(0f, 1f);
        sepRT.anchoredPosition = new Vector2(xPos, -(PadV + TitleH + 4f));
        sepRT.sizeDelta        = new Vector2(width, SepH);
    }

    // ── Layout math ───────────────────────────────────────────────────────────

    private static int   RowsForSlotCount(int count) => Mathf.CeilToInt((float)count / GridCols);
    private static float GridHeight(int rows)         => rows * ItemSlotWidget.Size + (rows - 1) * ItemSlotWidget.SlotGap;
    private static float GearColumnHeight()           => GearSlotNames.Length * GearRowH + (GearSlotNames.Length - 1) * GearRowGap;
    private static float GridContentTop()             => PadV + TitleH + 4f + SepH + 8f;
    private static float PanelHeight(float gridH, float gearH) => GridContentTop() + Mathf.Max(gridH, gearH) + PadV;

    // ── Data helpers ──────────────────────────────────────────────────────────

    private void ResizeItemArray()
    {
        var resized = new ItemStack[_slotCount];
        System.Array.Copy(_items, resized, Mathf.Min(_items.Length, _slotCount));
        _items = resized;
    }

    private static string ToDisplayName(string id)
    {
        if (string.IsNullOrEmpty(id)) return "";
        var parts = id.Split('_');
        for (int i = 0; i < parts.Length; i++)
            if (parts[i].Length > 0)
                parts[i] = char.ToUpper(parts[i][0]) + parts[i].Substring(1);
        return string.Join(" ", parts);
    }
}
