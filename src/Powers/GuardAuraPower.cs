using System.Collections.Generic;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using baldurs_enemys.src.Core;

namespace baldurs_enemys.src.Powers;

/// <summary>
/// 「守护灵光」：玩家回合开始时获得 1d4+3 点格挡，持续 2 回合。
/// 明萨拉对全体友方单位（包括她自己）施加。
///
/// 设计口径（规格 6.4 + 总则第5条 骰子随机数）：
/// - 格挡值改为每回合每个持有者分别掷骰（1d4+3），骰子走种子 RNG（Niche 通道），多人/SL 一致；
///   每个持有者持有独立的 power 实例，各自在自己的 AfterSideTurnStart 里掷骰，天然满足"每单位分别掷骰"。
/// - Amount 只承载施加计数（图标层数），不再承载格挡数值；因此 <see cref="ShouldScaleInMultiplayer"/> 关闭
///   （多人人数缩放对"掷骰"无意义，每人掷自己的骰子即可自然缩放）。
/// - 持续回合数放 internal data，避免与 Amount 冲突（TickDownDuration 递减的就是 Amount，不能混用）。
/// - 重复施加：ApplyInternal 用 SetAmount 语义 → 数值不会被叠成 10；这里再把时长刷新为 2，即"刷新时长、不叠数值"。
/// </summary>
public sealed class GuardAuraPower : CustomPowerModel
{
    public const int ApplyAmount = 5;
    public const int DurationTurns = 2;

    private sealed class AuraData
    {
        public int TurnsRemaining;
    }

    public override PowerType Type => PowerType.Buff;

    /// <summary>图标：mod 自制（images/powers/guard_aura.png 64x64 / big/guard_aura_big.png 256x256）。
    /// 程序化生成的原创符号化图标 ——「守护灵光」：回合开始 1d4+3 格挡。
    /// 注意 power 图标**不做染色**（ICustomPower 只覆写路径），与 boss 地图图标的纯白要求不同。</summary>
    public override string? CustomPackedIconPath => "res://baldurs_enemys/images/powers/guard_aura.png";

    /// <summary>大图标（256x256，power 详情/图鉴用）。</summary>
    public override string? CustomBigIconPath => "res://baldurs_enemys/images/powers/big/guard_aura_big.png";

    public override PowerStackType StackType => PowerStackType.Counter;

    // 格挡由每回合掷骰（1d4+3）决定，Amount 缩放不再适用
    public override bool ShouldScaleInMultiplayer => false;

    protected override object? InitInternalData() => new AuraData { TurnsRemaining = DurationTurns };

    /// <summary>
    /// 玩家回合开始时，持有者获得格挡。
    /// 完全照抄原版 RampartPower 的守卫（排除玩家额外回合的边界情况）。
    /// </summary>
    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (side != CombatSide.Player || CombatManager.Instance.PlayersTakingExtraTurn.Count > 0)
        {
            return;
        }
        if (Owner == null || !Owner.IsAlive)
        {
            return;
        }
        // 每回合每单位分别掷骰：1d4+3（种子 RNG，多人/SL 一致）
        int block = Dice.Roll(combatState, 1, 4, 3);
        await CreatureCmd.GainBlock(Owner, block, ValueProp.Unpowered, null);
    }

    /// <summary>
    /// 敌方回合结束时递减持续时长，归零后移除。
    /// </summary>
    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (side != CombatSide.Enemy) return;

        AuraData data = GetInternalData<AuraData>();
        data.TurnsRemaining--;
        if (data.TurnsRemaining <= 0)
        {
            await PowerCmd.Remove(this);
        }
    }

    /// <summary>
    /// 每次被施加（含重复施加）都把持续时长刷新为 2。
    /// 注意：施加发生在敌方回合内，所以"施加当回合末"的那次递减需要跳过 ——
    /// 时序为：施加(回合N) → 玩家回合N给格挡 → 敌方回合N末递减到1 → 玩家回合N+1给格挡 → 敌方回合N+1末递减到0并移除。
    /// 共给 2 次格挡，正好是"持续 2 回合"。
    /// </summary>
    public override async Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        GetInternalData<AuraData>().TurnsRemaining = DurationTurns;
        await base.AfterApplied(applier, cardSource);
    }
}
