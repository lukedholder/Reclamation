// Central menu coordinator.
//
// Manages PauseMenuPanel and SettingsPanel with flat-swap navigation.
// Also handles all ESC routing so no other component needs to intercept ESC.
//
// ESC priority:
//   1a. Build menu open                   →  close it
//   1b. Machine configuration panel open  →  close it
//   1c. Chest contents panel open         →  close it
//   1d. Inventory panel open              →  close it
//   2.  A menu panel is open              →  route via panel.BackTarget
//   3.  Nothing open                      →  open pause menu
//
// Input suppression:
//   All game-input components (Hotbar, BlockPlacer, etc.) guard with:
//     if (MenuManager.IsOpen) return;
//   PlayerController already guards on Cursor.lockState, which this class manages.
//
// Cursor:
//   Locked when nothing is open (gameplay).
//   Unlocked when any panel is open.
//   MachineInteractor manages cursor independently for its recipe panel.
//
// Setup: attach to any persistent scene object (e.g. GameManager).
//        Player GameObject must have MachineInteractor and ChestInteractor components.

using UnityEngine;

public class MenuManager : MonoBehaviour
{
    public static MenuManager Instance { get; private set; }

    // True while the pause menu or settings panel is visible.
    // Does NOT include the machine configuration panel (MachineInteractor manages that).
    public static bool IsOpen => Instance != null && Instance._current != null;

    private MenuPanel    _current;

    private PauseMenuPanel _pause;
    private SettingsPanel  _settings;

    private BuildMenu         _buildMenu;
    private MachineInteractor _machineInteractor;
    private ChestInteractor   _chestInteractor;
    private PlayerInventory   _inventory;

    // ── Unity ─────────────────────────────────────────────────────────────────

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        _buildMenu         = FindObjectOfType<BuildMenu>();
        _machineInteractor = FindObjectOfType<MachineInteractor>();
        _chestInteractor   = FindObjectOfType<ChestInteractor>();
        _inventory         = FindObjectOfType<PlayerInventory>();

        _pause    = PauseMenuPanel.Create(this);
        _settings = SettingsPanel.Create(this);
        _settings.SetBackTarget(_pause);    // ESC from settings → back to pause

        // Apply persisted settings now that PlayerController etc. exist.
        SettingsPanel.ApplySaved();
    }

    private void Update()
    {
        if (!Input.GetKeyDown(KeyCode.Escape)) return;

        // Priority 1a: build menu open → close it.
        if (_buildMenu != null && _buildMenu.IsOpen)
        {
            _buildMenu.Close();
            return;
        }

        // Priority 1b: machine config panel open → close it, stay in gameplay.
        if (_machineInteractor != null && _machineInteractor.IsOpen)
        {
            _machineInteractor.Close();
            return;
        }

        // Priority 1c: chest contents panel open → close it, stay in gameplay.
        if (_chestInteractor != null && _chestInteractor.IsOpen)
        {
            _chestInteractor.Close();
            return;
        }

        // Priority 1d: inventory panel open → close it, stay in gameplay.
        if (_inventory != null && _inventory.IsOpen)
        {
            _inventory.Close();
            return;
        }

        // Priority 2: a menu panel is open → route via BackTarget.
        if (_current != null)
        {
            var back = _current.BackTarget;
            CloseAll();
            if (back != null) Open(back);
            return;
        }

        // Priority 3: nothing open → open pause menu.
        Open(_pause);
    }

    // ── Navigation ────────────────────────────────────────────────────────────

    public void Open(MenuPanel panel)
    {
        if (_current != null) _current.Hide();
        _current = panel;
        _current.Show();
        SetCursor(locked: false);
    }

    public void CloseAll()
    {
        if (_current != null) _current.Hide();
        _current = null;
        SetCursor(locked: true);
    }

    // ── Accessors ─────────────────────────────────────────────────────────────

    public PauseMenuPanel Pause    => _pause;
    public SettingsPanel  Settings => _settings;

    // ── Cursor ────────────────────────────────────────────────────────────────

    private static void SetCursor(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible   = !locked;
    }
}
