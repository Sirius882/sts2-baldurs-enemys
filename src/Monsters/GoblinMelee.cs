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
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace baldurs_enemys.src.Monsters;

/// <summary>
/// 地精·近战（A10 血量 60，低进阶 57 = floor(60/1.05)）。
///
/// 原稿状态机（2026-10-02 修订版）：
///   攻击3 → 攻击5 → 强化+1力量 → 攻击3 → 攻击5 → 强化+1力量 → while{ 攻击3 → 攻击5 }
/// `for` 已在编译期展开，所以是 8 个 MoveState 的线性链，最后两节点互相成环。
///
/// 进阶换算（DeadlyEnemies 比例 /1.15 四舍五入）：攻击3 → 3/1.15=2.6 → 3（不变）；
/// 攻击5 → 5/1.15=4.3 → 4。
/// </summary>
public sealed class GoblinMelee : CustomMonsterModel
{
    public override int MinInitialHp => AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 60, 57);

    public override int MaxInitialHp => MinInitialHp;

    public override DamageSfxType TakeDamageSfxType => DamageSfxType.Armor;

    /// <summary>美术：mod 自带立绘（baldurs_enemys/images/monsters/goblin_melee.png）。</summary>
    public override NCreatureVisuals? CreateCustomVisuals() =>
        BaldurVisuals.Create("res://baldurs_enemys/images/monsters/goblin_melee.png", 260);

    private int HackDamage => AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 3, 3);

    private int JabDamage => AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 5, 4);

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        MoveState hack = new("HACK", HackMove, new SingleAttackIntent(HackDamage));
        MoveState jab = new("JAB", JabMove, new SingleAttackIntent(JabDamage));
        MoveState rally = new("RALLY", RallyMove, new BuffIntent());
        MoveState hack2 = new("HACK2", HackMove, new SingleAttackIntent(HackDamage));
        MoveState jab2 = new("JAB2", JabMove, new SingleAttackIntent(JabDamage));
        MoveState rally2 = new("RALLY2", RallyMove, new BuffIntent());
        MoveState hackLoop = new("HACK_LOOP", HackMove, new SingleAttackIntent(HackDamage));
        MoveState jabLoop = new("JAB_LOOP", JabMove, new SingleAttackIntent(JabDamage));

        hack.FollowUpState = jab;
        jab.FollowUpState = rally;
        rally.FollowUpState = hack2;
        hack2.FollowUpState = jab2;
        jab2.FollowUpState = rally2;
        rally2.FollowUpState = hackLoop;
        hackLoop.FollowUpState = jabLoop;
        jabLoop.FollowUpState = hackLoop;

        List<MonsterState> states = [hack, jab, rally, hack2, jab2, rally2, hackLoop, jabLoop];
        return new MonsterMoveStateMachine(states, hack);
    }

    /// <summary>
    /// 图鉴只显示 5 个不重复招式；带 "2" 的重复节点不进图鉴（战斗日志仍会用 loc 里的同名标题）。
    /// </summary>
    protected override bool ShouldShowMoveInBestiary(string moveStateId) =>
        moveStateId is "HACK" or "JAB" or "RALLY" or "HACK_LOOP" or "JAB_LOOP";

    private async Task HackMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(HackDamage).FromMonster(this)
            .WithAttackerAnim("Attack", 0.15f)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(null);
    }

    private async Task JabMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(JabDamage).FromMonster(this)
            .WithAttackerAnim("Attack", 0.15f)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(null);
    }

    private async Task RallyMove(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.TriggerAnim(base.Creature, "Cast", 0.2f);
        await PowerCmd.Apply<StrengthPower>(new ThrowingPlayerChoiceContext(), base.Creature, 1m, base.Creature, null);
    }
}
