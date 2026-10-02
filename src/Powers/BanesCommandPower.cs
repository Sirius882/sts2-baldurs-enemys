using System.Collections.Generic;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.ValueProps;
using baldurs_enemys.src.Core;

namespace baldurs_enemys.src.Powers;

/// <summary>
/// 「班恩之诫」（戈塔什，进场自带）：每回合开始时获得 1d20 格挡（每回合分别掷骰）；
/// 若骰出 20 点，则再获得 1 层无实体（原版 IntangiblePower）。
/// </summary>
public sealed class BanesCommandPower : CustomPowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (side != CombatSide.Enemy || Owner == null || !Owner.IsAlive)
        {
            return;
        }
        int roll = Dice.Roll(combatState, 1, 20, 0);
        await CreatureCmd.GainBlock(Owner, roll, ValueProp.Unpowered, null);
        if (roll == 20)
        {
            await PowerCmd.Apply<IntangiblePower>(new ThrowingPlayerChoiceContext(), Owner, 1m, Owner, null);
        }
    }
}
