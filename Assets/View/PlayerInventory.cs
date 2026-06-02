// Player inventory and equipment panel.
//
// General slots — count is set in the Inspector (Range 8–80).
// Changing SlotCount at runtime (via Inspector or code) resizes the grid and
// the panel height automatically; item data is preserved up to the new limit.
//
// Gear slots — fixed set defined by GearSlot enum (Head, Chest, Legs, Feet,
// Primary, Secondary, Utility). Each appears as a labelled row in the right column.
//
// Controls:
//   I  — toggle inventory panel
//   ESC — handled by MenuManager (closes panel if open)
//
// Setup: attach to the Player GameObject.
//        UIRoot must be in the scene.
//        Add this component's IsOpen / Close() to MenuManager (see MenuManager.cs).

using UnityEngine;
using UnityEngine.UI;
using TMPro;

// ── Gear slot enum ────────────────────────────────────────────────────────────

public enum GearSlot
{
    Head      = 0,
    Chest     = 1,
    Legs      = 2,
    Feet      = 3,
    Primary   = 4,   // primary weapon
    Secondary = 5,   // secondary weapon / off-hand
    Utility   = 6,   // consumable / tool slot
}

// ── PlayerInventory ───────────────────────────────────────────────────────────

public class PlayerInventory : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [Tooltip("Number of general inventory slots (8–80). " +
             "Change in the Inspector while playing — the grid rebuilds automatically.")]
    [Range(8, 80)]
    [SerializeField] private int _slotCount = 32;

    // ── Layout constants ──────────────────────────────────────────────────────

    private const int   GridCols   = 8;
    private const float SlotSize   = 48f;
    private const float SlotGap    = 4f;
    private const float GearRowH   = 40f;
    private const float GearRowGap = 4f;
    private const float TitleH     = 24f;
    private const float SepH       = 1f;
    private const float PadH       = 16f;   // left / right outer padding
    private const float PadV       = 12f;   // top / bottom outer padding
    private const float InnerGap   = 16f;   // gap between inventory and gear columns
    private const float GearColW   = 200f;

    // Pre-computed grid area width (never changes).
    private static readonly float GridW =
        GridCols * SlotSize + (GridCols - 1) * SlotGap;   // 412 px

    // ── Gear slot display names ───────────────────────────────────────────────

    private static readonly string[] GearSlotNames =
    {
        "Head", "Chest", "Legs", "Feet", "Primary", "Secondary", "Utility"
    };

    // ── Colours ───────────────────────────────────────────────────────────────

    private static readonly Color ColPanel    = new Color(0.10f, 0.10f, 0.10f, 0.97f);
    private static readonly Color ColSep      = new Color(0.30f, 0.30f, 0.30f, 1.00f);
    private static readonly Color ColSlotBg   = new Color(0.18f, 0.18f, 0.18f, 1.00f);
    private static readonly Color ColSlotOcc  = new Color(0.20f, 0.32f, 0.20f, 1.00f);   // green tint
    private static readonly Color ColGearBg   = new Color(0.18f, 0.18f, 0.26f, 1.00f);   // blue tint
    private static readonly Color ColGearOcc  = new Color(0.16f, 0.28f, 0.40f, 1.00f);
    private static readonly Color ColQtyBadge = new Color(0.00f, 0.00f, 0.00f, 0.65f);
    private static readonly Color ColTypeHint = new Color(0.55f, 0.55f, 0.55f, 1.00f);   // gear-slot type label

    // ── Data ──────────────────────────────────────────────────────────────────

    private ItemStack[] _items;   // length == _slotCount; resized on change
    private ItemStack[] _gear;    // length == GearSlotNames.Length (fixed)

    // ── UI references ─────────────────────────────────────────────────────────

    private bool       _open;
    private GameObject _panel;

    // Inventory grid — rebuilt when slot count changes.
    private Image[]           _slotBgs;
    private TextMeshProUGUI[] _slotLabels;
    private TextMeshProUGUI[] _slotQtys;
    private Transform         _gridContainer;

    // Gear rows — built once, refreshed each frame.
    private readonly Image[]           _gearBgs    = new Image[GearSlotNames.Length];
    private readonly TextMeshProUGUI[] _gearLabels = new TextMeshProUGUI[GearSlotNames.Length];

    // ── MenuManager ESC interface ─────────────────────────────────────────────

    public bool IsOpen => _open;
    public void Close() => ClosePanel();

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    private void Awake()
    {
        _items = new ItemStack[_slotCount];
        _gear  = new ItemStack[GearSlotNames.Length];
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

        if (Input.GetKeyDown(KeyCode.I))
        {
            if (_open) ClosePanel();
            else       OpenPanel();
        }

        if (_open) RefreshUI();
    }

    // Called by Unity whenever a serialised field changes in the Inspector.
    private void OnValidate()
    {
        _slotCount = Mathf.Clamp(_slotCount, 8, 80);

        // Resize data array, keeping whatever was already in it.
        if (_items != null && _items.Length != _slotCount)
            ResizeItemArray();

        // Rebuild the inventory grid if the panel already exists (play mode).
        if (_panel != null)
            RebuildInventoryGrid();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Stacks <paramref name="itemId"/> into existing slots of the same type, then into
    /// the first empty slot. Returns the leftover quantity that did not fit (0 = all added).
    /// </summary>
    public int TryAddItem(string itemId, int quantity)
    {
        // Pass 1 — top up existing stacks.
        for (int i = 0; i < _items.Length && quantity > 0; i++)
        {
            if (!_items[i].IsEmpty && _items[i].ItemId == itemId)
            {
                _items[i] = new ItemStack(itemId, _items[i].Quantity + quantity);
                quantity  = 0;
            }
        }
        // Pass 2 — fill empty slots.
        for (int i = 0; i < _items.Length && quantity > 0; i++)
        {
            if (_items[i].IsEmpty)
            {
                _items[i] = new ItemStack(itemId, quantity);
                quantity  = 0;
            }
        }
        return quantity;   // 0 = success; > 0 = could not fit all items
    }

    /// <summary>
    /// Removes up to <paramref name="quantity"/> of <paramref name="itemId"/>.
    /// Returns the amount actually removed.
    /// </summary>
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

    /// <summary>Total number of <paramref name="itemId"/> carried across all inventory slots.</summary>
    public int CountItem(string itemId)
    {
        int n = 0;
        for (int i = 0; i < _items.Length; i++)
            if (!_items[i].IsEmpty && _items[i].ItemId == itemId)
                n += _items[i].Quantity;
        return n;
    }

    /// <summary>Equip an item to a gear slot. Returns what was previously equipped (may be empty).</summary>
    public ItemStack SetGear(GearSlot slot, ItemStack item)
    {
        var old = _gear[(int)slot];
        _gear[(int)slot] = item;
        return old;
    }

    public ItemStack GetGear(GearSlot slot)  => _gear[(int)slot];
    public void      ClearGear(GearSlot slot) => _gear[(int)slot] = default;

    // ── Panel lifecycle ───────────────────────────────────────────────────────

    private void OpenPanel()
    {
        RefreshUI();
        _open = true;
        _panel.SetActive(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
    }

    private void ClosePanel()
    {
        _open = false;
        _panel.SetActive(false);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;
    }

    // ── UI refresh ────────────────────────────────────────────────────────────

    private void RefreshUI()
    {
        // Inventory slots.
        for (int i = 0; i < _slotBgs.Length; i++)
        {
            bool occ            = !_items[i].IsEmpty;
            _slotBgs[i].color   = occ ? ColSlotOcc : ColSlotBg;
            _slotLabels[i].text = occ ? ToDisplayName(_items[i].ItemId) : "";
            _slotQtys[i].text   = occ && _items[i].Quantity > 1
                                  ? _items[i].Quantity.ToString("N0")
                                  : "";
        }

        // Gear rows.
        for (int i = 0; i < GearSlotNames.Length; i++)
        {
            bool occ            = !_gear[i].IsEmpty;
            _gearBgs[i].color   = occ ? ColGearOcc : ColGearBg;
            _gearLabels[i].text = occ ? ToDisplayName(_gear[i].ItemId) : "—";
        }
    }

    // ── Panel construction (called once in Start) ─────────────────────────────

    private void BuildPanel()
    {
        int   rows   = RowsForSlotCount(_slotCount);
        float gridH  = GridHeight(rows);
        float gearH  = GearColumnHeight();
        float panelH = PanelHeight(gridH, gearH);
        float panelW = PadH + GridW + InnerGap + GearColW + PadH;

        // ── Root panel ────────────────────────────────────────────────────────
        _panel = new GameObject("InventoryPanel");
        _panel.transform.SetParent(UIRoot.Canvas.transform, false);
        var panelRT = _panel.AddComponent<RectTransform>();
        panelRT.anchorMin        =
        panelRT.anchorMax        =
        panelRT.pivot            = new Vector2(0.5f, 0.5f);
        panelRT.anchoredPosition = Vector2.zero;
        panelRT.sizeDelta        = new Vector2(panelW, panelH);

        var panelBg = _panel.AddComponent<Image>();
        panelBg.color         = ColPanel;
        panelBg.raycastTarget = true;  // blocks world clicks while open

        // ── Left column: inventory ────────────────────────────────────────────
        BuildColumnHeader(_panel.transform, "Inventory", PadH);
        BuildSeparator(_panel.transform, PadH, GridW);

        // Grid container — repositioned/resized by RebuildInventoryGrid().
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

    // Rebuild only the inventory grid (gear column is untouched).
    private void RebuildInventoryGrid()
    {
        int   rows  = RowsForSlotCount(_slotCount);
        float gridH = GridHeight(rows);
        float gearH = GearColumnHeight();

        // Resize grid container.
        var gridRT = _gridContainer.GetComponent<RectTransform>();
        gridRT.sizeDelta = new Vector2(GridW, gridH);

        // Resize panel height to accommodate the new grid (or gear if taller).
        var panelRT = _panel.GetComponent<RectTransform>();
        panelRT.sizeDelta = new Vector2(panelRT.sizeDelta.x, PanelHeight(gridH, gearH));

        PopulateGrid(rows);

        if (_open) RefreshUI();
    }

    // ── Slot & row builders ───────────────────────────────────────────────────

    // Destroys any previous inventory slot objects and creates fresh ones.
    private void PopulateGrid(int rows)
    {
        // Destroy old slots.
        for (int i = _gridContainer.childCount - 1; i >= 0; i--)
            Destroy(_gridContainer.GetChild(i).gameObject);

        _slotBgs    = new Image[_slotCount];
        _slotLabels = new TextMeshProUGUI[_slotCount];
        _slotQtys   = new TextMeshProUGUI[_slotCount];

        for (int i = 0; i < _slotCount; i++)
        {
            int col = i % GridCols;
            int row = i / GridCols;

            // Slot root
            var slotGO = new GameObject($"Slot_{i}");
            slotGO.transform.SetParent(_gridContainer, false);
            var slotRT = slotGO.AddComponent<RectTransform>();
            slotRT.anchorMin        = new Vector2(0f, 1f);
            slotRT.anchorMax        = new Vector2(0f, 1f);
            slotRT.pivot            = new Vector2(0f, 1f);
            slotRT.anchoredPosition = new Vector2( col * (SlotSize + SlotGap),
                                                  -row * (SlotSize + SlotGap));
            slotRT.sizeDelta = new Vector2(SlotSize, SlotSize);

            _slotBgs[i] = slotGO.AddComponent<Image>();
            _slotBgs[i].color = ColSlotBg;

            // Item name — centred in upper portion of slot.
            var lbl   = UIRoot.MakeText(slotGO.transform, "Label", 9,
                                        TextAlignmentOptions.Center);
            lbl.enableWordWrapping = true;
            var lblRT = lbl.GetComponent<RectTransform>();
            lblRT.anchorMin = Vector2.zero;
            lblRT.anchorMax = Vector2.one;
            lblRT.offsetMin = new Vector2(2f, 14f);   // clear the qty strip at bottom
            lblRT.offsetMax = new Vector2(-2f, -2f);
            _slotLabels[i] = lbl;

            // Quantity badge strip — bottom of slot.
            var qtyBg   = UIRoot.MakeImage(slotGO.transform, "QtyBg", ColQtyBadge);
            var qtyBgRT = qtyBg.GetComponent<RectTransform>();
            qtyBgRT.anchorMin = new Vector2(0f, 0f);
            qtyBgRT.anchorMax = new Vector2(1f, 0f);
            qtyBgRT.offsetMin = new Vector2(0f,  0f);
            qtyBgRT.offsetMax = new Vector2(0f, 13f);

            var qty   = UIRoot.MakeText(slotGO.transform, "Qty", 9,
                                        TextAlignmentOptions.Right);
            var qtyRT = qty.GetComponent<RectTransform>();
            qtyRT.anchorMin = new Vector2(0f, 0f);
            qtyRT.anchorMax = new Vector2(1f, 0f);
            qtyRT.offsetMin = new Vector2(2f,  1f);
            qtyRT.offsetMax = new Vector2(-2f, 13f);
            _slotQtys[i] = qty;
        }
    }

    // Fixed gear rows — built once, labels updated by RefreshUI().
    private void BuildGearRows(float xPos)
    {
        float yStart = GridContentTop();

        for (int i = 0; i < GearSlotNames.Length; i++)
        {
            float yOff = yStart + i * (GearRowH + GearRowGap);

            var rowGO = new GameObject($"GearRow_{GearSlotNames[i]}");
            rowGO.transform.SetParent(_panel.transform, false);
            var rowRT = rowGO.AddComponent<RectTransform>();
            rowRT.anchorMin        = new Vector2(0f, 1f);
            rowRT.anchorMax        = new Vector2(0f, 1f);
            rowRT.pivot            = new Vector2(0f, 1f);
            rowRT.anchoredPosition = new Vector2(xPos, -yOff);
            rowRT.sizeDelta        = new Vector2(GearColW, GearRowH);

            _gearBgs[i]               = rowGO.AddComponent<Image>();
            _gearBgs[i].color         = ColGearBg;
            _gearBgs[i].raycastTarget = false;

            // Slot type label — dim, left side.
            var typeLabel   = UIRoot.MakeText(rowGO.transform, "Type", 10,
                                              TextAlignmentOptions.Left);
            typeLabel.color = ColTypeHint;
            typeLabel.text  = GearSlotNames[i];
            var typeLabelRT = typeLabel.GetComponent<RectTransform>();
            typeLabelRT.anchorMin = new Vector2(0f, 0f);
            typeLabelRT.anchorMax = new Vector2(0f, 1f);
            typeLabelRT.offsetMin = new Vector2(8f, 0f);
            typeLabelRT.offsetMax = new Vector2(78f, 0f);   // 70 px wide

            // Item name — right portion, white when occupied.
            var itemLabel   = UIRoot.MakeText(rowGO.transform, "Item", 11,
                                              TextAlignmentOptions.Right);
            var itemLabelRT = itemLabel.GetComponent<RectTransform>();
            itemLabelRT.anchorMin = new Vector2(0f, 0f);
            itemLabelRT.anchorMax = new Vector2(1f, 1f);
            itemLabelRT.offsetMin = new Vector2(82f, 0f);
            itemLabelRT.offsetMax = new Vector2(-8f, 0f);
            _gearLabels[i] = itemLabel;
        }
    }

    // ── Shared column helpers ─────────────────────────────────────────────────

    private void BuildColumnHeader(Transform parent, string title, float xPos)
    {
        var label = UIRoot.MakeText(parent, title + "Title", 14, TextAlignmentOptions.Left);
        label.fontStyle = FontStyles.Bold;
        label.text      = title;
        var rt = label.GetComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0f, 1f);
        rt.anchorMax        = new Vector2(0f, 1f);
        rt.pivot            = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(xPos, -PadV);
        rt.sizeDelta        = new Vector2(xPos == PadH ? GridW : GearColW, TitleH);
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

    // ── Layout helpers ────────────────────────────────────────────────────────

    private static int   RowsForSlotCount(int count) =>
        Mathf.CeilToInt((float)count / GridCols);

    private static float GridHeight(int rows) =>
        rows * SlotSize + (rows - 1) * SlotGap;

    private static float GearColumnHeight() =>
        GearSlotNames.Length * GearRowH + (GearSlotNames.Length - 1) * GearRowGap;

    // Y offset from panel top to where the content area (grid / gear rows) starts.
    private static float GridContentTop() =>
        PadV + TitleH + 4f + SepH + 8f;

    private static float PanelHeight(float gridH, float gearH) =>
        PadV + TitleH + 4f + SepH + 8f + Mathf.Max(gridH, gearH) + PadV;

    // ── Data helpers ──────────────────────────────────────────────────────────

    private void ResizeItemArray()
    {
        var resized = new ItemStack[_slotCount];
        System.Array.Copy(_items, resized, Mathf.Min(_items.Length, _slotCount));
        _items = resized;
    }

    // "iron_ore" → "Iron Ore"
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
