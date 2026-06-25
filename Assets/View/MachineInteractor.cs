// Press E while aiming at a production machine to open its configuration panel.
// The panel shows:
//   • Input buffer slots (drag items in/out)
//   • Recipe / resource selection buttons
//   • Output buffer slots (drag items to your inventory)
//   • A clear button
//
// Selecting a recipe rebuilds the panel immediately so slot widgets update.
// Clearing the recipe closes the panel.
//
// In dual-panel mode the panel anchors to DualLeftX = -340 and the player
// inventory opens alongside it on the right.
//
// Setup: attach to the Player GameObject alongside Hotbar and Raycaster.
//        PlayerInventory, UIRoot, and DragDropController must be in the scene.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MachineInteractor : MonoBehaviour
{
    // ── Layout constants ──────────────────────────────────────────────────────

    private const float PanelW     = 380f;
    private const float DualLeftX  = -340f;
    private const float TitleH     =  30f;
    private const float ButtonH    =  30f;
    private const float BtnGap     =   4f;
    private const float SectionH   =  20f;   // "Input" / "Output" label height
    private const float SepGap     =   6f;
    private const float PadV       =  10f;
    private const float PadH       =  12f;
    private const int   SlotsPerRow =  6;

    // Y offset from panel top where dynamic content starts (after title + separator).
    private static readonly float DynTop = PadV + TitleH + SepGap + 1f + SepGap;

    // ── Colours ───────────────────────────────────────────────────────────────

    private static readonly Color ColPanel    = new Color(0.10f, 0.10f, 0.10f, 0.95f);
    private static readonly Color ColSep      = new Color(0.35f, 0.35f, 0.35f, 1.00f);
    private static readonly Color ColBtnNormal= new Color(0.18f, 0.18f, 0.18f, 1.00f);
    private static readonly Color ColBtnHover = new Color(0.28f, 0.28f, 0.28f, 1.00f);
    private static readonly Color ColBtnPress = new Color(0.42f, 0.36f, 0.08f, 1.00f);
    private static readonly Color ColBtnClear = new Color(0.22f, 0.10f, 0.10f, 1.00f);
    private static readonly Color ColSection  = new Color(0.55f, 0.55f, 0.55f, 1.00f);

    // ── Component references ──────────────────────────────────────────────────

    private Raycaster       _raycaster;
    private Hotbar          _hotbar;
    private PlayerInventory _playerInventory;

    private Simulation Sim => GameManager.Instance.Simulation;

    // ── Panel UI ──────────────────────────────────────────────────────────────

    private GameObject      _panel;
    private RectTransform   _panelRT;
    private TextMeshProUGUI _titleText;

    // Dynamic content — destroyed and rebuilt each time a machine is opened or
    // a recipe is applied.
    private readonly List<GameObject>     _dynObjects    = new List<GameObject>();
    private readonly List<ItemSlotWidget> _inputWidgets  = new List<ItemSlotWidget>();
    private readonly List<ItemSlotWidget> _outputWidgets = new List<ItemSlotWidget>();

    // ── Interaction state ─────────────────────────────────────────────────────

    private bool        _open;
    private Block       _targetBlock;
    private BaseMachine _targetMachine;

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
            RefreshBuffers();
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

        var machine = Sim.Machines.Get(bv.Block.Id);
        if (machine == null) return;

        var ftype      = bv.Block.Definition.FunctionalType;
        bool hasOptions = ftype == FunctionalType.Miner
                       || RecipeCatalogue.ForMachineType(ftype).Length > 0;
        if (!hasOptions) return;

        _targetBlock   = bv.Block;
        _targetMachine = machine;

        _titleText.text = _targetBlock.Definition.DisplayName;
        PopulateDynamic();

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

        _open          = false;
        _targetBlock   = null;
        _targetMachine = null;
        _panel.SetActive(false);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;
    }

    // ── Dynamic content ───────────────────────────────────────────────────────

    private void PopulateDynamic()
    {
        DestroyDynamic();

        float yOff = DynTop;

        var ms   = _targetBlock.MachineState;
        var ftype = _targetBlock.Definition.FunctionalType;

        // ── Input section ─────────────────────────────────────────────────────
        if (ms.InputBuffer.Slots.Count > 0)
        {
            AddSectionLabel("Input", yOff);
            yOff += SectionH + 4f;
            BuildSlotWidgets(ms.InputBuffer.Slots, _inputWidgets, yOff, isInput: true);
            int rows = CeilDiv(ms.InputBuffer.Slots.Count, SlotsPerRow);
            yOff += rows * (ItemSlotWidget.Size + ItemSlotWidget.SlotGap)
                  - ItemSlotWidget.SlotGap + SepGap;
        }

        // ── Recipe / resource buttons ─────────────────────────────────────────
        if (ftype == FunctionalType.Miner)
        {
            var mp = _targetBlock.Definition.Params as MinerParams;
            if (mp != null)
            {
                foreach (var res in mp.ResourceTypes)
                {
                    string      capturedId = res;
                    MinerParams capturedP  = mp;
                    AddDynButton(ToDisplayName(capturedId), ColBtnNormal,
                                 () => ApplyMinerResource(capturedId, capturedP), yOff);
                    yOff += ButtonH + BtnGap;
                }
                if (mp.ResourceTypes.Length > 0) yOff -= BtnGap;
            }
        }
        else
        {
            var recipes = RecipeCatalogue.ForMachineType(ftype);
            foreach (var recipe in recipes)
            {
                Recipe captured = recipe;
                AddDynButton(recipe.DisplayName, ColBtnNormal,
                             () => ApplyRecipe(captured), yOff);
                yOff += ButtonH + BtnGap;
            }
            if (recipes.Length > 0) yOff -= BtnGap;
        }

        yOff += SepGap;

        // ── Output section ────────────────────────────────────────────────────
        if (ms.OutputBuffer.Slots.Count > 0)
        {
            AddDynSeparator(yOff);
            yOff += 1f + SepGap;
            AddSectionLabel("Output", yOff);
            yOff += SectionH + 4f;
            BuildSlotWidgets(ms.OutputBuffer.Slots, _outputWidgets, yOff, isInput: false);
            int rows = CeilDiv(ms.OutputBuffer.Slots.Count, SlotsPerRow);
            yOff += rows * (ItemSlotWidget.Size + ItemSlotWidget.SlotGap)
                  - ItemSlotWidget.SlotGap + SepGap;
        }

        // ── Clear button ──────────────────────────────────────────────────────
        AddDynSeparator(yOff);
        yOff += 1f + SepGap;
        AddDynButton("— Clear Recipe —", ColBtnClear, ClearAndClose, yOff);
        yOff += ButtonH + PadV;

        // Resize panel to fit content
        _panelRT.sizeDelta = new Vector2(PanelW, yOff);
    }

    private void DestroyDynamic()
    {
        foreach (var go in _dynObjects) if (go) Destroy(go);
        _dynObjects.Clear();
        _inputWidgets.Clear();
        _outputWidgets.Clear();
    }

    // ── Buffer widget construction ────────────────────────────────────────────

    private void BuildSlotWidgets(List<ItemStack> slots,
                                  List<ItemSlotWidget> list,
                                  float yOff, bool isInput)
    {
        for (int i = 0; i < slots.Count; i++)
        {
            int   cap  = i;
            bool  inBuf = isInput;
            int   col  = i % SlotsPerRow;
            int   row  = i / SlotsPerRow;
            float x    = PadH + col * (ItemSlotWidget.Size + ItemSlotWidget.SlotGap);
            float y    = yOff + row * (ItemSlotWidget.Size + ItemSlotWidget.SlotGap);
            string tag = isInput ? $"InSlot_{i}" : $"OutSlot_{i}";

            var w = ItemSlotWidget.Create(_panel.transform, tag);
            w.SetOffset(x, y);
            w.Refresh(slots[i]);

            // Double-click transfers the slot's contents to the player inventory.
            w.OnSlotDoubleClicked = _ => TransferBufferSlotToInventory(inBuf, cap);

            _dynObjects.Add(w.Root);
            list.Add(w);
        }
    }

    /// <summary>Accepts items from the inventory into the machine's input buffer.</summary>
    public int TryAddToInput(ItemStack stack)
    {
        var ms = _targetBlock?.MachineState;
        if (ms == null) return 0;
        return ms.InputBuffer.Add(stack.ItemId, stack.Quantity);
    }

    private void TransferBufferSlotToInventory(bool isInput, int idx)
    {
        var ms = _targetBlock?.MachineState;
        if (ms == null || _playerInventory == null) return;

        var buffer = isInput ? ms.InputBuffer : ms.OutputBuffer;
        if (idx >= buffer.Slots.Count) return;
        var slot = buffer.Slots[idx];
        if (slot.IsEmpty) return;

        int maxQty = ItemCatalogue.Get(slot.ItemId)?.MaxStackSize ?? 50;
        int toTake = slot.Quantity < maxQty ? slot.Quantity : maxQty;
        if (!buffer.TryRemove(slot.ItemId, toTake)) return;

        int leftover = _playerInventory.TryAddItem(slot.ItemId, toTake);
        if (leftover > 0)
            buffer.Add(slot.ItemId, leftover);
    }

    // ── Per-frame buffer refresh ──────────────────────────────────────────────

    private void RefreshBuffers()
    {
        var ms = _targetBlock?.MachineState;
        if (ms == null) return;

        int inCount  = System.Math.Min(_inputWidgets.Count,  ms.InputBuffer.Slots.Count);
        int outCount = System.Math.Min(_outputWidgets.Count, ms.OutputBuffer.Slots.Count);

        for (int i = 0; i < inCount;  i++) _inputWidgets[i].Refresh(ms.InputBuffer.Slots[i]);
        for (int i = 0; i < outCount; i++) _outputWidgets[i].Refresh(ms.OutputBuffer.Slots[i]);
    }

    // ── Drag-drop callbacks ───────────────────────────────────────────────────

    private ItemStack TakeFromInputBuffer(int idx)
    {
        var ms = _targetBlock?.MachineState;
        if (ms == null || idx >= ms.InputBuffer.Slots.Count) return default;
        var slot = ms.InputBuffer.Slots[idx];
        if (slot.IsEmpty) return default;

        int maxQty = ItemCatalogue.Get(slot.ItemId)?.MaxStackSize ?? 50;
        int toTake = slot.Quantity < maxQty ? slot.Quantity : maxQty;
        return ms.InputBuffer.TryRemove(slot.ItemId, toTake)
            ? new ItemStack(slot.ItemId, toTake)
            : default;
    }

    private ItemStack PlaceIntoInputBuffer(int idx, ItemStack held)
    {
        var ms = _targetBlock?.MachineState;
        if (ms == null || idx >= ms.InputBuffer.Slots.Count) return held;
        var slot = ms.InputBuffer.Slots[idx];
        if (slot.ItemId != held.ItemId) return held;   // wrong item type
        int added    = ms.InputBuffer.Add(held.ItemId, held.Quantity);
        int leftover = held.Quantity - added;
        return leftover > 0 ? new ItemStack(held.ItemId, leftover) : default;
    }

    private ItemStack TakeFromOutputBuffer(int idx)
    {
        var ms = _targetBlock?.MachineState;
        if (ms == null || idx >= ms.OutputBuffer.Slots.Count) return default;
        var slot = ms.OutputBuffer.Slots[idx];
        if (slot.IsEmpty) return default;

        int maxQty = ItemCatalogue.Get(slot.ItemId)?.MaxStackSize ?? 50;
        int toTake = slot.Quantity < maxQty ? slot.Quantity : maxQty;
        return ms.OutputBuffer.TryRemove(slot.ItemId, toTake)
            ? new ItemStack(slot.ItemId, toTake)
            : default;
    }

    // ── Actions ───────────────────────────────────────────────────────────────

    private void ApplyMinerResource(string resourceId, MinerParams mp)
    {
        if (_targetMachine is MinerMachine miner)
            miner.SetResourceNode(resourceId, 1f / mp.ExtractRatePerSecond, 1);
        PopulateDynamic();
    }

    private void ApplyRecipe(Recipe recipe)
    {
        _targetMachine.SetRecipe(recipe);
        PopulateDynamic();
    }

    private void ClearAndClose()
    {
        _targetMachine.SetRecipe(null);
        ClosePanel();
    }

    // ── Panel construction (static elements, built once) ──────────────────────

    private void BuildPanel()
    {
        _panel = new GameObject("MachineConfigPanel");
        _panel.transform.SetParent(UIRoot.Canvas.transform, false);
        _panelRT = _panel.AddComponent<RectTransform>();
        _panelRT.anchorMin        =
        _panelRT.anchorMax        =
        _panelRT.pivot            = new Vector2(0.5f, 0.5f);
        _panelRT.anchoredPosition = new Vector2(DualLeftX, 0f);
        _panelRT.sizeDelta        = new Vector2(PanelW, 100f);   // height set by PopulateDynamic

        var bg = _panel.AddComponent<Image>();
        bg.color         = ColPanel;
        bg.raycastTarget = true;

        // Title text
        _titleText = UIRoot.MakeText(_panel.transform, "Title", 14, TextAlignmentOptions.Left);
        _titleText.fontStyle = FontStyles.Bold;
        var titleRT = _titleText.GetComponent<RectTransform>();
        titleRT.anchorMin        = new Vector2(0f, 1f);
        titleRT.anchorMax        = new Vector2(1f, 1f);
        titleRT.pivot            = new Vector2(0f, 1f);
        titleRT.anchoredPosition = new Vector2(PadH, -PadV);
        titleRT.sizeDelta        = new Vector2(-PadH * 2f, TitleH);

        // Separator below title
        var sepGO = new GameObject("TitleSep");
        sepGO.transform.SetParent(_panel.transform, false);
        var sepRT = sepGO.AddComponent<RectTransform>();
        sepRT.anchorMin        = new Vector2(0f, 1f);
        sepRT.anchorMax        = new Vector2(1f, 1f);
        sepRT.pivot            = new Vector2(0f, 1f);
        sepRT.anchoredPosition = new Vector2(0f, -(PadV + TitleH + SepGap));
        sepRT.sizeDelta        = new Vector2(0f, 1f);
        var sepImg = sepGO.AddComponent<Image>();
        sepImg.color         = ColSep;
        sepImg.raycastTarget = false;

        _panel.SetActive(false);
    }

    // ── Dynamic element builders ──────────────────────────────────────────────

    private void AddSectionLabel(string text, float yOff)
    {
        var lbl = UIRoot.MakeText(_panel.transform, text + "Label", 11f, TextAlignmentOptions.Left);
        lbl.color = ColSection;
        lbl.text  = text.ToUpper();
        var rt = lbl.GetComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0f, 1f);
        rt.anchorMax        = new Vector2(1f, 1f);
        rt.pivot            = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(PadH, -yOff);
        rt.sizeDelta        = new Vector2(-PadH * 2f, SectionH);
        _dynObjects.Add(lbl.gameObject);
    }

    private void AddDynSeparator(float yOff)
    {
        var sepGO = new GameObject("Sep");
        sepGO.transform.SetParent(_panel.transform, false);
        var rt = sepGO.AddComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0f, 1f);
        rt.anchorMax        = new Vector2(1f, 1f);
        rt.pivot            = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(0f, -yOff);
        rt.sizeDelta        = new Vector2(0f, 1f);
        var img = sepGO.AddComponent<Image>();
        img.color         = ColSep;
        img.raycastTarget = false;
        _dynObjects.Add(sepGO);
    }

    private void AddDynButton(string label, Color bgColor, System.Action onClick, float yOff)
    {
        var btnGO = new GameObject("Btn");
        btnGO.transform.SetParent(_panel.transform, false);
        _dynObjects.Add(btnGO);

        var rt = btnGO.AddComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0f, 1f);
        rt.anchorMax        = new Vector2(1f, 1f);
        rt.pivot            = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(PadH, -yOff);
        rt.sizeDelta        = new Vector2(-PadH * 2f, ButtonH);

        var img = btnGO.AddComponent<Image>();
        img.color = bgColor;

        var btn = btnGO.AddComponent<Button>();
        btn.image         = img;
        btn.targetGraphic = img;
        var cb = btn.colors;
        cb.normalColor      = bgColor;
        cb.highlightedColor = ColBtnHover;
        cb.pressedColor     = ColBtnPress;
        cb.selectedColor    = bgColor;
        cb.fadeDuration     = 0.05f;
        btn.colors = cb;
        btn.onClick.AddListener(() => onClick());

        var lbl   = UIRoot.MakeText(btnGO.transform, "Label", 12, TextAlignmentOptions.Left);
        var lblRT = lbl.GetComponent<RectTransform>();
        UIRoot.StretchToParent(lblRT);
        lblRT.offsetMin = new Vector2(8f, 0f);
        lblRT.offsetMax = new Vector2(-8f, 0f);
        lbl.text = label;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static int CeilDiv(int n, int d) => (n + d - 1) / d;

    private static string ToDisplayName(string id)
    {
        if (string.IsNullOrEmpty(id)) return id;
        var parts = id.Split('_');
        for (int i = 0; i < parts.Length; i++)
            if (parts[i].Length > 0)
                parts[i] = char.ToUpper(parts[i][0]) + parts[i].Substring(1);
        return string.Join(" ", parts);
    }
}
