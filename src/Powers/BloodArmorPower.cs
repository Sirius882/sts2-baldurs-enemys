using System.Threading.Tasks;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace baldurs_enemys.src.Powers;

/// <summary>
/// 「血护甲」（卡扎多尔，开场自带）：
/// - 卡扎多尔的格挡跨回合保留（原版 BarricadePower 的 ShouldClearBlock 同款）；
/// - 溢出生命值上限的回血值实时转化为等量格挡。
///
/// 溢出判定：CreatureCmd.Heal 结束时会对"请求治疗量"触发
/// <see cref="AfterCurrentHpChanged"/>（delta = 请求量，含被上限截断的部分）。
/// 本 power 自持上一快照 HP：actualGain = 当前HP - 快照；溢出 = delta - actualGain。
/// 伤害导致的 HP 变化 delta &lt; 0，只更新快照不转化。
/// </summary>
public sealed class BloodArmorPower : CustomPowerModel
{
    private decimal _observedHp = -1m;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    /// <summary>卡扎多尔的格挡不随回合清理（Barricade 同款签名）。</summary>
    public override bool ShouldClearBlock(Creature creature) => creature != Owner;

    public override async Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        if (Owner != null)
        {
            _observedHp = Owner.CurrentHp;
        }
        await base.AfterApplied(applier, cardSource);
    }

    public override async Task AfterCurrentHpChanged(Creature creature, decimal delta)
    {
        if (creature != Owner || Owner == null)
        {
            return;
        }
        decimal before = _observedHp;
        _observedHp = Owner.CurrentHp;
        if (before < 0m || delta <= 0m)
        {
            return;
        }
        decimal actualGain = Owner.CurrentHp - before;
        decimal overflow = delta - actualGain;
        if (overflow <= 0m)
        {
            return;
        }
        await CreatureCmd.GainBlock(Owner, overflow, ValueProp.Unpowered, null);
    }
}
