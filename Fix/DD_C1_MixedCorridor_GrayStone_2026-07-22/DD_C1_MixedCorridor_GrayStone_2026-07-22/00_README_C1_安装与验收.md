# Dream Dungeon Corridor Pass C1

## 目标

本补丁实现可切换的 `Mixed1And2` 一／两格混合走廊，并提供一个临时灰石表现 Profile。

- Start 到 Exit 的最短房间图主路保持双格。
- 门口、转角、交叉／汇合点保持双格。
- 足够长的次要支路直线中段收窄为单格。
- 混合走廊必须是原 `Uniform2` 双格安全包络的连通子集。
- 灰墙按朝向和固定 Seed 形成稳定的亮面、侧面、暗面及轻微逐格变化。
- 视觉 Profile 只改变 Sprite 与颜色，不改变 `FloorCells`、墙碰撞、Socket 或敌人导航。
- 保留 `Uniform2`＋无视觉 Profile 作为默认安全基线。

本补丁不包含 `GameScene.unity`，不会覆盖场景中的 UI、角色、敌人、Catalog 或房间配置。

## 与 EA3.1 敌人 A* 的边界

本补丁不修改任何 Enemy System 脚本，也不复制或替换敌人 A*。运行时仍由现有 `EnemyPathService` 读取最终 `FloorCells` 与合法 Socket 门边。

`Validate Live Mixed Corridor (C1)` 会核对：

- `FourDirections`
- `UsesHybridTraversalEdges=True`
- 开放门边大于 0
- 连通分量为 1
- `WalkableCellCount == FloorCells.Count`
- `AlgorithmChanged=False`

## 安装

1. 退出 Play Mode 与 Prefab Mode。
2. 确认打开 `Assets/Scenes/GameScene.unity`，场景无星号。
3. 确认 Catalog 为 `Graybox_R3`，Fixed Seed 为 `12345`。
4. 自行用 GitHub 完成当前工程备份。
5. 将本目录中的 `Assets` 合并进 Unity 工程并覆盖同名文件。
6. 不要复制 `Rollback_Uniform2_Code`。
7. 等待 Unity 编译与导入结束。既有 `CS0618` 警告不阻断；红错必须为 0。

## A. 第一轮：只读安装校验

Clear Console，然后执行：

```text
Tools > Dream Dungeon > Corridor Pass C1 >
Validate Installed Assets (C1)
```

应弹出：

```text
Corridor Pass C1 Assets Passed
```

日志应包含：

```text
Baseline=Uniform2
Catalog=Graybox_R3
FixedSeed=12345
RenderMode=HybridPrefabRooms
MixedModeAvailable=True
Uniform2DefaultValue=0
PrimaryRouteWide=True
DoorApron=2
CornerRadius=1
JunctionRadius=1
MinimumNarrowRun=3
VisualProfile=GrayStone_Temporary_C1
FloorMaskSlots=16
WallMaskSlots=16
SpriteSlotsOptional=True
SceneChanged=False
```

本轮结束后不要进入 Play Mode，也不要执行 Prepare；先保存 Console 截图。

## B. 第二轮：准备临时对照预览

仅在 A 通过后执行。保持退出 Play Mode、场景无星号，Clear Console 后执行：

```text
Tools > Dream Dungeon > Corridor Pass C1 >
Prepare Mixed Corridor Preview (C1)
```

应弹出：

```text
Mixed Corridor Preview Ready
```

此时 `GameScene*` 是预期的临时修改，不要手动保存。日志会对同一 Seed 的 `Uniform2` 与 `Mixed1And2` 做内存对照，并要求：

```text
SameRoomsSockets=True
MixedSubsetOfUniform=True
FullyConnected=True
WidthMode=Mixed1And2
CorridorWidthSafetyEnvelope=2
NarrowWidth=1
Profile=GrayStone_Temporary_C1
SceneSaved=False
GameSceneDirty=True
CatalogUnchanged=Graybox_R3
FixedSeedUnchanged=12345
```

## C. 第三轮：实机混合通道与导航校验

1. B 通过后进入 Play Mode，等待 Floor 1 完整生成。
2. 观察主路、门口、转角是否保持较宽，次要长直段是否出现单格收窄。
3. 观察灰墙是否形成上亮、侧中、下暗以及轻微不规则变化。
4. Clear Console，执行：

```text
Tools > Dream Dungeon > Corridor Pass C1 >
Validate Live Mixed Corridor (C1)
```

应弹出：

```text
Live Mixed Corridor Passed
```

重点字段：

```text
WidthMode=Mixed1And2
NarrowStraightCells=>0
WideTopologyCells=>0
DoorApron=2
MainRouteWide=True
Connected=True
FloorColliders=0
RenderedCorridorWalls=>0
WallColliders=RenderedCorridorWalls
DistinctWallColors=>=3
VisualProfile=GrayStone_Temporary_C1
EnemyTopology=FourDirections
HybridDoorEdges=True
OpenDoorTransitions=>0
Components=1
Walkable=FloorCells
AlgorithmChanged=False
Result=PASS
```

随后仍在 Play Mode 中依次执行：

```text
Tools > Dream Dungeon > Enemy System > Run EA3 Algorithm Audit
Tools > Dream Dungeon > Enemy System > Run EA3 Navigation Audit
```

两项都必须通过。再人工测试玩家移动、至少三只敌人追逐、转角、门口、单格支路会合与持续移动时的卡位／抽动。现有 Missing Script 警告按既有基线处理；红错必须为 0。

## D. 选择是否升为新基线

如果画面、手感、C 与 EA3 审计均通过：退出 Play Mode，不要手动保存，然后执行：

```text
Tools > Dream Dungeon > Corridor Pass C1 >
Save Mixed Corridor Baseline (C1)
```

应弹出 `Mixed Corridor Baseline Saved`，此时才会把 `Mixed1And2` 与临时灰石 Profile 保存进 `GameScene`。

如果不满意或任一运行验收失败：退出 Play Mode，不要手动保存，然后执行：

```text
Tools > Dream Dungeon > Corridor Pass C1 >
Restore Uniform2 and Save (C1)
```

应弹出 `Uniform2 Restored`。代码与灰石 Profile 资产会保留，但场景恢复原双格平面色安全基线。

## 后续正式视觉皮肤

`CorridorVisual_GrayStone_C1` 已预留 16 个地板和 16 个墙体 Sprite 槽位，索引采用四方向邻接 Mask：

```text
North=1, East=2, South=4, West=8
```

正式 PixelLab 图块完成后，只需填充 Profile 槽位；空槽会继续使用当前运行时方块与灰色明暗。无需修改走廊拓扑、碰撞、Socket 或敌人 A*。

## 代码级回滚

若安装后出现阻断编译错误，可将 `Rollback_Uniform2_Code/Assets` 合并进工程并覆盖同名文件。它会恢复原 `DungeonGenerator.SocketCorridors.cs` 与 `DungeonRenderer.cs`，并把 C1 新增的两个 partial／Editor 文件替换为无行为占位版本；无需删除资产。
