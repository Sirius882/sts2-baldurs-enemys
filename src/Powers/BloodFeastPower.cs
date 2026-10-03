using System.Threading.Tasks;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using baldurs_enemys.src.Core;

namespace baldurs_enemys.src.Powers;

/// <summary>
/// 「鲜血盛宴」（卡扎多尔，由其招式主动获得）：
/// - 卡扎多尔的攻击造成**未被格挡**的伤害时，等量转化为对自己的治疗；
/// - 每当一个单位死亡（任何阵营），卡扎多尔回血 2d8。
///
/// 吸血口径：挂在 <see cref="AfterDamageGiven"/>（施加方钩子），仅结算
/// <see cref="ValueProp.Move"/>（敌方攻击伤害的标志位）且 UnblockedDamage &gt; 0 的部分。
/// 溢出治疗由「血护甲」转化为格挡（两 power 的联动在卡扎多尔身上同时成立）。
/// </summary>
public sealed class BloodFeastPower : CustomPowerModel
{
    public override PowerType Type => PowerType.Buff;

    /// <summary>图标：mod 自制（images/powers/blood_feast.png 64x64 / big/blood_feast_big.png 256x256）。
    /// 程序化生成的原创符号化图标 ——「鲜血盛宴」：未格挡伤害转治疗，单位死亡回血。
    /// 注意 power 图标**不做染色**（ICustomPower 只覆写路径），与 boss 地图图标的纯白要求不同。</summary>
    public override string? CustomPackedIconPath => "res://baldurs_enemys/images/powers/blood_feast.png";

    /// <summary>大图标（256x256，power 详情/图鉴用）。</summary>
    public override string? CustomBigIconPath => "res://baldurs_enemys/images/powers/big/blood_feast_big.png";

    public override PowerStackType StackType => PowerStackType.Single;

    public override async Task AfterDamageGiven(PlayerChoiceContext choiceContext, Creature? dealer, DamageResult result, ValueProp props, Creature target, CardModel? cardSource)
    {
        if (dealer != Owner || result.UnblockedDamage <= 0)
        {
            return;
        }
        // 仅攻击伤害（ValueProp.Move = 攻击牌与敌人攻击）；死亡引爆之类的无来源伤害不吸血
        if (!props.HasFlag(ValueProp.Move))
        {
            return;
        }
        if (Owner == null || !Owner.IsAlive)
        {
            return;
        }
        await CreatureCmd.Heal(Owner, result.UnblockedDamage);
    }

    public override async Task AfterDeath(PlayerChoiceContext choiceContext, Creature creature, bool wasRemovalPrevented, float deathAnimLength)
    {
        if (wasRemovalPrevented || creature == Owner)
        {
            return;
        }
        if (Owner == null || !Owner.IsAlive)
        {
            return;
        }
        ICombatState? combatState = Owner.CombatState;
        if (combatState == null || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }
        await CreatureCmd.Heal(Owner, Dice.Roll(combatState, 2, 8, 0));
    }
}
