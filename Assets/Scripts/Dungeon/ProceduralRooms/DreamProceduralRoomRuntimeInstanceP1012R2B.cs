using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// P10.12A-1 R2B：把 DreamRoomPlacement 已提交的 Runtime Procedural Blocked
/// 一对一变成房间实例内的 BoxCollider2D。
///
/// 本组件只在 DungeonRenderer 实例化房间时动态添加，不写回 Prefab。
/// </summary>
[DisallowMultipleComponent]
public sealed class DreamProceduralRoomRuntimeInstanceP1012R2B :
    MonoBehaviour
{
    public const string StructureRootName =
        "RuntimeProceduralStructure_P10_12A1_R2B";

    private static Sprite debugSprite;

    private int roomIndex = -1;
    private int colliderCount;
    private int blockedCellCount;
    private int proceduralSeed;
    private DreamProceduralRoomArchetype archetype;

    public int RoomIndex => roomIndex;
    public int ColliderCount => colliderCount;
    public int BlockedCellCount => blockedCellCount;
    public int ProceduralSeed => proceduralSeed;
    public DreamProceduralRoomArchetype Archetype => archetype;

    public void Initialize(
        DreamRoomPlacement placement,
        int sourceRoomIndex)
    {
        if (placement == null ||
            !placement.HasRuntimeProceduralOverride)
        {
            throw new InvalidOperationException(
                "Runtime Procedural Instance 需要已提交 Override 的 RoomPlacement。");
        }

        roomIndex = sourceRoomIndex;
        proceduralSeed = placement.RuntimeProceduralSeed;
        archetype = placement.RuntimeProceduralArchetype;

        List<Vector2Int> blocked =
            new List<Vector2Int>();

        placement.GetRuntimeProceduralBlockedLocalCells(
            blocked);

        blockedCellCount = blocked.Count;

        if (blockedCellCount == 0)
        {
            throw new InvalidOperationException(
                "Runtime Procedural Override 没有 Blocked Cells。");
        }

        Transform oldRoot =
            transform.Find(StructureRootName);

        if (oldRoot != null)
        {
            Destroy(oldRoot.gameObject);
        }

        GameObject rootObject =
            new GameObject(StructureRootName);

        Transform structureRoot =
            rootObject.transform;

        structureRoot.SetParent(
            transform,
            false);

        structureRoot.localPosition = Vector3.zero;
        structureRoot.localRotation = Quaternion.identity;
        structureRoot.localScale = Vector3.one;

        DreamRoomTemplate instanceTemplate =
            GetComponent<DreamRoomTemplate>();

        if (instanceTemplate == null)
        {
            throw new MissingComponentException(
                "Runtime Procedural Room 实例缺少 DreamRoomTemplate。");
        }

        colliderCount = 0;

        for (int i = 0; i < blocked.Count; i++)
        {
            Vector2Int cell = blocked[i];

            GameObject obstacle =
                new GameObject(
                    "ProceduralBlocked_" +
                    cell.x + "_" + cell.y);

            obstacle.transform.SetParent(
                structureRoot,
                false);

            obstacle.transform.localPosition =
                instanceTemplate.GetLocalCellCenter(cell);

            obstacle.transform.localRotation =
                Quaternion.identity;

            obstacle.transform.localScale =
                Vector3.one;

            BoxCollider2D collider =
                obstacle.AddComponent<BoxCollider2D>();

            collider.size = Vector2.one;
            collider.offset = Vector2.zero;
            collider.isTrigger = false;

            colliderCount++;

            if (placement.RuntimeProceduralDebugVisible)
            {
                SpriteRenderer renderer =
                    obstacle.AddComponent<SpriteRenderer>();

                renderer.sprite = GetDebugSprite();
                renderer.color =
                    new Color(
                        1f,
                        0.18f,
                        0.10f,
                        0.30f);

                renderer.sortingOrder = 8;
            }
        }

        if (colliderCount != blockedCellCount)
        {
            throw new InvalidOperationException(
                "Runtime Procedural Collider 数量与 Blocked Cells 不一致：" +
                colliderCount + "/" + blockedCellCount + "。");
        }

        Debug.Log(
            "[P10.12A-1 R2B] Runtime Procedural Geometry COMMIT" +
            "\nRoomIndex=" + roomIndex +
            " | TemplateId=" + instanceTemplate.TemplateId +
            " | Seed=" + proceduralSeed +
            " | Archetype=" + archetype +
            " | Blocked=" + blockedCellCount +
            " | Colliders=" + colliderCount +
            "\nGeometryAuthority=RoomPlacement.RuntimeProceduralBlockedLocalCells" +
            " | PrefabAssetChanged=False",
            this);
    }

    private static Sprite GetDebugSprite()
    {
        if (debugSprite != null)
        {
            return debugSprite;
        }

        Texture2D texture =
            new Texture2D(
                1,
                1,
                TextureFormat.RGBA32,
                false);

        texture.name =
            "P10_12A1_R2B_DebugTexture";

        texture.hideFlags =
            HideFlags.HideAndDontSave;

        texture.SetPixel(
            0,
            0,
            Color.white);

        texture.Apply(false, true);

        debugSprite =
            Sprite.Create(
                texture,
                new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f),
                1f);

        debugSprite.name =
            "P10_12A1_R2B_DebugSprite";

        debugSprite.hideFlags =
            HideFlags.HideAndDontSave;

        return debugSprite;
    }
}
