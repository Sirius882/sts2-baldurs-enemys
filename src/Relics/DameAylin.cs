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
using baldurs_enemys.src.Monsters;

namespace baldurs_enemys.src.Relics;

/// <summary>
/// 「艾琳女士」（Dame Aylin）：交互暗夜之歌的唯一产物。
///
/// 效果（设计稿）：
/// - 每场战斗开始时，获得 3 层人工制品（ArtifactPower）；
/// - 持有该遗物时，每回合开始时掷 1d4，获得对应格挡（总则第5条：种子 RNG，多人/SL 一致）；
/// - 持有者面对凯瑟里克·索姆 Boss 战时，伊索贝尔见面直接死亡（在本类 BeforeCombatStart 里实现，
///   原因见该方法注释——绝不能放 Isobel.AfterAddedToRoom）；
///   洛若坎精英战的特殊效果待该战斗设计落地后补充。
///
/// 唯一来源是交互暗夜之歌：标注 [Pool(typeof(BaldursRelicPool))]，该池是 mod 专属池
/// （不参与任何随机掉落，理由见 BaldursRelicPool 类注释），再加 IsAllowedInShops=>false 双保险。
/// 注意：BaldursRelicPool 必须是 IsShared=>true，否则图鉴里本遗物会永久显示为「已锁定」，
/// 悬停只会弹出通用「已锁定」提示而非本遗物的说明。
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

    /// <summary>
    /// 面对凯瑟里克·索姆 Boss 战时，伊索贝尔见面直接死亡（设计稿）。
    ///
    /// 为什么必须挂在 BeforeCombatStart 而不能挂在 Isobel.AfterAddedToRoom：
    /// 引擎的 CombatManager.AfterCreatureAdded（CombatManager.cs:1128-1135）在
    /// `await creature.AfterAddedToRoom()` 之后**无条件**执行 `creature.Monster.RollMove(...)`，
    /// 且不检查该怪物是否已死。若在 AfterAddedToRoom 里 Kill 自己，RollMove 会对已死怪物取状态机 →
    /// NullReferenceException，战斗直接崩掉（实测报错栈正是
    /// MonsterModel.RollMove_Patch1 → AfterCreatureAdded → StartCombatInternal）。
    ///
    /// BeforeCombatStart 在 StartCombatInternal 中的调用点（:594）位于「全部怪物入场完毕」循环（:586-590）
    /// 之后，此时所有 RollMove 都已执行完毕，此时处决伊索贝尔是安全的。
    /// </summary>
    public override async Task BeforeCombatStart()
    {
        ICombatState? combatState = base.Owner.Creature.CombatState;
        if (combatState == null)
        {
            return;
        }

        // 找本场战斗里的伊索贝尔（持有者自己也是玩家侧，不会命中）
        List<Isobel> isobels = combatState.Enemies
            .Where(c => c.IsAlive && c.Monster is Isobel)
            .Select(c => c.Monster as Isobel)
            .Where(m => m != null)
            .ToList()!;

        if (isobels.Count == 0)
        {
            return;
        }

        foreach (Isobel isobel in isobels)
        {
            Creature creature = isobel.Creature;
            if (creature != null && creature.IsAlive)
            {
                // 无条件处决（force:true，跳过"是否会被击杀"的判定）
                await CreatureCmd.Kill(creature, force: true);
            }
        }
    }
}
