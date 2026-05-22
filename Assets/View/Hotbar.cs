// Tracks which block type the player currently has selected.
// Creative mode — blocks are infinite, no inventory required.
//
// Setup: attach to the Player GameObject alongside PlayerController.
//
// Controls:
//   Scroll wheel   — cycle through slots
//   1 – 7          — jump directly to a slot

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
    public BlockDefinition SelectedDefinition => Slots[SelectedIndex];

    private void Update()
    {
        HandleScrollInput();
        HandleNumberInput();
    }

    private void HandleScrollInput()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll > 0f) SelectedIndex = (SelectedIndex + 1) % Slots.Length;
        if (scroll < 0f) SelectedIndex = (SelectedIndex - 1 + Slots.Length) % Slots.Length;
    }

    private void HandleNumberInput()
    {
        for (int i = 0; i < Slots.Length && i < 9; i++)
            if (Input.GetKeyDown(KeyCode.Alpha1 + i))
                SelectedIndex = i;
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

            GUI.Label(
                new Rect(startX + i * slotW, y, slotW, slotH),
                $"[{i + 1}] {Slots[i].DisplayName}");

            GUI.color = oldColor;
        }
    }
}
