using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Entities.Powers;

namespace baldurs_enemys.src.Powers;

/// <summary>
/// 「可被交互」：纯标记位。
/// 「交互」卡通过 <c>target.HasPower&lt;InteractablePower&gt;()</c> 判断目标是否可以被交互。
/// 本 power 不承载任何数值逻辑，也不影响 AI。
/// </summary>
public sealed class InteractablePower : CustomPowerModel
{
    public override PowerType Type => PowerType.Buff;

    /// <summary>图标：mod 自制（images/powers/interactable.png 64x64 / big/interactable_big.png 256x256）。
    /// 程序化生成的原创符号化图标 ——「可被交互」：可交互标记位。
    /// 注意 power 图标**不做染色**（ICustomPower 只覆写路径），与 boss 地图图标的纯白要求不同。</summary>
    public override string? CustomPackedIconPath => "res://baldurs_enemys/images/powers/interactable.png";

    /// <summary>大图标（256x256，power 详情/图鉴用）。</summary>
    public override string? CustomBigIconPath => "res://baldurs_enemys/images/powers/big/interactable_big.png";

    /// <summary>
    /// Single：层数隐藏且恒为 1。重复施加不会叠加，正好符合"标记位"语义。
    /// </summary>
    public override PowerStackType StackType => PowerStackType.Single;
}
