using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Audio;
using MegaCrit.Sts2.Core.Combat;
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
using baldurs_enemys.src.Relics;

namespace baldurs_enemys.src.Monsters;

/// <summary>
/// 伊索贝尔（A10 血量 80，低进阶 76 = floor(80/1.05)）。凯瑟里克·索姆 Boss 战的前排。
///
/// 原稿状态机（2026-10-02 版）：
///   for i in range(5) { 攻击6*1; 强化：全体友方获得1力量; }
///   while(true) 攻击6*1;
/// for 已展开 → 5×(攻击/强化) 线性链，然后攻击自环。
///
/// 特殊：如果玩家持有遗物「艾琳女士」，伊索贝尔**见面直接死亡**
/// （在 AfterAddedToRoom 里检查并击杀；凯瑟里克仍存活，战斗继续）。
///
/// 攻击 6 低进阶 = round(6/1.15) = 5；治疗/强化量不吃进阶缩放（原版口径）。
/// </summary>
public sealed class Isobel : CustomMonsterModel
{
    public override int MinInitialHp => AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 80, 76);

    public override int MaxInitialHp => MinInitialHp;

    public override DamageSfxType TakeDamageSfxType => DamageSfxType.Magic;

    /// <summary>美术：mod 自带立绘（baldurs_enemys/images/monsters/isobel.png）。</summary>
    public override NCreatureVisuals? CreateCustomVisuals() =>
        BaldurVisuals.Create("res://baldurs_enemys/images/monsters/isobel.png", 280);

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();

        // 「艾琳女士」持有者面前，伊索贝尔见面直接死亡（无条件 → force:true）
        ICombatState? combatState = base.Creature.CombatState;
        if (combatState != null && combatState.Players.Any(p => p.Relics.Any(r => r is DameAylin)))
        {
            await CreatureCmd.Kill(base.Creature, force: true);
        }
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        MoveState smite = new("SMITE", SmiteMove, new SingleAttackIntent(SmiteDamage));
        MoveState bless = new("BLESS", BlessMove, new BuffIntent());
        MoveState smite2 = new("SMITE2", SmiteMove, new SingleAttackIntent(SmiteDamage));
        MoveState bless2 = new("BLESS2", BlessMove, new BuffIntent());
        MoveState smite3 = new("SMITE3", SmiteMove, new SingleAttackIntent(SmiteDamage));
        MoveState bless3 = new("BLESS3", BlessMove, new BuffIntent());
        MoveState smite4 = new("SMITE4", SmiteMove, new SingleAttackIntent(SmiteDamage));
        MoveState bless4 = new("BLESS4", BlessMove, new BuffIntent());
        MoveState smite5 = new("SMITE5", SmiteMove, new SingleAttackIntent(SmiteDamage));
        MoveState bless5 = new("BLESS5", BlessMove, new BuffIntent());
        MoveState smiteLoop = new("SMITE_LOOP", SmiteMove, new SingleAttackIntent(SmiteDamage));

        smite.FollowUpState = bless;
        bless.FollowUpState = smite2;
        smite2.FollowUpState = bless2;
        bless2.FollowUpState = smite3;
        smite3.FollowUpState = bless3;
        bless3.FollowUpState = smite4;
        smite4.FollowUpState = bless4;
        bless4.FollowUpState = smite5;
        smite5.FollowUpState = bless5;
        bless5.FollowUpState = smiteLoop;
        smiteLoop.FollowUpState = smiteLoop;

        List<MonsterState> states =
        [
            smite, bless, smite2, bless2, smite3, bless3,
            smite4, bless4, smite5, bless5, smiteLoop
        ];
        return new MonsterMoveStateMachine(states, smite);
    }

    /// <summary>图鉴只显示不重复招式；带数字后缀的展开节点不进图鉴。</summary>
    protected override bool ShouldShowMoveInBestiary(string moveStateId) =>
        moveStateId is "SMITE" or "BLESS" or "SMITE_LOOP";

    /// <summary>攻击6：低进阶 round(6/1.15) = 5。</summary>
    private int SmiteDamage => Balthazar.ScaleDeadly(6);

    private async Task SmiteMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(SmiteDamage).FromMonster(this)
            .WithAttackerAnim("Attack", 0.2f)
            .WithHitFx("vfx/vfx_attack_magic")
            .Execute(null);
    }

    /// <summary>
    /// 全体友方单位（含凯瑟里克）获得 1 力量。
    /// 用 Enemies（完整名单）而非 HittableEnemies，由引擎的 CanReceivePowers 过滤已死者。
    /// </summary>
    private async Task BlessMove(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.TriggerAnim(base.Creature, "Cast", 0.2f);
        await PowerCmd.Apply<StrengthPower>(
            new ThrowingPlayerChoiceContext(), base.Creature.CombatState!.Enemies, 1m, base.Creature, null);
    }
}
