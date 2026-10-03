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
using MegaCrit.Sts2.Core.Models.Powers;
using baldurs_enemys.src.Core;

namespace baldurs_enemys.src.Powers;

/// <summary>
/// 「勇气灵光」：玩家回合开始时掷 1d4-1，≥1 则获得对应层数的活力，持续 2 回合。
/// 明萨拉对全体友方单位（包括她自己）施加。
///
/// 「活力」是原版真实存在的 power（<see cref="VigorPower"/>：持有者下一次有来源攻击额外加伤，命中后消耗），
/// 所以本 power 只负责"每回合掷骰发活力 + 管理持续时长"，不自己造加伤逻辑。
///
/// 掷骰口径（总则第5条）：每个持有者每回合分别掷 1d4-1，走种子 RNG（Niche 通道）；
/// 掷出 ≤0 视为失误，不发活力。Amount 只承载施加计数，不再承载数值 → <see cref="ShouldScaleInMultiplayer"/> 关闭。
/// </summary>
public sealed class ValorAuraPower : CustomPowerModel
{
    public const int ApplyAmount = 1;
    public const int DurationTurns = 2;

    private sealed class AuraData
    {
        public int TurnsRemaining;
    }

    public override PowerType Type => PowerType.Buff;

    /// <summary>图标：mod 自制（images/powers/valor_aura.png 64x64 / big/valor_aura_big.png 256x256）。
    /// 程序化生成的原创符号化图标 ——「勇气灵光」：掷骰获得活力。
    /// 注意 power 图标**不做染色**（ICustomPower 只覆写路径），与 boss 地图图标的纯白要求不同。</summary>
    public override string? CustomPackedIconPath => "res://baldurs_enemys/images/powers/valor_aura.png";

    /// <summary>大图标（256x256，power 详情/图鉴用）。</summary>
    public override string? CustomBigIconPath => "res://baldurs_enemys/images/powers/big/valor_aura_big.png";

    public override PowerStackType StackType => PowerStackType.Counter;

    // 活力层数由每回合掷骰（1d4-1）决定，Amount 缩放不再适用
    public override bool ShouldScaleInMultiplayer => false;

    protected override object? InitInternalData() => new AuraData { TurnsRemaining = DurationTurns };

    /// <summary>
    /// 玩家回合开始时，给持有者发 1 层原版「活力」。
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
        // 每回合每单位分别掷骰：1d4-1，掷出 ≤0 视为失误、不发活力
        int vigor = Dice.Roll(combatState, 1, 4, -1);
        if (vigor <= 0)
        {
            return;
        }
        await PowerCmd.Apply<VigorPower>(new ThrowingPlayerChoiceContext(), Owner, vigor, Owner, null);
    }

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

    public override async Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        GetInternalData<AuraData>().TurnsRemaining = DurationTurns;
        await base.AfterApplied(applier, cardSource);
    }
}
