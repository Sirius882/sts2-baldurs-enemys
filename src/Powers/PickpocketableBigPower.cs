using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Entities.Powers;

namespace baldurs_enemys.src.Powers;

/// <summary>
/// 「可被拾取·大」（暗夜之歌专属）：需要对其打出**两次**「拾取」才能触发拾取效果。
///
/// Counter 计数：施加时 Amount = 2；第一次拾取递减到 1（无其他效果），
/// 第二次拾取（Amount == 1）真正触发拾取 —— 逻辑在拾取卡里实现。
/// </summary>
public sealed class PickpocketableBigPower : CustomPowerModel
{
    public const int PickupsRequired = 2;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;
}
