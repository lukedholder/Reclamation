// Visual and display data for one item type, assigned in the Unity Inspector.
//
// This ScriptableObject bridges the simulation layer (ItemCatalogue, plain C#)
// and the view layer (UI icons, slot tint colours).  One asset per item; all
// assets are registered in an ItemLibrary and accessed via ItemLibrary.Get(id).
//
// To create: right-click in the Project window → Reclamation → Item Definition.
// Set Id to exactly match the corresponding entry in ItemCatalogue (e.g. "iron_ore").

using UnityEngine;

[CreateAssetMenu(menuName = "Reclamation/Item Definition", fileName = "ItemDef_New")]
public class ItemDefinitionSO : ScriptableObject
{
    [Tooltip("Must match the item Id in ItemCatalogue exactly (e.g. \"iron_ore\").")]
    public string Id;

    [Tooltip("Icon shown in inventory and machine slots.  Assign a Sprite in the Inspector.")]
    public Sprite Icon;

    [Tooltip("Background tint applied to the slot when this item is present.  " +
             "Leave near-dark for items without a strong identity colour.")]
    public Color UiTint = new Color(0.22f, 0.22f, 0.22f, 1f);
}
