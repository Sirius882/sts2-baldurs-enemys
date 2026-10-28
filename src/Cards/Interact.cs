using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Potions;
using MegaCrit.Sts2.Core.Models.Relics;
using baldurs_enemys.src.Core;
using baldurs_enemys.src.Powers;
using baldurs_enemys.src.Relics;

namespace baldurs_enemys.src.Cards;

/// <summary>
/// 「交互」：1 费，消耗 + 虚无，战斗内生成的临时牌。
///
/// 就像在博德之门 3 里，回合制下花费一个动作交互场地上的一件物品 ——
/// 塔 2 不存在玩家方和敌方以外的中立/无行为物体，所以改成交互敌人。
///
/// 注册进 TokenCardPool（原版 Shiv 同类）：Token 池只用于"效果生成"，从不出现在卡牌奖励/商店，
/// 所以既能被 ModelDb 解析到，又不会被玩家抽到。
///
/// 效果（对没有"可被交互"类power 的敌人使用则没有任何效果 —— 但仍然消耗能量与卡牌、
/// 仍然结束回合，与 BG3"花费一个动作"的语义一致）：
/// - 可被交互（油桶）：变成原版 aoe 爆炸药水（爆炸安瓿 ExplosiveAmpoule），敌人死亡；
/// - 可被交互·大（暗夜之歌）：第一次交互只递减计数（无其他效果）；第二次交互才真正触发：
///   获得遗物「艾琳女士」；若目标带塞伦涅的庇佑，则清除所有玩家的死云术；
/// - 人质（奥林 Boss 战）：变成原版遗物「布质果实」+「李家华夫饼」（人质死亡/被交互
///   同时触发奥林 +5 力量并进入杀戮者形态，见 BhaalsBlessingPower）；
/// - 博德安的巨人杀手（安苏精英战）：变成遗物「博德安的巨人杀手」（死亡/被交互同时
///   触发安苏的「被背叛」，见 BetrayedPower）；
/// - 大枭熊不可被交互；枭熊战仅小枭熊可被交互。
/// - 小枭熊（枭熊精英战）：立即死亡（force:true 绕过死亡防止），随后掷 1d20，&gt; 12
///   则获得原版遗物「佩尔的士兵」（PaelsLegion 宠物遗物，战斗中即刻召唤助战）；
/// - 无论哪种，成功触发交互时目标死亡都是无条件的（force:true 绕过死亡防止）。
/// </summary>
[Pool(typeof(TokenCardPool))]
public sealed class Interact : CustomCardModel
{
    private const int CardEnergyCost = 1;

    public Interact() : base(CardEnergyCost, CardType.Skill, CardRarity.Token, TargetType.AnyEnemy, showInCardLibrary: false)
    {
    }

    /// <summary>
    /// 卡面：mod 自带美术（baldurs_enemys/images/cards/interact.png），经BaseLib 的
    /// <c>CustomPortraitPath</c> 接入（会同时覆写 PortraitPngPath / Portrait / PortraitPath 三个 getter）。
    /// 规格对齐原版 colorless 卡图 1000×760（比例 50:38）。
    /// </summary>
    public override string? CustomPortraitPath => "res://baldurs_enemys/images/cards/interact.png";

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Exhaust,
        CardKeyword.Ethereal
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        Creature? target = cardPlay.Target;

        if (target != null)
        {
            // 人质 / 巨人杀手 / 小枭熊：按怪物类型分流（都带可被交互 power，
            // 必须在普通交互路径之前判定）。目标死亡本身会触发各自的 Boss 联动。
            if (target.Monster is global::baldurs_enemys.src.Monsters.Hostage)
            {
                await InteractWithHostage(target);
            }
            else if (target.Monster is Monsters.BalduransGiantslayer)
            {
                await InteractWithGiantslayer(target);
            }
            else if (target.Monster is Monsters.OwlbearCub)
            {
                await InteractWithOwlbearCub(target);
            }
            else
            {
                InteractableBigPower? big = target.GetPower<InteractableBigPower>();
                if (big != null)
                {
                    await InteractWithBig(target, big);
                }
                else if (target.HasPower<InteractablePower>())
                {
                    await InteractWithNormal(target);
                }
            }
        }

        // 只有战斗仍在进行才结束回合：交互目标死亡可能已触发胜利结算，
        // 此时再 EndTurn 会对一个已结束的回合做「准备结束」标记，没有意义。
        if (CombatManager.Instance.IsInProgress)
        {
            PlayerCmd.EndTurn(base.Owner, canBackOut: false);
        }
    }

    /// <summary>普通交互（油桶）： suppress 死亡引爆 → 变成爆炸安瓿 → 击杀。</summary>
    private async Task InteractWithNormal(Creature target)
    {
        // 先打标记：本次死亡是被交互的，不应触发「死亡引爆」
        // （死亡钩子不带击杀者/来源参数，只能由交互卡自行标记）
        DeathExplosionPower? explosion = target.GetPower<DeathExplosionPower>();
        if (explosion != null)
        {
            explosion.SuppressExplosion = true;
        }

        await PotionCmd.TryToProcure<ExplosiveAmpoule>(base.Owner);
        await CreatureCmd.Kill(target, force: true);
    }

    /// <summary>
    /// 交互人质（奥林 Boss 战）：变成原版遗物「布质果实」+「李家华夫饼」→ 击杀。
    /// 人质死亡/被交互触发奥林 +5 力量并进入杀戮者形态（BhaalsBlessingPower.AfterDeath）。
    /// </summary>
    private async Task InteractWithHostage(Creature target)
    {
        await RelicCmd.Obtain(ModelDb.Relic<LoomingFruit>().ToMutable(), base.Owner);
        await RelicCmd.Obtain(ModelDb.Relic<LeesWaffle>().ToMutable(), base.Owner);
        await CreatureCmd.Kill(target, force: true);
    }

    /// <summary>
    /// 交互博德安的巨人杀手（安苏精英战）：变成遗物「博德安的巨人杀手」→ 击杀。
    /// 巨人杀手死亡/被交互触发安苏的「被背叛」（BetrayedPower.AfterDeath）。
    /// </summary>
    private async Task InteractWithGiantslayer(Creature target)
    {
        await RelicCmd.Obtain(ModelDb.Relic<BalduransGiantslayer>().ToMutable(), base.Owner);
        await CreatureCmd.Kill(target, force: true);
    }

    /// <summary>
    /// 交互小枭熊（枭熊精英战）：立即死亡 → 掷 1d20，&gt; 12 获得原版遗物「佩尔的士兵」。
    /// 顺序严格按设计稿「立即死亡。然后掷 1d20」：先 Kill 再掷骰。
    /// 必须在 Kill 之前把 combatState 取引用：目标死亡后其 CombatState 可能已被置空。
    /// </summary>
    private async Task InteractWithOwlbearCub(Creature target)
    {
        ICombatState? combatState = target.CombatState;

        await CreatureCmd.Kill(target, force: true);

        if (combatState == null)
        {
            return;
        }

        if (Dice.Roll(combatState, 1, 20, 0) > Monsters.OwlbearCub.InteractRewardThreshold)
        {
            // 原版 Ancient 宠物遗物：战斗中获得会立刻把「佩尔的士兵」召唤到玩家侧助战
            // （PaelsLegion.AfterObtained 内会判断 IsInProgress 并 SummonPet）。
            await RelicCmd.Obtain(ModelDb.Relic<PaelsLegion>().ToMutable(), base.Owner);
        }
    }

    /// <summary>
    /// 大型交互（暗夜之歌）：两次交互才触发。第一次只递减计数；
    /// 第二次：授予遗物「艾琳女士」→ 塞伦涅的庇佑清死云术 → 击杀。
    /// </summary>
    private async Task InteractWithBig(Creature target, InteractableBigPower big)
    {
        if (big.Amount > 1)
        {
            // 第一次交互：只消耗一层计数，不产生其他效果
            await PowerCmd.Decrement(big);
            return;
        }

        // 第二次交互：真正触发
        await RelicCmd.Obtain(ModelDb.Relic<DameAylin>().ToMutable(), base.Owner);

        // 塞伦涅的庇佑：被交互时清除所有玩家身上的死云术
        if (target.HasPower<SelunesBlessingPower>())
        {
            List<Creature> players = target.CombatState?.Players
                .Select(p => p.Creature)
                .Where(c => c != null && c.HasPower<DeathCloudPower>())
                .ToList() ?? new List<Creature>();
            foreach (Creature player in players)
            {
                await PowerCmd.Remove<DeathCloudPower>(player);
            }
        }

        await CreatureCmd.Kill(target, force: true);
    }
}
