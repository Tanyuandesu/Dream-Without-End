using UnityEngine;

/// <summary>
/// 一種道具的靜態資料。
///
/// 顯示名稱、說明、Icon 已預留給之後的文本與 UI；
/// Progression Value、Tags 可供地圖難度與配置系統讀取。
/// </summary>
[CreateAssetMenu(
    fileName = "ItemDefinition",
    menuName = "Game/Items/Item Definition")]
public sealed class ItemDefinition : ScriptableObject
{
    [Header("識別")]
    [SerializeField] private string itemId = "item_id";
    [SerializeField] private string displayName = "New Item";

    [TextArea(2, 6)]
    [SerializeField] private string description;

    [Header("UI 預留")]
    [SerializeField] private Sprite icon;

    [Header("場景表現")]
    [Tooltip("可選。留空時 ItemSpawner 會生成測試色塊。")]
    [SerializeField] private GameObject pickupPrefab;

    [SerializeField] private Color fallbackColor =
        new Color(0.2f, 0.95f, 1f, 1f);

    [Min(0.05f)]
    [SerializeField] private float fallbackVisualScale = 0.55f;

    [Header("進度")]
    [Min(0)]
    [SerializeField] private int progressionValue = 1;

    [Tooltip("開啟後，同一局中只能收集一次。")]
    [SerializeField] private bool uniqueInRun = true;

    [Tooltip("之後可用於房間池、敵人配置、結局條件等。")]
    [SerializeField] private string[] progressionTags =
        new string[0];

    [Header("刷新權重")]
    [Min(1)]
    [SerializeField] private int spawnWeight = 1;

    public string ItemId => itemId;
    public string DisplayName => displayName;
    public string Description => description;
    public Sprite Icon => icon;
    public GameObject PickupPrefab => pickupPrefab;
    public Color FallbackColor => fallbackColor;
    public float FallbackVisualScale => fallbackVisualScale;
    public int ProgressionValue => progressionValue;
    public bool UniqueInRun => uniqueInRun;
    public string[] ProgressionTags => progressionTags;
    public int SpawnWeight => Mathf.Max(1, spawnWeight);

    public bool HasTag(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag) ||
            progressionTags == null)
        {
            return false;
        }

        for (int i = 0; i < progressionTags.Length; i++)
        {
            if (progressionTags[i] == tag)
            {
                return true;
            }
        }

        return false;
    }

#if UNITY_EDITOR
    public void ConfigureForEditor(
        string newId,
        string newDisplayName,
        string newDescription,
        Color newFallbackColor,
        int newProgressionValue,
        bool newUniqueInRun,
        int newSpawnWeight,
        string[] newTags)
    {
        itemId = newId;
        displayName = newDisplayName;
        description = newDescription;
        fallbackColor = newFallbackColor;
        progressionValue = Mathf.Max(0, newProgressionValue);
        uniqueInRun = newUniqueInRun;
        spawnWeight = Mathf.Max(1, newSpawnWeight);
        progressionTags = newTags ?? new string[0];
    }
#endif

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            itemId = name;
        }

        fallbackVisualScale =
            Mathf.Max(0.05f, fallbackVisualScale);

        progressionValue =
            Mathf.Max(0, progressionValue);

        spawnWeight =
            Mathf.Max(1, spawnWeight);
    }
}
