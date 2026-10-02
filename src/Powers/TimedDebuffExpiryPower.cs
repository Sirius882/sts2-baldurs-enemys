using System.Collections.Generic;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace baldurs_enemys.src.Powers;

/// <summary>
/// 「定时消散」（内部辅助 power，不单独出现在设计稿）：给原版无时长机制的减益
/// （魂缚锁链 / 力量降低 / 敏捷降低 / 恶咒 / 抑制）挂"持续 N 回合"的倒计时。
/// 倒计时口径与原版 Weak 一致：敌方阵营回合结束递减 1（含施加当回合末），
/// 归零时移除被跟踪的 power 实例并自毁。
///
/// 已知近似：被跟踪的 power 实例是 Counter 语义的共享实例——若同一来源在到期前
/// 重复施加，层数会叠加，而到期时会整实例移除（多段施加的贡献一起清掉）。
/// 本 mod 内各减益来源互不同场，实际战斗中不会出现跨来源串扰。
/// </summary>
public sealed class TimedDebuffExpiryPower : CustomPowerModel
{
    private readonly List<PowerModel> _targets = new();

    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Counter;

    /// <summary>登记到期的目标 power（施加后立即调用；同一实例只登记一次）。</summary>
    public void Track(PowerModel power)
    {
        if (!_targets.Contains(power))
        {
            _targets.Add(power);
        }
    }

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (side != CombatSide.Enemy || Owner == null)
        {
            return;
        }
        await PowerCmd.TickDownDuration(this);
        if (Amount > 0)
        {
            return;
        }
        foreach (PowerModel target in _targets)
        {
            if (target.Owner == Owner)
            {
                await PowerCmd.Remove(target);
            }
        }
        await PowerCmd.Remove(this);
    }

    /// <summary>
    /// 施加一个带时长的原版减益，并（复用或新建）本 power 挂倒计时。
    /// 若目标身上已有本 power（上一个组合还没到期），复用之并把剩余回合刷新为 rounds。
    /// </summary>
    public static async Task ApplyTimedDebuff<TDebuff>(PlayerChoiceContext choiceContext, Creature target, decimal amount, Creature? applier, int rounds)
        where TDebuff : PowerModel
    {
        await PowerCmd.Apply<TDebuff>(choiceContext, target, amount, applier, null);
        PowerModel? debuff = target.GetPower<TDebuff>();

        TimedDebuffExpiryPower? expiry = target.GetPower<TimedDebuffExpiryPower>();
        if (expiry == null)
        {
            await PowerCmd.Apply<TimedDebuffExpiryPower>(choiceContext, target, rounds, applier, null);
            expiry = target.GetPower<TimedDebuffExpiryPower>();
        }
        else if (expiry.Amount < rounds)
        {
            await PowerCmd.ModifyAmount(choiceContext, expiry, rounds - expiry.Amount, applier, null);
        }

        if (expiry != null && debuff != null)
        {
            expiry.Track(debuff);
        }
    }
}
