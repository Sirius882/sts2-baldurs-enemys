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

    /// <summary>图标：mod 自制（images/powers/betrayed.png 64x64 / big/betrayed_big.png 256x256）。
    /// 程序化生成的原创符号化图标 ——「被背叛」：2d4 格挡；巨人杀手死亡时反噬。
    /// 注意 power 图标**不做染色**（ICustomPower 只覆写路径），与 boss 地图图标的纯白要求不同。</summary>
    public override string? CustomPackedIconPath => "res://baldurs_enemys/images/powers/betrayed.png";

    /// <summary>大图标（256x256，power 详情/图鉴用）。</summary>
    public override string? CustomBigIconPath => "res://baldurs_enemys/images/powers/big/betrayed_big.png";

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
