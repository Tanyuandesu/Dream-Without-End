/// <summary>
/// Hybrid Socket Corridor 的宽度策略。
///
/// Uniform2 必须保持枚举值 0：旧 Scene 没有保存本字段时，
/// 会继续得到已经封板的 R6～R9.4 双格走廊基线。
/// </summary>
public enum DungeonCorridorWidthMode
{
    Uniform2 = 0,
    Mixed1And2 = 1
}
