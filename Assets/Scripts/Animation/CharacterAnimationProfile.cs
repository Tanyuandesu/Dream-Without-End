using System;
using UnityEngine;

[Serializable]
public sealed class SpriteAnimationSequence
{
    [SerializeField] private Sprite[] frames = new Sprite[0];

    [Min(0.01f)]
    [SerializeField] private float framesPerSecond = 8f;

    [SerializeField] private bool loop = true;

    public int FrameCount =>
        frames != null ? frames.Length : 0;

    public float FramesPerSecond =>
        Mathf.Max(0.01f, framesPerSecond);

    public bool Loop => loop;

    public bool HasFrames => FrameCount > 0;

    public Sprite GetFrame(int index)
    {
        if (!HasFrames)
        {
            return null;
        }

        int safeIndex = Mathf.Clamp(
            index,
            0,
            frames.Length - 1);

        return frames[safeIndex];
    }

    public void Configure(
        Sprite[] newFrames,
        float newFramesPerSecond,
        bool shouldLoop)
    {
        frames = newFrames != null
            ? newFrames
            : new Sprite[0];

        framesPerSecond =
            Mathf.Max(0.01f, newFramesPerSecond);

        loop = shouldLoop;
    }
}

[Serializable]
public sealed class DirectionalSpriteSequenceSet
{
    [SerializeField] private SpriteAnimationSequence south =
        new SpriteAnimationSequence();

    [SerializeField] private SpriteAnimationSequence southEast =
        new SpriteAnimationSequence();

    [SerializeField] private SpriteAnimationSequence east =
        new SpriteAnimationSequence();

    [SerializeField] private SpriteAnimationSequence northEast =
        new SpriteAnimationSequence();

    [SerializeField] private SpriteAnimationSequence north =
        new SpriteAnimationSequence();

    [SerializeField] private SpriteAnimationSequence northWest =
        new SpriteAnimationSequence();

    [SerializeField] private SpriteAnimationSequence west =
        new SpriteAnimationSequence();

    [SerializeField] private SpriteAnimationSequence southWest =
        new SpriteAnimationSequence();

    public SpriteAnimationSequence GetExact(
        CharacterFacingDirection direction)
    {
        switch (direction)
        {
            case CharacterFacingDirection.South:
                return south;

            case CharacterFacingDirection.SouthEast:
                return southEast;

            case CharacterFacingDirection.East:
                return east;

            case CharacterFacingDirection.NorthEast:
                return northEast;

            case CharacterFacingDirection.North:
                return north;

            case CharacterFacingDirection.NorthWest:
                return northWest;

            case CharacterFacingDirection.West:
                return west;

            case CharacterFacingDirection.SouthWest:
                return southWest;

            default:
                return south;
        }
    }

    public void Set(
        CharacterFacingDirection direction,
        Sprite[] frames,
        float framesPerSecond,
        bool loop)
    {
        GetExact(direction).Configure(
            frames,
            framesPerSecond,
            loop);
    }
}

/// <summary>
/// 一种角色或敌人的方向动画资源配置。
///
/// CA1 不要求 Animator Controller。
/// 运行时驱动器直接读取这里的 Sprite 序列，适合当前由 Spawner
/// 动态创建玩家和敌人的工程结构，也便于以后批量接入 PixelLab 输出。
/// </summary>
[CreateAssetMenu(
    fileName = "CharacterAnimationProfile",
    menuName = "Dream Dungeon/Character Animation Profile")]
public sealed class CharacterAnimationProfile : ScriptableObject
{
    [Header("方向规则")]
    [SerializeField] private CharacterAnimationDirectionMode directionMode =
        CharacterAnimationDirectionMode.EightDirections;

    [Tooltip(
        "开启后，左、左上、左下可以复用右侧动画并水平翻转。" +
        "不对称角色可关闭。")]
    [SerializeField] private bool mirrorLeftDirections = true;

    [SerializeField] private CharacterFacingDirection defaultFacing =
        CharacterFacingDirection.South;

    [Header("当前使用")]
    [SerializeField] private DirectionalSpriteSequenceSet idle =
        new DirectionalSpriteSequenceSet();

    [SerializeField] private DirectionalSpriteSequenceSet walk =
        new DirectionalSpriteSequenceSet();

    [Header("后续预留")]
    [SerializeField] private DirectionalSpriteSequenceSet attack =
        new DirectionalSpriteSequenceSet();

    [SerializeField] private DirectionalSpriteSequenceSet hurt =
        new DirectionalSpriteSequenceSet();

    [SerializeField] private DirectionalSpriteSequenceSet death =
        new DirectionalSpriteSequenceSet();

    [SerializeField] private DirectionalSpriteSequenceSet special =
        new DirectionalSpriteSequenceSet();

    public CharacterAnimationDirectionMode DirectionMode =>
        directionMode;

    public bool MirrorLeftDirections =>
        mirrorLeftDirections;

    public CharacterFacingDirection DefaultFacing =>
        defaultFacing;

    public SpriteAnimationSequence GetSequence(
        CharacterAnimationState state,
        CharacterFacingDirection direction,
        out bool flipX)
    {
        direction = NormalizeDirection(direction);

        DirectionalSpriteSequenceSet requestedSet =
            GetSet(state);

        SpriteAnimationSequence sequence =
            GetDirectionalSequence(
                requestedSet,
                direction,
                out flipX);

        if (sequence != null && sequence.HasFrames)
        {
            return sequence;
        }

        if (state != CharacterAnimationState.Idle)
        {
            sequence = GetDirectionalSequence(
                idle,
                direction,
                out flipX);

            if (sequence != null && sequence.HasFrames)
            {
                return sequence;
            }
        }

        flipX = false;

        sequence = requestedSet.GetExact(defaultFacing);

        if (sequence != null && sequence.HasFrames)
        {
            return sequence;
        }

        sequence = idle.GetExact(defaultFacing);

        return sequence;
    }

    public bool HasSequence(
        CharacterAnimationState state,
        CharacterFacingDirection direction)
    {
        direction = NormalizeDirection(direction);

        bool unusedFlip;

        SpriteAnimationSequence sequence =
            GetDirectionalSequence(
                GetSet(state),
                direction,
                out unusedFlip);

        return sequence != null && sequence.HasFrames;
    }

    /// <summary>
    /// 供 CA1 Editor 安装工具和未来 PixelLab 导入工具使用。
    /// </summary>
    public void ConfigureDirectionRules(
        CharacterAnimationDirectionMode newMode,
        bool shouldMirrorLeft,
        CharacterFacingDirection newDefaultFacing)
    {
        directionMode = newMode;
        mirrorLeftDirections = shouldMirrorLeft;
        defaultFacing = newDefaultFacing;
    }

    /// <summary>
    /// 清空旧序列，供可重复执行的导入工具使用。
    /// </summary>
    public void ClearAllSequences()
    {
        idle = new DirectionalSpriteSequenceSet();
        walk = new DirectionalSpriteSequenceSet();
        attack = new DirectionalSpriteSequenceSet();
        hurt = new DirectionalSpriteSequenceSet();
        death = new DirectionalSpriteSequenceSet();
        special = new DirectionalSpriteSequenceSet();
    }

    /// <summary>
    /// 供 Editor 导入工具写入标准化帧序列。
    /// </summary>
    public void SetSequence(
        CharacterAnimationState state,
        CharacterFacingDirection direction,
        Sprite[] frames,
        float framesPerSecond,
        bool loop)
    {
        GetSet(state).Set(
            direction,
            frames,
            framesPerSecond,
            loop);
    }

    private CharacterFacingDirection NormalizeDirection(
        CharacterFacingDirection direction)
    {
        if (directionMode ==
            CharacterAnimationDirectionMode.NoDirection)
        {
            return defaultFacing;
        }

        if (directionMode !=
            CharacterAnimationDirectionMode.FourDirections)
        {
            return direction;
        }

        switch (direction)
        {
            case CharacterFacingDirection.NorthEast:
            case CharacterFacingDirection.NorthWest:
                return CharacterFacingDirection.North;

            case CharacterFacingDirection.SouthEast:
            case CharacterFacingDirection.SouthWest:
                return CharacterFacingDirection.South;

            default:
                return direction;
        }
    }

    private SpriteAnimationSequence GetDirectionalSequence(
        DirectionalSpriteSequenceSet set,
        CharacterFacingDirection direction,
        out bool flipX)
    {
        flipX = false;

        SpriteAnimationSequence exact =
            set.GetExact(direction);

        if (exact != null && exact.HasFrames)
        {
            return exact;
        }

        if (mirrorLeftDirections && IsLeftDirection(direction))
        {
            CharacterFacingDirection mirrored =
                MirrorToRight(direction);

            SpriteAnimationSequence mirroredSequence =
                set.GetExact(mirrored);

            if (mirroredSequence != null &&
                mirroredSequence.HasFrames)
            {
                flipX = true;
                return mirroredSequence;
            }
        }

        CharacterFacingDirection firstFallback;
        CharacterFacingDirection secondFallback;

        GetCardinalFallbacks(
            direction,
            out firstFallback,
            out secondFallback);

        SpriteAnimationSequence first =
            set.GetExact(firstFallback);

        if (first != null && first.HasFrames)
        {
            return first;
        }

        SpriteAnimationSequence second =
            set.GetExact(secondFallback);

        return second;
    }

    private DirectionalSpriteSequenceSet GetSet(
        CharacterAnimationState state)
    {
        switch (state)
        {
            case CharacterAnimationState.Walk:
                return walk;

            case CharacterAnimationState.Attack:
                return attack;

            case CharacterAnimationState.Hurt:
                return hurt;

            case CharacterAnimationState.Death:
                return death;

            case CharacterAnimationState.Special:
                return special;

            default:
                return idle;
        }
    }

    private static bool IsLeftDirection(
        CharacterFacingDirection direction)
    {
        return direction == CharacterFacingDirection.West ||
               direction == CharacterFacingDirection.NorthWest ||
               direction == CharacterFacingDirection.SouthWest;
    }

    private static CharacterFacingDirection MirrorToRight(
        CharacterFacingDirection direction)
    {
        switch (direction)
        {
            case CharacterFacingDirection.West:
                return CharacterFacingDirection.East;

            case CharacterFacingDirection.NorthWest:
                return CharacterFacingDirection.NorthEast;

            case CharacterFacingDirection.SouthWest:
                return CharacterFacingDirection.SouthEast;

            default:
                return direction;
        }
    }

    private static void GetCardinalFallbacks(
        CharacterFacingDirection direction,
        out CharacterFacingDirection first,
        out CharacterFacingDirection second)
    {
        switch (direction)
        {
            case CharacterFacingDirection.NorthEast:
                first = CharacterFacingDirection.North;
                second = CharacterFacingDirection.East;
                break;

            case CharacterFacingDirection.NorthWest:
                first = CharacterFacingDirection.North;
                second = CharacterFacingDirection.West;
                break;

            case CharacterFacingDirection.SouthEast:
                first = CharacterFacingDirection.South;
                second = CharacterFacingDirection.East;
                break;

            case CharacterFacingDirection.SouthWest:
                first = CharacterFacingDirection.South;
                second = CharacterFacingDirection.West;
                break;

            default:
                first = direction;
                second = CharacterFacingDirection.South;
                break;
        }
    }
}
