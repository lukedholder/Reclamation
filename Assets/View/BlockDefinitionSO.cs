// Designer-authored block type, created in the Unity Inspector.
//
// Bridges to the pure-C# simulation: ToDefinition() produces a BlockDefinition that
// the simulation consumes, exactly like ItemDefinitionSO feeds string ids to the UI.
// This keeps the simulation layer Unity-free while letting designers add blocks and
// assign real prefabs/meshes without editing code.
//
// Best suited to structural / decorative blocks (FunctionalType.None). Functional
// blocks (miners, generators, etc.) still need their Params defined in code; an SO
// can still override such a block's prefab by using the same Id.
//
// To create: right-click in the Project window → Reclamation → Block Definition.

using UnityEngine;

[CreateAssetMenu(menuName = "Reclamation/Block Definition", fileName = "BlockDef_New")]
public class BlockDefinitionSO : ScriptableObject
{
    [System.Serializable]
    public struct CostEntry
    {
        public string itemId;
        public int    quantity;
    }

    [Header("Identity")]
    [Tooltip("Unique id used in saves and the registry (e.g. \"glass_panel\").")]
    public string Id;
    public string DisplayName;

    [Header("Classification")]
    public BlockCategory Category = BlockCategory.Structural;
    [Tooltip("Leave as None for structural/decorative blocks. Functional types still " +
             "need their behaviour Params defined in code.")]
    public FunctionalType FunctionalType = FunctionalType.None;
    public int TierRequired = 0;

    [Header("Size & Physics")]
    [Tooltip("Footprint in grid cells (1 cell = 0.5 m).")]
    public Vector3Int Size = Vector3Int.one;
    public int   MaxDurability = 200;
    public float Mass = 5f;

    [Header("Power")]
    public float          PowerDrawKW = 0f;
    public float          PowerOutputKW = 0f;
    public PowerInterface PowerInterface = PowerInterface.None;

    [Header("Construction")]
    public CostEntry[] ConstructionCost;

    [Header("Visual")]
    [Tooltip("Prefab spawned for each placed block. If empty, a footprint-sized cube is used.")]
    public GameObject Prefab;

    public BlockDefinition ToDefinition()
    {
        ItemStack[] cost = System.Array.Empty<ItemStack>();
        if (ConstructionCost != null && ConstructionCost.Length > 0)
        {
            cost = new ItemStack[ConstructionCost.Length];
            for (int i = 0; i < ConstructionCost.Length; i++)
                cost[i] = new ItemStack(ConstructionCost[i].itemId, ConstructionCost[i].quantity);
        }

        return new BlockDefinition
        {
            Id             = Id,
            DisplayName    = string.IsNullOrEmpty(DisplayName) ? Id : DisplayName,
            Category       = Category,
            FunctionalType = FunctionalType,
            TierRequired   = TierRequired,
            SizeX          = Mathf.Max(1, Size.x),
            SizeY          = Mathf.Max(1, Size.y),
            SizeZ          = Mathf.Max(1, Size.z),
            MaxDurability  = MaxDurability,
            Mass           = Mass,
            PowerDrawKW    = PowerDrawKW,
            PowerOutputKW  = PowerOutputKW,
            PowerInterface = PowerInterface,
            ConstructionCost = cost,
            Params         = null,
        };
    }
}
