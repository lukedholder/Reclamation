// A collection of designer-authored BlockDefinitionSO assets, registered into the
// runtime BlockRegistry at startup by BlockLibraryLoader.
//
// Setup:
//   1. Create a library asset: right-click in Project → Reclamation → Block Library.
//   2. Populate the Blocks array with your BlockDefinitionSO assets.
//   3. Add BlockLibraryLoader to the GameManager GameObject and assign this library.

using UnityEngine;

[CreateAssetMenu(menuName = "Reclamation/Block Library", fileName = "BlockLibrary")]
public class BlockLibrary : ScriptableObject
{
    [SerializeField] private BlockDefinitionSO[] _blocks = new BlockDefinitionSO[0];

    public void RegisterAll()
    {
        if (_blocks == null) return;
        foreach (var so in _blocks)
        {
            if (so == null || string.IsNullOrEmpty(so.Id)) continue;
            BlockRegistry.Register(so.ToDefinition(), so.Prefab);
        }
    }
}
