using System.Collections.Generic;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using baldurs_enemys.src.Core;
using baldurs_enemys.src.Monsters;

namespace baldurs_enemys.src.Powers;

/// <summary>
/// 「巴尔的赐福」（奥林，进场自带）：
/// - 不处于杀戮者形态时，每回合开始时获得 1d4-1 层滑溜溜（掷出 ≤0 视为失误不发）；
/// - 杀戮者形态下，每回合开始时获得 1d4 层活力（原版 VigorPower）。
/// 每回合分别掷骰（种子 RNG，多人/SL 一致）。
///
/// 形态切换由两处驱动：奥林的转阶段招式（+3 力量并切形态）与
/// 人质死亡/被拾取（+5 力量并切形态）——分别调用 <see cref="EnterSlayerForm"/>。
/// </summary>
public sealed class BhaalsBlessingPower : CustomPowerModel
{
    public bool SlayerForm { get; set; }

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    public void EnterSlayerForm()
    {
        SlayerForm = true;
        // 双形态立绘切换（mod 自带美术）：奥林在场时把视觉换成杀戮者形态
        if (Owner?.Monster is Orin orin)
        {
            orin.ShowSlayerForm();
        }
        Flash();
    }

    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (side != CombatSide.Enemy || Owner == null || !Owner.IsAlive)
        {
            return;
        }
        if (SlayerForm)
        {
            int vigor = Dice.Roll(combatState, 1, 4, 0);
            if (vigor > 0)
            {
                await PowerCmd.Apply<VigorPower>(new ThrowingPlayerChoiceContext(), Owner, vigor, Owner, null);
            }
        }
        else
        {
            int slippery = Dice.Roll(combatState, 1, 4, -1);
            if (slippery > 0)
            {
                await PowerCmd.Apply<SlipperyPower>(new ThrowingPlayerChoiceContext(), Owner, slippery, Owner, null);
            }
        }
    }

    /// <summary>人质死亡或被拾取时：奥林获得 5 力量并进入狂怒（杀戮者形态）。</summary>
    public override async Task AfterDeath(PlayerChoiceContext choiceContext, Creature creature, bool wasRemovalPrevented, float deathAnimLength)
    {
        if (wasRemovalPrevented || creature == Owner || SlayerForm)
        {
            return;
        }
        if (creature.Monster is not Hostage || Owner == null || !Owner.IsAlive)
        {
            return;
        }
        await PowerCmd.Apply<StrengthPower>(new ThrowingPlayerChoiceContext(), Owner, 5m, Owner, null);
        EnterSlayerForm();
    }
}
