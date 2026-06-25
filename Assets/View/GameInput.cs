// Single source of truth for all player controls.
//
// Every key / button / axis the game reads lives here, exposed as named, semantic
// signals (GameInput.InteractDown, GameInput.Move, ...). No other script should call
// UnityEngine.Input directly — they read GameInput instead. This keeps the whole
// control scheme in one file and makes rebinding a one-line change.
//
// CONTEXT
//   GameInput.Context reports what the player is currently doing (Gameplay, Piloting,
//   a panel, the build menu, or paused), derived from the existing global state.
//   Components gate on it with a single check instead of re-deriving the same flags:
//
//       if (GameInput.Context != InputContext.Gameplay) return;
//
// Bindings are static FIELDS (not consts) so a future settings/rebinding screen can
// reassign them at runtime. Migrating to Unity's Input System later means changing
// only this file — the rest of the game keeps reading the same properties.

using UnityEngine;

public enum InputContext
{
    Gameplay,   // first-person on foot; cursor locked
    Piloting,   // seated in a vehicle; cursor locked, VehicleController drives
    Panel,      // a machine / chest / inventory panel is open; cursor free
    BuildMenu,  // the build menu is open; cursor free
    Paused,     // pause / settings menu is open; cursor free
}

public static class GameInput
{
    // ── Rebindable bindings ─────────────────────────────────────────────────────
    public static KeyCode Interact    = KeyCode.E;
    public static KeyCode Inventory   = KeyCode.I;
    public static KeyCode BuildMenuKey = KeyCode.Q;
    public static KeyCode Pilot       = KeyCode.F;
    public static KeyCode Pause      = KeyCode.Escape;
    public static KeyCode Ascend     = KeyCode.Space;        // jump on foot / thrust up when piloting
    public static KeyCode Descend    = KeyCode.LeftControl;  // thrust down when piloting
    public static KeyCode RollLeft   = KeyCode.Q;
    public static KeyCode RollRight  = KeyCode.E;
    public static KeyCode Save       = KeyCode.F5;
    public static KeyCode Load       = KeyCode.F9;

    public const int PrimaryButton   = 0;   // left mouse
    public const int SecondaryButton = 1;   // right mouse

    private const string AxisHorizontal = "Horizontal";
    private const string AxisVertical   = "Vertical";
    private const string AxisMouseX     = "Mouse X";
    private const string AxisMouseY     = "Mouse Y";
    private const string AxisScroll     = "Mouse ScrollWheel";

    // ── Movement / look (raw; consumers apply their own sensitivity) ─────────────
    public static Vector2 Move   => new Vector2(Input.GetAxisRaw(AxisHorizontal), Input.GetAxisRaw(AxisVertical));
    public static Vector2 Look   => new Vector2(Input.GetAxis(AxisMouseX), Input.GetAxis(AxisMouseY));
    public static float   Scroll => Input.GetAxis(AxisScroll);

    // Raw cursor position (screen space) — used by the drag-drop ghost.
    public static Vector3 MousePosition => Input.mousePosition;

    // ── Discrete actions (true only on the frame the input went down) ────────────
    public static bool PrimaryDown   => Input.GetMouseButtonDown(PrimaryButton);
    public static bool SecondaryDown => Input.GetMouseButtonDown(SecondaryButton);
    public static bool InteractDown  => Input.GetKeyDown(Interact);
    public static bool InventoryDown => Input.GetKeyDown(Inventory);
    public static bool BuildMenuDown => Input.GetKeyDown(BuildMenuKey);
    public static bool PilotDown     => Input.GetKeyDown(Pilot);
    public static bool PauseDown     => Input.GetKeyDown(Pause);
    public static bool JumpDown      => Input.GetKeyDown(Ascend);
    public static bool SaveDown      => Input.GetKeyDown(Save);
    public static bool LoadDown      => Input.GetKeyDown(Load);

    // ── Held actions (level) ─────────────────────────────────────────────────────
    public static bool AscendHeld    => Input.GetKey(Ascend);
    public static bool DescendHeld   => Input.GetKey(Descend);
    public static bool RollLeftHeld  => Input.GetKey(RollLeft);
    public static bool RollRightHeld => Input.GetKey(RollRight);

    /// <summary>Hotbar slot pressed this frame: 0-8 for keys 1-9, 9 for key 0, or -1 if none.</summary>
    public static int HotbarSlotDown
    {
        get
        {
            for (int i = 0; i < 9; i++)
                if (Input.GetKeyDown(KeyCode.Alpha1 + i)) return i;
            if (Input.GetKeyDown(KeyCode.Alpha0)) return 9;
            return -1;
        }
    }

    // ── Context ──────────────────────────────────────────────────────────────────
    // Derived from existing global state so every component shares one definition of
    // "what is the player doing right now" instead of re-checking these flags.
    public static InputContext Context
    {
        get
        {
            if (MenuManager.IsOpen)                        return InputContext.Paused;
            if (BuildMenu.IsMenuOpen)                      return InputContext.BuildMenu;
            if (VehiclePilot.IsPiloting)                   return InputContext.Piloting;
            if (Cursor.lockState != CursorLockMode.Locked) return InputContext.Panel;
            return InputContext.Gameplay;
        }
    }

    public static bool IsGameplay => Context == InputContext.Gameplay;
}
