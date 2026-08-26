Dream Dungeon CB9 战斗配置整理与全链路审计

本阶段不改变既有战斗数值和玩法结果。

完成内容
- PlayerSpawner 的战斗 Inspector 标题去除阶段编号，按长期用途整理为：
  非致命击退、直接伤害攻击、鼠标与全键盘输入、双动作仲裁。
- EnemyDefinition 的击退恢复与弱受击配置继续保持每种敌人独立。
- 增加跨敌人死亡与换层仍保留的战斗诊断统计。
- 增加 CB9 Full Combat Audit，一次检查配置、左右动作隔离、弱受击、仲裁、死亡封口和活动引用。

推荐运行流程
1. 进入 Play Mode，等待三名敌人生成。
2. 用左键/J/Z 成功击退至少一名敌人。
3. 用右键/K/X 命中一名敌人，但至少保留一名敌人存活。
4. 用直接攻击杀死至少一名敌人。
5. 停止输入并等待约一秒。
6. 运行：
   Tools > Dream Dungeon > Combat > Run CB9 Full Combat Audit

完整通过应包含
- Push > 0
- Direct > 0
- Direct Damage Hits > 0
- Weak Response 依据当前配置被观察到
- Isolation Violations 全部为 0
- Player Kills > 0
- Death Chain Violations = 0
- DualStart Violations = 0
- Active Transients 全部清空
- PASS

若显示 INCOMPLETE，通常只是尚未同时完成“击退、伤害、击杀并留一名活敌人”的代表性流程。
