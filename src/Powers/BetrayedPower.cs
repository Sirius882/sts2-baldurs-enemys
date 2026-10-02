using System.Collections.Generic;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.ValueProps;
using baldurs_enemys.src.Core;
using baldurs_enemys.src.Monsters;
using MegaCrit.Sts2.Core.Models.Powers;

namespace baldurs_enemys.src.Powers;

/// <summary>
/// 「被背叛」（安苏，开场自带）：
/// - 每回合开始时获得 2d4 格挡（每回合分别掷骰）；
/// - 博德安的巨人杀手死亡时，安苏立即获得 5 力量，并受到最大生命值 1/2 的伤害。
/// </summary>
public sealed class BetrayedPower : CustomPowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (side != CombatSide.Enemy || Owner == null || !Owner.IsAlive)
        {
            return;
        }
        await CreatureCmd.GainBlock(Owner, Dice.Roll(combatState, 2, 4, 0), ValueProp.Unpowered, null);
    }

    public override async Task AfterDeath(PlayerChoiceContext choiceContext, Creature creature, bool wasRemovalPrevented, float deathAnimLength)
    {
        if (wasRemovalPrevented || creature == Owner)
        {
            return;
        }
        if (creature.Monster is not BalduransGiantslayer || Owner == null || !Owner.IsAlive)
        {
            return;
        }
        await CreatureCmd.TriggerAnim(Owner, "Cast", 0.2f);
        await PowerCmd.Apply<StrengthPower>(new ThrowingPlayerChoiceContext(), Owner, 5m, Owner, null);
        int damage = Owner.MaxHp / 2;
        if (damage > 0)
        {
            await CreatureCmd.Damage(choiceContext, Owner, damage, ValueProp.Unpowered, null, null);
        }
    }
}
