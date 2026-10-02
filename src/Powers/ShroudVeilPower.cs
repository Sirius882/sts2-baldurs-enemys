using System.Collections.Generic;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using baldurs_enemys.src.Core;
using MegaCrit.Sts2.Core.Commands;

namespace baldurs_enemys.src.Powers;

/// <summary>
/// 「暗影帷幕」（维康妮亚）：每次将受到伤害时掷 1d4，掷出 1 则该次伤害无效。
/// 每触发一次层数 -1，层数为 0 时该 power 消失；重复施加刷新至 3 层。
///
/// 实现口径：
/// - 掷骰放在 <see cref="ModifyHpLostAfterOsty"/>（只在伤害**实算执行**路径上被调用，
///   卡牌预览 DamageVar 走的是 ModifyDamage 钩子族，不会触发本掷骰）——
///   预览/悬停不消耗种子 RNG，保证多人/SL 一致（Intangible/Slippery 同款位置）。
/// - "无效"落地为 HP 损失归零；格挡仍会被正常消耗（引擎的格挡吸收不经过 HP 损失钩子，
///   这是引擎限制下的近似实现，原版 SlipperyPower 同样只作用于 HP 损失）。
/// - 层数递减：ModifyHpLost 钩子是同步方法，不能 await PowerCmd.Decrement ——
///   先置 _pendingDecrement 标记，由紧随其后的 <see cref="AfterDamageReceived"/> 异步递减；
///   并在敌方回合结束时兜底补扣（防钩子路径意外漏扣）。
/// </summary>
public sealed class ShroudVeilPower : CustomPowerModel
{
    /// <summary>维康妮亚每次施加/刷新的层数。</summary>
    public const int ApplyAmount = 3;

    private bool _pendingDecrement;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override decimal ModifyHpLostAfterOsty(Creature target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (target != Owner || amount < 1m || Amount <= 0)
        {
            return amount;
        }
        ICombatState? combatState = Owner.CombatState;
        if (combatState == null)
        {
            return amount;
        }
        if (Dice.Roll(combatState, 1, 4, 0) == 1)
        {
            _pendingDecrement = true;
            Flash();
            return 0m;
        }
        return amount;
    }

    public override async Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature target, DamageResult result, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (target != Owner || !_pendingDecrement)
        {
            return;
        }
        _pendingDecrement = false;
        await DecrementLayer();
    }

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        // 兜底：本回合内有过"掷出 1"但 AfterDamageReceived 没走到的极端情况，回合末补扣
        if (side != CombatSide.Enemy || !_pendingDecrement)
        {
            return;
        }
        _pendingDecrement = false;
        await DecrementLayer();
    }

    private async Task DecrementLayer()
    {
        if (Amount <= 1)
        {
            await PowerCmd.Remove(this);
        }
        else
        {
            await PowerCmd.Decrement(this);
        }
    }
}
