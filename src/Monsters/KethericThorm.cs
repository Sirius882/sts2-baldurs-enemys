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
using baldurs_enemys.src.MonsterMoves;

namespace baldurs_enemys.src.Monsters;

/// <summary>
/// 凯瑟里克·索姆（A10 血量 300，低进阶 285 = floor(300/1.05)）。第二层 Boss。
///
/// 原稿状态机（2026-10-02 版）：
///   while(伊索贝尔存活)
///   {
///       攻击13*1;
///       攻击4*3;
///   }
///   强化：获得3力量;
///   int i=0;
///   while(true)
///   {
///       i += 1;
///       攻击(i+3)*3;
///       自己眩晕;
///       攻击(i+3)*2;
///   }
///
/// 执行口径（总则：实际动作占 1 回合；i 的定义/累加是纯逻辑）：
/// - while 条件在每次循环顶求值 → 每招之后各一个分支节点（伊索贝尔存活 → 继续，死亡 → 落到强化）；
///   入口也有一个分支（若玩家持有艾琳女士，伊索贝尔开场即死 → 直接跳过第一阶段，与 C++ 首次条件判断一致）；
/// - 二阶段回合序列：i=1 → 攻击4×3 → 眩晕 → 攻击4×2 → i=2 → 攻击5×3 → 眩晕 → 攻击5×2 → ……
///   伤害每整循环 +1，攻击(i+3) 的 i 在两段攻击中同值。
///
/// 实现注记（与巴萨扎尔同因）：ConditionalBranchState 条件会被重复求值，所以 i 的自增
/// 放在「攻击(i+3)*3」的执行体开头；意图 lambda 补偿：
/// - 第一段攻击意图 = (i+1)+3 = i+4（预测时 i 尚未自增）；
/// - 第二段攻击意图 = i+3（中间隔着眩晕招，预测发生在自增之后）。
///
/// 攻击 13 → 低进阶 11（round(13/1.15)）；攻击 4 → 3；二阶段动态伤害同样按 round(值/1.15)。
/// 强化量不吃进阶缩放。
/// </summary>
public sealed class KethericThorm : CustomMonsterModel
{
    public override int MinInitialHp => AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 300, 285);

    public override int MaxInitialHp => MinInitialHp;

    public override DamageSfxType TakeDamageSfxType => DamageSfxType.Armor;

    /// <summary>美术：mod 自带立绘（baldurs_enemys/images/monsters/ketheric_thorm.png）。</summary>
    public override NCreatureVisuals? CreateCustomVisuals() =>
        BaldurVisuals.Create("res://baldurs_enemys/images/monsters/ketheric_thorm.png", 340);

    /// <summary>二阶段计数 i（C++ 语义：声明在循环之外，跨循环累加、永不重置）。</summary>
    private int _reapIndex;

    private int ReapIndex
    {
        get
        {
            return _reapIndex;
        }
        set
        {
            AssertMutable();
            _reapIndex = value;
        }
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        // ---- 第一阶段：while(伊索贝尔存活) { 攻击13 → 攻击4×3 } ----
        ConditionalBranchState entry = new("ENTRY"); // 首次条件判断（伊索贝尔可能开场即死）
        MoveState smash = new("SMASH", SmashMove, new SingleAttackIntent(SmashDamage));
        ConditionalBranchState postSmash = new("POST_SMASH");
        MoveState barrage = new("BARRAGE", BarrageMove, new MultiAttackIntent(BarrageDamage, 3));
        ConditionalBranchState postBarrage = new("POST_BARRAGE");

        // ---- 转阶段 ----
        MoveState empower = new("EMPOWER", EmpowerMove, new BuffIntent());

        // ---- 第二阶段：i += 1 → 攻击(i+3)*3 → 眩晕 → 攻击(i+3)*2 ----
        MoveState reap = new("REAP", ReapMove, new DynamicMultiAttackIntent(() => Balthazar.ScaleDeadly(ReapIndex + 4), 3));
        MoveState recover = new("RECOVER", RecoverMove, new StunIntent());
        MoveState reap2 = new("REAP2", Reap2Move, new DynamicMultiAttackIntent(() => Balthazar.ScaleDeadly(ReapIndex + 3), 2));

        entry.AddState(smash, IsobelAlive);
        entry.AddState(empower, () => true);

        smash.FollowUpState = postSmash;
        postSmash.AddState(barrage, IsobelAlive);
        postSmash.AddState(empower, () => true);

        barrage.FollowUpState = postBarrage;
        postBarrage.AddState(smash, IsobelAlive);
        postBarrage.AddState(empower, () => true);

        empower.FollowUpState = reap;
        reap.FollowUpState = recover;
        recover.FollowUpState = reap2;
        reap2.FollowUpState = reap;

        List<MonsterState> states =
        [
            entry, smash, postSmash, barrage, postBarrage,
            empower, reap, recover, reap2
        ];
        return new MonsterMoveStateMachine(states, entry);
    }

    protected override bool ShouldShowMoveInBestiary(string moveStateId) =>
        moveStateId is "SMASH" or "BARRAGE" or "EMPOWER" or "REAP" or "REAP2" or "RECOVER";

    /// <summary>「伊索贝尔存活」：按类型找活着的伊索贝尔；找不到（不应发生）按死亡处理，避免状态机卡死。</summary>
    private bool IsobelAlive()
    {
        ICombatState? combatState = base.Creature.CombatState;
        if (combatState == null) return false;

        return combatState.Enemies.Any(c => c.Monster is Isobel && c.IsAlive);
    }

    /// <summary>攻击13：低进阶 round(13/1.15) = 11。</summary>
    private int SmashDamage => Balthazar.ScaleDeadly(13);

    /// <summary>攻击4：低进阶 round(4/1.15) = 3。</summary>
    private int BarrageDamage => Balthazar.ScaleDeadly(4);

    private async Task SmashMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(SmashDamage).FromMonster(this)
            .WithAttackerAnim("Attack", 0.3f)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(null);
    }

    private async Task BarrageMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(BarrageDamage).WithHitCount(3).FromMonster(this)
            .OnlyPlayAnimOnce()
            .WithAttackerAnim("MultiAttackTrigger", 0.2f)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(null);
    }

    /// <summary>转阶段：获得 3 力量（固定值，不吃进阶缩放）。</summary>
    private async Task EmpowerMove(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.TriggerAnim(base.Creature, "Cast", 0.25f);
        await PowerCmd.Apply<StrengthPower>(new ThrowingPlayerChoiceContext(), base.Creature, 3m, base.Creature, null);
    }

    /// <summary>
    /// 攻击(i+3)*3：执行时先 i += 1，再攻击 (i+3)×3。
    /// A10 值 = i+3；低进阶 = round(值/1.15)。
    /// </summary>
    private async Task ReapMove(IReadOnlyList<Creature> targets)
    {
        ReapIndex += 1;
        int damage = ReapIndex + 3;
        await DamageCmd.Attack(Balthazar.ScaleDeadly(damage)).WithHitCount(3).FromMonster(this)
            .OnlyPlayAnimOnce()
            .WithAttackerAnim("MultiAttackTrigger", 0.2f)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(null);
    }

    /// <summary>攻击(i+3)*2：i 不再自增（与同循环第一段攻击同值）。</summary>
    private async Task Reap2Move(IReadOnlyList<Creature> targets)
    {
        int damage = ReapIndex + 3;
        await DamageCmd.Attack(Balthazar.ScaleDeadly(damage)).WithHitCount(2).FromMonster(this)
            .OnlyPlayAnimOnce()
            .WithAttackerAnim("MultiAttackTrigger", 0.2f)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(null);
    }

    /// <summary>自己眩晕：这回合不行动（设计稿明确的意图，非被外部眩晕）。</summary>
    private Task RecoverMove(IReadOnlyList<Creature> targets) => Task.CompletedTask;
}
