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
using baldurs_enemys.src.MonsterMoves;
using baldurs_enemys.src.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Models.Powers;

namespace baldurs_enemys.src.Monsters;

/// <summary>
/// 拉斐尔（A10 血量 340，低进阶 323）。第三层 Boss，两侧各两根灵魂柱。
///
/// 原稿状态机（#define t = 本场战斗经过的回合数）：
///   while(灵魂柱数量 >= 2)
///   {
///       自己眩晕;
///       攻击(5+t)*2;
///   }
///   强化：获得3力量;
///   int i=0;
///   while(true)
///   {
///       i++;
///       攻击(5+i)*2;
///       攻击(t+i)*1;
///   }
///
/// t 取 <see cref="ICombatState.RoundNumber"/>（意图展示与实际执行在同一回合内，
/// 取值一致，无补偿问题）；i 的自增放「攻击(5+i)*2」执行体开头（巴萨扎尔/凯瑟里克同款）：
/// - 段一意图 = 5 + (i+1) = i+6（预测时 i 尚未自增）；
/// - 段二意图 = t + i（预测发生在自增之后）。
/// </summary>
public sealed class Raphael : BaldurMonsterModel
{
    public override int MinInitialHp => AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 340, 323);

    public override int MaxInitialHp => MinInitialHp;

    public override DamageSfxType TakeDamageSfxType => DamageSfxType.Fur;

    /// <summary>美术：mod 自带立绘（baldurs_enemys/images/monsters/raphael.png）。</summary>
    public override NCreatureVisuals? CreateCustomVisuals() =>
        BaldurVisuals.Create("res://baldurs_enemys/images/monsters/raphael.png", 340);

    private int _loopIndex;

    private int LoopIndex
    {
        get => _loopIndex;
        set
        {
            AssertMutable();
            _loopIndex = value;
        }
    }

    /// <summary>本场战斗经过的回合数（RoundNumber，从 1 起）。</summary>
    private int Turn => base.Creature.CombatState?.RoundNumber ?? 0;

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        ConditionalBranchState entry = new("ENTRY"); // 首次条件判断
        MoveState idle = new("IDLE", IdleMove, new StunIntent());
        MoveState hellfire = new("HELLFIRE", HellfireMove, new DynamicMultiAttackIntent(() => Balthazar.ScaleDeadly(5 + Turn), 2));
        ConditionalBranchState postHellfire = new("POST_HELLFIRE");
        MoveState empower = new("EMPOWER", EmpowerMove, new BuffIntent());
        MoveState gash = new("GASH", GashMove, new DynamicMultiAttackIntent(() => Balthazar.ScaleDeadly(6 + LoopIndex), 2));
        MoveState brand = new("BRAND", BrandMove, new DynamicMultiAttackIntent(() => Balthazar.ScaleDeadly(Turn + LoopIndex), 1));

        entry.AddState(idle, TwoPillarsAlive);
        entry.AddState(empower, () => true);

        idle.FollowUpState = hellfire;
        hellfire.FollowUpState = postHellfire;
        postHellfire.AddState(idle, TwoPillarsAlive);
        postHellfire.AddState(empower, () => true);

        empower.FollowUpState = gash;
        gash.FollowUpState = brand;
        brand.FollowUpState = gash;

        List<MonsterState> states =
        [
            entry, idle, hellfire, postHellfire, empower, gash, brand
        ];
        return new MonsterMoveStateMachine(states, entry);
    }

    protected override bool ShouldShowMoveInBestiary(string moveStateId) =>
        moveStateId is "IDLE" or "HELLFIRE" or "EMPOWER" or "GASH" or "BRAND";

    /// <summary>「灵魂柱数量 >= 2」。</summary>
    private bool TwoPillarsAlive()
    {
        ICombatState? combatState = base.Creature.CombatState;
        return combatState != null && combatState.Enemies.Count(c => c.Monster is SoulPillar && c.IsAlive) >= 2;
    }

    private Task IdleMove(IReadOnlyList<Creature> targets) => Task.CompletedTask;

    private async Task HellfireMove(IReadOnlyList<Creature> targets)
    {
        int damage = Balthazar.ScaleDeadly(5 + Turn);
        await DamageCmd.Attack(damage).WithHitCount(2).FromMonster(this)
            .OnlyPlayAnimOnce()
            .WithAttackerAnim("Attack", 0.3f)
            .WithHitFx("vfx/vfx_attack_lightning")
            .Execute(null);
    }

    private async Task EmpowerMove(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.TriggerAnim(base.Creature, "Cast", 0.25f);
        await PowerCmd.Apply<StrengthPower>(new ThrowingPlayerChoiceContext(), base.Creature, 3m, base.Creature, null);
    }

    /// <summary>攻击(5+i)*2：执行时先 i++，再攻击 (5+i)×2。</summary>
    private async Task GashMove(IReadOnlyList<Creature> targets)
    {
        LoopIndex += 1;
        int damage = Balthazar.ScaleDeadly(5 + LoopIndex);
        await DamageCmd.Attack(damage).WithHitCount(2).FromMonster(this)
            .OnlyPlayAnimOnce()
            .WithAttackerAnim("Attack", 0.3f)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(null);
    }

    /// <summary>攻击(t+i)*1：i 不再自增（与同循环第一段攻击同值）。</summary>
    private async Task BrandMove(IReadOnlyList<Creature> targets)
    {
        int damage = Balthazar.ScaleDeadly(Turn + LoopIndex);
        await DamageCmd.Attack(damage).FromMonster(this)
            .WithAttackerAnim("Attack", 0.3f)
            .WithHitFx("vfx/vfx_attack_lightning")
            .Execute(null);
    }
}
