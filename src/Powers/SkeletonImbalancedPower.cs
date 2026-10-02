using System.Threading.Tasks;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using baldurs_enemys.src.Monsters;

namespace baldurs_enemys.src.Powers;

/// <summary>
/// 「失衡」骷髅专用版（设计稿：「与原版游戏的盛碗虫（石）完全一样，就是换皮，直接全套照抄」）。
///
/// 为什么不能直接用原版 <see cref="ImbalancedPower"/>：
/// 该power 的 AfterDamageGiven 内部按 `if (!(Owner.Monster is BowlbugRock))` 分流——
/// 是 BowlbugRock 就置IsOffBalance = true，不是则退化为`CreatureCmd.Stun(Owner)`。
/// 骷髅虽已照抄了 IsOffBalance 状态机与 DIZZY 招式（原版 BowlbugRock 同样**没有**覆写
/// AfterDamageGiven，全靠这个类型判断分流），但类型判断只认 BowlbugRock，
/// 于是骷髅会走进Stun 分支 → 被外部真正眩晕，与原版「自己头槌后失衡、下一回合自己发呆」的
/// 机制不等价，且power 图鉴上显示为原版「失衡」的说明、行为却不对。
///
/// 本类把同一段逻辑对骷髅类型再做一次，等价于「把 BowlbugRock 的换皮也照抄到位」。
/// </summary>
public sealed class SkeletonImbalancedPower : CustomPowerModel
{
    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Single;

    public override Task AfterDamageGiven(PlayerChoiceContext choiceContext, Creature? dealer,
        DamageResult result, ValueProp props, Creature target, CardModel? cardSource)
    {
        if (dealer != Owner || !result.WasFullyBlocked)
        {
            return Task.CompletedTask;
        }

        if (Owner.Monster is Skeleton skeleton)
        {
            Flash();
            skeleton.IsOffBalance = true;
        }

        return Task.CompletedTask;
    }
}
