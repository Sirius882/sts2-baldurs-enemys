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
/// 「博德之门大公爵候选人」恩维尔·戈塔什大人（A10 血量 320，低进阶 304）。
/// 第三层 Boss，两侧各一钢铁卫士。
///
/// 原稿状态机：
///   while(任一钢铁卫士存活)
///   {
///       指挥：所有友方单位获得1力量;
///       攻击11*1;
///       攻击5*2;
///   }
///   强化：获得3力量;
///   while(true) { 攻击15*1; 攻击8*2; }
///
/// 进场自带「班恩之诫」：每回合 1d20 格挡，骰出 20 再获得 1 层无实体。
/// </summary>
public sealed class Gortash : BaldurMonsterModel
{
    public override int MinInitialHp => AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 320, 304);

    public override int MaxInitialHp => MinInitialHp;

    public override DamageSfxType TakeDamageSfxType => DamageSfxType.Armor;

    /// <summary>美术：mod 自带立绘（baldurs_enemys/images/monsters/gortash.png）。</summary>
    public override NCreatureVisuals? CreateCustomVisuals() =>
        BaldurVisuals.Create("res://baldurs_enemys/images/monsters/gortash.png", 330);

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        await PowerCmd.Apply<BanesCommandPower>(new ThrowingPlayerChoiceContext(), base.Creature, 1m, base.Creature, null);
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        ConditionalBranchState entry = new("ENTRY"); // 首次条件判断（卫士可能开场即死）
        MoveState command = new("COMMAND", CommandMove, new BuffIntent());
        MoveState strike = new("STRIKE", StrikeMove, new SingleAttackIntent(Balthazar.ScaleDeadly(11)));
        MoveState doubleSwing = new("DOUBLE_SWING", DoubleSwingMove, new MultiAttackIntent(Balthazar.ScaleDeadly(5), 2));
        ConditionalBranchState postSwing = new("POST_SWING");
        MoveState empower = new("EMPOWER", EmpowerMove, new BuffIntent());
        MoveState slam = new("SLAM", SlamMove, new SingleAttackIntent(Balthazar.ScaleDeadly(15)));
        MoveState crush = new("CRUSH", CrushMove, new MultiAttackIntent(Balthazar.ScaleDeadly(8), 2));

        entry.AddState(command, SteelWatcherAlive);
        entry.AddState(empower, () => true);

        command.FollowUpState = strike;
        strike.FollowUpState = doubleSwing;
        doubleSwing.FollowUpState = postSwing;
        postSwing.AddState(command, SteelWatcherAlive);
        postSwing.AddState(empower, () => true);

        empower.FollowUpState = slam;
        slam.FollowUpState = crush;
        crush.FollowUpState = slam;

        List<MonsterState> states =
        [
            entry, command, strike, doubleSwing, postSwing, empower, slam, crush
        ];
        return new MonsterMoveStateMachine(states, entry);
    }

    protected override bool ShouldShowMoveInBestiary(string moveStateId) =>
        moveStateId is "COMMAND" or "STRIKE" or "DOUBLE_SWING" or "EMPOWER" or "SLAM" or "CRUSH";

    /// <summary>「任一钢铁卫士存活」。</summary>
    private bool SteelWatcherAlive()
    {
        ICombatState? combatState = base.Creature.CombatState;
        return combatState != null && combatState.Enemies.Any(c => c.Monster is SteelWatcher && c.IsAlive);
    }

    /// <summary>指挥：所有友方单位（存活敌人）获得 1 力量。</summary>
    private async Task CommandMove(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.TriggerAnim(base.Creature, "Cast", 0.25f);
        ICombatState? combatState = base.Creature.CombatState;
        if (combatState == null)
        {
            return;
        }
        PlayerChoiceContext choiceContext = new ThrowingPlayerChoiceContext();
        foreach (Creature ally in combatState.Enemies)
        {
            if (!ally.IsAlive) continue;
            await PowerCmd.Apply<StrengthPower>(choiceContext, ally, 1m, base.Creature, null);
        }
    }

    private async Task StrikeMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(Balthazar.ScaleDeadly(11)).FromMonster(this)
            .WithAttackerAnim("Attack", 0.3f)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(null);
    }

    private async Task DoubleSwingMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(Balthazar.ScaleDeadly(5)).WithHitCount(2).FromMonster(this)
            .OnlyPlayAnimOnce()
            .WithAttackerAnim("Attack", 0.25f)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(null);
    }

    private async Task EmpowerMove(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.TriggerAnim(base.Creature, "Cast", 0.25f);
        await PowerCmd.Apply<StrengthPower>(new ThrowingPlayerChoiceContext(), base.Creature, 3m, base.Creature, null);
    }

    private async Task SlamMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(Balthazar.ScaleDeadly(15)).FromMonster(this)
            .WithAttackerAnim("Attack", 0.3f)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(null);
    }

    private async Task CrushMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(Balthazar.ScaleDeadly(8)).WithHitCount(2).FromMonster(this)
            .OnlyPlayAnimOnce()
            .WithAttackerAnim("Attack", 0.25f)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(null);
    }
}
