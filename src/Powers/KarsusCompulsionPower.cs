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
using MegaCrit.Sts2.Core.ValueProps;

namespace baldurs_enemys.src.Powers;

/// <summary>
/// 「卡尔萨斯强迫术」（君主，二阶段招式反复施加）：1 回合后，对所有玩家造成 999 点伤害。
///
/// 计时口径：施加时 Amount=2，每个敌方阵营回合结束 TickDownDuration 递减 1
/// （与原版 Weak/Vulnerable 的时长语义一致——施加当回合末计第一次递减），
/// 递减到 0（即"1 回合后"）触发 999 无阻挡无来源伤害并自毁。
/// 君主每两回合重新施加一次 → 实际效果：每逢他眩晕的那个回合末落雷一次，
/// 必须在他下一次刷新前把他打掉（他只有 100 血）。
/// </summary>
public sealed class KarsusCompulsionPower : CustomPowerModel
{
    /// <summary>施加时的初始倒计时（1 回合后触发 → 引擎时长语义下填 2）。</summary>
    public const int ApplyAmount = 2;

    public const int DoomDamage = 999;

    public override PowerType Type => PowerType.Buff;

    /// <summary>图标：mod 自制（images/powers/karsus_compulsion.png 64x64 / big/karsus_compulsion_big.png 256x256）。
    /// 程序化生成的原创符号化图标 ——「卡尔萨斯强迫术」：1 回合后 999 伤害。
    /// 注意 power 图标**不做染色**（ICustomPower 只覆写路径），与 boss 地图图标的纯白要求不同。</summary>
    public override string? CustomPackedIconPath => "res://baldurs_enemys/images/powers/karsus_compulsion.png";

    /// <summary>大图标（256x256，power 详情/图鉴用）。</summary>
    public override string? CustomBigIconPath => "res://baldurs_enemys/images/powers/big/karsus_compulsion_big.png";

    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (side != CombatSide.Enemy || Owner == null || !participants.Contains(Owner))
        {
            return;
        }
        await PowerCmd.TickDownDuration(this);
        if (Amount > 0)
        {
            return;
        }
        Flash();
        foreach (Creature player in Owner.CombatState!.PlayerCreatures)
        {
            if (!player.IsAlive) continue;
            await CreatureCmd.Damage(choiceContext, player, DoomDamage, ValueProp.Unblockable | ValueProp.Unpowered, null, null);
        }
        await PowerCmd.Remove(this);
    }
}
