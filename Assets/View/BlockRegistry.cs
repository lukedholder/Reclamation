// Runtime registry of all placeable block types — the bridge between the pure-C#
// BlockCatalogue (code-defined simulation blocks) and designer-authored blocks
// (BlockDefinitionSO assets, loaded via BlockLibraryLoader).
//
// It is seeded lazily from BlockCatalogue.All() the first time it is touched, so the
// game works with zero setup. A BlockLibrary then Register()s any SO blocks on top,
// optionally overriding a code block of the same id and supplying a visual prefab.
//
// Consumers (BuildMenu, SaveLoadManager, BlockViewFactory) read from here instead of
// BlockCatalogue so designer blocks are first-class: placeable, saveable, and visual.

using System.Collections.Generic;
using UnityEngine;

public static class BlockRegistry
{
    private static readonly List<BlockDefinition>         _all     = new List<BlockDefinition>();
    private static readonly Dictionary<string, int>       _index   = new Dictionary<string, int>();   // id → position in _all
    private static readonly Dictionary<string, GameObject> _prefabs = new Dictionary<string, GameObject>();
    private static bool _seeded;

    private static void EnsureSeeded()
    {
        if (_seeded) return;
        _seeded = true;
        foreach (var def in BlockCatalogue.All())
            Insert(def);
    }

    // Adds a new definition, or replaces an existing one with the same id in place
    // (so catalogue ordering stays stable for the build menu).
    private static void Insert(BlockDefinition def)
    {
        if (def == null || string.IsNullOrEmpty(def.Id)) return;
        if (_index.TryGetValue(def.Id, out int i)) _all[i] = def;
        else { _index[def.Id] = _all.Count; _all.Add(def); }
    }

    /// <summary>Registers (or overrides) a designer block and its optional prefab.</summary>
    public static void Register(BlockDefinition def, GameObject prefab)
    {
        EnsureSeeded();
        if (def == null || string.IsNullOrEmpty(def.Id)) return;
        Insert(def);
        if (prefab != null) _prefabs[def.Id] = prefab;
    }

    public static IReadOnlyList<BlockDefinition> All() { EnsureSeeded(); return _all; }

    public static BlockDefinition GetById(string id)
    {
        EnsureSeeded();
        return id != null && _index.TryGetValue(id, out int i) ? _all[i] : null;
    }

    public static GameObject GetPrefab(string id)
    {
        EnsureSeeded();
        _prefabs.TryGetValue(id ?? string.Empty, out var p);
        return p;
    }
}
