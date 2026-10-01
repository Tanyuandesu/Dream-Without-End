# ArtRoom_01 11x18 重做规格

日期：2026-10-01  
状态：方向修正。重做仅指按新尺寸重新实装，不重新设计。

## 1. 核心原则

ArtRoom_01 的视觉设计、题材、物件关系与既有画室方案继续作为权威设计。

这次重做的目标只有：

- 把旧 13x21 的实现重建为 11x18。
- 不把旧画室直接缩放成 11x18。
- 不另做一套新画室概念设计。
- 尽量保留原有画室的构图关系、视觉主题、主要物件和叙事信息。
- 只在新尺寸无法容纳时做必要的压缩、位移、裁切或间距调整。
- 碰撞、门口、可走区和四层 Runtime 资源按 11x18 重新实装。

## 2. 固定技术规格

- TemplateId：Production_ArtRoom_01
- Fidelity：HighPrecision
- RoomTag：Standard
- Size：11 x 18 cells
- Cell：1 Unity Unit
- PPU：64
- Runtime Canvas：704 x 1152 px
- Rotation：禁止 90 度旋转
- 每层最多 1
- Socket：South_0
- Door Width：2 cells
- 当前门格：x=4,5 / y=0
- Stage 2 期间继续保持未发布状态

统一 Sorting Order：

- Floor：-10
- Objects：0
- Player / Enemy：20
- Effects：25
- Foreground：30

## 3. 旧设计继承规则

优先继承旧 ArtRoom_01 的：

- 画室整体视觉主题
- 主要家具／画架／画布／工作区关系
- 原有 Floor / Objects / Foreground / Effects 的职责划分
- 原有叙事重点与空间气质
- 旧版中确实有意义的遮挡与光影关系

不继承旧实现中的：

- 13x21 尺寸本身
- 96 个 blockedCells
- 34 个 HardBlock
- 3 个局部 PolygonCollider
- 因旧尺寸产生的 walkableCells 补丁
- 旧外围墙与旧门坐标

## 4. 11x18 适配方式

适配顺序固定为：

1. 先以旧画室视觉为依据，重新排入 704x1152 画布。
2. 优先保留主物件的相对关系，不随意新增或删改设计。
3. 新尺寸不足时，先缩短空白间距和边缘留白。
4. 仍不足时，再移动次要物件。
5. 最后才考虑删除最次要的装饰元素。
6. 不通过整体非等比缩放解决尺寸问题。
7. 不把旧图简单裁成 11x18 后直接使用。

## 5. 四层继续沿用

### Floor

继续承载旧设计中的地面、固定墙体、门框与永久平面信息。

### Objects

继续承载旧设计中的实体家具、画架、画布、桌椅、柜体和其他需要碰撞依据的物件。

### Foreground

只保留旧设计中确实需要遮住角色的近景部分。

### Effects

沿用旧画室已有的光影／雾／反射思路，但排序改为 25，使其覆盖 Player / Enemy。

## 6. 碰撞重做原则

碰撞必须根据 11x18 适配后的最终画面重新生成。

- 不复制旧 blockedCells。
- 不复制旧 HardBlocks。
- 不复制旧 PolygonCollider。
- South_0 两格门保持可走。
- 新 walkable / blocked 关系从最终 11x18 图重新审。
- 只保留和实际物件轮廓一致的局部碰撞。

## 7. Stage 2 顺序

1. 取得旧 ArtRoom_01 视觉资源作为基准。
2. 将旧设计重新排版到 11x18 / 704x1152。
3. 生成新的 Floor / Objects / Foreground / Effects。
4. 审图并重新生成碰撞。
5. P10.7 Validate。
6. 通过后重新 Publish 到 Production_Main。
7. 完成运行时验收。
8. 最后删除一次性 Stage 1 重建工具。

## 8. 明确禁止

- 禁止把这次重做解释为“重新设计画室”。
- 禁止引入新的画室主题、叙事或主要构图概念。
- 禁止以新的概念图替代旧 ArtRoom_01 设计。
- 禁止因为画布变小就把房间改造成另一间画室。


## 9. Stage 2B 实装映射

已锁定为纯 1:1 像素适配，不做任何生成式重画或整体缩放。

旧 13x21 / 832x1344 → 新 11x18 / 704x1152：

- 水平：左侧裁 64 px，右侧裁 64 px。
- 垂直：保留南侧边界不动，北侧裁 192 px。
- 像素比例保持 1:1。
- PPU 保持 64。
- Floor / Objects / Foreground / Effects 四层使用完全相同的裁切矩形。
- South_0 入口因此继续保持在南侧，不因重排发生垂直漂移。
- Stage 2B 只完成视觉重新实装，不复制任何旧碰撞。

旧四层源图以原 Git blob 原样恢复到：
`Documentation/ArtRoom_01_Legacy13x21/`

Unity 一次性工具：
`Tools > Dream Dungeon > Production Rooms > ArtRoom Rebuild > Stage 2 - Fit Existing Art to 11x18`
