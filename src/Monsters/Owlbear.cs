using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Audio;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
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
/// 大枭熊（A10 血量 140，低进阶 133 = floor(140/1.05)）。第一层精英战「枭熊」的首领。
///
/// 原稿状态机（设计稿 2026-10-04 版）：
/// ```
/// while(true)
/// {
///     if(小枭熊存活)
///     {
///         强化获得2活力;
///         攻击4*3;
///         if(小枭熊满血) 攻击3*3;
///         else 给小枭熊回血10;
///     }
///     else
///     {
///         攻击6*3;
///         强化获得3荆棘，持续1回合;
///     }
/// }
/// ```
///
/// 【实现注记】
/// - 每个动作独占 1 回合（总则口径），所以 while 循环体由 MoveState 链 + ConditionalBranchState 组成：
///   小枭熊存活时 VIGOR → MAUL → {MAUL2 | FEED} →（回 while 顶部重新判存活）；
///   小枭熊死亡后 MAUL_ENRAGED → THORNS →（自环，等价于回 while 顶部的 else 分支）。
/// - `ConditionalBranchState` 的条件在每次预测/转移时都会重新求值（`GetNextState` 无缓存），
///   这正好等价于原稿"每轮 while 重新判断"：玩家在任意敌方回合间隙杀死小枭熊，
///   下一回合就会自动切到暴怒分支。
/// - 「小枭熊满血」用 `CurrentHp >= MaxHp` 判定；受伤后由大枭熊下一轮的 FEED 拉满，
///   拉满后下一轮又改走 MAUL2 —— 形成原稿的来回切换。
/// - 活力/荆棘/回血都是强化与治疗量，不吃进阶缩放（原版口径）；攻击伤害低进阶 = round(A10/1.15)。
/// - 「3 荆棘持续 1 回合」用 `TimedDebuffExpiryPower` 挂 rounds=2：它的倒计时在**敌方回合结束**递减 1
///   （含施加当回合末），rounds=2 施加完当回合末剩 1、下一敌方回合末才归零移除，
///   即"下一个敌方回合内仍生效"，正是设计稿的"持续 1 回合"。写 rounds=1 会当回合末立刻被移除（无效）。
///
/// 附带 power：可被交互（<see cref="InteractablePower"/>）——
/// 被打出「交互」后掷 1d20，≥ 14 则玩家直接赢得本场战斗（检定失败可在后续回合继续尝试），
/// 分流逻辑在 <see cref="baldurs_enemys.src.Cards.Interact"/>。
/// </summary>
public sealed class Owlbear : BaldurMonsterModel
{
    /// <summary>「交互」检定阈值：1d20 ≥ 该值即直接获胜（设计稿 ≥14）。</summary>
    public const int InteractSuccessThreshold = 14;

    public override int MinInitialHp => AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 140, 133);

    public override int MaxInitialHp => MinInitialHp;

    public override DamageSfxType TakeDamageSfxType => DamageSfxType.Armor;

    /// <summary>美术：mod 自带立绘（baldurs_enemys/images/monsters/owl_bear.png）。</summary>
    public override NCreatureVisuals? CreateCustomVisuals() =>
        BaldurVisuals.Create("res://baldurs_enemys/images/monsters/owl_bear.png", 310);

    /// <summary>可被交互：必须在进房时挂（照抄油桶），这样动态生成的枭熊也能正确获得该标记。</summary>
    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        await PowerCmd.Apply<InteractablePower>(new ThrowingPlayerChoiceContext(), base.Creature, 1m, base.Creature, null);
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        // 强化：获得 2 活力
        MoveState vigor = new("VIGOR", VigorMove, new BuffIntent());
        // 攻击 4*3（低进阶 3*3）
        MoveState maul = new("MAUL", MaulMove, new MultiAttackIntent(Balthazar.ScaleDeadly(4), 3));
        // 小枭熊满血：攻击 3*3（低进阶 3*3）
        MoveState maulSecondary = new("MAUL_SECONDARY", MaulSecondaryMove, new MultiAttackIntent(Balthazar.ScaleDeadly(3), 3));
        // 小枭熊未满血：给它回血 10（不显示数值）
        MoveState feed = new("FEED", FeedMove, new HealIntent());
        // 小枭熊死亡：攻击 6*3（低进阶 5*3）
        MoveState maulEnraged = new("MAUL_ENRAGED", MaulEnragedMove, new MultiAttackIntent(Balthazar.ScaleDeadly(6), 3));
        // 强化：获得 3 荆棘，持续 1 回合
        MoveState thorns = new("THORNS", ThornsMove, new BuffIntent());

        ConditionalBranchState branchCubAlive = new("BRANCH_CUB_ALIVE");
        ConditionalBranchState branchCubFullHp = new("BRANCH_CUB_FULL_HP");

        // 小枭熊存活 → 走"喂食"循环；否则走"暴怒"循环
        branchCubAlive.AddState(vigor, () => CubAlive());
        branchCubAlive.AddState(maulEnraged, () => true);

        vigor.FollowUpState = maul;
        maul.FollowUpState = branchCubFullHp;

        // 满血 → 追加一记攻击；未满血 → 改为回血；已死 → 立刻切暴怒分支。
        // AddState 自上而下取第一个成立的分支，所以"存活"判定必须写在后两条之前。
        branchCubFullHp.AddState(maulSecondary, () => CubAlive() && CubAtFullHp());
        branchCubFullHp.AddState(feed, () => CubAlive());
        branchCubFullHp.AddState(maulEnraged, () => true);

        maulSecondary.FollowUpState = branchCubAlive;
        feed.FollowUpState = branchCubAlive;

        maulEnraged.FollowUpState = thorns;
        thorns.FollowUpState = maulEnraged;

        List<MonsterState> states =
        [
            branchCubAlive, branchCubFullHp,
            vigor, maul, maulSecondary, feed,
            maulEnraged, thorns
        ];
        return new MonsterMoveStateMachine(states, branchCubAlive);
    }

    protected override bool ShouldShowMoveInBestiary(string moveStateId) => true;

    /// <summary>按类型在场上的敌人里找活着的小枭熊；找不到（不应发生）按死亡处理。</summary>
    private bool CubAlive()
    {
        ICombatState? combatState = base.Creature.CombatState;
        if (combatState == null) return false;

        return combatState.Enemies.Any(c => c.Monster is OwlbearCub && c.IsAlive);
    }

    /// <summary>存活的小枭熊是否满血（用于"攻击 3*3 / 回血 10"的二选一）。</summary>
    private bool CubAtFullHp()
    {
        ICombatState? combatState = base.Creature.CombatState;
        if (combatState == null) return false;

        return combatState.Enemies.Any(c => c.Monster is OwlbearCub && c.IsAlive && c.CurrentHp >= c.MaxHp);
    }

    /// <summary>强化：获得 2 活力（原版 VigorPower：下一次有来源攻击额外加伤，命中后消耗）。</summary>
    private async Task VigorMove(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.TriggerAnim(base.Creature, "Cast", 0.2f);
        await PowerCmd.Apply<VigorPower>(new ThrowingPlayerChoiceContext(), base.Creature, 2m, base.Creature, null);
    }

    private async Task MaulMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(Balthazar.ScaleDeadly(4)).FromMonster(this)
            .WithAttackerAnim("Attack", 0.2f)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(null);
    }

    private async Task MaulSecondaryMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(Balthazar.ScaleDeadly(3)).FromMonster(this)
            .WithAttackerAnim("Attack", 0.2f)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(null);
    }

    /// <summary>给小枭熊回血 10（治疗量不吃进阶缩放；找不到目标则无事发生）。</summary>
    private async Task FeedMove(IReadOnlyList<Creature> targets)
    {
        ICombatState? combatState = base.Creature.CombatState;
        if (combatState == null) return;

        Creature? cub = combatState.Enemies.FirstOrDefault(c => c.Monster is OwlbearCub && c.IsAlive);
        if (cub == null) return;

        await CreatureCmd.TriggerAnim(base.Creature, "Cast", 0.2f);
        await CreatureCmd.Heal(cub, 10m);
    }

    private async Task MaulEnragedMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(Balthazar.ScaleDeadly(6)).FromMonster(this)
            .WithAttackerAnim("Attack", 0.25f)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(null);
    }

    /// <summary>
    /// 强化：获得 3 荆棘，持续 1 回合。
    /// 荆棘（ThornsPower）：持有者受到有来源的攻击时，攻击者受到等同层数的伤害。
    /// 定时消散 rounds=2 ⇒ 覆盖下一个敌方回合（见类注释「3 荆棘持续 1 回合」的换算说明）。
    /// </summary>
    private async Task ThornsMove(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.TriggerAnim(base.Creature, "Cast", 0.2f);
        await TimedDebuffExpiryPower.ApplyTimedDebuff<ThornsPower>(
            new ThrowingPlayerChoiceContext(), base.Creature, 3m, base.Creature, rounds: 2);
    }
}
