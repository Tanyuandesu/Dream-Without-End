/// <summary>
/// Hybrid Socket Corridor 的宽度策略。
///
/// Uniform2 必须保持枚举值 0：旧 Scene 没有保存本字段时，
/// 会继续得到已经封板的 R6～R9.4 双格走廊基线。
/// </summary>
public enum DungeonCorridorWidthMode
{
    Uniform2 = 0,
    Mixed1And2 = 1,

    /// <summary>
    /// C2 的层次宽度：保留 C1 的少量一格支路，以两格为主体，
    /// 并在通过空间校验的短开阔段加入三格宽度。
    /// </summary>
    Mixed1To3 = 2
}
