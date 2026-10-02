using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Entities.Powers;

namespace baldurs_enemys.src.Powers;

/// <summary>
/// 「塞伦涅的庇佑」（暗夜之歌专属标记）：暗夜之歌被拾取成功时，清除所有玩家身上的死云术。
/// 效果由「拾取」卡在拾取成功路径上实现（检查该标记后清 debuff），本类只是标记。
/// </summary>
public sealed class SelunesBlessingPower : CustomPowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;
}
