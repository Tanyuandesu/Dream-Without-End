Dream Fog Overlay
=================

內容
----
- DreamFog_1024_Transparent.png
- DreamFogDrift.cs

用途
----
作為 A+B 背景的第二層：慢速夢境霧。

建議 Hierarchy
--------------
Main Camera
└── DreamBackgroundSystem
    ├── BackgroundBase
    ├── FogLayer_A
    └── FogLayer_B  可選

Import Settings
---------------
DreamFog_1024_Transparent.png：

Texture Type = Sprite (2D and UI)
Sprite Mode = Single
Pixels Per Unit = 100
Mesh Type = Full Rect
Filter Mode = Bilinear
Compression = None
Alpha Is Transparency = On

FogLayer_A
----------
Add Component:
- SpriteRenderer
- DreamFogDrift

SpriteRenderer:
- Sprite = DreamFog_1024_Transparent
- Sorting Layer = Default
- Order in Layer = -90
- Color = 淡藍灰，Alpha 會由腳本控制

Transform:
- Local Position = 0, 0, 10
- Scale = 比 BackgroundBase 略大，例如 2, 2, 1

DreamFogDrift 建議：
- Drift Amplitude = 0.35, 0.22
- Drift Speed = 0.06, 0.04
- Base Alpha = 0.14 ~ 0.20
- Alpha Pulse Amount = 0.03 ~ 0.05
- Scale Pulse Amount = 0.01 ~ 0.02

FogLayer_B 可選
---------------
複製 FogLayer_A：
- Order in Layer = -89
- Scale 稍大或稍小
- Rotation Z = 180
- Phase Offset = 7
- Base Alpha = 0.08 ~ 0.12

排序建議
--------
BackgroundBase: -100
FogLayer_A: -90
FogLayer_B: -89
Floor: 0
Walls: 5
Items/Enemies: 10+
Player: 20+
UI: Canvas

驗收
----
- 霧能慢速漂移
- 中心仍然能看清玩家、敵人、牆、出口、道具
- 沒有明顯方形邊框
- 沒有遮擋 gameplay
