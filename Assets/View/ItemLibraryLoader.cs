// Registers the ItemLibrary ScriptableObject into the static singleton at startup.
//
// Attach to the GameManager GameObject (or any persistent scene object).
// Awake() runs during scene load — before any UI Start() that calls ItemLibrary.Get().

using UnityEngine;

public class ItemLibraryLoader : MonoBehaviour
{
    [SerializeField] private ItemLibrary _library;

    private void Awake()
    {
        if (_library != null)
            ItemLibrary.SetInstance(_library);
        else
            Debug.LogWarning("ItemLibraryLoader: no ItemLibrary assigned — item icons will be missing.");
    }
}
