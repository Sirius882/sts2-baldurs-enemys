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
/// 巴萨扎尔（A10 血量 300，低进阶 285 = floor(300/1.05)）。第二层精英。
///
/// 原稿状态机（2026-10-03 修订版，设计稿标注「有改动」）：
///   int i = 0;                          // 纯逻辑，战斗开始时执行，跨越循环不重置
///   减益：给所有玩家施加死云术power;      // 开场一次性
///   while(true)
///   {
///       i += 1;                          // 纯逻辑，不占回合
///       if (暗夜之歌存活) 恢复(i+10)点血量，并攻击15*1;
///       else            攻击(15+i)*1;
///   }
///
/// 执行口径（总则：实际动作占 1 回合；i 的定义/累加是纯逻辑，不占回合）：
/// - 原版「自愈 10」独立招式（MEND）已按新稿删除 —— 现在只在暗夜之歌存活的那一回合里附带回血。
/// - 暗夜之歌存活分支是**一招两动作**（先回血再攻击），用 HealIntent + SingleAttackIntent 组合意图
///   （MoveState 构造函数的 params AbstractIntent[] 天然支持多意图）。
/// - 暗夜之歌死亡（被击杀或被交互）后固定走「攻击(15+i)」，且因 i 持续递增，伤害逐轮回涨。
/// - 实现注记：ConditionalBranchState 的条件在每次预测/转移时都会重新求值
///   （GetNextState 无缓存），所以 i 的自增**放在招式执行体内**而不是分支条件里，
///   避免重复累加。意图 lambda 相应补偿（显示值 = 执行前补上本次自增后的值）：
///   攻击意图显示 15 + (i+1) = i+16。回血 i+10 用 HealIntent（不显示数值），无需补偿。
///
/// 治疗量与死云术数值不吃进阶缩放；攻击伤害低进阶 = round(A10/1.15)。
/// 死云术见<see cref="DeathCloudPower"/>：1d8+1、永久持续。
/// </summary>
public sealed class Balthazar : BaldurMonsterModel
{
    public override int MinInitialHp => AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 300, 285);

    public override int MaxInitialHp => MinInitialHp;

    public override DamageSfxType TakeDamageSfxType => DamageSfxType.Insect;

    /// <summary>美术：mod 自带立绘（baldurs_enemys/images/monsters/balthazar.png）。</summary>
    public override NCreatureVisuals? CreateCustomVisuals() =>
        BaldurVisuals.Create("res://baldurs_enemys/images/monsters/balthazar.png", 300);

    /// <summary>循环计数 i（C++ 语义：声明在循环之外，跨循环累加、永不重置）。</summary>
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

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        // 开场一次性施加死云术（设计稿：减益语句在 while 循环之前）。
        // 死云术现为永久持续，只需施加一次。
        await PowerCmd.Apply<DeathCloudPower>(
            new ThrowingPlayerChoiceContext(),
            base.Creature.CombatState!.Players.Select(p => p.Creature),
            DeathCloudPower.ApplyAmount, base.Creature, null);
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        // 暗夜之歌存活：回血 (i+10) + 攻击 15*1（同一回合的两个动作）
        MoveState drain = new("DRAIN", DrainMove,
            new HealIntent(), new SingleAttackIntent(Balthazar.ScaleDeadly(15)));
        // 暗夜之歌死亡：攻击 (15+i)*1
        MoveState slam = new("SHADOW_BURST", SlamMove,
            new SingleAttackIntent(() => Balthazar.ScaleDeadly(DrainIndex + 16)));
        ConditionalBranchState branch = new("BRANCH");

        branch.AddState(drain, NightsongAlive);
        branch.AddState(slam, () => true);
        drain.FollowUpState = branch;
        slam.FollowUpState = branch;

        List<MonsterState> states = [branch, drain, slam];
        return new MonsterMoveStateMachine(states, branch);
    }

    protected override bool ShouldShowMoveInBestiary(string moveStateId) =>
        moveStateId is "DRAIN" or "SHADOW_BURST";

    /// <summary>「暗夜之歌存活」：按类型在场上的敌人里找活着的暗夜之歌；找不到（不应发生）按死亡处理。</summary>
    private bool NightsongAlive()
    {
        ICombatState? combatState = base.Creature.CombatState;
        if (combatState == null) return false;

        return combatState.Enemies.Any(c => c.Monster is Nightsong && c.IsAlive);
    }

    /// <summary>
    /// 暗夜之歌存活分支：执行时先 i += 1，再回血 (i+10)，然后攻击 15*1。
    /// 攻击 15 为固定值，不随 i 增长。
    /// </summary>
    private async Task DrainMove(IReadOnlyList<Creature> targets)
    {
        DrainIndex += 1;
        await CreatureCmd.TriggerAnim(base.Creature, "Cast", 0.25f);
        await CreatureCmd.Heal(base.Creature, DrainIndex + 10m);
        await DamageCmd.Attack(Balthazar.ScaleDeadly(15)).FromMonster(this)
            .WithAttackerAnim("Attack", 0.25f)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(null);
    }

    /// <summary>
    /// 暗夜之歌死亡分支：执行时先 i += 1，再攻击 (15+i)*1。
    /// 意图 lambda 补偿：预测时 i 尚未自增，故显示 15 + (i+1) = i+16。
    /// </summary>
    private async Task SlamMove(IReadOnlyList<Creature> targets)
    {
        DrainIndex += 1;
        int damage = 15 + DrainIndex;
        await DamageCmd.Attack(Balthazar.ScaleDeadly(damage)).FromMonster(this)
            .WithAttackerAnim("Attack", 0.25f)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(null);
    }

    /// <summary>DeadlyEnemies 进阶换算：A10 原值 / 低进阶 round(原值/1.15)（四舍五入）。</summary>
    internal static int ScaleDeadly(int a10Value) =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, a10Value,
            (int)Math.Round(a10Value / 1.15, MidpointRounding.AwayFromZero));
}
