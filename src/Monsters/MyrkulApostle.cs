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
using baldurs_enemys.src.Core;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Models.Powers;

namespace baldurs_enemys.src.Monsters;

/// <summary>
/// 「死亡之神的面容」米尔寇的使徒（A10 血量 450，低进阶 428）。第二层 Boss（单怪战）。
///
/// 原稿状态机（id20 =掷 i 个 d20，用户口径：i==3 时 id20 就是 3d20）：
///   int i=2;                          // ← 2026-10-03 设计稿「有改动」：初始值由 0 改为 2
///   while(true)
///   {
///       for(j=1;j<=3;j++)
///       {
///           攻击(5+j)*2;
///           攻击3*j;
///           回血id20;      // 掷 i 个 d20 的总血量
///       }
///       i += 2;
///       强化获得2力量;
///   }
///
/// 初始拥有 5 层人工制品和 2 力量。
/// i 的自增（i += 2）放在 for 循环最后一个回血招的执行体末尾（治疗在增前结算，
/// 与 C++ 语义一致：本循环内三次回血都用旧 i）。
/// </summary>
public sealed class MyrkulApostle : BaldurMonsterModel
{
    public override int MinInitialHp => AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 450, 428);

    public override int MaxInitialHp => MinInitialHp;

    public override DamageSfxType TakeDamageSfxType => DamageSfxType.Fur;

    /// <summary>美术：mod 自带立绘（baldurs_enemys/images/monsters/myrkul_apostle.png）。</summary>
    public override NCreatureVisuals? CreateCustomVisuals() =>
        BaldurVisuals.Create("res://baldurs_enemys/images/monsters/myrkul_apostle.png", 330);

    /// <summary>
    /// 回血骰数 i。C++ 语义：`int i=2`（2026-10-03 设计稿改动，原为 0）——
    /// 所以**战斗一开始每次回血就掷 2d20**，首个外层循环三次回血都是 2d20。
    /// </summary>
    private int _healDice = 2;

    private int HealDice
    {
        get => _healDice;
        set
        {
            AssertMutable();
            _healDice = value;
        }
    }

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        PlayerChoiceContext choiceContext = new ThrowingPlayerChoiceContext();
        // 初始拥有 5 层人工制品和 2 力量
        await PowerCmd.Apply<ArtifactPower>(choiceContext, base.Creature, 5m, base.Creature, null);
        await PowerCmd.Apply<StrengthPower>(choiceContext, base.Creature, 2m, base.Creature, null);
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        // ---- j=1：攻击(5+1)*2 → 攻击3*1 → 回血id20 ----
        MoveState slashA1 = new("SLASH_A1", SlashA1Move, new MultiAttackIntent(Balthazar.ScaleDeadly(6), 2));
        MoveState jabA1 = new("JAB_A1", JabA1Move, new SingleAttackIntent(Balthazar.ScaleDeadly(3)));
        MoveState healA1 = new("HEAL_A1", HealMove, new HealIntent());

        // ---- j=2：攻击(5+2)*2 → 攻击3*2 → 回血id20 ----
        MoveState slashA2 = new("SLASH_A2", SlashA2Move, new MultiAttackIntent(Balthazar.ScaleDeadly(7), 2));
        MoveState jabA2 = new("JAB_A2", JabA2Move, new MultiAttackIntent(Balthazar.ScaleDeadly(3), 2));
        MoveState healA2 = new("HEAL_A2", HealMove, new HealIntent());

        // ---- j=3：攻击(5+3)*2 → 攻击3*3 → 回血id20（之后 i += 2）----
        MoveState slashA3 = new("SLASH_A3", SlashA3Move, new MultiAttackIntent(Balthazar.ScaleDeadly(8), 2));
        MoveState jabA3 = new("JAB_A3", JabA3Move, new MultiAttackIntent(Balthazar.ScaleDeadly(3), 3));
        MoveState healA3 = new("HEAL_A3", HealLastMove, new HealIntent());

        MoveState empower = new("EMPOWER", EmpowerMove, new BuffIntent());

        slashA1.FollowUpState = jabA1;
        jabA1.FollowUpState = healA1;
        healA1.FollowUpState = slashA2;
        slashA2.FollowUpState = jabA2;
        jabA2.FollowUpState = healA2;
        healA2.FollowUpState = slashA3;
        slashA3.FollowUpState = jabA3;
        jabA3.FollowUpState = healA3;
        healA3.FollowUpState = empower;
        empower.FollowUpState = slashA1;

        List<MonsterState> states =
        [
            slashA1, jabA1, healA1, slashA2, jabA2, healA2, slashA3, jabA3, healA3, empower
        ];
        return new MonsterMoveStateMachine(states, slashA1);
    }

    protected override bool ShouldShowMoveInBestiary(string moveStateId) =>
        moveStateId is not ("HEAL_A1" or "HEAL_A2" or "HEAL_A3");

    /// <summary>回血 id20：掷 HealDice 个 d20，总血量治疗（i=0 时不掷不回）。</summary>
    private async Task HealCore()
    {
        ICombatState? combatState = base.Creature.CombatState;
        if (combatState == null || HealDice <= 0)
        {
            return;
        }
        await CreatureCmd.Heal(base.Creature, Dice.Roll(combatState, HealDice, 20, 0));
    }

    private Task HealMove(IReadOnlyList<Creature> targets) => HealCore();

    /// <summary>for 循环最后一次回血：治疗结算后 i += 2（纯逻辑，不占回合）。</summary>
    private async Task HealLastMove(IReadOnlyList<Creature> targets)
    {
        await HealCore();
        HealDice += 2;
    }

    private async Task SlashA1Move(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(Balthazar.ScaleDeadly(6)).WithHitCount(2).FromMonster(this)
            .OnlyPlayAnimOnce()
            .WithAttackerAnim("Attack", 0.3f)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(null);
    }

    private async Task JabA1Move(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(Balthazar.ScaleDeadly(3)).FromMonster(this)
            .WithAttackerAnim("Attack", 0.3f)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(null);
    }

    private async Task SlashA2Move(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(Balthazar.ScaleDeadly(7)).WithHitCount(2).FromMonster(this)
            .OnlyPlayAnimOnce()
            .WithAttackerAnim("Attack", 0.3f)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(null);
    }

    private async Task JabA2Move(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(Balthazar.ScaleDeadly(3)).WithHitCount(2).FromMonster(this)
            .OnlyPlayAnimOnce()
            .WithAttackerAnim("Attack", 0.25f)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(null);
    }

    private async Task SlashA3Move(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(Balthazar.ScaleDeadly(8)).WithHitCount(2).FromMonster(this)
            .OnlyPlayAnimOnce()
            .WithAttackerAnim("Attack", 0.3f)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(null);
    }

    private async Task JabA3Move(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(Balthazar.ScaleDeadly(3)).WithHitCount(3).FromMonster(this)
            .OnlyPlayAnimOnce()
            .WithAttackerAnim("Attack", 0.2f)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(null);
    }

    private async Task EmpowerMove(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.TriggerAnim(base.Creature, "Cast", 0.25f);
        await PowerCmd.Apply<StrengthPower>(new ThrowingPlayerChoiceContext(), base.Creature, 2m, base.Creature, null);
    }
}
