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
/// 骷髅（A10 血量 60，低进阶 57 = floor(60/1.05)）。巴萨扎尔精英战的杂兵。
///
/// 设计稿原文只有一句：「同地精·近战」（2026-10-03 标注「有改动」，此前版本是
/// 「与原版游戏的盛碗虫（石）完全一样，就是换皮，全套照抄」——那条已作废）。
/// 因此这里的**数据与状态机与 <see cref="GoblinMelee"/> 完全一致**：
///   攻击5 → 攻击7 → 强化+1力量 → 攻击5 → 攻击7 → 强化+1力量 → while{ 攻击5 → 攻击7 }
///
/// 差异只在美术：骷髅用mod 自带立绘 skeleton.png（GoblinMelee 用 goblin_melee.png），
/// 音效沿用原版默认（与地精·近战一样，不再有 workbug_rock 的头槌/眩晕占位音）。
///
/// 【为什么不再有「失衡」】
/// 失衡（<see cref="SkeletonImbalancedPower"/> + DIZZY 眩晕招）是上一版「照抄盛碗虫（石）」
/// 的配套机制。既然设计稿改成「同地精·近战」，这条机制已无设计稿依据，
/// 故连同 SkeletonImbalancedPower 一并移除（详见 docs/更新日志.txt）。
/// </summary>
public sealed class Skeleton : BaldurMonsterModel
{
    public override int MinInitialHp => AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 60, 57);

    public override int MaxInitialHp => MinInitialHp;

    public override DamageSfxType TakeDamageSfxType => DamageSfxType.Armor;

    /// <summary>美术：mod 自带立绘（baldurs_enemys/images/monsters/skeleton.png）。</summary>
    public override NCreatureVisuals? CreateCustomVisuals() =>
        BaldurVisuals.Create("res://baldurs_enemys/images/monsters/skeleton.png", 280);

    /// <summary>攻击5：低进阶 5/1.15=4.3 → 4。</summary>
    private int HackDamage => AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 5, 4);

    /// <summary>攻击7：低进阶 7/1.15=6.1 → 6。</summary>
    private int JabDamage => AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 7, 6);

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

    /// <summary>图鉴只显示 5 个不重复招式；带 "2" 的重复节点不进图鉴（战斗日志仍会用 loc 里的同名标题）。</summary>
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
