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
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Nodes.Combat;
using baldurs_enemys.src.Powers;

namespace baldurs_enemys.src.Monsters;

/// <summary>
/// 巴萨扎尔（A10 血量 200，低进阶 190 = floor(200/1.05)）。第二层精英。
///
/// 原稿状态机（2026-10-02 版）：
///   int i = 0;                          // 纯逻辑，战斗开始时执行，跨越外层循环不重置
///   while(true)
///   {
///       恢复10点血量;
///       减益：给所有玩家施加死云术power;
///       while(true)
///       {
///           i += 5;
///           if (暗夜之歌存活) 恢复(i+10)点血量;
///           else 攻击(15+i/5)*1;
///       }
///   }
///
/// 执行口径（总则：实际动作占 1 回合；i 的定义/累加是纯逻辑，不占回合）：
/// - 回合序列：自愈10 → 死云术 → (i=5) 治疗15/攻击16 → (i=10) 治疗20/攻击17 → ……
///   i 每轮 +5，治疗量与攻击伤害随 i 增长；暗夜之歌死亡（被击杀或被拾取）后转入攻击分支。
/// - 实现注记：ConditionalBranchState 的条件在每次预测/转移时都会重新求值
///   （GetNextState 无缓存），所以 i 的自增**放在招式执行体内**而不是分支条件里，
///   避免重复累加。意图 lambda 相应补偿（显示值 = 执行前补上本次自增后的值）：
///   攻击意图显示 15 + (_i+5)/5 = 16 + _i/5（_i 恒为 5 的倍数）。
///   治疗(HealIntent)不显示数值，无需补偿。
///
/// 治疗量与死云术数值不吃进阶缩放（原版治疗类同样不缩放）；攻击伤害低进阶 = round(A10/1.15)。
/// </summary>
public sealed class Balthazar : BaldurMonsterModel
{
    public override int MinInitialHp => AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 200, 190);

    public override int MaxInitialHp => MinInitialHp;

    public override DamageSfxType TakeDamageSfxType => DamageSfxType.Insect;

    /// <summary>美术：mod 自带立绘（baldurs_enemys/images/monsters/balthazar.png）。</summary>
    public override NCreatureVisuals? CreateCustomVisuals() =>
        BaldurVisuals.Create("res://baldurs_enemys/images/monsters/balthazar.png", 300);

    /// <summary>内层循环计数 i（C++ 语义：声明在两个循环之外，跨外层循环累加、永不重置）。</summary>
    private int _drainIndex;

    private int DrainIndex
    {
        get
        {
            return _drainIndex;
        }
        set
        {
            AssertMutable();
            _drainIndex = value;
        }
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        // ---- 外层 while(true) 的两招 ----
        MoveState mend = new("MEND", MendMove, new HealIntent());
        MoveState cloud = new("DEATH_CLOUD", DeathCloudMove, new DebuffIntent());

        // ---- 内层 while(true)：i += 5 后按暗夜之歌生死二选一 ----
        MoveState drain = new("DRAIN", DrainMove, new HealIntent());
        MoveState slam = new("SHADOW_BURST", SlamMove, new SingleAttackIntent(() => 16m + DrainIndex / 5m));
        ConditionalBranchState innerBranch = new("INNER_BRANCH");

        mend.FollowUpState = cloud;
        cloud.FollowUpState = innerBranch;
        innerBranch.AddState(drain, NightsongAlive);
        innerBranch.AddState(slam, () => true);
        drain.FollowUpState = innerBranch;
        slam.FollowUpState = innerBranch;

        List<MonsterState> states = [mend, cloud, innerBranch, drain, slam];
        return new MonsterMoveStateMachine(states, mend);
    }

    protected override bool ShouldShowMoveInBestiary(string moveStateId) =>
        moveStateId is "MEND" or "DEATH_CLOUD" or "DRAIN" or "SHADOW_BURST";

    /// <summary>「暗夜之歌存活」：按类型在场上的敌人里找活着的暗夜之歌；找不到（不应发生）按死亡处理。</summary>
    private bool NightsongAlive()
    {
        ICombatState? combatState = base.Creature.CombatState;
        if (combatState == null) return false;

        return combatState.Enemies.Any(c => c.Monster is Nightsong && c.IsAlive);
    }

    /// <summary>自愈 10 点（固定值，不吃进阶缩放）。</summary>
    private async Task MendMove(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.TriggerAnim(base.Creature, "Cast", 0.25f);
        await CreatureCmd.Heal(base.Creature, 10m);
    }

    /// <summary>给所有玩家施加死云术（3 点/回合，持续 8 回合；重复施加刷新时长）。</summary>
    private async Task DeathCloudMove(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.TriggerAnim(base.Creature, "Cast", 0.25f);
        await PowerCmd.Apply<DeathCloudPower>(
            new ThrowingPlayerChoiceContext(),
            base.Creature.CombatState!.Players.Select(p => p.Creature),
            DeathCloudPower.ApplyAmount, base.Creature, null);
    }

    /// <summary>治疗 (i+10) 点：执行时先 i += 5，再治疗 i+10。</summary>
    private async Task DrainMove(IReadOnlyList<Creature> targets)
    {
        DrainIndex += 5;
        await CreatureCmd.TriggerAnim(base.Creature, "Cast", 0.25f);
        await CreatureCmd.Heal(base.Creature, DrainIndex + 10m);
    }

    /// <summary>
    /// 攻击 (15+i/5)*1：执行时先 i += 5（与治疗分支共用同一段 C++ 前缀语句），
    /// 伤害取 A10 值 15+i/5，低进阶 = round(A10/1.15)（四舍五入，与原版 DeadlyEnemies 口径一致）。
    /// </summary>
    private async Task SlamMove(IReadOnlyList<Creature> targets)
    {
        DrainIndex += 5;
        int damage = 15 + DrainIndex / 5;
        await DamageCmd.Attack(ScaleDeadly(damage)).FromMonster(this)
            .WithAttackerAnim("Attack", 0.25f)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(null);
    }

    /// <summary>DeadlyEnemies 进阶换算：A10 原值 / 低进阶 round(原值/1.15)（四舍五入）。</summary>
    internal static int ScaleDeadly(int a10Value) =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, a10Value,
            (int)Math.Round(a10Value / 1.15, MidpointRounding.AwayFromZero));
}
