using System.Collections.Generic;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Audio;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Vfx;

namespace baldurs_enemys.src.Monsters;

/// <summary>
/// 尤格（A10 血量 156，低进阶 150）。第二层精英。
///
/// 逻辑 = 原版三层 Boss「实验体」(TestSubject) 的第三阶段招式循环：
///   while(true) { 撕碎(3段) → 大力猛扑 → 燃烧之吼 }
/// 燃烧之吼：向玩家弃牌堆塞灼烧，并给自己加力量。
///
/// 用户裁决（2026-10-02）：
/// - 伤害向下取整减半：撕碎 A10 11→5（低进阶 10→5，相同）、猛扑 45→22（不吃进阶）；
///   血量为实验体三阶段 A10 313→156（低进阶 300→150）。
/// - 灼烧张数与力量加成**不减半**，照抄原版进阶规则（灼烧 A10=5/低=3，力量 A10=3/低=2）。
/// - 进场自带 NemesisPower（复仇之力：每隔一个敌方回合结束获得 1 回合无敌），
///   与实验体进入第三阶段时一致。
///
/// 美术占位：原版实验体（res://scenes/creature_visuals/test_subject.tscn）——同一形象，
/// 其 Spine 动画触发器（MultiAttackTrigger / BiteTrigger / BurnTrigger）因此全部可用。
/// </summary>
public sealed class Yurg : CustomMonsterModel
{
    public override int MinInitialHp => AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 156, 150);

    public override int MaxInitialHp => MinInitialHp;

    public override DamageSfxType TakeDamageSfxType => DamageSfxType.Armor;

    /// <summary>美术：mod 自带立绘（baldurs_enemys/images/monsters/yurg.png）。</summary>
    public override NCreatureVisuals? CreateCustomVisuals() =>
        BaldurVisuals.Create("res://baldurs_enemys/images/monsters/yurg.png", 330);

    /// <summary>实验体撕碎 A10=11 → floor(11/2)=5（低进阶 10→5，相同）。</summary>
    private int LacerateDamage => 5;

    /// <summary>实验体大力猛扑 45（不吃进阶）→ floor(45/2)=22。</summary>
    private int BigPounceDamage => 22;

    /// <summary>灼烧张数：不减半，照抄原版进阶规则。</summary>
    private int BurningGrowlBurnCount => AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 5, 3);

    /// <summary>力量加成：不减半，照抄原版进阶规则。</summary>
    private int BurningGrowlStrengthGain => AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 3, 2);

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        // 实验体第三阶段进场即持有复仇之力（Single 层，数量恒为 1）。
        await PowerCmd.Apply<NemesisPower>(new ThrowingPlayerChoiceContext(), base.Creature, 1m, base.Creature, null);
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        MoveState lacerate = new("LACERATE", LacerateMove, new MultiAttackIntent(LacerateDamage, 3));
        MoveState bigPounce = new("BIG_POUNCE", BigPounceMove, new SingleAttackIntent(BigPounceDamage));
        MoveState growl = new("BURNING_GROWL", BurningGrowlMove, new StatusIntent(BurningGrowlBurnCount), new BuffIntent());

        lacerate.FollowUpState = bigPounce;
        bigPounce.FollowUpState = growl;
        growl.FollowUpState = lacerate;

        List<MonsterState> states = [lacerate, bigPounce, growl];
        return new MonsterMoveStateMachine(states, lacerate);
    }

    protected override bool ShouldShowMoveInBestiary(string moveStateId) => true;

    /// <summary>撕碎：3 段攻击（写法照抄 TestSubject.Phase3LacerateMove）。</summary>
    private async Task LacerateMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(LacerateDamage).WithHitCount(3).FromMonster(this)
            .OnlyPlayAnimOnce()
            .WithAttackerAnim("MultiAttackTrigger", 0.2f)
            .Execute(null);
    }

    /// <summary>大力猛扑：单段重击（实验体用 BiteTrigger 动画）。</summary>
    private async Task BigPounceMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(BigPounceDamage).FromMonster(this)
            .WithAttackerAnim("BiteTrigger", 0.25f)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(null);
    }

    /// <summary>燃烧之吼：脚下火焰 VFX + 弃牌堆塞灼烧 + 自身加力量（照抄 TestSubject.BurningGrowlMove）。</summary>
    private async Task BurningGrowlMove(IReadOnlyList<Creature> targets)
    {
        base.Creature.GetBackVfxContainer()?.AddChildSafely(NTestSubjectBurnVfx.Create());
        await CreatureCmd.TriggerAnim(base.Creature, "BurnTrigger", 1.25f);
        await CardPileCmd.AddToCombatAndPreview<Burn>(targets, PileType.Discard, BurningGrowlBurnCount, null);
        await PowerCmd.Apply<StrengthPower>(new ThrowingPlayerChoiceContext(), base.Creature, BurningGrowlStrengthGain, base.Creature, null);
    }
}
