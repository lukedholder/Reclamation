// Tracks which block type the player currently has selected.
// Creative mode — blocks are infinite, no inventory required.
//
// Setup: attach to the Player GameObject alongside PlayerController.
//
// Controls:
//   Scroll wheel   — cycle through slots
//   1 – 7          — jump directly to a slot
//   R              — rotate selected block 90° (0 → 90 → 180 → 270 → 0)

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
    };

    public int             SelectedIndex      { get; private set; }
    public int             RotationSteps      { get; private set; }   // 0–3 → 0°/90°/180°/270°
    public BlockDefinition SelectedDefinition => Slots[SelectedIndex];

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

            string suffix = (i == SelectedIndex && RotationSteps != 0)
                ? $" {RotationSteps * 90}°"
                : "";

            GUI.Label(
                new Rect(startX + i * slotW, y, slotW, slotH),
                $"[{i + 1}] {Slots[i].DisplayName}{suffix}");

            GUI.color = oldColor;
        }
    }
}
