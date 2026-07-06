using UnityEngine;

/// <summary>
/// 核心道具的樓層刷新規則。
///
/// 預設：
/// - 第一個道具固定在第 2 層。
/// - 收集道具後，下一層使用 Base Chance。
/// - 之後每多一層未收集到道具，機率增加。
/// </summary>
[CreateAssetMenu(
    fileName = "ItemSpawnPolicy",
    menuName = "Game/Items/Item Spawn Policy")]
public sealed class ItemSpawnPolicy : ScriptableObject
{
    [Header("第一個道具")]
    [Min(1)]
    [SerializeField] private int firstGuaranteedFloor = 2;

    [Tooltip(
        "關閉：第一個道具只在指定樓層出現。\n" +
        "開啟：錯過後，後續樓層仍會保證出現，直到收集。")]
    [SerializeField] private bool keepOfferingFirstItemUntilCollected =
        false;

    [Header("後續道具概率")]
    [Range(0f, 1f)]
    [SerializeField] private float baseChanceAfterCollection = 0.20f;

    [Range(0f, 1f)]
    [SerializeField] private float chanceIncreasePerFloor = 0.12f;

    [Range(0f, 1f)]
    [SerializeField] private float maximumChance = 0.85f;

    [Tooltip(
        "收集道具後隔幾層才可再次刷新。\n" +
        "1 表示下一層就可以刷新。")]
    [Min(1)]
    [SerializeField] private int minimumFloorGapAfterCollection = 1;

    public int FirstGuaranteedFloor => firstGuaranteedFloor;
    public bool KeepOfferingFirstItemUntilCollected =>
        keepOfferingFirstItemUntilCollected;

    public float GetSpawnChance(
        int floorNumber,
        bool hasCollectedAnyItem,
        int lastCollectedFloor)
    {
        if (!hasCollectedAnyItem)
        {
            if (floorNumber == firstGuaranteedFloor)
            {
                return 1f;
            }

            if (keepOfferingFirstItemUntilCollected &&
                floorNumber > firstGuaranteedFloor)
            {
                return 1f;
            }

            return 0f;
        }

        int floorGap =
            floorNumber - lastCollectedFloor;

        if (floorGap < minimumFloorGapAfterCollection)
        {
            return 0f;
        }

        int increaseSteps = Mathf.Max(
            0,
            floorGap - minimumFloorGapAfterCollection);

        float chance =
            baseChanceAfterCollection +
            chanceIncreasePerFloor * increaseSteps;

        return Mathf.Clamp(
            chance,
            0f,
            maximumChance);
    }

#if UNITY_EDITOR
    public void ConfigureForEditor(
        int newFirstGuaranteedFloor,
        bool newKeepOffering,
        float newBaseChance,
        float newIncrease,
        float newMaximum,
        int newMinimumGap)
    {
        firstGuaranteedFloor =
            Mathf.Max(1, newFirstGuaranteedFloor);

        keepOfferingFirstItemUntilCollected =
            newKeepOffering;

        baseChanceAfterCollection =
            Mathf.Clamp01(newBaseChance);

        chanceIncreasePerFloor =
            Mathf.Clamp01(newIncrease);

        maximumChance =
            Mathf.Clamp01(newMaximum);

        minimumFloorGapAfterCollection =
            Mathf.Max(1, newMinimumGap);
    }
#endif

    private void OnValidate()
    {
        firstGuaranteedFloor =
            Mathf.Max(1, firstGuaranteedFloor);

        baseChanceAfterCollection =
            Mathf.Clamp01(baseChanceAfterCollection);

        chanceIncreasePerFloor =
            Mathf.Clamp01(chanceIncreasePerFloor);

        maximumChance =
            Mathf.Clamp01(maximumChance);

        minimumFloorGapAfterCollection =
            Mathf.Max(1, minimumFloorGapAfterCollection);
    }
}
