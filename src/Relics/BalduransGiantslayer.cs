using System.Threading.Tasks;
using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Entities.Players;
using baldurs_enemys.src.Core;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Models.Powers;

namespace baldurs_enemys.src.Relics;

/// <summary>
/// 「博德安的巨人杀手」：拾取安苏战巨剑的唯一产物。
///
/// 效果（设计稿）：每场战斗开始时掷骰获得力量——
/// - 普通战：1d4 力量；
/// - 精英战：1d8 力量；
/// - Boss 战：2d20/3 力量（两骰之和 ÷3 向下取整），且若任意一骰骰出 20，再掷 1d20 追加力量
///   （第二次再出 20 不继续）。
///
/// 唯一来源是拾取博德安的巨人杀手：标注 [Pool(typeof(BaldursRelicPool))]（IsShared=false 的私有池，
/// 不进任何共享奖励池，仅为满足 BaseLib 的 PoolAttribute 强制校验）+ IsAllowedInShops=>false 双保险。
/// 图标：与怪物本体共用 mod 自带美术（baldurs_enemys/images/relics/baldurans_giantslayer.png）。
/// </summary>
[Pool(typeof(BaldursRelicPool))]
public sealed class BalduransGiantslayer : CustomRelicModel
{
    public override RelicRarity Rarity => RelicRarity.Event;

    public override bool IsAllowedInShops => false;

    protected override string IconBaseName => "blessed_antler";

    public override async Task AfterRoomEntered(AbstractRoom room)
    {
        if (room is not CombatRoom combatRoom)
        {
            return;
        }
        Player? player = base.Owner;
        if (player == null)
        {
            return;
        }
        ICombatState? combatState = player.Creature.CombatState;
        if (combatState == null)
        {
            return;
        }

        Flash();
        int strength;
        switch (combatRoom.RoomType)
        {
            case RoomType.Elite:
                strength = Dice.Roll(combatState, 1, 8, 0);
                break;
            case RoomType.Boss:
            {
                // 2d20/3 力量；任意一骰为 20 → 再掷 1d20 追加（第二次 20 不继续）
                int die1 = Dice.Roll(combatState, 1, 20, 0);
                int die2 = Dice.Roll(combatState, 1, 20, 0);
                strength = (die1 + die2) / 3;
                if (die1 == 20 || die2 == 20)
                {
                    strength += Dice.Roll(combatState, 1, 20, 0);
                }
                break;
            }
            default:
                // 普通战
                strength = Dice.Roll(combatState, 1, 4, 0);
                break;
        }

        if (strength > 0)
        {
            await PowerCmd.Apply<StrengthPower>(
                new ThrowingPlayerChoiceContext(), player.Creature, strength, player.Creature, null);
        }
    }
}
