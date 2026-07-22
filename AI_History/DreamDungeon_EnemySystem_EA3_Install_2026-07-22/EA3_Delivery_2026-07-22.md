# 2D环形梦境：敌人系统 EA3 交付说明

## 本阶段结果

EA3 已把旧的“每只敌人各自执行线性 Open Set A* 与 Rigidbody2D
移动”替换为共享寻路服务、单敌人导航代理和独立移动执行器。

当前画面与玩法职责不扩张：仍只生成 3 只相同的 `Wanderer` 火柴猫，
仍使用 EA2 的 `Idle → Chase → InvestigateLastKnownPosition → Idle`
状态关系、原有移动速度、检测距离、接触伤害和 CA1 位移动画。

本阶段没有启用另外四种敌人，没有加入巡逻、区域搜索、返回出生房、
主动攻击、局部避让或房间遭遇配置；这些仍分别属于 EA4–EA6。

## EA3 运行结构

### 每层唯一的 EnemyPathService

生成全部敌人前，`EnemySpawner` 会在 `GeneratedDungeon_Floor_N` 根节点
建立一份共享服务：

- 只读取当前 `DungeonLayout.FloorCells`，不修改 Layout。
- 当前拓扑明确固定为四方向；八方向角色动画与导航拓扑完全分离。
- 预先计算连通分量，不可达目标会在 A* 前得到明确失败。
- A* Open Set 改为二叉最小堆，不再每轮扫描整个 List。
- 路径请求统一排队，默认每帧最多处理 2 次。
- 默认单次最多展开 4096 个格子；当前地图约 823 格，仍有充足余量。
- 默认保留每个格子中心作为 waypoint，不启用直线跳点／平滑，以便验证
  直角、非矩形房和窄通道。

### 每只敌人的导航组件

- `EnemyNavigationAgent`：保存目标格、请求状态、路径、waypoint、失败原因、
  重寻路节流和卡住恢复。
- `EnemyMotor2D`：唯一负责向 Rigidbody2D 发出 `MovePosition`，不决定追谁。
- `EnemyPathfinder`：保留旧接口，但已经变成共享服务的薄适配器；自身不再
  包含 A*。
- `TestEnemyAI`：保留给 EA2 接口和审计的兼容桥；没有 `Update`、
  `FixedUpdate`、路径数组、A* 或 Rigidbody2D 位移。
- `EnemyStateMachine`：状态契约不变，但正式调用 `EnemyNavigationAgent`。

因此运行时不存在旧 AI 与新导航同时移动同一只敌人的情况。

## 重寻路与故障恢复规则

- 玩家进入新格子后才产生新的路径目标。
- 敌人正在两个格子之间时，不会立刻丢弃当前路径；抵达当前 waypoint 后再
  安全重算，保留 EA2 已验收的移动契约。
- 每只敌人默认至少间隔 0.08 秒才能再次请求；全层还受每帧 2 次的集中预算
  限制。
- 同一格内不调用 A*，直接移动到玩家或最后已知位置。
- 空路径不再同时代表“已经到达”和“寻路失败”；`EnemyPathResult` 明确区分
  Success 与 FailureReason。
- 无效目标、不可达、节点上限和成本上限都有独立失败原因。
- 失败后默认 0.5 秒再次尝试，不会因一次空路径永久停机，也不会每帧刷日志。
- 预期移动连续 0.75 秒没有达到最小位移时触发卡住恢复：取消旧请求、清除
  旧路径，并只在 0.8 世界单位内对最近可行走格执行受限重置，再重新请求。
- 连续恢复超过 3 次会显示 `RecoveryAttemptsExhausted`，随后仍以延迟方式重试。

`EnemyDefinition.MaximumChasePathCost` 在 EA3 暂不启用。这样不会在替换导航
的同时悄悄缩短 EA2 已验收的追击距离；该限制会在 EA4 的追击／返回规则中
正式接入。

## Inspector 新增内容

`EnemySpawner`：

- `Navigation Topology = Four Directions`
- `Max Path Queries Per Frame = 2`
- `Max Expanded Path Nodes Per Query = 4096`
- `Navigation Start Recovery Radius In Cells = 1`
- `Simplify Collinear Path Waypoints = false`

每个 `EnemyDefinition`：

- `Minimum Repath Interval = 0.08`
- `Stuck Timeout = 0.75`
- `Stuck Movement Threshold = 0.015`
- `Maximum Recovery Attempts = 3`
- `Maximum Recovery Snap Distance = 0.8`
- `Failed Path Retry Delay = 0.5`

运行时选择楼层根节点可查看服务队列、连通分量和累计请求；选择任意
`Wanderer` 可查看 Agent 的当前状态、等待请求、waypoint、失败原因、卡住时间
和恢复次数，以及 Motor 的位移命令。路径 Gizmo 可独立关闭。

## 修改范围

修改既有文件：

- `Assets/Scripts/Enemy/EnemySpawner.cs`
- `Assets/Scripts/Enemy/EnemyDefinition.cs`
- `Assets/Scripts/Enemy/EnemyPathfinder.cs`
- `Assets/Scripts/Enemy/EnemyRuntimeContext.cs`
- `Assets/Scripts/Enemy/EnemyStateMachine.cs`
- `Assets/Scripts/Enemy/TestEnemyAI.cs`

新增文件：

- `EnemyNavigationTypes.cs`
- `EnemyPathService.cs`
- `EnemyMotor2D.cs`
- `EnemyNavigationAgent.cs`
- `Editor/EnemyEA3AlgorithmAudit.cs`
- `Editor/EnemyEA3NavigationAudit.cs`
- `EA3_README.txt`

没有修改 `GameScene`、五份 Enemy Definition 资产、房间／生成数据、动画资产、
Packages 或 ProjectSettings。因此用户在场景中关闭的旧 Stage 与 HP 调试显示
不会被覆盖。

## 静态验证

- `Assets/Scripts` 共 105 份 C# 文件通过 C# 语法树解析，0 个语法错误。
- 新增脚本 Meta 完整；项目 Meta GUID 无重复。
- EA2 的 `EnemySpawner` 29 个序列化字段全部保留，只新增 5 个 EA3 字段。
- EA1 的 `EnemyDefinition` 40 个序列化字段全部保留，只新增 6 个稳定性字段。
- `GameScene.unity`、`Enemy_Wanderer.asset` 与 CA1 火柴猫动画资产字节哈希未变。
- `TestEnemyAI` 已无 `Update/FixedUpdate`；A* Open Set 只存在于共享服务。
- 新运行结构中只有状态机执行单敌人固定帧 Tick，只有 Motor 发出位移。
- 回退后的 EA3 新增文件仍能与 EA2 类型边界共同编译，不要求压缩包删除文件。

本环境没有 Unity Editor，最终脚本编译、物理碰撞、路径移动、换层和运行时
Inspector 状态仍须在 Unity 6000.0.26f1 中验收。

## 安装与第一轮验收

1. 备份项目并关闭 Unity。
2. 将 EA3 安装包解压到项目根目录并允许覆盖；不要解压进 `Assets` 内。
3. 打开 Unity，等待编译完成。
4. 打开 `GameScene`，确认 Console 没有红色 Error。
5. 在非 Play Mode 运行：
   `Tools > Dream Dungeon > Enemy System > Run EA1 Configuration Audit`
6. 预期 EA1 仍为 `Result=PASS`。
7. 继续在非 Play Mode 运行：
   `Tools > Dream Dungeon > Enemy System > Run EA3 Algorithm Audit`
8. 预期：

   ```text
   CasesPassed=8/8
   TopologyBaseline=FourDirections
   PriorityQueue=BinaryMinHeap
   WaypointSimplification=Disabled
   Result=PASS
   ```

9. 进入 Play Mode，确认仍生成 3 只相同火柴猫；靠近并移动几格，让它们进行
   追击和至少一次重寻路。
10. 运行 EA2 Runtime Audit，预期仍为 `Result=PASS`。
11. 运行：
    `Tools > Dream Dungeon > Enemy System > Run EA3 Navigation Audit`
12. 关键结果应为：

    ```text
    RuntimeEnemies=3
    InitializedAgents=3
    InitializedMotors=3
    InitializedPathfinderFacades=3
    InitializedCompatibilityBridges=3
    StateMachineNavigationLinks=3
    SharedPathServices=1
    UniqueAgentServices=1
    Topology=FourDirections
    Components=1
    ImmediateConnectivityProbes=3
    ActiveFailures=0
    EnemyManagerActive=3
    Result=PASS
    ```

`FloorCells`、查询次数、状态分布和 PeakQueued 会随地图与测试时机变化，不要求
固定数字。若还没有触发追击，`Processed=0` 只会附带 NOTE，不导致失败。

## 第二轮运行回归

第一轮通过后再逐项验证：

1. 同房间直追和停止距离正常。
2. 长直通道无周期性停顿。
3. 直角与多次转弯不穿墙、不在墙角持续抖动。
4. 可跨房间追击，经过程序化走廊与非矩形房间。
5. 玩家快速切换目标格时，敌人在 waypoint 边界重算，不突然回拉。
6. 丢失玩家后仍到达最后已知位置并回到 Idle；EA4 搜索尚未启用。
7. 接触伤害、圆形 HP、临时猫 Idle/Walk 动画正常。
8. 进入下一层并按一次 R，仍各生成 3 个全新实例；每层只有 1 个服务。
9. Console 没有新红色 Error，也没有路径日志洪水。

算法审计已经覆盖不可达目标、成本限制、非矩形多转弯、偏离格起点恢复和
禁止斜穿墙角。实际物理卡住恢复只在发生真实阻挡时触发，不要求为了截图故意
破坏场景。

## 回退

关闭 Unity，将 EA3 回退包解压至项目根目录并覆盖，会恢复 EA2 的 6 份既有
源文件。EA3 新增文件可以保留：EA2 生成器与场景不会引用它们，且它们仍可
编译。

如要文件级完全清理，必须在 Unity 关闭时按照回退说明把全部 EA3 新文件及其
`.meta` 成组删除，不能只删一部分。最稳妥的完整恢复方式仍是开始前的项目备份。
