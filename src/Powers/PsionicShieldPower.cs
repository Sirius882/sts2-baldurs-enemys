using System.Collections.Generic;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using baldurs_enemys.src.Core;

namespace baldurs_enemys.src.Powers;

/// <summary>
/// 「灵能护盾」（君主，开场招式获得）：每回合开始时获得 2d8 格挡（每回合分别掷骰）。
/// </summary>
public sealed class PsionicShieldPower : CustomPowerModel
{
    public override PowerType Type => PowerType.Buff;

    /// <summary>图标：mod 自制（images/powers/psionic_shield.png 64x64 / big/psionic_shield_big.png 256x256）。
    /// 程序化生成的原创符号化图标 ——「灵能护盾」：每回合 2d8 格挡。
    /// 注意 power 图标**不做染色**（ICustomPower 只覆写路径），与 boss 地图图标的纯白要求不同。</summary>
    public override string? CustomPackedIconPath => "res://baldurs_enemys/images/powers/psionic_shield.png";

    /// <summary>大图标（256x256，power 详情/图鉴用）。</summary>
    public override string? CustomBigIconPath => "res://baldurs_enemys/images/powers/big/psionic_shield_big.png";

    public override PowerStackType StackType => PowerStackType.Single;

    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (side != CombatSide.Enemy || Owner == null || !Owner.IsAlive)
        {
            return;
        }
        await CreatureCmd.GainBlock(Owner, Dice.Roll(combatState, 2, 8, 0), ValueProp.Unpowered, null);
    }
}
