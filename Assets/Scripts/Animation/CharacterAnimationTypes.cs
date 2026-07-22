using UnityEngine;

/// <summary>
/// 角色动画支持的方向数量。
/// 移动逻辑本身不受这个枚举影响。
/// </summary>
public enum CharacterAnimationDirectionMode
{
    NoDirection = 0,
    FourDirections = 4,
    EightDirections = 8
}

/// <summary>
/// 统一使用八方向保存朝向。
/// 四方向角色只会使用 North、East、South、West。
/// </summary>
public enum CharacterFacingDirection
{
    South = 0,
    SouthEast = 1,
    East = 2,
    NorthEast = 3,
    North = 4,
    NorthWest = 5,
    West = 6,
    SouthWest = 7
}

/// <summary>
/// CA1 当前实装 Idle 与 Walk。
/// 其余状态先作为 PixelLab 正式资源的稳定接口。
/// </summary>
public enum CharacterAnimationState
{
    Idle = 0,
    Walk = 1,
    Attack = 2,
    Hurt = 3,
    Death = 4,
    Special = 5
}
