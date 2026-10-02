using System;
using System.Collections.Generic;
using System.Linq;
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
/// 「博德之心」安苏（A10 血量 300，低进阶 285）。第三层精英，身旁是博德安的巨人杀手。
///
/// 原稿状态机：
///   while(true)
///   {
///       强化获得1力量;
///       攻击13*1;
///       if (巨人杀手存活) 攻击6*2;
///       else 攻击2*6;
///   }
///
/// 开场自带「被背叛」：每回合 2d4 格挡；巨人杀手死亡时 +5 力量并受到半血自伤。
/// 拾取巨剑 = 拿遗物，但安苏转为 6 连轻击并狂怒——经典取舍。
/// </summary>
public sealed class Ansur : CustomMonsterModel
{
    public override int MinInitialHp => AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 300, 285);

    public override int MaxInitialHp => MinInitialHp;

    public override DamageSfxType TakeDamageSfxType => DamageSfxType.Armor;

    /// <summary>美术：mod 自带立绘（baldurs_enemys/images/monsters/ansur.png）。</summary>
    public override NCreatureVisuals? CreateCustomVisuals() =>
        BaldurVisuals.Create("res://baldurs_enemys/images/monsters/ansur.png", 300);

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        await PowerCmd.Apply<BetrayedPower>(new ThrowingPlayerChoiceContext(), base.Creature, 1m, base.Creature, null);
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        MoveState empower = new("EMPOWER", EmpowerMove, new BuffIntent());
        MoveState tail = new("TAIL", TailMove, new SingleAttackIntent(Balthazar.ScaleDeadly(13)));
        ConditionalBranchState branch = new("BRANCH");
        MoveState claws = new("CLAWS", ClawsMove, new MultiAttackIntent(Balthazar.ScaleDeadly(6), 2));
        MoveState enragedClaws = new("ENRAGED_CLAWS", EnragedClawsMove, new MultiAttackIntent(Balthazar.ScaleDeadly(2), 6));

        empower.FollowUpState = tail;
        tail.FollowUpState = branch;
        branch.AddState(claws, GiantslayerAlive);
        branch.AddState(enragedClaws, () => true);
        claws.FollowUpState = empower;
        enragedClaws.FollowUpState = empower;

        List<MonsterState> states = [empower, tail, branch, claws, enragedClaws];
        return new MonsterMoveStateMachine(states, empower);
    }

    protected override bool ShouldShowMoveInBestiary(string moveStateId) =>
        moveStateId is "EMPOWER" or "TAIL" or "CLAWS" or "ENRAGED_CLAWS";

    /// <summary>「巨人杀手存活」。</summary>
    private bool GiantslayerAlive()
    {
        ICombatState? combatState = base.Creature.CombatState;
        return combatState != null && combatState.Enemies.Any(c => c.Monster is BalduransGiantslayer && c.IsAlive);
    }

    private async Task EmpowerMove(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.TriggerAnim(base.Creature, "Cast", 0.25f);
        await PowerCmd.Apply<StrengthPower>(new ThrowingPlayerChoiceContext(), base.Creature, 1m, base.Creature, null);
    }

    private async Task TailMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(Balthazar.ScaleDeadly(13)).FromMonster(this)
            .WithAttackerAnim("Attack", 0.3f)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(null);
    }

    private async Task ClawsMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(Balthazar.ScaleDeadly(6)).WithHitCount(2).FromMonster(this)
            .OnlyPlayAnimOnce()
            .WithAttackerAnim("Attack", 0.2f)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(null);
    }

    private async Task EnragedClawsMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(Balthazar.ScaleDeadly(2)).WithHitCount(6).FromMonster(this)
            .OnlyPlayAnimOnce()
            .WithAttackerAnim("Attack", 0.15f)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(null);
    }
}
