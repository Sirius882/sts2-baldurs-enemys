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
/// 人质（A10 血量 12，低进阶 11）。奥林 Boss 战的可拾取物。
///
/// 被拾取后变成原版遗物「布质果实」+「李家华夫饼」（在拾取卡里实现）；
/// 人质死亡或被拾取时，奥林获得 5 力量并进入狂怒（见 BhaalsBlessingPower.AfterDeath）。
/// </summary>
public sealed class Hostage : CustomMonsterModel
{
    public override int MinInitialHp => AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 12, 11);

    public override int MaxInitialHp => MinInitialHp;

    public override DamageSfxType TakeDamageSfxType => DamageSfxType.Fur;

    /// <summary>留尸：死亡后本体不淡出（可拾取物口径，同油桶）。</summary>
    public override bool ShouldFadeAfterDeath => false;

    /// <summary>美术：mod 自带立绘（baldurs_enemys/images/monsters/hostage.png）。</summary>
    public override NCreatureVisuals? CreateCustomVisuals() =>
        BaldurVisuals.Create("res://baldurs_enemys/images/monsters/hostage.png", 240);

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        await PowerCmd.Apply<PickpocketablePower>(new ThrowingPlayerChoiceContext(), base.Creature, 1m, base.Creature, null);
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
