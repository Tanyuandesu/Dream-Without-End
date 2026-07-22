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
├── Item_FourthMemory.asset
├── Item_FifthMemory.asset
├── Item_SixthMemory.asset
├── Item_SeventhMemory.asset
├── ItemCatalog.asset
└── ItemSpawnPolicy.asset

若場景中已存在 ItemManager，
工具只會在欄位為空時自動指派 Catalog 和 Policy。

生成器可安全重跑：
- 只初始化尚不存在的道具資產。
- 不覆蓋既有 Display Name、Description、Icon、
  Pickup Prefab、顏色、標籤或權重。
- 不清空 Catalog 中已存在的人工配置，
  只補入缺少的第二至第七件測試道具。

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

Keep Offering First Item Until Collected = 開啟

因此第一個道具會從第 2 層開始保證出現；
玩家若錯過，後續樓層仍會繼續提供，
直到實際收集為止，不會形成永久斷檔。

六、測試
--------
1. 從 TitleScene 開始遊戲。
2. Floor 1 不應生成道具。
3. 進入 Floor 2。
4. 應看到青色測試方塊。
5. 接觸後 Console 顯示 Collected item。
6. Debug Overlay 的 Items 與 Progression Score 增加。
7. 後續樓層按概率刷新第二至第七件不同色道具。
8. 七件都設為 Unique In Run，同一局不會重複抽到。
9. 收集第七件後，Required Value = 7 應觸發結局。
10. 在同一層按 R，刷新結果不會重新擲骰。

七、配置防呆
------------
DemoVictoryController 啟動時，會把目前的：
- Condition Mode
- Required Value

交給 ItemManager 與 ItemCatalog 聯合檢查。

會檢查：
- Catalog / SpawnPolicy / ItemSpawner 是否存在。
- Catalog 是否有空引用。
- Item ID 是否為空或重複。
- 核心道具是否全部保持 Unique In Run，避免重複凑數通關。
- Unique In Run 條件下，道具總數是否足以通關。
- Progression Score 模式下，總分上限是否足夠。

若配置不足，Console 會立即顯示紅色錯誤，
不再等玩家收集到第三件後才靜默卡死。

若運行中候選池仍意外耗盡，
ItemManager 也會只輸出一次明確錯誤，
指出已收集數與 Catalog 後續池數量。

八、文本與 UI 接口
------------------
ItemDefinition 已包含：
- DisplayName
- Description
- Icon
- Pickup Prefab
- 穩定且唯一的 Item ID

ItemManager 事件：
- ItemCollected
- ProgressChanged
- SpawnDecisionMade

正式拾取提示、道具列表、文本窗口可以訂閱：

itemManager.ItemCollected += HandleItemCollected;

簡短說明可直接讀取：

collectedEvent.Definition.Description

後續台詞或正式文本系統應以 Item ID 查表，
不需要修改收集、刷新或房間放置邏輯。

九、下一層地圖配置接口
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

十、目前測試外觀與正式貼圖替換
------------------------------
ItemDefinition 的 Pickup Prefab 留空時，
ItemSpawner 會生成彩色方塊。

之後建立正式道具 Prefab，只需：
1. 在 ItemDefinition 的 Pickup Prefab 拖入 Prefab。
2. Prefab 有 Collider2D 即可。
3. ItemPickup 缺少時會自動補上。
4. 背包或提示框用圖可另外拖入 Icon。

生成器重跑時不會覆蓋上述正式資源與文本。
