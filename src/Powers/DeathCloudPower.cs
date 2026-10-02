using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using baldurs_enemys.src.Core;

namespace baldurs_enemys.src.Powers;

/// <summary>
/// 「死云术」（巴萨扎尔的减益）：回合结束时掷 1d4+1，受到对应伤害，持续 8 回合。
/// 施加对象是玩家 —— "回合结束时"实现为持有者所在阵营回合结束时（照抄原版 DemisePower 的写法：
/// AfterSideTurnEnd + ValueProp.Unblockable | Unpowered 的无来源伤害）。
///
/// 伤害口径（总则第5条 骰子随机数）：每次结算时掷 1d4+1，走种子 RNG（Niche 通道），多人/SL 一致；
/// Amount 只承载施加计数（图标层数），不再承载每回合伤害值 → <see cref="ShouldScaleInMultiplayer"/> 关闭
/// （多人人数缩放对"掷骰"无意义）。
/// 持续回合数（8）放 internal data（同守护灵光的口径）；
/// 重复施加（巴萨扎尔每个外层循环补一次）：ApplyInternal 是 SetAmount 语义不会叠数值，
/// AfterApplied 把时长刷新为 8，即"刷新时长、不叠数值"。
/// </summary>
public sealed class DeathCloudPower : CustomPowerModel
{
    public const int ApplyAmount = 3;
    public const int DurationTurns = 8;

    private sealed class CloudData
    {
        public int TurnsRemaining;
    }

    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Counter;

    // 每次结算伤害由掷骰（1d4+1）决定，Amount 缩放不再适用
    public override bool ShouldScaleInMultiplayer => false;

    protected override object? InitInternalData() => new CloudData { TurnsRemaining = DurationTurns };

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
        // 每次结算掷 1d4+1（种子 RNG，多人/SL 一致）
        int damage = Dice.Roll(Owner.CombatState!, 1, 4, 1);
        // 无来源、无视格挡的伤害（DemisePower 同款）
        await CreatureCmd.Damage(choiceContext, Owner, damage, ValueProp.Unblockable | ValueProp.Unpowered, null, null);

        CloudData data = GetInternalData<CloudData>();
        data.TurnsRemaining--;
        if (data.TurnsRemaining <= 0)
        {
            await PowerCmd.Remove(this);
        }
    }

    public override async Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        GetInternalData<CloudData>().TurnsRemaining = DurationTurns;
        await base.AfterApplied(applier, cardSource);
    }
}
