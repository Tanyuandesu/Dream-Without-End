using System;

/// <summary>
/// 房间用途标签。
/// 使用 Flags 后，一个模板可以同时承担多个候选用途。
/// R9.4.1 起，StartCandidate 与 ExitCandidate 正式参与房间选择；
/// R9.4.2 起，Rare 使用既有 RandomWeight 与单层 Template 上限；
/// R9.4.3 起，CoreItemCandidate 建立保留槽与道具出生作用域；
/// Special 按阶段 9 的后续小步启用。
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
