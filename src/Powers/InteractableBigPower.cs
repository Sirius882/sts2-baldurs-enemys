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

    /// <summary>图标：mod 自制（images/powers/interactable_big.png 64x64 / big/interactable_big_big.png 256x256）。
    /// 程序化生成的原创符号化图标 ——「可被交互·大」：需两次交互才触发。
    /// 注意 power 图标**不做染色**（ICustomPower 只覆写路径），与 boss 地图图标的纯白要求不同。</summary>
    public override string? CustomPackedIconPath => "res://baldurs_enemys/images/powers/interactable_big.png";

    /// <summary>大图标（256x256，power 详情/图鉴用）。</summary>
    public override string? CustomBigIconPath => "res://baldurs_enemys/images/powers/big/interactable_big_big.png";

    public override PowerStackType StackType => PowerStackType.Counter;
}
