// Tracks which block type (or tool) the player currently has selected.
// Creative mode — blocks are infinite, no inventory required.
//
// Setup: attach to the Player GameObject alongside PlayerController.
//        UIRoot must be in the scene (attached to GameManager or similar).
//
// Controls:
//   Scroll wheel   — cycle through slots
//   1 – 8          — jump directly to a slot
//   R              — rotate selected block 90° (no effect on Wire slot)

using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class Hotbar : MonoBehaviour
{
    private static readonly BlockDefinition[] Slots =
    {
        BlockCatalogue.SmallCube,
        BlockCatalogue.SteamGenerator,
        BlockCatalogue.SmallBattery,
        BlockCatalogue.SmallPowerPole,
        BlockCatalogue.BasicMiner,
        BlockCatalogue.ElectricFurnace,
        BlockCatalogue.AssemblerMk1,
        null,   // slot 8 — Wire tool (no block definition)
    };

    public int             SelectedIndex      { get; private set; }
    public int             RotationSteps      { get; private set; }   // 0–3 → 0°/90°/180°/270°
    public BlockDefinition SelectedDefinition => Slots[SelectedIndex];  // null when Wire slot
    public bool            IsWireMode         => Slots[SelectedIndex] == null;

    // ── HUD constants ─────────────────────────────────────────────────────────

    private const float SlotW   = 120f;
    private const float SlotH   =  40f;
    private const float SlotGap =   4f;

    private static readonly Color ColNormal    = new Color(0.05f, 0.05f, 0.05f, 0.75f);
    private static readonly Color ColSelected  = new Color(0.55f, 0.45f, 0.00f, 0.90f);
    private static readonly Color ColNumBadge  = new Color(1.00f, 1.00f, 1.00f, 0.50f);

    // ── HUD state ─────────────────────────────────────────────────────────────

    private Image[]          _slotBgs;
    private TextMeshProUGUI[] _slotLabels;

    // ── Unity ─────────────────────────────────────────────────────────────────

    private void Start()
    {
        BuildHUD();
    }

    private void Update()
    {
        HandleScrollInput();
        HandleNumberInput();
        HandleRotationInput();
        RefreshHUD();
    }

    // ── Input ─────────────────────────────────────────────────────────────────

    private void HandleScrollInput()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll > 0f) { SelectedIndex = (SelectedIndex + 1) % Slots.Length; RotationSteps = 0; }
        if (scroll < 0f) { SelectedIndex = (SelectedIndex - 1 + Slots.Length) % Slots.Length; RotationSteps = 0; }
    }

    private void HandleNumberInput()
    {
        for (int i = 0; i < Slots.Length && i < 9; i++)
            if (Input.GetKeyDown(KeyCode.Alpha1 + i))
            {
                if (SelectedIndex != i) RotationSteps = 0;
                SelectedIndex = i;
            }
    }

    private void HandleRotationInput()
    {
        if (IsWireMode) return;
        if (Input.GetKeyDown(KeyCode.R))
            RotationSteps = (RotationSteps + 1) % 4;
    }

    // ── HUD building (called once in Start) ───────────────────────────────────

    private void BuildHUD()
    {
        float totalW = Slots.Length * SlotW + (Slots.Length - 1) * SlotGap;

        // Root container — anchored bottom-centre.
        var bar   = new GameObject("Hotbar");
        bar.transform.SetParent(UIRoot.Canvas.transform, false);
        var barRT = bar.AddComponent<RectTransform>();
        barRT.anchorMin        = new Vector2(0.5f, 0f);
        barRT.anchorMax        = new Vector2(0.5f, 0f);
        barRT.pivot            = new Vector2(0.5f, 0f);
        barRT.anchoredPosition = new Vector2(0f, 8f);
        barRT.sizeDelta        = new Vector2(totalW, SlotH);

        _slotBgs    = new Image[Slots.Length];
        _slotLabels = new TextMeshProUGUI[Slots.Length];

        for (int i = 0; i < Slots.Length; i++)
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
            numText.text  = (i + 1).ToString();

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
        for (int i = 0; i < Slots.Length; i++)
        {
            string name   = Slots[i] != null ? Slots[i].DisplayName : "Wire Tool";
            string suffix = (i == SelectedIndex && Slots[i] != null && RotationSteps != 0)
                ? $" ({RotationSteps * 90}°)"
                : "";

            _slotLabels[i].text = name + suffix;
            _slotBgs[i].color   = (i == SelectedIndex) ? ColSelected : ColNormal;
        }
    }
}
