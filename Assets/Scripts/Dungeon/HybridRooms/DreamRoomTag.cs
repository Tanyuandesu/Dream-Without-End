using System;

/// <summary>
/// 房间用途标签。
/// 使用 Flags 后，一个模板可以同时承担多个候选用途。
/// R1 只保存数据，不会改变当前生成概率或游戏流程。
/// </summary>
[Flags]
public enum DreamRoomTag
{
    None = 0,
    Standard = 1 << 0,
    StartCandidate = 1 << 1,
    ExitCandidate = 1 << 2,
    Rare = 1 << 3,
    CoreItemCandidate = 1 << 4,
    Special = 1 << 5
}

public static class DreamRoomTagUtility
{
    public static bool HasAny(
        this DreamRoomTag value,
        DreamRoomTag requestedTags)
    {
        if (requestedTags == DreamRoomTag.None)
        {
            return value == DreamRoomTag.None;
        }

        return (value & requestedTags) != 0;
    }

    public static bool HasAll(
        this DreamRoomTag value,
        DreamRoomTag requestedTags)
    {
        return (value & requestedTags) == requestedTags;
    }
}
