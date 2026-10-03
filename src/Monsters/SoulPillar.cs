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
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Nodes.Combat;
using baldurs_enemys.src.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models.Powers;

namespace baldurs_enemys.src.Monsters;

/// <summary>
/// 灵魂柱（A10 血量 80，低进阶 76）。拉斐尔 Boss 战的供能装置，自己眩晕杵着。
///
/// 「灵魂」：每回合开始时使拉斐尔回血 2d4（每根柱子分别掷骰）——
/// 四根柱子全场最多每回合 8d4 的治疗，先拆柱子还是先打本体是本场核心取舍。
/// </summary>
public sealed class SoulPillar : BaldurMonsterModel
{
    public override int MinInitialHp => AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 80, 76);

    public override int MaxInitialHp => MinInitialHp;

    public override DamageSfxType TakeDamageSfxType => DamageSfxType.Magic;

    /// <summary>美术：mod 自带立绘（baldurs_enemys/images/monsters/soul_pillar.png）。</summary>
    public override NCreatureVisuals? CreateCustomVisuals() =>
        BaldurVisuals.Create("res://baldurs_enemys/images/monsters/soul_pillar.png", 300);

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        await PowerCmd.Apply<SoulSacrificePower>(new ThrowingPlayerChoiceContext(), base.Creature, 1m, base.Creature, null);
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
