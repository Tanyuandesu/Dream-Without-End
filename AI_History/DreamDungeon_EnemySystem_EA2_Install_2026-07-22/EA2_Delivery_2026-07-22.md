# 2D环形梦境：敌人系统 EA2 交付说明

## 本阶段结果

EA2 已建立统一的运行时上下文与可观察状态机，并把 EA1 的旧追击行为迁入状态机控制。当前画面和手感预期不变：仍然只生成 3 只相同火柴猫，仍使用原有四方向 A*、速度、检测半径、接触伤害与 CA1 位移动画。

本阶段实际启用的状态为：

`Spawn → Idle → Chase → InvestigateLastKnownPosition → Idle`

任何存活状态在生命归零时都会进入 `Dead`。`Patrol`、正式区域搜索、返回出生房、主动攻击和五种敌人的专属行为仍未提前启用。

## 新运行时结构

- `EnemyRuntimeIdentity`：继续保存 EA1 的不可变身份与出生来源。
- `EnemyRuntimeContext`：集中保存 EnemyId、Definition、玩家目标、Home Anchor、最后已知位置、导航目标及组件引用。
- `EnemyStateMachine`：唯一负责状态切换、死亡生命周期和可选的状态变化日志。
- `TestEnemyAI`：EA2 临时兼容适配层，只保留旧 A* 请求、路径点跟随与同格直移；不再拥有自己的 `Update/FixedUpdate`。

在 Play Mode 中选择 `GeneratedDungeon_Floor_1/Wanderer_1`，可以直接在 Inspector 看到：

- `Enemy Runtime Context`：Enemy Id、Current Target、Home Anchor、Last Known Target Position、Navigation Destination。
- `Enemy State Machine`：Current State、Previous State、Transition Count、State Entered At、Last Transition Reason。
- `Test Enemy AI`：当前路径长度、Waypoint、目标格和等待中的重寻路目标。

`Log State Transitions` 默认关闭。手动开启时只记录真实状态变化，不会每帧刷日志。

## 修改范围

修改的既有文件只有：

- `Assets/Scripts/Enemy/EnemySpawner.cs`
- `Assets/Scripts/Enemy/TestEnemyAI.cs`

新增：

- `EnemyRuntimeState.cs`
- `EnemyRuntimeContext.cs`
- `EnemyStateMachine.cs`
- `Editor/EnemyEA2RuntimeAudit.cs`
- `EA2_README.txt`

没有修改 `GameScene`、`GameManager`、`EnemyManager`、`EnemyDefinition`、五种敌人资产、房间系统或 CA1 动画资产。因此用户已经在场景中关闭的旧 Stage/HP 调试显示不会被本包覆盖。

## 静态验证

- Enemy 与 Combat 范围共 26 份 C# 文件通过语法树解析，0 个语法错误。
- 新增资源 Meta 完整，项目内 Meta GUID 无重复。
- `GameScene.unity` 与 EA1 基线字节哈希一致。
- `Enemy_Wanderer.asset` 与 EA1 基线字节哈希一致。
- `EnemySpawner` 的 29 个序列化字段名称没有增加、删除或改名，现有场景引用不会因 EA2 迁移。
- `TestEnemyAI` 已无 `Update/FixedUpdate`；只有 `EnemyStateMachine` 持有这两个运行入口，不会重复执行两套移动。
- `Health.Died` 的状态机订阅与 `EnemyManager` 记录订阅保持独立；死亡仍由 Manager 正常移除和记账。

本环境没有 Unity Editor，最终编译、物理移动、换层和 Inspector 状态变化仍需在 Unity 6000.0.26f1 中验收。

## 安装与首轮验收

1. 备份项目并关闭 Unity。
2. 将 EA2 安装包解压到项目根目录，允许覆盖；不要解压进 `Assets` 文件夹内。
3. 打开 Unity，等待编译完成。
4. 打开 `GameScene`，在非 Play Mode 运行：
   `Tools > Dream Dungeon > Enemy System > Run EA1 Configuration Audit`
5. 确认 EA1 仍为 `Result=PASS`。
6. 进入 Play Mode，等待 3 只敌人生成。
7. 运行：
   `Tools > Dream Dungeon > Enemy System > Run EA2 Runtime Audit`
8. 预期 Console 显示：

   ```text
   RuntimeEnemies=3
   InitializedContexts=3
   InitializedStateMachines=3
   LegacyChaseAdapters=3
   EnemyManagerActive=3
   Result=PASS
   ```

   `States=` 后面可以是 `Idle`、`Chase` 或 `InvestigateLastKnownPosition` 的任意实际分布。

9. 保持 Play Mode，在 Hierarchy 展开 `GeneratedDungeon_Floor_1`，选择任意 `Wanderer`，确认 `Enemy Runtime Context` 与 `Enemy State Machine` 均为 Initialized，且 Current State 会随玩家接近或离开改变。
10. 确认追击、接触伤害、圆形 HP、火柴猫 Idle/Walk 动画、换层和 `R` 重生成仍正常，Console 没有红色 Error。

## 回退

退出 Unity 后，将 EA2 回退包解压至项目根目录并覆盖，会恢复 EA1 版本的 `EnemySpawner.cs` 与 `TestEnemyAI.cs`。新增 EA2 文件即使保留也不会再被生成器引用，不影响 EA1 行为。

如果需要文件级完全清理，应在 Unity 关闭时成组删除所有新增 EA2 文件及其 `.meta`，不能只删除其中一部分；最稳妥的完整恢复方式仍是使用开始前的项目备份。
