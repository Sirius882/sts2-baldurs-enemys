using System.Collections.Generic;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Audio;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.ValueProps;
using baldurs_enemys.src.Powers;

namespace baldurs_enemys.src.Monsters;

/// <summary>
/// 小枭熊（A10 血量 40，低进阶 38 = floor(40/1.05)）。第一层精英战「枭熊」的幼崽。
///
/// 原稿状态机（设计稿 2026-10-04 版）：
/// ```
/// while(true) 强化获得5格挡;
/// ```
/// 即每回合只做一件事：给自己 5 点格挡（自环节点）。
/// 格挡是防御强化量，不吃进阶缩放（原版口径）。
///
/// 附带 power：可被交互（<see cref="InteractablePower"/>）——
/// 被打出「交互」后**立即死亡**，然后掷 1d20，> 12 则玩家获得遗物「佩尔的士兵」
/// （原版 Ancient 宠物遗物 PaelsLegion），分流逻辑在 <see cref="baldurs_enemys.src.Cards.Interact"/>。
/// </summary>
public sealed class OwlbearCub : BaldurMonsterModel
{
    /// <summary>「交互」奖励阈值：1d20 > 该值则获得「佩尔的士兵」（设计稿 &gt;12）。</summary>
    public const int InteractRewardThreshold = 12;

    public override int MinInitialHp => AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 40, 38);

    public override int MaxInitialHp => MinInitialHp;

    public override DamageSfxType TakeDamageSfxType => DamageSfxType.Armor;

    /// <summary>美术：mod 自带立绘（baldurs_enemys/images/monsters/owl_bear_cub.png）。</summary>
    public override NCreatureVisuals? CreateCustomVisuals() =>
        BaldurVisuals.Create("res://baldurs_enemys/images/monsters/owl_bear_cub.png", 190);

    /// <summary>可被交互：必须在进房时挂（照抄油桶），这样动态生成的枭熊幼崽也能正确获得该标记。</summary>
    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        await PowerCmd.Apply<InteractablePower>(new ThrowingPlayerChoiceContext(), base.Creature, 1m, base.Creature, null);
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        MoveState guard = new("GUARD", GuardMove, new BuffIntent());
        guard.FollowUpState = guard;
        List<MonsterState> states = [guard];
        return new MonsterMoveStateMachine(states, guard);
    }

    protected override bool ShouldShowMoveInBestiary(string moveStateId) => true;

    /// <summary>强化：获得 5 格挡。</summary>
    private async Task GuardMove(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.TriggerAnim(base.Creature, "Cast", 0.2f);
        await CreatureCmd.GainBlock(base.Creature, 5, ValueProp.Unpowered, null);
    }
}
