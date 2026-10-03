using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Entities.Powers;

namespace baldurs_enemys.src.Powers;

/// <summary>
/// 「塞伦涅的庇佑」（暗夜之歌专属标记）：暗夜之歌被交互成功时，清除所有玩家身上的死云术。
/// 效果由「交互」卡在交互成功路径上实现（检查该标记后清 debuff），本类只是标记。
/// </summary>
public sealed class SelunesBlessingPower : CustomPowerModel
{
    public override PowerType Type => PowerType.Buff;

    /// <summary>图标：mod 自制（images/powers/selunes_blessing.png 64x64 / big/selunes_blessing_big.png 256x256）。
    /// 程序化生成的原创符号化图标 ——「塞伦涅的庇佑」：交互成功清除死云。
    /// 注意 power 图标**不做染色**（ICustomPower 只覆写路径），与 boss 地图图标的纯白要求不同。</summary>
    public override string? CustomPackedIconPath => "res://baldurs_enemys/images/powers/selunes_blessing.png";

    /// <summary>大图标（256x256，power 详情/图鉴用）。</summary>
    public override string? CustomBigIconPath => "res://baldurs_enemys/images/powers/big/selunes_blessing_big.png";

    public override PowerStackType StackType => PowerStackType.Single;
}
