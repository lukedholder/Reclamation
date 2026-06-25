// Tracks which block type (or tool) the player currently has selected.
// Creative mode — blocks are infinite, no inventory required.
//
// Setup: attach to the Player GameObject alongside PlayerController.
//        UIRoot must be in the scene (attached to GameManager or similar).
//
// Controls:
//   Scroll wheel   — rotate selected block (15° on terrain, 90° on construct grids)
//   1 – 9          — jump directly to a slot; same slot again toggles deselect

using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class Hotbar : MonoBehaviour
{
    // Mutable so BuildMenu can reassign any of the 8 block slots (indices 0–7).
    // Indices 8 and 9 are reserved for Wire and Belt tools and are never reassigned.
    private BlockDefinition[] _slots;

    public int             SelectedIndex      { get; private set; }
    public float           RotationAngleY     => _rotationAngleY;    // 0°–345° in 15° steps on terrain
    public int             RotationSteps      => Mathf.RoundToInt(_rotationAngleY / 90f) % 4; // 0–3
    public BlockDefinition SelectedDefinition => NoBlockActive ? null : _slots[SelectedIndex];
    public bool            IsWireMode         => SelectedIndex == 8;   // key 9
    public bool            IsBeltMode         => SelectedIndex == 9;   // key 0
    public bool            IsToolMode         => IsWireMode || IsBeltMode;
    public bool            NoBlockActive      => IsToolMode || _deselected;

    private bool      _deselected;
    private float     _rotationAngleY;   // degrees, kept in [0, 360)
    private Raycaster _raycaster;

    // ── HUD constants ─────────────────────────────────────────────────────────

    private const float SlotW   = 120f;
    private const float SlotH   =  40f;
    private const float SlotGap =   4f;

    private static readonly Color ColNormal     = new Color(0.05f, 0.05f, 0.05f, 0.75f);
    private static readonly Color ColSelected   = new Color(0.55f, 0.45f, 0.00f, 0.90f);
    private static readonly Color ColCancelled  = new Color(0.15f, 0.15f, 0.15f, 0.60f);
    private static readonly Color ColNumBadge   = new Color(1.00f, 1.00f, 1.00f, 0.50f);

    // ── HUD state ─────────────────────────────────────────────────────────────

    private Image[]          _slotBgs;
    private TextMeshProUGUI[] _slotLabels;

    // ── Unity ─────────────────────────────────────────────────────────────────

    private void Awake()
    {
        _raycaster = GetComponent<Raycaster>();

        // Default slot assignments — can be overwritten at runtime via AssignSlot().
        _slots = new BlockDefinition[]
        {
            BlockCatalogue.SmallCube,        // key 1
            BlockCatalogue.SteamGenerator,   // key 2
            BlockCatalogue.GunTurret,        // key 3
            BlockCatalogue.SmallPowerPole,   // key 4
            BlockCatalogue.BasicMiner,       // key 5
            BlockCatalogue.ElectricFurnace,  // key 6
            BlockCatalogue.AssemblerMk1,     // key 7
            BlockCatalogue.StorageChest,     // key 8
            null,                            // key 9 — Wire tool  (not reassignable)
            null,                            // key 0 — Belt tool  (not reassignable)
        };
    }

    // ── Public API (called by BuildMenu) ──────────────────────────────────────

    /// <summary>
    /// Assigns a block definition to the given block slot (0–7) and activates it.
    /// Slots 8–9 (wire/belt tools) are ignored.
    /// </summary>
    public void AssignSlot(int slotIndex, BlockDefinition def)
    {
        if (slotIndex < 0 || slotIndex > 7) return;
        _slots[slotIndex] = def;
        SelectedIndex   = slotIndex;
        _deselected     = false;
        _rotationAngleY = 0f;
    }

    /// <summary>Returns the definition currently in a given slot (null for tools or empty).</summary>
    public BlockDefinition GetSlot(int slotIndex) =>
        (slotIndex >= 0 && slotIndex < _slots.Length) ? _slots[slotIndex] : null;

    private void Start()
    {
        BuildHUD();
    }

    private void Update()
    {
        // Hotbar selection/rotation is on-foot gameplay only (covers menus, panels, piloting).
        if (GameInput.Context == InputContext.Gameplay)
        {
            HandleScrollInput();
            HandleNumberInput();
        }
        RefreshHUD();
    }

    // ── Input ─────────────────────────────────────────────────────────────────

    private void HandleScrollInput()
    {
        float scroll = GameInput.Scroll;
        if (scroll == 0f || NoBlockActive) return;

        // Step size: 90° when hovering a construct block, 15° on terrain.
        bool onConstruct = _raycaster != null && _raycaster.HasHit &&
                           _raycaster.Hit.collider != null &&
                           _raycaster.Hit.collider.GetComponent<BlockView>() != null;

        float step = onConstruct ? 90f : 15f;
        float dir  = scroll > 0f ? 1f : -1f;
        _rotationAngleY = (_rotationAngleY + dir * step + 360f) % 360f;

        // Snap to the nearest 90° multiple when on a construct grid.
        if (onConstruct)
            _rotationAngleY = (Mathf.Round(_rotationAngleY / 90f) % 4) * 90f;
    }

    private void HandleNumberInput()
    {
        // Keys 1–9 map to slots 0–8; key 0 maps to slot 9 (Belt tool).
        int i = GameInput.HotbarSlotDown;
        if (i < 0 || i >= _slots.Length) return;

        if (i == SelectedIndex && _slots[i] != null)
            _deselected = !_deselected;     // same slot again: toggle cancel
        else
        {
            if (SelectedIndex != i) _rotationAngleY = 0f;
            SelectedIndex = i;
            _deselected   = false;           // switching slots always re-arms
        }
    }

    // ── HUD building (called once in Start) ───────────────────────────────────

    private void BuildHUD()
    {
        float totalW = _slots.Length * SlotW + (_slots.Length - 1) * SlotGap;

        // Root container — anchored bottom-centre.
        var bar   = new GameObject("Hotbar");
        bar.transform.SetParent(UIRoot.Canvas.transform, false);
        var barRT = bar.AddComponent<RectTransform>();
        barRT.anchorMin        = new Vector2(0.5f, 0f);
        barRT.anchorMax        = new Vector2(0.5f, 0f);
        barRT.pivot            = new Vector2(0.5f, 0f);
        barRT.anchoredPosition = new Vector2(0f, 8f);
        barRT.sizeDelta        = new Vector2(totalW, SlotH);

        _slotBgs    = new Image[_slots.Length];
        _slotLabels = new TextMeshProUGUI[_slots.Length];

        for (int i = 0; i < _slots.Length; i++)
        {
            // Slot panel
            var slot   = new GameObject($"Slot{i + 1}");
            slot.transform.SetParent(bar.transform, false);
            var slotRT = slot.AddComponent<RectTransform>();
            slotRT.anchorMin        = Vector2.zero;
            slotRT.anchorMax        = Vector2.zero;
            slotRT.pivot            = Vector2.zero;
            slotRT.sizeDelta        = new Vector2(SlotW, SlotH);
            slotRT.anchoredPosition = new Vector2(i * (SlotW + SlotGap), 0f);

            _slotBgs[i]               = slot.AddComponent<Image>();
            _slotBgs[i].color         = ColNormal;
            _slotBgs[i].raycastTarget = false;

            // Slot-number badge — explicit rect, top-left corner, static.
            // Slot-number badge — top-left corner, static.
            var numText = UIRoot.MakeText(slot.transform, "Num", 9, TextAlignmentOptions.TopLeft);
            var numRT   = numText.GetComponent<RectTransform>();
            numRT.anchorMin        = Vector2.zero;
            numRT.anchorMax        = Vector2.zero;
            numRT.pivot            = Vector2.zero;
            numRT.anchoredPosition = new Vector2(4f, SlotH - 14f);
            numRT.sizeDelta        = new Vector2(20f, 14f);
            numText.color = ColNumBadge;
            numText.text  = i < 9 ? (i + 1).ToString() : "0";

            // Item name — full-slot rect, centred, updated every frame.
            var label   = UIRoot.MakeText(slot.transform, "Label", 12, TextAlignmentOptions.Center);
            var labelRT = label.GetComponent<RectTransform>();
            labelRT.anchorMin        = Vector2.zero;
            labelRT.anchorMax        = Vector2.zero;
            labelRT.pivot            = Vector2.zero;
            labelRT.anchoredPosition = Vector2.zero;
            labelRT.sizeDelta        = new Vector2(SlotW, SlotH);
            _slotLabels[i] = label;
        }
    }

    // ── HUD update (called every frame) ───────────────────────────────────────

    private void RefreshHUD()
    {
        for (int i = 0; i < _slots.Length; i++)
        {
            string name   = _slots[i] != null ? _slots[i].DisplayName
                          : i == 8           ? "Wire Tool"
                          :                   "Belt Tool";
            string suffix = (i == SelectedIndex && _slots[i] != null && _rotationAngleY != 0f)
                ? $" ({_rotationAngleY:F0}°)"
                : "";

            _slotLabels[i].text = name + suffix;

            Color bg;
            if      (i != SelectedIndex) bg = ColNormal;
            else if (_deselected)        bg = ColCancelled;
            else                         bg = ColSelected;
            _slotBgs[i].color = bg;
        }
    }
}
