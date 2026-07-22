# Dream Dungeon ItemFix R1

日期：2026-07-22  
适用 Unity：6000.0.26f1  
核对基线：`Assets_Packages_ProjectSettings.zip`  
基线 SHA-256：`f2d5d36bb8070b79ad6ea2d6437eee2afac9c54da09cda043c77bc06f0c7b81c`

## 修复结果

- 补齐 `Fourth Memory` 至 `Seventh Memory`，全局共有七件不同 `ItemDefinition`。
- `ItemCatalog` 保持第一件固定，后续池扩充为第二至第七件。
- 七件均为 `Progression Value = 1`、`Unique In Run = true`，可满足 `Required Value = 7`。
- 测试资产生成器只初始化缺少的资产，不再覆盖已存在的名称、说明、Icon、Pickup Prefab、颜色、标签或权重。
- 生成器不再强行替换场景中已有的 Catalog 与 SpawnPolicy 引用。
- 新建 SpawnPolicy 时默认开启“错过第一件后继续提供”。现行资产也已确认开启。
- 游戏启动时会把 VictorySystem 的真实通关条件交给 ItemCatalog 检查。
- 空引用、空／重复 Item ID、非一次性核心道具、内容数量或总分不足都会产生明确红色错误。
- 运行中候选池意外耗尽时只报错一次，不再静默卡死。

## 安装

1. 退出 Play Mode，并关闭 Unity。
2. 备份当前可运行工程。
3. 将本 ZIP 解压到较短路径，例如 `C:\DD_IF1\`。
4. 把包内的 `Assets` 文件夹复制到 Unity 项目根目录，允许合并与覆盖；`.meta` 文件也要一并复制。
5. 重新打开 Unity，等待脚本编译和 Asset Database 导入结束。

安装时不需要执行 `Tools → Dream Dungeon → Generate Test Item Assets`；包内 Catalog 已经补齐。该菜单现在可以安全重跑，但仍只建议在确实需要补回缺失测试资产时使用。

## Inspector 快速确认

打开 `Assets/GeneratedTestItems/ItemCatalog.asset`：

- `First Guaranteed Item`：`Item_FirstMemory`
- `Subsequent Items → Size`：`6`
- 顺序：`Second`、`Third`、`Fourth`、`Fifth`、`Sixth`、`Seventh Memory`

打开 `Assets/GeneratedTestItems/ItemSpawnPolicy.asset`：

- `Keep Offering First Item Until Collected`：勾选
- 正常概率：`0.20 / +0.12 / Max 0.85`

进入 Play Mode 后，Console 不应出现：

`Item progression configuration invalid`

## 完整验收

1. 必须退出旧 Play Mode 后重新开始一局；旧局已经缓存的楼层计划不能作为验收基线。
2. 为加速测试，可暂时把 `Base Chance After Collection` 和 `Maximum Chance` 都改为 `1`。
3. 依次收集七件道具，确认 Debug Overlay 的 Items 从 1 增加到 7，且不会重复取得同一 Item ID。
4. 第七件应触发现有 VictorySystem，并进入 `EndingScene`。
5. 将概率恢复为 `Base 0.20 / Increase 0.12 / Maximum 0.85`。
6. 再以正常概率开始新局，确认漏过第二层第一件后，后续层仍会继续提供第一件。

## 后续正式资源接入

- 地图中的正式贴图、动画、粒子和音效：制作 Prefab 后拖入对应 `ItemDefinition → Pickup Prefab`。
- UI 图标：拖入 `Icon`。
- 简短说明：填写 `Description`。
- 后续台词／文本系统：订阅 `ItemManager.ItemCollected`，通过稳定 `Item ID` 查找文本。

以上内容都不需要改动刷新概率、房间放置或收集逻辑；重跑新版生成器也不会覆盖已存在的正式内容。

## 改动边界

只修改了核心道具数据、道具校验、测试资产生成器，以及 `DemoVictoryController` 的启动校验调用。未修改：

- `GameScene`
- `GameManager`
- DungeonGenerator / DungeonLayout / DungeonRenderer
- Graybox 房间与 Catalog
- R9 房间审计工具
- 玩家移动、敌人 A*、敌人配置与动画系统

包内已通过 C# 语法解析、七件资产／GUID／Catalog 引用闭环、全工程 GUID 唯一性和改动边界检查。由于交付环境没有 Unity Editor，最终 Play Mode 与第七件触发结局仍需按上述步骤在 Unity 中确认。
