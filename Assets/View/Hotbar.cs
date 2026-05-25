// Tracks which block type the player currently has selected.
// Creative mode — blocks are infinite, no inventory required.
//
// Setup: attach to the Player GameObject alongside PlayerController.
//
// Controls:
//   Scroll wheel   — cycle through slots
//   1 – 8          — jump directly to a slot
//   R              — rotate selected block 90° (0 → 90 → 180 → 270 → 0)
//                    (no effect when Wire slot is active)

using UnityEngine;

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
    public BlockDefinition SelectedDefinition => Slots[SelectedIndex];  // null when wire slot
    public bool            IsWireMode         => Slots[SelectedIndex] == null;

    private void Update()
    {
        HandleScrollInput();
        HandleNumberInput();
        HandleRotationInput();
    }

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
        // Wire tool has no rotation.
        if (IsWireMode) return;
        if (Input.GetKeyDown(KeyCode.R))
            RotationSteps = (RotationSteps + 1) % 4;
    }

    // ── HUD ───────────────────────────────────────────────────────────────────

    private void OnGUI()
    {
        float slotW  = 120f;
        float slotH  = 28f;
        float totalW = Slots.Length * slotW;
        float startX = (Screen.width - totalW) / 2f;
        float y      = Screen.height - slotH - 8f;

        for (int i = 0; i < Slots.Length; i++)
        {
            var oldColor = GUI.color;
            GUI.color = (i == SelectedIndex) ? Color.yellow : Color.white;

            string slotName = Slots[i] != null ? Slots[i].DisplayName : "Wire";
            string suffix   = (i == SelectedIndex && Slots[i] != null && RotationSteps != 0)
                ? $" {RotationSteps * 90}°"
                : "";

            GUI.Label(
                new Rect(startX + i * slotW, y, slotW, slotH),
                $"[{i + 1}] {slotName}{suffix}");

            GUI.color = oldColor;
        }
    }
}
