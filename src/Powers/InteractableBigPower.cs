using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Entities.Powers;

namespace baldurs_enemys.src.Powers;

/// <summary>
/// 「可被交互·大」（暗夜之歌专属）：需要对其打出**两次**「交互」才能触发交互效果。
///
/// Counter 计数：施加时 Amount = 2；第一次交互递减到 1（无其他效果），
/// 第二次交互（Amount == 1）真正触发 —— 逻辑在交互卡里实现。
/// </summary>
public sealed class InteractableBigPower : CustomPowerModel
{
    public const int InteractionsRequired = 2;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;
}
