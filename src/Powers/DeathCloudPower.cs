using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.ValueProps;
using baldurs_enemys.src.Core;

namespace baldurs_enemys.src.Powers;

/// <summary>
/// 「死云术」（巴萨扎尔的减益）：回合结束时掷 1d8+1，受到对应伤害，**永久持续**。
/// 施加对象是玩家 —— "回合结束时"实现为持有者所在阵营回合结束时（照抄原版 DemisePower 的写法：
/// AfterSideTurnEnd + ValueProp.Unblockable | Unpowered 的无来源伤害）。
///
/// 伤害口径（总则第5 条 骰子随机数）：每次结算时掷 1d8+1，走种子 RNG（Niche 通道），多人/SL 一致；
/// Amount 只承载施加计数（图标层数），不再承载每回合伤害值 → <see cref="ShouldScaleInMultiplayer"/> 关闭
/// （多人人数缩放对"掷骰"无意义）。
///
/// 【2026-10-03 设计稿「有改动」：由「1d4+1、持续 8 回合」改为「1d8+1、永久持续」】
/// - 掷骰 1d4+1 → 1d8+1；
/// - 移除 internal data 与倒计时（原 CloudData / DurationTurns / AfterSideTurnEnd 里的
///   PowerCmd.Remove 分支 / AfterApplied 刷新），语义与原版 <see cref="DemisePower"/> 完全一致：
///   只结算伤害，永不移除。施加一次即永久，无需再刷新时长。
/// </summary>
public sealed class DeathCloudPower : CustomPowerModel
{
    public const int ApplyAmount = 3;

    public override PowerType Type => PowerType.Debuff;

    /// <summary>图标：mod 自制（images/powers/death_cloud.png 64x64 / big/death_cloud_big.png 256x256）。
    /// 程序化生成的原创符号化图标 ——「死云术」：回合结束 1d8+1 伤害，永久。
    /// 注意 power 图标**不做染色**（ICustomPower 只覆写路径），与 boss 地图图标的纯白要求不同。</summary>
    public override string? CustomPackedIconPath => "res://baldurs_enemys/images/powers/death_cloud.png";

    /// <summary>大图标（256x256，power 详情/图鉴用）。</summary>
    public override string? CustomBigIconPath => "res://baldurs_enemys/images/powers/big/death_cloud_big.png";

    public override PowerStackType StackType => PowerStackType.Counter;

    // 每次结算伤害由掷骰（1d8+1）决定，Amount 缩放不再适用
    public override bool ShouldScaleInMultiplayer => false;

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (!participants.Contains(Owner))
        {
            return;
        }
        if (Owner == null || !Owner.IsAlive)
        {
            return;
        }

        Flash();
        // 每次结算掷 1d8+1（种子 RNG，多人/SL 一致）
        int damage = Dice.Roll(Owner.CombatState!, 1, 8, 1);
        // 无来源、无视格挡的伤害（DemisePower 同款）
        await CreatureCmd.Damage(choiceContext, Owner, damage, ValueProp.Unblockable | ValueProp.Unpowered, null, null);
    }
}
