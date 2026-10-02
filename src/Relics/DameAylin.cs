using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;
using baldurs_enemys.src.Core;

namespace baldurs_enemys.src.Relics;

/// <summary>
/// 「艾琳女士」（Dame Aylin）：拾取暗夜之歌的唯一产物。
///
/// 效果（设计稿）：
/// - 每场战斗开始时，获得 3 层人工制品（ArtifactPower）；
/// - 持有该遗物时，每回合开始时掷 1d4，获得对应格挡（总则第5条：种子 RNG，多人/SL 一致）；
/// - 持有者面对凯瑟里克·索姆 Boss 战时，伊索贝尔见面直接死亡（在 Isobel.AfterAddedToRoom 里实现）；
///   洛若坎精英战的特殊效果待该战斗设计落地后补充。
///
/// 唯一来源是拾取暗夜之歌：标注 [Pool(typeof(BaldursRelicPool))]（IsShared=false 的私有池，
/// 不进任何共享奖励池，仅为满足 BaseLib 的 PoolAttribute 强制校验），再加 IsAllowedInShops=>false 双保险。
///
/// 图标：mod 自带美术（baldurs_enemys/images/relics/dame_aylin.png），经 RelicImageOverridePatch 接入。
/// </summary>
[Pool(typeof(BaldursRelicPool))]
public sealed class DameAylin : CustomRelicModel
{
    public const int ArtifactsPerCombat = 3;

    public override RelicRarity Rarity => RelicRarity.Event;

    public override bool IsAllowedInShops => false;

    protected override string IconBaseName => "blessed_antler";

    /// <summary>每场战斗开始时给持有者 3 层人工制品（照抄 EmberTea/DataDisk 的 AfterRoomEntered 范式）。</summary>
    public override async Task AfterRoomEntered(AbstractRoom room)
    {
        if (room is CombatRoom)
        {
            Flash();
            await PowerCmd.Apply<ArtifactPower>(
                new ThrowingPlayerChoiceContext(), base.Owner.Creature, ArtifactsPerCombat, base.Owner.Creature, null);
        }
    }

    /// <summary>持有者回合开始时掷 1d4，获得对应格挡（照抄 Brimstone 的守卫写法）。</summary>
    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (side != CombatSide.Player || !participants.Contains(base.Owner.Creature))
        {
            return;
        }
        Flash();
        int block = Dice.Roll(combatState, 1, 4, 0);
        await CreatureCmd.GainBlock(base.Owner.Creature, block, ValueProp.Unpowered, null);
    }
}
