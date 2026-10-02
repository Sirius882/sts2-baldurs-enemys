using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Entities.Powers;

namespace baldurs_enemys.src.Powers;

/// <summary>
/// 「可被拾取」：纯标记位。
/// 「拾取」卡通过 <c>target.HasPower&lt;PickpocketablePower&gt;()</c> 判断目标是否可以被收进行囊。
/// 本 power 不承载任何数值逻辑，也不影响 AI。
/// </summary>
public sealed class PickpocketablePower : CustomPowerModel
{
    public override PowerType Type => PowerType.Buff;

    /// <summary>
    /// Single：层数隐藏且恒为 1。重复施加不会叠加，正好符合"标记位"语义。
    /// </summary>
    public override PowerStackType StackType => PowerStackType.Single;
}
