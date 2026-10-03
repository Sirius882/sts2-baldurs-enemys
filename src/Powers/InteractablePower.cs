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

    /// <summary>
    /// Single：层数隐藏且恒为 1。重复施加不会叠加，正好符合"标记位"语义。
    /// </summary>
    public override PowerStackType StackType => PowerStackType.Single;
}
