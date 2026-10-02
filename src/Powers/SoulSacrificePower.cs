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
using baldurs_enemys.src.Core;
using baldurs_enemys.src.Monsters;

namespace baldurs_enemys.src.Powers;

/// <summary>
/// 「灵魂」（灵魂柱，进场自带）：每回合开始时使拉斐尔回血 2d4（每根柱子分别掷骰）。
/// </summary>
public sealed class SoulSacrificePower : CustomPowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (side != CombatSide.Enemy || Owner == null || !Owner.IsAlive)
        {
            return;
        }
        Creature? raphael = combatState.Enemies.FirstOrDefault(c => c.Monster is Raphael && c.IsAlive);
        if (raphael == null)
        {
            return;
        }
        await CreatureCmd.Heal(raphael, Dice.Roll(combatState, 2, 4, 0));
    }
}
