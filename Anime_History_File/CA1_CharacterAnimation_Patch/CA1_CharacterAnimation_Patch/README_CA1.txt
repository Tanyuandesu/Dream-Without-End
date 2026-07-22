CA1 方向动画基础与临时角色补丁
================================

适用工程：Unity 6000.0.26f1
目标：
1. 完整保留玩家现有连续移动。
2. 不修改敌人 AI、四方向 A*、同格直接追击与房间系统。
3. 玩家使用八方向动画。
4. 敌人按实际位移决定动画方向。
5. 每种敌人可在 EnemySpawner 的 Animation Profiles 中使用独立 Profile。
6. 当前实装 Idle 与 Walk。
7. 预留 Attack、Hurt、Death、Special 调用接口。
8. 使用临时 32x32 像素火柴人与火柴猫。

本补丁不会修改：
- RuntimeDungeonPlayer.cs
- TestEnemyAI.cs
- EnemyPathfinder.cs
- EnemyDetection.cs
- GameManager.cs
- DungeonGenerator 与房间 R9.x 文件
- Health 与 ContactDamage2D

需要替换的现有文件：
- Assets/Scripts/Player/PlayerSpawner.cs
- Assets/Scripts/Enemy/EnemySpawner.cs
- Assets/Scripts/Enemy/EnemyVisual.cs

新增内容：
- Assets/Scripts/Animation/
- Assets/Art/Characters/Temporary/

安装步骤：
1. 退出 Play Mode。
2. 用 GitHub 做一次当前工程备份。
3. 把本补丁内的 Assets 文件夹复制到 Unity 工程根目录。
4. 允许覆盖上面列出的三个现有脚本。
5. 等待 Unity 编译结束，确认 Console 没有红色错误。
6. 打开 Assets/Scenes/GameScene.unity。
7. 执行菜单：
   Tools > Dream Dungeon > Character Animation >
   CA1 Install Temporary Stick Characters
8. Console 出现 [CA1] 安装完成后保存 GameScene。
9. 再执行：
   Tools > Dream Dungeon > Character Animation >
   CA1 Validate Temporary Setup
10. 验证通过后进入 Play Mode。

第一轮测试：
A. 玩家静止时保持 Idle。
B. W、A、S、D 分别播放四个主方向。
C. W+D、S+D、W+A、S+A 播放四个斜方向。
D. 松开按键后保持最后朝向并回到 Idle。
E. 玩家速度、碰撞、回血、受伤和楼层传送与补丁前一致。
F. 三只敌人显示为火柴猫，移动时按真实位移改变方向。
G. 敌人沿 A* 转弯、同格追击、接触伤害和死亡与补丁前一致。
H. 按 R 重生成楼层后，新敌人仍有动画。
I. 进入下一层后，玩家动画继续存在，新敌人正常生成。

资源标准：
- 单帧：32x32 PNG，透明背景。
- Pixels Per Unit：32。
- Filter Mode：Point。
- Compression：Uncompressed。
- 临时素材实际制作 S、SE、E、NE、N 五个方向。
- W、SW、NW 由运行时水平镜像获得。
- Idle：每方向 1 帧。
- Walk：每方向 4 帧。

未来 PixelLab 接口：
CharacterAnimationProfile 已经包含以下状态：
Idle / Walk / Attack / Hurt / Death / Special

DirectionalSpriteAnimator 提供：
PlayAction(state, restart, returnToLocomotion)
ClearAction()
SetProfile(profile)
SetFacingDirection(direction)

注意：
当前工程实际上只有一种敌人逻辑类型，场景中生成三个相同实例。
CA1 先把多动画 Profile 的入口铺好，不在本阶段擅自拆分敌人数值、AI 或攻击方式。
