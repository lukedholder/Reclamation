// Satisfactory-style build menu.
//
// Press Q to open / close.  Category tabs on the left filter the block grid on
// the right.  Two ways to assign a block to the hotbar:
//
//   Hover a block + press 1 – 8  →  assigns to that slot, menu stays open.
//   Click a block                →  assigns to the currently active slot (or slot 0),
//                                   then closes the menu.
//
// Setup: attach to the Player GameObject alongside Hotbar.
//
// TODO: enforce build cost from BlockDefinition.ConstructionCost before allowing
//       placement.  Currently all blocks are free.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class BuildMenu : MonoBehaviour
{
    // ── Static access (read by Hotbar to suppress number-key input) ───────────

    public static BuildMenu Instance    { get; private set; }
    public static bool       IsMenuOpen => Instance != null && Instance._open;

    // ── Layout ────────────────────────────────────────────────────────────────

    private const float PanelW    = 960f;
    private const float PanelH    = 560f;
    private const float TopBarH   =  40f;
    private const float HintH     =  34f;
    private const float CatColW   = 158f;
    private const float PadH      =  10f;
    private const float PadV      =  10f;
    private const float EntryW    = 188f;
    private const float EntryH    =  64f;
    private const float EntryGap  =   6f;
    private const float CatBtnH   =  36f;
    private const float CatBtnGap =   4f;

    // ── Colours ───────────────────────────────────────────────────────────────

    private static readonly Color ColPanel   = new Color(0.08f, 0.08f, 0.10f, 0.97f);
    private static readonly Color ColTopBar  = new Color(0.04f, 0.04f, 0.06f, 1.00f);
    private static readonly Color ColSep     = new Color(0.22f, 0.22f, 0.26f, 1.00f);
    private static readonly Color ColCatNorm = new Color(0.12f, 0.12f, 0.15f, 1.00f);
    private static readonly Color ColCatSel  = new Color(0.22f, 0.32f, 0.48f, 1.00f);
    private static readonly Color ColCatHov  = new Color(0.16f, 0.20f, 0.28f, 1.00f);
    private static readonly Color ColHintBg  = new Color(0.05f, 0.05f, 0.07f, 1.00f);
    private static readonly Color ColDim     = new Color(0.50f, 0.50f, 0.52f, 1.00f);
    private static readonly Color ColAssign  = new Color(0.55f, 0.44f, 0.05f, 1.00f);

    private static Color CategoryBase(BlockCategory cat) => cat switch
    {
        BlockCategory.Structural => new Color(0.18f, 0.20f, 0.26f),
        BlockCategory.Power      => new Color(0.26f, 0.20f, 0.07f),
        BlockCategory.Production => new Color(0.10f, 0.22f, 0.10f),
        BlockCategory.Storage    => new Color(0.20f, 0.16f, 0.10f),
        BlockCategory.Defense    => new Color(0.26f, 0.09f, 0.09f),
        BlockCategory.Vehicle    => new Color(0.12f, 0.18f, 0.24f),
        _                        => new Color(0.16f, 0.16f, 0.18f),
    };

    private static Color CategoryHover(BlockCategory cat)
    {
        var c = CategoryBase(cat);
        return new Color(c.r + 0.12f, c.g + 0.12f, c.b + 0.12f, 1f);
    }

    // ── State ─────────────────────────────────────────────────────────────────

    private bool             _open;
    private Hotbar           _hotbar;
    private BlockDefinition  _hoveredDef;
    private BlockCategory?   _filterCategory;    // null = All

    // Flash feedback when a block is assigned to a slot.
    private BlockDefinition  _flashedDef;
    private float            _flashTimer;
    private const float      FlashDuration = 0.55f;

    // ── UI references ─────────────────────────────────────────────────────────

    private GameObject _panel;
    private Transform  _gridContainer;

    // (definition, entry background, flash overlay image)
    private readonly List<(BlockDefinition def, Image bg, Image flash)> _entries =
        new List<(BlockDefinition, Image, Image)>();

    // (category filter, button background)
    private readonly List<(BlockCategory? cat, Image bg)> _catBtns =
        new List<(BlockCategory?, Image)>();

    // ── Public interface ──────────────────────────────────────────────────────

    public bool IsOpen => _open;
    public void Close() => CloseMenu();

    // ── Unity ─────────────────────────────────────────────────────────────────

    private void Awake()
    {
        Instance = this;
        _hotbar  = GetComponent<Hotbar>();
    }

    private void Start()
    {
        BuildPanel();
    }

    private void Update()
    {
        // Don't interact with the pause / settings menu layer.
        if (MenuManager.IsOpen) return;

        // While piloting a vehicle, Q is the roll control — don't open the build menu.
        if (VehiclePilot.IsPiloting) return;

        // Q toggles the menu.
        if (GameInput.BuildMenuDown)
        {
            if (_open) CloseMenu();
            else       OpenMenu();
            return;
        }

        if (!_open) return;

        // Hover + number key (1–8) → assign to that hotbar slot.
        if (_hoveredDef != null)
        {
            int slot = GameInput.HotbarSlotDown;
            if (slot >= 0 && slot < 8)
                AssignAndFlash(_hoveredDef, slot);
        }

        // Decay the assignment flash overlay.
        if (_flashTimer > 0f)
        {
            _flashTimer -= Time.deltaTime;
            RefreshFlash();
            if (_flashTimer <= 0f)
            {
                _flashedDef = null;
                RefreshFlash();
            }
        }
    }

    // ── Open / Close ──────────────────────────────────────────────────────────

    private void OpenMenu()
    {
        _open           = true;
        _filterCategory = null;
        _hoveredDef     = null;
        _flashedDef     = null;
        _flashTimer     = 0f;
        RefreshCatButtons();
        PopulateGrid();
        _panel.SetActive(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
    }

    private void CloseMenu()
    {
        _open       = false;
        _hoveredDef = null;
        _panel.SetActive(false);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;
    }

    // ── Assignment ────────────────────────────────────────────────────────────

    private void AssignAndFlash(BlockDefinition def, int slotIndex)
    {
        _hotbar.AssignSlot(slotIndex, def);
        _flashedDef = def;
        _flashTimer = FlashDuration;
        RefreshFlash();
    }

    // ── Grid ─────────────────────────────────────────────────────────────────

    private void PopulateGrid()
    {
        // Remove previous entries.
        foreach (var (_, bg, _) in _entries)
            if (bg != null) Object.Destroy(bg.gameObject);
        _entries.Clear();

        // Filter blocks by selected category (code blocks + designer-authored blocks).
        var visible = new List<BlockDefinition>();
        foreach (var def in BlockRegistry.All())
            if (_filterCategory == null || def.Category == _filterCategory)
                visible.Add(def);

        // Layout in rows of `cols` entries.
        float areaW = PanelW - CatColW - 2f - PadH * 2f;   // available width in grid area
        int   cols  = Mathf.Max(1, Mathf.FloorToInt((areaW + EntryGap) / (EntryW + EntryGap)));

        for (int i = 0; i < visible.Count; i++)
        {
            float x = (i % cols) * (EntryW + EntryGap);
            float y = (i / cols) * (EntryH + EntryGap);
            CreateEntry(visible[i], x, y);
        }
    }

    private void CreateEntry(BlockDefinition def, float x, float y)
    {
        Color baseCol  = CategoryBase(def.Category);
        Color hoverCol = CategoryHover(def.Category);

        // Root
        var go = new GameObject($"Entry_{def.Id}");
        go.transform.SetParent(_gridContainer, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0f, 1f);
        rt.anchorMax        = new Vector2(0f, 1f);
        rt.pivot            = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, -y);
        rt.sizeDelta        = new Vector2(EntryW, EntryH);

        var bg           = go.AddComponent<Image>();
        bg.color         = baseCol;
        bg.raycastTarget = true;

        // Button for click-to-assign, with hover colour tint.
        var btn         = go.AddComponent<Button>();
        btn.image       = bg;
        btn.transition  = Selectable.Transition.ColorTint;
        var bc = btn.colors;
        bc.normalColor      = baseCol;
        bc.highlightedColor = hoverCol;
        bc.pressedColor     = hoverCol;
        bc.selectedColor    = baseCol;
        bc.fadeDuration     = 0.05f;
        btn.colors          = bc;

        var captured = def;
        btn.onClick.AddListener(() =>
        {
            // Click: assign to the active hotbar slot (fallback to slot 0).
            int target = (_hotbar.SelectedIndex < 8) ? _hotbar.SelectedIndex : 0;
            AssignAndFlash(captured, target);
            CloseMenu();
        });

        // Hover tracking.
        var trigger = go.AddComponent<EventTrigger>();
        AddTrigger(trigger, EventTriggerType.PointerEnter, _ => _hoveredDef = captured);
        AddTrigger(trigger, EventTriggerType.PointerExit,  _ => { if (_hoveredDef == captured) _hoveredDef = null; });

        // Flash overlay (covers the entry; alpha driven by _flashTimer).
        var flashGO = new GameObject("Flash");
        flashGO.transform.SetParent(go.transform, false);
        var flashRT = flashGO.AddComponent<RectTransform>();
        UIRoot.StretchToParent(flashRT);
        var flashImg           = flashGO.AddComponent<Image>();
        flashImg.color         = new Color(ColAssign.r, ColAssign.g, ColAssign.b, 0f);
        flashImg.raycastTarget = false;

        // Left accent bar (category colour, slightly brighter).
        var accentGO = new GameObject("Accent");
        accentGO.transform.SetParent(go.transform, false);
        var accentRT = accentGO.AddComponent<RectTransform>();
        accentRT.anchorMin        = new Vector2(0f, 0f);
        accentRT.anchorMax        = new Vector2(0f, 1f);
        accentRT.pivot            = new Vector2(0f, 0.5f);
        accentRT.anchoredPosition = Vector2.zero;
        accentRT.sizeDelta        = new Vector2(3f, 0f);
        var accentImg       = accentGO.AddComponent<Image>();
        accentImg.color     = hoverCol;
        accentImg.raycastTarget = false;

        // Block name.
        var nameT = UIRoot.MakeText(go.transform, "Name", 12f, TextAlignmentOptions.Left);
        nameT.fontStyle = FontStyles.Bold;
        nameT.text      = def.DisplayName;
        var nameRT = nameT.GetComponent<RectTransform>();
        nameRT.anchorMin = new Vector2(0f, 0.45f);
        nameRT.anchorMax = new Vector2(1f, 1.00f);
        nameRT.offsetMin = new Vector2(12f, 0f);
        nameRT.offsetMax = new Vector2(-6f, -6f);

        // Build cost line.
        // TODO: replace "Free" with formatted cost from def.ConstructionCost
        //       e.g. string.Join("  ", def.ConstructionCost.Select(s => $"{s.Quantity}× {s.ItemId}"))
        var costT = UIRoot.MakeText(go.transform, "Cost", 9f, TextAlignmentOptions.Left);
        costT.color = ColDim;
        costT.text  = "Free";   // TODO: show actual build cost
        var costRT  = costT.GetComponent<RectTransform>();
        costRT.anchorMin = new Vector2(0f, 0f);
        costRT.anchorMax = new Vector2(1f, 0.45f);
        costRT.offsetMin = new Vector2(12f, 4f);
        costRT.offsetMax = new Vector2(-6f, 0f);

        _entries.Add((def, bg, flashImg));
    }

    private void RefreshFlash()
    {
        foreach (var (def, _, flash) in _entries)
        {
            float a = (def == _flashedDef && _flashTimer > 0f)
                ? Mathf.Clamp01(_flashTimer / FlashDuration) * 0.75f
                : 0f;
            flash.color = new Color(ColAssign.r, ColAssign.g, ColAssign.b, a);
        }
    }

    // ── Category buttons ──────────────────────────────────────────────────────

    private void RefreshCatButtons()
    {
        foreach (var (cat, bg) in _catBtns)
            bg.color = cat == _filterCategory ? ColCatSel : ColCatNorm;
    }

    // ── Panel construction (once, in Start) ───────────────────────────────────

    private void BuildPanel()
    {
        // ── Root panel ────────────────────────────────────────────────────────
        _panel = new GameObject("BuildMenuPanel");
        _panel.transform.SetParent(UIRoot.Canvas.transform, false);
        var panelRT = _panel.AddComponent<RectTransform>();
        panelRT.anchorMin = panelRT.anchorMax = panelRT.pivot = new Vector2(0.5f, 0.5f);
        panelRT.anchoredPosition = Vector2.zero;
        panelRT.sizeDelta        = new Vector2(PanelW, PanelH);
        var panelBg           = _panel.AddComponent<Image>();
        panelBg.color         = ColPanel;
        panelBg.raycastTarget = true;

        // ── Top bar ───────────────────────────────────────────────────────────
        var topBar = MakeRect(_panel.transform, "TopBar",
            anchorMin: new Vector2(0, 1), anchorMax: new Vector2(1, 1),
            pivot: new Vector2(0, 1), pos: Vector2.zero, size: new Vector2(0, TopBarH));
        topBar.AddComponent<Image>().color = ColTopBar;

        var titleT = UIRoot.MakeText(topBar.transform, "Title", 15f, TextAlignmentOptions.Left);
        titleT.fontStyle = FontStyles.Bold;
        titleT.text      = "BUILD MENU";
        Stretch(titleT.GetComponent<RectTransform>(), left: 14f, right: 50f);

        var closeGO  = MakeRect(topBar.transform, "CloseBtn",
            anchorMin: new Vector2(1, 0), anchorMax: new Vector2(1, 1),
            pivot: new Vector2(1, 0.5f), pos: new Vector2(-6f, 0), size: new Vector2(38f, 0));
        var closeBg  = closeGO.AddComponent<Image>();
        closeBg.color = new Color(0.35f, 0.10f, 0.10f, 1f);
        var closeBtn = closeGO.AddComponent<Button>();
        closeBtn.image = closeBg;
        var cc = closeBtn.colors;
        cc.normalColor      = new Color(0.35f, 0.10f, 0.10f, 1f);
        cc.highlightedColor = new Color(0.55f, 0.18f, 0.18f, 1f);
        cc.pressedColor     = new Color(0.65f, 0.22f, 0.22f, 1f);
        cc.selectedColor    = cc.normalColor;
        closeBtn.colors     = cc;
        closeBtn.onClick.AddListener(CloseMenu);
        var xText = UIRoot.MakeText(closeGO.transform, "X", 13f, TextAlignmentOptions.Center);
        xText.text = "✕";
        UIRoot.StretchToParent(xText.GetComponent<RectTransform>());

        // ── Hint bar (bottom) ─────────────────────────────────────────────────
        var hintBar = MakeRect(_panel.transform, "HintBar",
            anchorMin: Vector2.zero, anchorMax: new Vector2(1, 0),
            pivot: Vector2.zero, pos: Vector2.zero, size: new Vector2(0, HintH));
        hintBar.AddComponent<Image>().color = ColHintBg;
        var hintT = UIRoot.MakeText(hintBar.transform, "Hint", 9.5f, TextAlignmentOptions.Center);
        hintT.color = ColDim;
        hintT.text  = "Hover a block  →  press  1 – 8  to assign to a hotbar slot  |  Click to assign to active slot and close";
        Stretch(hintT.GetComponent<RectTransform>(), left: 12f, right: 12f);

        // ── Body (between bars) ───────────────────────────────────────────────
        var body = MakeRect(_panel.transform, "Body",
            anchorMin: Vector2.zero, anchorMax: Vector2.one,
            pivot: Vector2.zero, pos: Vector2.zero, size: Vector2.zero);
        var bodyRT    = body.GetComponent<RectTransform>();
        bodyRT.offsetMin = new Vector2(0f,  HintH);
        bodyRT.offsetMax = new Vector2(0f, -TopBarH);

        // ── Category column ───────────────────────────────────────────────────
        var catCol = MakeRect(body.transform, "CatColumn",
            anchorMin: new Vector2(0, 0), anchorMax: new Vector2(0, 1),
            pivot: new Vector2(0, 1), pos: Vector2.zero, size: new Vector2(CatColW, 0));

        // Vertical separator between category column and grid.
        var sep = MakeRect(body.transform, "Sep",
            anchorMin: new Vector2(0, 0), anchorMax: new Vector2(0, 1),
            pivot: new Vector2(0, 0.5f), pos: new Vector2(CatColW, 0), size: new Vector2(1f, 0));
        sep.AddComponent<Image>().color = ColSep;

        // Category filter definitions.
        var categories = new (string label, BlockCategory? cat)[]
        {
            ("All",        null),
            ("Structural", BlockCategory.Structural),
            ("Power",      BlockCategory.Power),
            ("Production", BlockCategory.Production),
            ("Storage",    BlockCategory.Storage),
            ("Defense",    BlockCategory.Defense),
            ("Vehicle",    BlockCategory.Vehicle),
        };

        float catY = PadV;
        foreach (var (label, cat) in categories)
        {
            var btnGO = MakeRect(catCol.transform, $"Cat_{label}",
                anchorMin: new Vector2(0, 1), anchorMax: new Vector2(1, 1),
                pivot: new Vector2(0, 1),
                pos: new Vector2(PadH, -catY), size: new Vector2(-PadH * 2f, CatBtnH));

            var btnBg           = btnGO.AddComponent<Image>();
            btnBg.color         = cat == _filterCategory ? ColCatSel : ColCatNorm;
            btnBg.raycastTarget = true;

            var btn             = btnGO.AddComponent<Button>();
            btn.image           = btnBg;
            btn.transition      = Selectable.Transition.None;   // manual colour management

            var capturedCat = cat;
            btn.onClick.AddListener(() =>
            {
                _filterCategory = capturedCat;
                RefreshCatButtons();
                PopulateGrid();
            });

            // Hover tint via EventTrigger.
            var trig = btnGO.AddComponent<EventTrigger>();
            AddTrigger(trig, EventTriggerType.PointerEnter, _ => btnBg.color = ColCatHov);
            AddTrigger(trig, EventTriggerType.PointerExit,
                _ => btnBg.color = capturedCat == _filterCategory ? ColCatSel : ColCatNorm);

            _catBtns.Add((cat, btnBg));

            var lbl = UIRoot.MakeText(btnGO.transform, "Label", 11f, TextAlignmentOptions.Left);
            lbl.text = label;
            Stretch(lbl.GetComponent<RectTransform>(), left: 10f, right: 4f);

            catY += CatBtnH + CatBtnGap;
        }

        // ── Grid area ─────────────────────────────────────────────────────────
        var gridArea = MakeRect(body.transform, "GridArea",
            anchorMin: Vector2.zero, anchorMax: Vector2.one,
            pivot: Vector2.zero, pos: Vector2.zero, size: Vector2.zero);
        var gaRT        = gridArea.GetComponent<RectTransform>();
        gaRT.offsetMin  = new Vector2(CatColW + 1f + PadH, PadV);
        gaRT.offsetMax  = new Vector2(-PadH, -PadV);
        _gridContainer  = gridArea.transform;

        _panel.SetActive(false);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static GameObject MakeRect(Transform parent, string name,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt           = go.AddComponent<RectTransform>();
        rt.anchorMin     = anchorMin;
        rt.anchorMax     = anchorMax;
        rt.pivot         = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta     = size;
        return go;
    }

    private static void Stretch(RectTransform rt, float left = 0, float right = 0,
                                                   float bottom = 0, float top = 0)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(left,   bottom);
        rt.offsetMax = new Vector2(-right, -top);
    }

    private static void AddTrigger(EventTrigger trigger, EventTriggerType type,
                                   UnityEngine.Events.UnityAction<BaseEventData> action)
    {
        var entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(action);
        trigger.triggers.Add(entry);
    }
}
