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

    /// <summary>图标：mod 自制（images/powers/banes_command.png 64x64 / big/banes_command_big.png 256x256）。
    /// 程序化生成的原创符号化图标 ——「班恩之诫」：每回合 1d20 格挡，掷出 20 获无实体。
    /// 注意 power 图标**不做染色**（ICustomPower 只覆写路径），与 boss 地图图标的纯白要求不同。</summary>
    public override string? CustomPackedIconPath => "res://baldurs_enemys/images/powers/banes_command.png";

    /// <summary>大图标（256x256，power 详情/图鉴用）。</summary>
    public override string? CustomBigIconPath => "res://baldurs_enemys/images/powers/big/banes_command_big.png";

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
