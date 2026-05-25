// Press E while aiming at a production machine to open its configuration panel.
// Miners let you choose which resource to extract.
// Furnaces and Assemblers let you choose a production recipe.
// Press E or Escape to close the panel.
//
// While the panel is open the cursor is unlocked so the player can click
// buttons. Raycaster returns HasHit=false when the cursor is not locked,
// which automatically suppresses block placement and dismantling.
//
// Setup: attach to the Player GameObject alongside Hotbar and Raycaster.
//        UIRoot must be in the scene (provides the Canvas and Font).

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MachineInteractor : MonoBehaviour
{
    // ── Layout constants (Canvas pixels at 1920×1080 reference) ──────────────

    private const float PanelW   = 280f;
    private const float TitleH   =  30f;
    private const float ButtonH  =  30f;
    private const float BtnGap   =   4f;
    private const float PadV     =   8f;
    private const float PadH     =  10f;
    private const float SepGap   =   6f;   // gap between title and first button

    // ── Colours ───────────────────────────────────────────────────────────────

    private static readonly Color ColPanel     = new Color(0.10f, 0.10f, 0.10f, 0.95f);
    private static readonly Color ColSeparator = new Color(0.35f, 0.35f, 0.35f, 1.00f);
    private static readonly Color ColBtnNormal = new Color(0.18f, 0.18f, 0.18f, 1.00f);
    private static readonly Color ColBtnHover  = new Color(0.28f, 0.28f, 0.28f, 1.00f);
    private static readonly Color ColBtnPress  = new Color(0.42f, 0.36f, 0.08f, 1.00f);
    private static readonly Color ColBtnClear  = new Color(0.22f, 0.10f, 0.10f, 1.00f);

    // ── Component references ──────────────────────────────────────────────────

    private Raycaster _raycaster;
    private Hotbar    _hotbar;

    private Simulation Sim => GameManager.Instance.Simulation;

    // ── Panel UI ──────────────────────────────────────────────────────────────

    private GameObject       _panel;
    private RectTransform    _panelRT;
    private TextMeshProUGUI  _titleText;
    private Transform        _btnContainer;
    private List<GameObject> _dynButtons = new List<GameObject>();

    // ── Interaction state ─────────────────────────────────────────────────────

    private bool        _open;
    private Block       _targetBlock;
    private BaseMachine _targetMachine;

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
        // Auto-close if the machine was dismantled while the panel was open.
        if (_open && (_targetBlock == null || !Sim.Blocks.ById.ContainsKey(_targetBlock.Id)))
        {
            ClosePanel();
            return;
        }

        bool eKey  = Input.GetKeyDown(KeyCode.E);
        bool esc   = Input.GetKeyDown(KeyCode.Escape);

        if (_open)
        {
            if (eKey || esc) ClosePanel();
        }
        else
        {
            // Only open when cursor is locked (FPS mode) and wire tool is not active.
            if (eKey && !_hotbar.IsWireMode) TryOpen();
        }
    }

    // ── Panel lifecycle ───────────────────────────────────────────────────────

    private void TryOpen()
    {
        if (!_raycaster.HasHit) return;

        var bv = _raycaster.Hit.collider.GetComponent<BlockView>();
        if (bv == null) return;

        var machine = Sim.Machines.Get(bv.Block.Id);
        if (machine == null) return;    // structural / power block — not configurable

        _targetBlock   = bv.Block;
        _targetMachine = machine;

        _titleText.text = _targetBlock.Definition.DisplayName;
        PopulateButtons();

        _open = true;
        _panel.SetActive(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
    }

    private void ClosePanel()
    {
        _open          = false;
        _targetBlock   = null;
        _targetMachine = null;
        _panel.SetActive(false);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;
    }

    // ── Button population ─────────────────────────────────────────────────────

    private void PopulateButtons()
    {
        foreach (var go in _dynButtons) Destroy(go);
        _dynButtons.Clear();

        var type = _targetBlock.Definition.FunctionalType;

        if (type == FunctionalType.Miner)
        {
            // Miners pick which resource to extract from MinerParams.ResourceTypes.
            var mp = _targetBlock.Definition.Params as MinerParams;
            if (mp != null)
            {
                foreach (var res in mp.ResourceTypes)
                {
                    string      capturedId = res;
                    MinerParams capturedP  = mp;
                    AddButton(ToDisplayName(capturedId), ColBtnNormal,
                              () => ApplyMinerResource(capturedId, capturedP));
                }
            }
        }
        else
        {
            // All other machines pick from RecipeCatalogue, filtered by machine type.
            foreach (var recipe in RecipeCatalogue.ForMachineType(type))
            {
                Recipe captured = recipe;
                AddButton(recipe.DisplayName, ColBtnNormal, () => ApplyRecipe(captured));
            }
        }

        // "Clear" always appears last.
        AddButton("— Clear Recipe —", ColBtnClear, ClearAndClose);

        ResizePanel();
    }

    // ── Actions ───────────────────────────────────────────────────────────────

    private void ApplyMinerResource(string resourceId, MinerParams mp)
    {
        if (_targetMachine is MinerMachine miner)
            miner.SetResourceNode(resourceId, 1f / mp.ExtractRatePerSecond, 1);
        ClosePanel();
    }

    private void ApplyRecipe(Recipe recipe)
    {
        _targetMachine.SetRecipe(recipe);
        ClosePanel();
    }

    private void ClearAndClose()
    {
        _targetMachine.SetRecipe(null);
        ClosePanel();
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

    // Recalculate the panel height based on the current dynamic buttons.
    private void ResizePanel()
    {
        int   n      = _dynButtons.Count;
        float btnH   = n * ButtonH + (n - 1) * BtnGap;
        float totalH = PadV + TitleH + SepGap + 1f + SepGap + btnH + PadV;
        _panelRT.sizeDelta = new Vector2(PanelW, totalH);

        // Reposition button container below separator.
        var bcRT = _btnContainer.GetComponent<RectTransform>();
        float btnContainerTop = PadV + TitleH + SepGap + 1f + SepGap;
        bcRT.anchoredPosition = new Vector2(PadH, -btnContainerTop);
        bcRT.sizeDelta        = new Vector2(PanelW - PadH * 2f, btnH);
    }

    // ── Panel construction ────────────────────────────────────────────────────

    private void BuildPanel()
    {
        // Root panel — centred on screen.
        _panel = new GameObject("MachineConfigPanel");
        _panel.transform.SetParent(UIRoot.Canvas.transform, false);
        _panelRT = _panel.AddComponent<RectTransform>();
        _panelRT.anchorMin        =
        _panelRT.anchorMax        =
        _panelRT.pivot            = new Vector2(0.5f, 0.5f);
        _panelRT.anchoredPosition = Vector2.zero;
        _panelRT.sizeDelta        = new Vector2(PanelW, 100f);   // height overwritten on open

        // Opaque background — raycastTarget=true blocks clicks from reaching the world.
        var bg = _panel.AddComponent<Image>();
        bg.color         = ColPanel;
        bg.raycastTarget = true;

        // Title — top of panel.
        _titleText = UIRoot.MakeText(_panel.transform, "Title", 14, TextAlignmentOptions.Left);
        _titleText.fontStyle = FontStyles.Bold;
        var titleRT = _titleText.GetComponent<RectTransform>();
        titleRT.anchorMin        = new Vector2(0f, 1f);
        titleRT.anchorMax        = new Vector2(1f, 1f);
        titleRT.pivot            = new Vector2(0f, 1f);
        titleRT.anchoredPosition = new Vector2(PadH, -PadV);
        titleRT.sizeDelta        = new Vector2(-PadH * 2f, TitleH);

        // Separator line.
        var sepGO = new GameObject("Separator");
        sepGO.transform.SetParent(_panel.transform, false);
        var sepRT = sepGO.AddComponent<RectTransform>();
        sepRT.anchorMin        = new Vector2(0f, 1f);
        sepRT.anchorMax        = new Vector2(1f, 1f);
        sepRT.pivot            = new Vector2(0f, 1f);
        sepRT.anchoredPosition = new Vector2(0f, -(PadV + TitleH + SepGap));
        sepRT.sizeDelta        = new Vector2(0f, 1f);
        var sepImg = sepGO.AddComponent<Image>();
        sepImg.color         = ColSeparator;
        sepImg.raycastTarget = false;

        // Button container — repositioned by ResizePanel().
        var bcGO = new GameObject("Buttons");
        bcGO.transform.SetParent(_panel.transform, false);
        var bcRT = bcGO.AddComponent<RectTransform>();
        bcRT.anchorMin = new Vector2(0f, 1f);
        bcRT.anchorMax = new Vector2(0f, 1f);
        bcRT.pivot     = new Vector2(0f, 1f);
        _btnContainer  = bcGO.transform;

        _panel.SetActive(false);
    }

    // Appends one button to _btnContainer at the next vertical slot.
    private void AddButton(string label, Color bgColor, System.Action onClick)
    {
        int i = _dynButtons.Count;

        var btnGO = new GameObject($"Btn_{i}");
        btnGO.transform.SetParent(_btnContainer, false);

        var btnRT = btnGO.AddComponent<RectTransform>();
        btnRT.anchorMin        = new Vector2(0f, 1f);
        btnRT.anchorMax        = new Vector2(1f, 1f);
        btnRT.pivot            = new Vector2(0f, 1f);
        btnRT.anchoredPosition = new Vector2(0f, -i * (ButtonH + BtnGap));
        btnRT.sizeDelta        = new Vector2(0f, ButtonH);

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
        btn.colors          = cb;

        btn.onClick.AddListener(() => onClick());

        // Label
        var lbl = UIRoot.MakeText(btnGO.transform, "Label", 12, TextAlignmentOptions.Left);
        var lblRT = lbl.GetComponent<RectTransform>();
        UIRoot.StretchToParent(lblRT);
        lblRT.offsetMin = new Vector2(8f, 0f);
        lblRT.offsetMax = new Vector2(-8f, 0f);
        lbl.text = label;

        _dynButtons.Add(btnGO);
    }
}
