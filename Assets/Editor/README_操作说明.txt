Dream Dungeon 第一轮无损瘦身——增量执行工具

为什么需要这个工具
==================
审计用压缩包没有包含你原项目中的全部照片和本地美术素材，因此不能用清理后的Assets文件夹覆盖原项目。
本工具只删除已经确认的50个精确路径，并在现有GameScene内移除3个旧Debug/Preview组件；列表之外的照片、PNG、Prefab和代码不会被覆盖。

执行前
======
1. 确认Unity版本为6000.0.26f1。
2. 退出Play Mode。
3. 保存所有Scene和Prefab修改，确保Hierarchy名称旁没有“*”。
4. 建议先提交一次Git，或完整复制项目文件夹。

安装与执行
==========
1. 解压本工具包。
2. 在你的“完整原项目”中打开Assets文件夹。
3. 如果Assets下没有Editor文件夹，请新建一个，名称必须是Editor。
4. 只把DreamDungeonFirstPassCleanupApplier.cs复制到：
   Assets/Editor/DreamDungeonFirstPassCleanupApplier.cs
5. 返回Unity，等待右下角编译结束，确认Console没有新增红错。
6. 点击顶部菜单：
   Tools > Dream Dungeon > Maintenance > Apply First-Pass No-Loss Cleanup
7. 阅读确认窗口，点击“建立备份并执行”。
8. 等待完成窗口出现，然后等待Unity重新编译。

工具会自动完成
==============
- 检查Unity版本和关键保留文件，避免对错误项目执行。
- 在项目根目录建立：_FirstPassCleanupBackup_日期_时间
- 备份全部待删文件、对应.meta以及GameScene。
- 在你当前的GameScene中，仅移除：
  DungeonSocketCorridorR6Preview
  HealthDebugHUD
  DreamBackgroundProgressionDebug
- 删除第一轮确认的历史Tools、旧审计、TMP Examples、TutorialInfo和R9测试资产。
- 保留P10.7、P10.9、MusicRoom、C2、CB9/CB9.5、EA3和CB10A相关工具。
- 成功后尝试自我删除清理脚本。

执行后测试
==========
1. Console必须零红错。
2. 执行P10.7的Audit Production_Main。
3. 运行GameScene，确认房间、走廊、玩家、敌人、战斗和道具正常。
4. 通过出口切换楼层。
5. 收集7件道具进入EndingScene。
6. 确认MusicRoom相关菜单仍然存在。

如何恢复
========
如果工具执行中报错，会自动尝试恢复。
如果Unity回归测试发现问题：
1. 关闭Unity。
2. 打开项目根目录下最新的_FirstPassCleanupBackup_日期_时间文件夹。
3. 将其中的Assets文件夹复制回原项目根目录，选择合并并覆盖同名文件。
4. 重新打开Unity。

不要做的事情
============
- 不要把之前的完整瘦身ZIP直接覆盖你的Assets。
- 不要删除自动备份，至少保留到完整回归测试通过并提交Git之后。
