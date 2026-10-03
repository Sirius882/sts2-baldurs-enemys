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
using MegaCrit.Sts2.Core.ValueProps;

namespace baldurs_enemys.src.Monsters;

/// <summary>
/// 钢铁卫士（A10 血量 70，低进阶 66）。戈塔什 Boss 战的护卫 construct。
///
/// 原稿状态机：while(true) { 攻击6*1; 攻击5*2; 攻击3*3; }
///
/// 「过载熔毁」（OverloadMeltdownPower）负责两件事：
/// - 受伤后血量 ≤20 → 立即把意图变为自爆 16（SetMoveImmediate，StunInternal 同款）；
///   自爆意图被打断（被动眩晕）则顺延一回合；
/// - 死亡时对玩家方 2d8 / 对怪物方 2d20（死于自爆不触发）。
/// </summary>
public sealed class SteelWatcher : BaldurMonsterModel
{
    public override int MinInitialHp => AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 70, 66);

    public override int MaxInitialHp => MinInitialHp;

    public override DamageSfxType TakeDamageSfxType => DamageSfxType.Armor;

    /// <summary>美术：mod 自带立绘（baldurs_enemys/images/monsters/steel_watcher.png）。</summary>
    public override NCreatureVisuals? CreateCustomVisuals() =>
        BaldurVisuals.Create("res://baldurs_enemys/images/monsters/steel_watcher.png", 300);

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        await PowerCmd.Apply<OverloadMeltdownPower>(new ThrowingPlayerChoiceContext(), base.Creature, 1m, base.Creature, null);
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        MoveState punch = new("PUNCH", PunchMove, new SingleAttackIntent(Balthazar.ScaleDeadly(6)));
        MoveState doubleSwing = new("DOUBLE_SWING", DoubleSwingMove, new MultiAttackIntent(Balthazar.ScaleDeadly(5), 2));
        MoveState barrage = new("BARRAGE", BarrageMove, new MultiAttackIntent(Balthazar.ScaleDeadly(3), 3));

        punch.FollowUpState = doubleSwing;
        doubleSwing.FollowUpState = barrage;
        barrage.FollowUpState = punch;

        List<MonsterState> states = [punch, doubleSwing, barrage];
        return new MonsterMoveStateMachine(states, punch);
    }

    protected override bool ShouldShowMoveInBestiary(string moveStateId) =>
        moveStateId is "PUNCH" or "DOUBLE_SWING" or "BARRAGE";

    /// <summary>
    /// 受到伤害后血量 ≤20 时由 OverloadMeltdownPower 调用：
    /// 立即把当前意图替换为自爆 16（原版 StunInternal 的 SetMoveImmediate 范式，
    /// MustPerformOnceBeforeTransitioning 保证下回合执行的就是它）。
    /// </summary>
    public void EnterSelfDestruct()
    {
        MoveState selfDestruct = new("SELF_DESTRUCT", SelfDestructMove, new SingleAttackIntent(Balthazar.ScaleDeadly(16)))
        {
            MustPerformOnceBeforeTransitioning = true
        };
        SetMoveImmediate(selfDestruct);
    }

    private async Task PunchMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(Balthazar.ScaleDeadly(6)).FromMonster(this)
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

    private async Task BarrageMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(Balthazar.ScaleDeadly(3)).WithHitCount(3).FromMonster(this)
            .OnlyPlayAnimOnce()
            .WithAttackerAnim("Attack", 0.2f)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(null);
    }

    /// <summary>
    /// 自爆：对所有玩家造成 16 点伤害后自毁。
    /// 自爆死亡前置 SuppressExplosion → 过载熔毁不触发（原稿：死于自爆意图不会触发该效果）。
    /// </summary>
    private async Task SelfDestructMove(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.TriggerAnim(base.Creature, "Cast", 0.25f);
        ICombatState? combatState = base.Creature.CombatState;
        if (combatState != null)
        {
            PlayerChoiceContext choiceContext = new ThrowingPlayerChoiceContext();
            foreach (Creature player in combatState.PlayerCreatures)
            {
                if (!player.IsAlive) continue;
                await CreatureCmd.Damage(choiceContext, player, Balthazar.ScaleDeadly(16), ValueProp.Unpowered, null, null);
            }
        }
        OverloadMeltdownPower? meltdown = base.Creature.GetPower<OverloadMeltdownPower>();
        if (meltdown != null)
        {
            meltdown.SuppressExplosion = true;
        }
        await CreatureCmd.Kill(base.Creature, force: true);
    }
}
