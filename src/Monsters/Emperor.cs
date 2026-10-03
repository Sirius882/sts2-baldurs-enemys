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
/// 君主（A10 血量 100，低进阶 95）。第三层 Boss，两侧各一梦境守护者。
/// 血量极低但有大杀器——本场是"在落雷前斩首"的竞速。
///
/// 原稿状态机：
///   强化：获得灵能护盾和3力量
///   while(true)
///   {
///       攻击20*1;
///       攻击9*2;
///       if (两个梦境守护者都死了) break;
///   }
///   while(true)
///   {
///       强化：获得卡尔萨斯强迫术;
///       自己眩晕;
///   }
///
/// 灵能护盾：每回合开始 2d8 格挡（PsionicShieldPower）；
/// 卡尔萨斯强迫术：1 回合后对所有玩家造成 999 无阻挡伤害
/// （KarsusCompulsionPower；反复施加持续刷新 → 每两回合落雷一次）。
/// </summary>
public sealed class Emperor : BaldurMonsterModel
{
    public override int MinInitialHp => AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 100, 95);

    public override int MaxInitialHp => MinInitialHp;

    public override DamageSfxType TakeDamageSfxType => DamageSfxType.Fur;

    /// <summary>美术：mod 自带立绘（baldurs_enemys/images/monsters/emperor.png）。</summary>
    public override NCreatureVisuals? CreateCustomVisuals() =>
        BaldurVisuals.Create("res://baldurs_enemys/images/monsters/emperor.png", 330);

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        MoveState psionicEmpower = new("PSIONIC_EMPOWER", PsionicEmpowerMove, new BuffIntent());
        MoveState tentacle = new("TENTACLE", TentacleMove, new SingleAttackIntent(Balthazar.ScaleDeadly(20)));
        ConditionalBranchState postTentacle = new("POST_TENTACLE");
        MoveState mindStorm = new("MIND_STORM", MindStormMove, new MultiAttackIntent(Balthazar.ScaleDeadly(9), 2));
        ConditionalBranchState postStorm = new("POST_STORM");
        MoveState karsus = new("KARSUS", KarsusMove, new DebuffIntent());
        MoveState recover = new("RECOVER", RecoverMove, new StunIntent());

        psionicEmpower.FollowUpState = tentacle;
        tentacle.FollowUpState = postTentacle;
        postTentacle.AddState(mindStorm, GuardiansAlive);
        postTentacle.AddState(karsus, () => true);
        mindStorm.FollowUpState = postStorm;
        postStorm.AddState(tentacle, GuardiansAlive);
        postStorm.AddState(karsus, () => true);
        karsus.FollowUpState = recover;
        recover.FollowUpState = karsus;

        List<MonsterState> states =
        [
            psionicEmpower, tentacle, postTentacle, mindStorm, postStorm, karsus, recover
        ];
        return new MonsterMoveStateMachine(states, psionicEmpower);
    }

    protected override bool ShouldShowMoveInBestiary(string moveStateId) =>
        moveStateId is "PSIONIC_EMPOWER" or "TENTACLE" or "MIND_STORM" or "KARSUS" or "RECOVER";

    /// <summary>「两个梦境守护者都死了」取反：任一存活即继续普通循环。</summary>
    private bool GuardiansAlive()
    {
        ICombatState? combatState = base.Creature.CombatState;
        return combatState != null && combatState.Enemies.Any(c => c.Monster is DreamGuardian && c.IsAlive);
    }

    /// <summary>获得灵能护盾（每回合 2d8 格挡）和 3 力量。</summary>
    private async Task PsionicEmpowerMove(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.TriggerAnim(base.Creature, "Cast", 0.25f);
        PlayerChoiceContext choiceContext = new ThrowingPlayerChoiceContext();
        await PowerCmd.Apply<PsionicShieldPower>(choiceContext, base.Creature, 1m, base.Creature, null);
        await PowerCmd.Apply<StrengthPower>(choiceContext, base.Creature, 3m, base.Creature, null);
    }

    private async Task TentacleMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(Balthazar.ScaleDeadly(20)).FromMonster(this)
            .WithAttackerAnim("Attack", 0.3f)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(null);
    }

    private async Task MindStormMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(Balthazar.ScaleDeadly(9)).WithHitCount(2).FromMonster(this)
            .OnlyPlayAnimOnce()
            .WithAttackerAnim("Attack", 0.25f)
            .WithHitFx("vfx/vfx_attack_lightning")
            .Execute(null);
    }

    /// <summary>（重新）施加卡尔萨斯强迫术：Amount 刷回 2，倒计时重新开始。</summary>
    private async Task KarsusMove(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.TriggerAnim(base.Creature, "Cast", 0.25f);
        await PowerCmd.Apply<KarsusCompulsionPower>(
            new ThrowingPlayerChoiceContext(), base.Creature, KarsusCompulsionPower.ApplyAmount, base.Creature, null);
    }

    private Task RecoverMove(IReadOnlyList<Creature> targets) => Task.CompletedTask;
}
