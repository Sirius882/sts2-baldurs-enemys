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
/// 钢铁卫士（A10 血量 67，低进阶 63）。戈塔什 Boss 战的护卫 construct。
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
    public override int MinInitialHp => AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 67, 63);

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

        // 自爆状态必须**注册进状态机**，否则见 EnterSelfDestruct 的详细说明。
        // 自引用（FollowUpState = 自己）实现设计稿「被打到 20 血以下就不断尝试自爆」。
        //
        // 幂等保护：引擎有多处会**反复**调用本方法生成全新状态机实例
        // （MonsterModel.cs:412 初始化、:300 的 GetIntents 收集资源、
        //  以及本 mod 基类 BaldurMonsterModel.CollectIntents 也走同一模式）。
        // 若每次都无条件重建，字段会被指向「未被注册进 States 的那一份」，
        // EnterSelfDestruct 拿到的实例与状态机内的不是同一个 → 切换无效。
        // 故只在首次创建时缓存，后续复用同一实例。
        if (_selfDestruct == null)
        {
            _selfDestruct = new MoveState("SELF_DESTRUCT", SelfDestructMove, new SingleAttackIntent(Balthazar.ScaleDeadly(16)));
            _selfDestruct.FollowUpState = _selfDestruct;
            _selfDestruct.MustPerformOnceBeforeTransitioning = false;
        }

        List<MonsterState> states = [punch, doubleSwing, barrage, _selfDestruct];
        return new MonsterMoveStateMachine(states, punch);
    }

    /// <summary>
    /// 自爆状态实例。构造状态机时创建并注册，<see cref="EnterSelfDestruct"/> 只做「切过去」。
    /// </summary>
    private MoveState? _selfDestruct;

    protected override bool ShouldShowMoveInBestiary(string moveStateId) =>
        moveStateId is "PUNCH" or "DOUBLE_SWING" or "BARRAGE";

    /// <summary>
    /// 受到伤害后血量 ≤20 时由 OverloadMeltdownPower 调用：
    /// 立即把当前意图替换为自爆 16。
    ///
    /// 【为什么必须用"预注册 + 自引用"的状态，而不是现场 new 一个 MoveState】
    /// 原实现是每次调用都`new MoveState("SELF_DESTRUCT", ...)` 再 SetMoveImmediate，
    /// 有一个致命缺陷：**这个新状态没有注册进 MonsterMoveStateMachine.States**。
    /// 后果有两条：
    /// 1. `SetMoveImmediate` → `ForceCurrentState` 把 `_currentState` 设成这个**游离状态**。
    ///    下一回合 `RollMove` → `FindNextMoveState` → `_currentState.GetNextState()`
    ///    （MoveState.cs:69）在 `FollowUpState`/`FollowUpStateId` 皆空时
    ///    **直接抛 InvalidOperationException("No valid followup state.")** —— 硬崩溃。
    /// 2. 即便侥幸没崩，状态机里查不到 "SELF_DESTRUCT"，
    ///    `FindNextMoveState` 的 `States.ContainsKey(nextState)` 校验也会失败。
    ///
    /// 正确做法：在 `GenerateMoveStateMachine()` 里把自爆状态**连同 PUNCH/DOUBLE_SWING/BARRAGE
    /// 一起注册**，并让它 `FollowUpState = 自己`。这样：
    /// -注册保证 `States["SELF_DESTRUCT"]`存在，状态机能正确转移，不会抛异常；
    /// - 自引用实现设计稿「被打到 20 血以下只会不断尝试自爆，意图状态机就没用了」；
    /// - `MustPerformOnceBeforeTransitioning = false`：不需要"必须先执行一次"的强制语义，
    ///   否则 `CanTransitionAway` 在首次执行前为 false（MoveState.cs:27-37），
    ///   `SetMoveImmediate` 的守卫 `NextMove.CanTransitionAway`（MonsterModel.cs:423）会挡下切换。
    ///
    /// 【forceTransition 的作用】
    /// `SetMoveImmediate(state, forceTransition)` 的守卫是
    /// `if (NextMove.CanTransitionAway || forceTransition)`。传 true 可确保即使当前意图
    /// 处于"不可离开"状态也能强行切换，避免玩家看到意图卡住不动。
    /// </summary>
    public void EnterSelfDestruct()
    {
        if (_selfDestruct == null)
        {
            // 理论上不会发生：构造时已创建。仅为防御性兜底，避免 NRE。
            return;
        }

        SetMoveImmediate(_selfDestruct, forceTransition: true);
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
