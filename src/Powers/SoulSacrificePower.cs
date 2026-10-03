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

    /// <summary>图标：mod 自制（images/powers/soul_sacrifice.png 64x64 / big/soul_sacrifice_big.png 256x256）。
    /// 程序化生成的原创符号化图标 ——「灵魂」：每回合治疗拉斐尔。
    /// 注意 power 图标**不做染色**（ICustomPower 只覆写路径），与 boss 地图图标的纯白要求不同。</summary>
    public override string? CustomPackedIconPath => "res://baldurs_enemys/images/powers/soul_sacrifice.png";

    /// <summary>大图标（256x256，power 详情/图鉴用）。</summary>
    public override string? CustomBigIconPath => "res://baldurs_enemys/images/powers/big/soul_sacrifice_big.png";

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
