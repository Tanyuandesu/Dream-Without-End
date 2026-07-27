Dream Dungeon Combat CB7：右键伤害的弱受击反应

一、阶段目标
CB7 为鼠标右键／K／X 的直接伤害加入轻量、可配置的受击反馈：
1. 极短、碰撞安全的微小位移。
2. 位移完成后的短暂 Hit 停顿。
3. 继续由 EnemyMotor2D 独占 Rigidbody2D 位移。
4. 继续由 EnemyStateMachine 独占受击中断。
5. 不推进左键击退衰减层级。
6. 不刷新左键抗性恢复窗口。
7. 不触发 CB4 落地追击加速。
8. 不取消已经运行中的 CB4 追击加速。

二、玩家侧配置位置
GameScene > PlayerSystem > PlayerSpawner
“战斗：CB7 八方向直接伤害与弱受击反应”

新增参数：
- Weak Displacement Distance：基础微小位移距离，默认 0.12。设为 0 可关闭位移。
- Weak Displacement Duration：基础位移时间，默认 0.06 秒。
- Weak Hit Pause Duration：基础 Hit 停顿，默认 0.08 秒。设为 0 可关闭停顿。

三、敌人侧独立配置位置
Assets/GeneratedEnemySettings/Enemy_XXX
“CB7 direct-attack weak hit response”

每种敌人可独立调整：
- Direct Attack Weak Displacement Multiplier：微小位移倍率，默认 1。
- Direct Attack Weak Hit Pause Multiplier：Hit 停顿倍率，默认 1。

实际效果：
实际位移距离 = 玩家基础位移距离 × 敌人位移倍率
实际 Hit 停顿 = 玩家基础停顿 × 敌人停顿倍率
位移时间也随位移倍率缩放，以保持近似一致的冲击速度。

四、与左键系统的边界
右键弱受击不会：
- 增加 Knockback Resistance 层级。
- 改写 Decay Build Window 或 Recovery 时间。
- 触发 Post Knockback Pause。
- 请求 Post Knockback Pursuit Boost。

若敌人已经处于更重要的受击／眩晕流程中，CB7 不会重启或缩短该反应；伤害仍可结算，微小位移会在 Motor 可用时执行。

五、安装
1. 退出 Play Mode。
2. 备份当前通过 CB6 的项目。
3. 将补丁 Assets 合并到项目根目录并覆盖同名文件。
4. 等待 Unity 编译，确认 Console 无红色 Error。

六、验收
1. 重新进入一局，至少保留一名敌人存活。
2. 用右键／K／X 命中敌人。
3. 观察敌人只出现轻微位移与极短停顿，明显弱于左键击退。
4. 连续使用右键，确认不会逐层降低位移或改变左键抗性。
5. 可先左键推开敌人，等待其进入追击加速，再用右键命中；右键不应把加速直接取消。
6. 停止输入约 0.2 秒后运行：
   Tools > Dream Dungeon > Combat > Run CB7 Weak Hit Reaction Audit

通过重点：
- Direct Damage、Weak Displacement、Hit Reaction 均有观察记录。
- Payload / Decay / Pursuit Isolation Violations 全部为 0。
- Active Weak Displacements 与 Active Hit Reactions 均为 0。
- 最终输出 PASS。

七、暂未包含
- CB8 左右动作的统一仲裁与同帧优先级。
- 攻击动画、命中特效、音效与 hit-stop。
