敵人 Inspector 參數版
======================

請替換四個腳本：
1. GameManager.cs
2. EnemySpawner.cs
3. EnemyDetection.cs
4. TestEnemyAI.cs

EnemyPathfinder.cs 使用上一版平滑尋路版本，不需要替換。

Unity 中的使用方法
------------------
選中 Hierarchy 裡的 GameManager。

同一個 GameObject 上應該能看到：
- GameManager
- DungeonGenerator
- DungeonRenderer
- PlayerSpawner
- ExitSpawner
- CameraManager
- EnemySpawner

若 EnemySpawner 沒有自動出現：
1. 先等待 Unity 編譯完成。
2. 仍未出現時，手動 Add Component > EnemySpawner。
   或移除再重新掛一次 GameManager。

EnemySpawner 可調內容
---------------------
生成數量：
- Enemy Count
- Spawn Near Player First
- Exclude Exit Room

移動：
- Move Speed
- Waypoint Tolerance
- Stop Distance
- Last Position Tolerance

索敵：
- Detection Radius
- Lose Target Radius
- Require Line Of Sight
- Obstacle Mask

外觀與碰撞：
- Visual Scale
- Collider Scale
- Enemy Color

重要
----
Enemy Count 改變後，按 R 或進入下一層才會重新生成相應數量。
其他參數也是在生成敵人時套用，修改後按 R 最穩定。
