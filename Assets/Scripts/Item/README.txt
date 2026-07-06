核心道具系統
============

設計目標
--------
- 第一個核心道具固定在第 2 層。
- 收集第一個道具後，後續樓層刷新機率逐層增加。
- 按 R 重生成同一層不會重新擲刷新機率。
- 道具進度跨樓層保留，返回標題並開始新遊戲時重置。
- 文本、UI、存檔、地圖難度、敵人配置均有接口。

檔案
----
新增：
- ItemDefinition.cs
- ItemCatalog.cs
- ItemSpawnPolicy.cs
- ItemProgressSnapshot.cs
- IItemProgressionReader.cs
- RunProgressionContext.cs
- ItemCollectedEvent.cs
- ItemSpawnDecision.cs
- ItemPickup.cs
- ItemSpawner.cs
- ItemManager.cs
- Editor/GenerateTestItemAssets.cs

替換：
- GameManager.cs

Hierarchy
---------
GameManager
├── PlayerSystem
├── EnemySystem
├── ItemSystem
│   ├── ItemManager
│   └── ItemSpawner
└── GeneratedDungeon_Floor_X

一、安裝腳本
------------
1. 備份目前可運行版本。
2. 把所有新增腳本放進例如：
   Assets/Scripts/Items
3. GenerateTestItemAssets.cs 必須放在 Editor 資料夾。
4. 用新版 GameManager.cs 覆蓋舊檔。
5. 等待 Unity 編譯完成。

二、建立 ItemSystem
-------------------
1. 在 GameManager 下建立 Empty：
   ItemSystem
2. Reset Transform。
3. 掛 ItemManager。
4. RequireComponent 會自動補 ItemSpawner。
5. 選中 GameManager，把 ItemSystem 上的 ItemManager
   拖進 GameManager > Item Manager。

三、生成測試道具資料
--------------------
Unity 上方選單：

Tools
→ Dream Dungeon
→ Generate Test Item Assets

會生成：

Assets/GeneratedTestItems
├── Item_FirstMemory.asset
├── Item_SecondMemory.asset
├── Item_ThirdMemory.asset
├── ItemCatalog.asset
└── ItemSpawnPolicy.asset

若場景中已存在 ItemManager，
工具會自動把 Catalog 和 Policy 指派給它。

四、預設規則
------------
第一個道具：
- Floor 2
- 100% 出現
- Item_FirstMemory

收集第一個道具後：
- 下一層：20%
- 再下一層：32%
- 再下一層：44%
- 之後每層 +12%
- 最大 85%

收集下一個道具後，概率從 20% 重新開始。

五、錯過第一個道具
------------------
ItemSpawnPolicy 預設：

Keep Offering First Item Until Collected = 關閉

因此第一個道具只在第 2 層出現。

若不希望玩家錯過後永久失去進度，
可將此選項開啟。
開啟後，第 2 層之後每層都保證出現，
直到玩家收集第一個道具。

六、測試
--------
1. 從 TitleScene 開始遊戲。
2. Floor 1 不應生成道具。
3. 進入 Floor 2。
4. 應看到青色測試方塊。
5. 接觸後 Console 顯示 Collected item。
6. Debug Overlay 的 Items 與 Progression Score 增加。
7. 後續樓層按概率刷新紫色或橙色道具。
8. 在同一層按 R，刷新結果不會重新擲骰。

七、文本與 UI 接口
------------------
ItemDefinition 已包含：
- DisplayName
- Description
- Icon

ItemManager 事件：
- ItemCollected
- ProgressChanged
- SpawnDecisionMade

正式拾取提示、道具列表、文本窗口可以訂閱：

itemManager.ItemCollected += HandleItemCollected;

八、下一層地圖配置接口
----------------------
GameManager 在生成每一層之前，會建立：

RunProgressionContext

其中包含：
- FloorNumber
- ItemProgress.CollectedCount
- ItemProgress.ProgressionScore
- ItemProgress.CollectedItems
- ItemProgress.CountWithTag(...)

未來 DungeonGenerator、DungeonManager、
EnemyManager 或 RoomCatalogSelector 可實作：

IRunProgressionConsumer

例如：

public sealed class DungeonDifficultyResolver :
    MonoBehaviour,
    IRunProgressionConsumer
{
    public void ApplyRunProgression(
        RunProgressionContext context)
    {
        int itemCount =
            context.ItemProgress.CollectedCount;

        // 根據 itemCount 調整房間數、敵人數、
        // 房間池、陷阱與出口距離。
    }
}

GameManager 會在 DungeonGenerator.Generate() 前
自動廣播這份進度。

九、目前測試外觀
----------------
ItemDefinition 的 Pickup Prefab 留空時，
ItemSpawner 會生成彩色方塊。

之後建立正式道具 Prefab，只需：
1. 在 ItemDefinition 的 Pickup Prefab 拖入 Prefab。
2. Prefab 有 Collider2D 即可。
3. ItemPickup 缺少時會自動補上。
