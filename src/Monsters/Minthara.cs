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
using baldurs_enemys.src.Powers;

namespace baldurs_enemys.src.Monsters;

/// <summary>
/// 「夜影守望者」明萨拉（A10 血量 200，低进阶 190 = floor(200/1.05)）。第一层 Boss。
///
/// 原稿状态机（2026-10-02 修订版）：
///   while(true) {
///     for i in range(2) {
///       守护灵光; 勇气灵光;
///       if(两地精死) break;
///       攻击10; 强化+2力量;
///       if(两地精死) break;
///     }
///     攻击25;
///     if(两地精死) break;
///   }
///   while(true) { 攻击25; 强化+1力量; }
///
/// 执行口径（总则：有实际动作的语句占 1 回合，纯逻辑语句瞬时求值；按严格 C++ 语义展开）：
/// - for 内的 break 只跳出 for，落到 for 之后的第一条语句（攻击25）；while 末尾的 break
///   才真正进入第二阶段。因此地精中途全灭时：跳过 for 剩余轮次 → 补一发攻击25 → 转阶段。
/// - 地精全程存活时，第一阶段是固定 9 招循环：
///   灵光/灵光/10/强化/灵光/灵光/10/强化/25，然后从灵光重新开始（外层 while(true)）。
/// - 每个断点都实现为一个 ConditionalBranchState（瞬时求值，不占回合、不进战斗日志）。
///
/// 两个灵光均对全体友方单位（玩家的敌人，含明萨拉自己）上 buff。
/// </summary>
public sealed class Minthara : BaldurMonsterModel
{
    public override int MinInitialHp => AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 200, 190);

    public override int MaxInitialHp => MinInitialHp;

    public override DamageSfxType TakeDamageSfxType => DamageSfxType.Armor;

    /// <summary>美术：mod 自带立绘（baldurs_enemys/images/monsters/minthara.png）。</summary>
    public override NCreatureVisuals? CreateCustomVisuals() =>
        BaldurVisuals.Create("res://baldurs_enemys/images/monsters/minthara.png", 330);

    /// <summary>攻击10：低进阶 10/1.15=8.7 → 9。</summary>
    private int SlamDamage => AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 10, 9);

    /// <summary>攻击25：低进阶 25/1.15=21.7 → 22。</summary>
    private int SlamBigDamage => AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 25, 22);

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        // ---- 第一阶段：for i in range(2) 展开（两轮 灵光/灵光/攻击/强化）----
        MoveState auraGuard = new("AURA_GUARD", GuardAuraMove, new BuffIntent());
        MoveState auraValor = new("AURA_VALOR", ValorAuraMove, new BuffIntent());
        ConditionalBranchState checkA1 = new("CHECK_A1"); // for 第1轮：灵光之后的 break
        MoveState slam = new("SLAM", SlamMove, new SingleAttackIntent(SlamDamage));
        MoveState rally = new("RALLY", RallyMove, new BuffIntent());
        ConditionalBranchState checkB1 = new("CHECK_B1"); // for 第1轮：攻击+强化之后的 break
        MoveState auraGuard2 = new("AURA_GUARD2", GuardAuraMove, new BuffIntent());
        MoveState auraValor2 = new("AURA_VALOR2", ValorAuraMove, new BuffIntent());
        ConditionalBranchState checkA2 = new("CHECK_A2"); // for 第2轮：灵光之后的 break
        MoveState slam2 = new("SLAM2", SlamMove, new SingleAttackIntent(SlamDamage));
        MoveState rally2 = new("RALLY2", RallyMove, new BuffIntent());
        ConditionalBranchState checkB2 = new("CHECK_B2"); // for 第2轮：攻击+强化之后的 break

        // ---- 攻击25 + 外层 while 的 break 判定 ----
        MoveState slamBig = new("SLAM_BIG", SlamBigMove, new SingleAttackIntent(SlamBigDamage));
        ConditionalBranchState checkC = new("CHECK_C"); // 外层 while 末尾的 break

        // ---- 第二阶段：while(true) { 攻击25 → 强化+1力量 } ----
        MoveState slamLoop = new("SLAM_LOOP", SlamBigMove, new SingleAttackIntent(SlamBigDamage));
        MoveState rallyLoop = new("RALLY_LOOP", RallyLoopMove, new BuffIntent());

        // for 内的 break：跳出 for → 攻击25（不是直接转阶段）
        auraGuard.FollowUpState = auraValor;
        auraValor.FollowUpState = checkA1;
        checkA1.AddState(slamBig, BothGoblinsDead);
        checkA1.AddState(slam, () => true);
        slam.FollowUpState = rally;
        rally.FollowUpState = checkB1;
        checkB1.AddState(slamBig, BothGoblinsDead);
        checkB1.AddState(auraGuard2, () => true);
        auraGuard2.FollowUpState = auraValor2;
        auraValor2.FollowUpState = checkA2;
        checkA2.AddState(slamBig, BothGoblinsDead);
        checkA2.AddState(slam2, () => true);
        slam2.FollowUpState = rally2;
        rally2.FollowUpState = checkB2;
        checkB2.AddState(slamBig, BothGoblinsDead);
        checkB2.AddState(slamBig, () => true);
        slamBig.FollowUpState = checkC;

        // 外层 while 的 break：真转阶段；否则回到 for 开头（外层 while(true) 循环）
        checkC.AddState(slamLoop, BothGoblinsDead);
        checkC.AddState(auraGuard, () => true);

        slamLoop.FollowUpState = rallyLoop;
        rallyLoop.FollowUpState = slamLoop;

        List<MonsterState> states =
        [
            auraGuard, auraValor, checkA1, slam, rally, checkB1,
            auraGuard2, auraValor2, checkA2, slam2, rally2, checkB2,
            slamBig, checkC, slamLoop, rallyLoop
        ];
        return new MonsterMoveStateMachine(states, auraGuard);
    }

    /// <summary>图鉴只显示每招一次；带 "2" 的重复节点与瞬时求值的 CHECK_* 分支节点不进图鉴。</summary>
    protected override bool ShouldShowMoveInBestiary(string moveStateId) =>
        moveStateId is "AURA_GUARD" or "AURA_VALOR" or "SLAM" or "RALLY" or "SLAM_BIG" or "SLAM_LOOP" or "RALLY_LOOP";

    /// <summary>
    /// 「两个地精队友都死了」：油桶不算队友 —— 「爪牙」(MinionPower) 在引擎层把它标成了
    /// IsSecondaryEnemy（次要敌人），语义完全吻合，所以这里只按类型匹配两只地精。
    /// 若场上没有地精（不应发生），视为已全灭以免状态机卡死。
    /// </summary>
    private bool BothGoblinsDead()
    {
        ICombatState? combatState = base.Creature.CombatState;
        if (combatState == null) return true;

        List<Creature> goblins = combatState.Enemies
            .Where(c => c.Monster is GoblinMelee or GoblinRanged)
            .ToList();
        if (goblins.Count == 0) return true;
        return goblins.All(c => c.IsDead);
    }

    private async Task GuardAuraMove(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.TriggerAnim(base.Creature, "Cast", 0.25f);
        await PowerCmd.Apply<GuardAuraPower>(
            new ThrowingPlayerChoiceContext(), FriendlyUnits(), GuardAuraPower.ApplyAmount, base.Creature, null);
    }

    private async Task ValorAuraMove(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.TriggerAnim(base.Creature, "Cast", 0.25f);
        await PowerCmd.Apply<ValorAuraPower>(
            new ThrowingPlayerChoiceContext(), FriendlyUnits(), ValorAuraPower.ApplyAmount, base.Creature, null);
    }

    private async Task SlamMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(SlamDamage).FromMonster(this)
            .WithAttackerAnim("Attack", 0.2f)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(null);
    }

    private async Task SlamBigMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(SlamBigDamage).FromMonster(this)
            .WithAttackerAnim("Attack", 0.3f)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(null);
    }

    private async Task RallyMove(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.TriggerAnim(base.Creature, "Cast", 0.2f);
        await PowerCmd.Apply<StrengthPower>(new ThrowingPlayerChoiceContext(), base.Creature, 2m, base.Creature, null);
    }

    private async Task RallyLoopMove(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.TriggerAnim(base.Creature, "Cast", 0.2f);
        await PowerCmd.Apply<StrengthPower>(new ThrowingPlayerChoiceContext(), base.Creature, 1m, base.Creature, null);
    }

    /// <summary>
    /// 全体友方单位 = 玩家的敌人，含明萨拉自己。
    /// 用 Enemies（完整名单）而非 HittableEnemies，由引擎的 CanReceivePowers 过滤已死者。
    /// </summary>
    private IReadOnlyList<Creature> FriendlyUnits() => base.Creature.CombatState!.Enemies;
}
