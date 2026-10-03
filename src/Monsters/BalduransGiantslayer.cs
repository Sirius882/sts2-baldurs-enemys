using System;
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
using baldurs_enemys.src.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Models.Powers;

namespace baldurs_enemys.src.Monsters;

/// <summary>
/// 博德安的巨人杀手（A10 血量 200，低进阶 190）。安苏精英战的可交互物：
/// 插在龙骸上的巨剑，自己眩晕杵着，可被「交互」。
///
/// 被交互后变成遗物「博德安的巨人杀手」（见 Relics/BalduransGiantslayer）；
/// 它死亡（无论死因）时，安苏的「被背叛」立即触发（+5 力量 + 自伤半血）——
/// 拿剑有真实代价。
/// </summary>
public sealed class BalduransGiantslayer : BaldurMonsterModel
{
    public override int MinInitialHp => AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 200, 190);

    public override int MaxInitialHp => MinInitialHp;

    public override DamageSfxType TakeDamageSfxType => DamageSfxType.Armor;

    /// <summary>留尸：死亡后本体不淡出（照抄油桶/GasBomb 的可交互物口径）。</summary>
    public override bool ShouldFadeAfterDeath => false;

    /// <summary>
    /// 美术：与遗物图标共用同一张图（baldurs_enemys/images/relics/baldurans_giantslayer.png）。
    /// 怪物本体是插在龙骸上的巨剑，遗物图标也是这把剑，一图两用。
    /// </summary>
    public override NCreatureVisuals? CreateCustomVisuals() =>
        BaldurVisuals.Create("res://baldurs_enemys/images/relics/baldurans_giantslayer.png", 260);

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        await PowerCmd.Apply<InteractablePower>(new ThrowingPlayerChoiceContext(), base.Creature, 1m, base.Creature, null);
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        MoveState idle = new("IDLE", IdleMove, new StunIntent());
        idle.FollowUpState = idle;
        List<MonsterState> states = [idle];
        return new MonsterMoveStateMachine(states, idle);
    }

    protected override bool ShouldShowMoveInBestiary(string moveStateId) => true;

    private Task IdleMove(IReadOnlyList<Creature> targets) => Task.CompletedTask;
}
