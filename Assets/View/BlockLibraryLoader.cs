// Registers a BlockLibrary's designer-authored blocks into BlockRegistry at startup.
//
// Attach to the GameManager GameObject (or any persistent scene object) and assign a
// BlockLibrary. Awake() runs during scene load — before BuildMenu opens or
// SaveLoadManager auto-loads — so designer blocks are available everywhere they're used.
//
// Optional: with no library assigned the game still works; only the code-defined
// BlockCatalogue blocks are available.

using UnityEngine;

public class BlockLibraryLoader : MonoBehaviour
{
    [SerializeField] private BlockLibrary _library;

    private void Awake()
    {
        if (_library != null)
            _library.RegisterAll();
    }
}
