using UnityEngine;

/// <summary>
/// 場景中的可拾取道具。
/// 目前使用最簡單的 Trigger 判定。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public sealed class ItemPickup : MonoBehaviour
{
    private ItemDefinition definition;
    private ItemManager itemManager;
    private int floorNumber;
    private bool collected;

    public ItemDefinition Definition => definition;

    public void Initialize(
        ItemDefinition newDefinition,
        ItemManager newItemManager,
        int newFloorNumber)
    {
        definition = newDefinition;
        itemManager = newItemManager;
        floorNumber = newFloorNumber;

        Collider2D pickupCollider =
            GetComponent<Collider2D>();

        if (pickupCollider != null)
        {
            pickupCollider.isTrigger = true;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (collected ||
            definition == null ||
            itemManager == null)
        {
            return;
        }

        Health playerHealth =
            other.GetComponentInParent<Health>();

        if (playerHealth == null ||
            playerHealth.Faction != DamageFaction.Player ||
            playerHealth.IsDead)
        {
            return;
        }

        if (!itemManager.TryCollect(
                definition,
                floorNumber,
                playerHealth.gameObject))
        {
            return;
        }

        collected = true;
        Destroy(gameObject);
    }
}
