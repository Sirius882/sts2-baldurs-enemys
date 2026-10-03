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
/// 地精·远程（A10 血量 45，低进阶 42 = floor(45/1.05)）。
///
/// 原稿状态机（2026-10-03 修订版，设计稿标注「有改动」）：while{ 攻击5 → 攻击5 → 自己眩晕 → 强化+1力量 }
/// 「自己眩晕」是"这回合不行动"，不是被外部眩晕 —— 所以实现为一个 onPerform 为空、
/// 意图为 StunIntent 的 MoveState（照抄原版 Wriggler 的 SPAWNED_MOVE 写法），
/// 而**不是** CreatureCmd.Stun（那是给"被外部眩晕"用的，缺省语义是重复上一次已执行的行动，与本设计不等价）。
///
/// 进阶换算（DeadlyEnemies 比例 /1.15 四舍五入）：攻击5 → 5/1.15=4.3 → 4。
/// 四招循环 → 它每 4 回合只打 10 点，配 45 血，仍是偏辅助的输出源。
/// </summary>
public sealed class GoblinRanged : BaldurMonsterModel
{
    public override int MinInitialHp => AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 45, 42);

    public override int MaxInitialHp => MinInitialHp;

    public override DamageSfxType TakeDamageSfxType => DamageSfxType.Armor;

    /// <summary>美术：mod 自带立绘（baldurs_enemys/images/monsters/goblin_ranged.png）。</summary>
    public override NCreatureVisuals? CreateCustomVisuals() =>
        BaldurVisuals.Create("res://baldurs_enemys/images/monsters/goblin_ranged.png", 260);

    private int ShootDamage => AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 5, 4);

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        MoveState shoot = new("SHOOT", ShootMove, new SingleAttackIntent(ShootDamage));
        MoveState shoot2 = new("SHOOT2", ShootMove, new SingleAttackIntent(ShootDamage));
        MoveState reload = new("RELOAD", ReloadMove, new StunIntent());
        MoveState rally = new("RALLY", RallyMove, new BuffIntent());

        shoot.FollowUpState = shoot2;
        shoot2.FollowUpState = reload;
        reload.FollowUpState = rally;
        rally.FollowUpState = shoot;

        List<MonsterState> states = [shoot, shoot2, reload, rally];
        return new MonsterMoveStateMachine(states, shoot);
    }

    /// <summary>图鉴不显示带 "2" 的重复节点（战斗日志仍会用 loc 里的同名标题）。</summary>
    protected override bool ShouldShowMoveInBestiary(string moveStateId) =>
        moveStateId is "SHOOT" or "RELOAD" or "RALLY";

    private async Task ShootMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(ShootDamage).FromMonster(this)
            .WithAttackerAnim("Attack", 0.15f)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(null);
    }

    /// <summary>装填：这回合不行动。没有任何实际效果，只显示"眩晕"意图。</summary>
    private Task ReloadMove(IReadOnlyList<Creature> targets) => Task.CompletedTask;

    private async Task RallyMove(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.TriggerAnim(base.Creature, "Cast", 0.2f);
        await PowerCmd.Apply<StrengthPower>(new ThrowingPlayerChoiceContext(), base.Creature, 1m, base.Creature, null);
    }
}
