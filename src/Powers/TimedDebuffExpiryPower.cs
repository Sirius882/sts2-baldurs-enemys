using System;
using System.Collections.Generic;
using System.Linq;
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
/// 当前按原版 TickDownDuration 处理：对玩家施加的减益跳过施加当回合末，之后每个敌方回合末递减。
/// 旧实现的近似说明（2026-10-24 已修正，保留原注释）：
/// 已知近似：被跟踪的 power 实例是 Counter 语义的共享实例——若同一来源在到期前
/// 重复施加，层数会叠加，而到期时会整实例移除（多段施加的贡献一起清掉）。
/// 本 mod 内各减益来源互不同场，实际战斗中不会出现跨来源串扰。
///
/// 每次施加都创建独立的隐藏计时器实例，并记录本次施加的层数；到期时只撤销这一次
/// 施加的层数。因此多个梦境守护者、或同一来源的多次施加，不会共用或提前清除计时。
/// </summary>
public sealed class TimedDebuffExpiryPower : CustomPowerModel
{
    private sealed class ExpiryData
    {
        public readonly List<(PowerModel Template, PowerModel? Instance, int Amount)> Targets = new();
    }

    protected override object InitInternalData() => new ExpiryData();

    public override PowerType Type => PowerType.Debuff;

    public override bool ShouldScaleInMultiplayer => false;

    public override PowerInstanceType InstanceType => PowerInstanceType.Instanced;

    protected override bool IsVisibleInternal => false;

    /// <summary>图标：mod 自制（images/powers/timed_debuff_expiry.png 64x64 / big/timed_debuff_expiry_big.png 256x256）。
    /// 程序化生成的原创符号化图标 ——「定时消散」：内部辅助：减益倒计时。
    /// 注意 power 图标**不做染色**（ICustomPower 只覆写路径），与 boss 地图图标的纯白要求不同。</summary>
    public override string? CustomPackedIconPath => "res://baldurs_enemys/images/powers/timed_debuff_expiry.png";

    /// <summary>大图标（256x256，power 详情/图鉴用）。</summary>
    public override string? CustomBigIconPath => "res://baldurs_enemys/images/powers/big/timed_debuff_expiry_big.png";

    public override PowerStackType StackType => PowerStackType.Counter;

    /// <summary>登记到期的目标 power（施加后立即调用；同一实例只登记一次）。</summary>
    // 现在每次施加登记一笔实际增量，实例被抵消到 0 时仍保留恢复力量/敏捷所需的信息。
    private void Track(PowerModel template, PowerModel? instance, int amount)
    {
        GetInternalData<ExpiryData>().Targets.Add((template, instance, amount));
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
        foreach (var contribution in GetInternalData<ExpiryData>().Targets)
        {
            if (contribution.Template.AllowNegative)
            {
                // 力量/敏捷在叠加后可能恰好归零、从而移除实例；到期仍须归还本次扣除量。
                await PowerCmd.Apply(choiceContext, contribution.Template.ToMutable(), Owner,
                    -contribution.Amount, Owner, null);
            }
            else if (contribution.Instance is { } target && Owner.Powers.Contains(target))
            {
                // 只撤销本次实际附加量，保留其他来源以及后续回合仍未到期的层数。
                await PowerCmd.ModifyAmount(choiceContext, target,
                    -Math.Min(contribution.Amount, target.Amount), Owner, null);
            }
        }
        // TickDownDuration 归零时已移除本计时器，不重复触发移除通知。
        if (Owner.Powers.Contains(this))
            await PowerCmd.Remove(this);
    }

    /// <summary>
    /// 施加一个带时长的原版减益，并（复用或新建）本 power 挂倒计时。
    /// 若目标身上已有本 power（上一个组合还没到期），复用之并把剩余回合刷新为 rounds。
    /// </summary>
    // 上述复用说明是旧行为；当前每次调用都创建独立计时器，不刷新任何旧批次。
    public static async Task ApplyTimedDebuff<TDebuff>(PlayerChoiceContext choiceContext, Creature target, decimal amount, Creature? applier, int rounds)
        where TDebuff : PowerModel
    {
        if (rounds <= 0 || amount == 0m || !target.IsAlive) return;

        int before = target.GetPower<TDebuff>()?.Amount ?? 0;
        await PowerCmd.Apply<TDebuff>(choiceContext, target, amount, applier, null);
        PowerModel? debuff = target.GetPower<TDebuff>();
        int appliedAmount = (debuff?.Amount ?? 0) - before;
        // 人工制品或其他规则阻止附加时，不创建一个会错误归还属性的计时器。
        if (appliedAmount == 0) return;

        TimedDebuffExpiryPower? expiry = await PowerCmd.Apply<TimedDebuffExpiryPower>(
            choiceContext, target, rounds, applier, null);
        expiry?.Track(ModelDb.Power<TDebuff>(), debuff, appliedAmount);
    }
}
