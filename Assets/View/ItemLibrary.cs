// Catalogue of ItemDefinitionSO assets — the bridge from string item IDs to Unity UI data.
//
// Usage (from any view-layer code):
//   ItemLibrary.Get("iron_ore")     → ItemDefinitionSO (or null if not registered)
//
// Setup:
//   1. Create a library asset: right-click in Project → Reclamation → Item Library.
//   2. Populate the Items array with all your ItemDefinitionSO assets.
//   3. Add ItemLibraryLoader to the GameManager GameObject and assign the library asset.
//      ItemLibraryLoader.Awake() calls SetInstance() before any UI Start() runs.
//
// The lookup dictionary is rebuilt lazily on first Get() and invalidated by OnValidate()
// so Inspector edits are reflected immediately in Play Mode.

using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Reclamation/Item Library", fileName = "ItemLibrary")]
public class ItemLibrary : ScriptableObject
{
    [SerializeField] private ItemDefinitionSO[] _items = new ItemDefinitionSO[0];

    // ── Static singleton access ───────────────────────────────────────────────

    private static ItemLibrary _instance;

    /// <summary>Called by ItemLibraryLoader.Awake() to register the library.</summary>
    public static void SetInstance(ItemLibrary lib)
    {
        _instance = lib;
        if (lib != null) lib._lookup = null;   // force rebuild on next Get()
    }

    /// <summary>Returns the definition for <paramref name="itemId"/>, or null if not found.</summary>
    public static ItemDefinitionSO Get(string itemId)
    {
        if (_instance == null || string.IsNullOrEmpty(itemId)) return null;
        if (_instance._lookup == null) _instance.BuildLookup();
        _instance._lookup.TryGetValue(itemId, out var def);
        return def;
    }

    // ── Lazy lookup ───────────────────────────────────────────────────────────

    private Dictionary<string, ItemDefinitionSO> _lookup;

    private void BuildLookup()
    {
        int cap = _items != null ? _items.Length : 0;
        _lookup = new Dictionary<string, ItemDefinitionSO>(cap);
        if (_items == null) return;
        foreach (var def in _items)
            if (def != null && !string.IsNullOrEmpty(def.Id))
                _lookup[def.Id] = def;
    }

    // Invalidate on Inspector edits so changes are reflected immediately in Play Mode.
    private void OnValidate() => _lookup = null;
}
