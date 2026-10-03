using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.ValueProps;
using baldurs_enemys.src.Core;
using baldurs_enemys.src.Patches;

namespace baldurs_enemys.src.Powers;

/// <summary>
/// 「死亡引爆」（2026-10-03 骰子版，设计稿标注「有改动」）：被**爪牙效果**和卡牌「交互」以外的方式击败时，
/// 对所有玩家各造成 1d4+7 点伤害，对所有敌人各造成其最大生命值 ×1/(1d8+1) 的伤害
/// （每个受伤的单位逐个掷骰，各自受到的伤害不一定相同）。
///
/// 实现要点（均按原版实测规则钉死）：
/// 1. 必须挂 <see cref="AbstractModel.AfterDeath"/> 而不是 BeforeDeath（BeforeDeath 在死亡被防住时也执行）。
/// 2. 「爪牙效果」击败不触发：主要敌人死亡后引擎在其死亡处理帧内 await Kill(teammates) 连带处决爪牙。
///    由 <see cref="PrimaryEnemyDeathTrackerPatch"/> 的深度计数识别 —— 连带处决时深度 &gt; 0。
/// 3. 油桶连环爆炸**不受**该排除：B 桶死于 A 桶爆炸的伤害结算，不经过主要敌人的死亡处理帧 → 深度 0 → 照常引爆。
/// 4. 被「交互」杀死时不触发：交互卡在 Kill 之前置 SuppressExplosion = true。
/// 5. 骰子全部走 <see cref="Dice"/>（run 种子的 Niche 通道）；逐单位按 Players/Enemies 列表顺序掷骰。
/// 6. 伤害一律以 dealer: null 结算，爆炸来源靠 <see cref="DeathExplosionDamageSource"/> 的标志位识别。
///    原因：AfterDeath 时油桶 HP 必已被归零，而 CreatureCmd.Damage 开头有
///    `dealer != null &amp;&amp; dealer.IsDead` 早退，传自身会导致伤害被整段跳过（同源的
///    <see cref="OverloadMeltdownPower"/>「过载熔毁」一直用 null，因此不受影响）。
/// </summary>
public sealed class DeathExplosionPower : CustomPowerModel
{
    private bool _hasExploded;

    public override PowerType Type => PowerType.Buff;

    /// <summary>图标：mod 自制（images/powers/death_explosion.png 64x64 / big/death_explosion_big.png 256x256）。
    /// 程序化生成的原创符号化图标 ——「死亡引爆」：死亡时全体伤害。
    /// 注意 power 图标**不做染色**（ICustomPower 只覆写路径），与 boss 地图图标的纯白要求不同。</summary>
    public override string? CustomPackedIconPath => "res://baldurs_enemys/images/powers/death_explosion.png";

    /// <summary>大图标（256x256，power 详情/图鉴用）。</summary>
    public override string? CustomBigIconPath => "res://baldurs_enemys/images/powers/big/death_explosion_big.png";

    /// <summary>Single：层数隐藏且恒为 1，重复施加不会叠加。</summary>
    public override PowerStackType StackType => PowerStackType.Single;

    /// <summary>
    /// 由「交互」卡在击杀前置位：本次死亡是被交互的，不应引爆。
    /// </summary>
    public bool SuppressExplosion { get; set; }

    public override async Task AfterDeath(PlayerChoiceContext choiceContext, Creature target, bool wasRemovalPrevented, float deathAnimLength)
    {
        // 死亡被防住（如瓶中仙灵）→ 没有死，不炸
        if (wasRemovalPrevented) return;

        // 只在拥有者本人死亡时触发（AfterDeath 会对每个监听者触发，需过滤）
        if (target != Owner) return;

        // 被「交互」杀死 → 不炸
        if (SuppressExplosion) return;

        // 双保险：每个油桶整场只爆一次
        if (_hasExploded) return;

        // 战斗确实已经结束 → 不炸（兜底）
        if (CombatManager.Instance.IsOverOrEnding) return;

        // 被"爪牙效果"击败（正在主要敌人的死亡处理帧内被连带处决）→ 不炸
        if (PrimaryEnemyDeathTrackerPatch.PrimaryEnemyDeathDepth > 0) return;

        // 场上没有存活的主要敌人（战斗实际已赢）→ 不炸（兜底）
        if (!HasLivingPrimaryEnemy()) return;

        _hasExploded = true;

        Creature self = Owner;
        ICombatState combatState = self.CombatState!;

        // 视觉：借原版爆炸安瓿的同款火焰烟雾特效
        foreach (Creature viewer in combatState.PlayerCreatures.Concat(combatState.HittableEnemies))
        {
            NCombatRoom.Instance?.CombatVfxContainer.AddChildSafely(NFireSmokePuffVfx.Create(viewer));
        }
        await Cmd.CustomScaledWait(0.2f, 0.3f);

        // 整个伤害结算窗口内置来源标志位（当前无消费者，见 DeathExplosionDamageSource 的说明）。
        // 用 using（深度计数）而非 try/finally 布尔：链式引爆时 B 桶的 AfterDeath 会嵌套在
        // A 桶的循环内触发，布尔会在 B 桶清位后让 A 桶的剩余伤害丢失标记。
        using (DeathExplosionDamageSource.Enter())
        {
            // 第一段：对所有存活玩家**逐个**掷 1d4+7 并结算
            // dealer 必须传 null：引擎的 CreatureCmd.Damage 有一道 `dealer != null && dealer.IsDead`
            // 早退，而 AfterDeath 时油桶 HP 已被归零（先 LoseHpInternal 后 await AfterDeath），
            // 传自身会让伤害被整段跳过。来源识别改由标志位承担，见 DeathExplosionDamageSource。
            foreach (Creature player in combatState.PlayerCreatures)
            {
                if (!player.IsAlive) continue;
                int damage = Dice.Roll(combatState, 1, 4, 7);
                await CreatureCmd.Damage(choiceContext, player, damage, ValueProp.Unpowered, null, null, null);
            }

            // 第二段：对所有存活的其他敌人**逐个**掷 1d8+1 得到 d，造成最大生命值/d（向下取整）
            // 逐个结算；若因此击杀另一个油桶，其 AfterDeath 会在这里嵌套触发 → 形成用户定义的链式引爆
            List<Creature> others = combatState.HittableEnemies.Where(c => c != self).ToList();
            foreach (Creature other in others)
            {
                if (!other.IsAlive) continue;
                int denominator = Dice.Roll(combatState, 1, 8, 1);
                int damage = other.MaxHp / denominator;
                if (damage <= 0) continue;
                await CreatureCmd.Damage(choiceContext, other, damage, ValueProp.Unpowered, null, null, null);
            }
        }
    }

    /// <summary>
    /// 场上是否还有存活的"主要敌人"。
    /// 油桶因「爪牙」(MinionPower) 是 IsSecondaryEnemy，主要敌人全灭意味着战斗已经赢了。
    /// </summary>
    private bool HasLivingPrimaryEnemy()
    {
        ICombatState? combatState = Owner.CombatState;
        if (combatState == null) return false;
        return combatState.Enemies.Any(c => c.IsAlive && c.IsPrimaryEnemy);
    }
}
