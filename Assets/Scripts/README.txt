RuntimeDungeon_Modular
======================

零 Prefab、零 Tilemap 的模組化 Unity 2D 隨機迷宮示範。

主要組件
--------
GameManager
├── DungeonGenerator
├── DungeonRenderer
├── PlayerSpawner
├── ExitSpawner
└── CameraManager

必要輔助檔案
------------
DungeonLayout.cs
RuntimeDungeonPlayer.cs
RuntimeDungeonExit.cs

使用方法
--------
1. 把整個 RuntimeDungeon_Modular 資料夾放進 Unity 專案 Assets。
2. 若先前放過 RuntimeDungeonDemo.cs，請刪除或移出 Assets，避免同時生成兩套迷宮。
3. 建立空場景。
4. 建立一個 Empty GameObject，命名為 GameManager。
5. 只掛上 GameManager.cs。
6. 按 Play。

GameManager 會自動把另外五個主要組件加到同一個 GameObject。

操作
----
WASD / 方向鍵：移動
R：重新生成目前樓層
走到黃色方塊：進入下一個隨機迷宮

輸入注意
--------
腳本使用舊 Input Manager API。
若專案只啟用了新版 Input System：
Edit > Project Settings > Player > Active Input Handling > Both
