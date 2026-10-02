using System.Collections.Generic;
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
using baldurs_enemys.src.Monsters;

namespace baldurs_enemys.src.Powers;

/// <summary>
/// 「过载熔毁」（钢铁卫士，进场自带；死亡爆炸逻辑同油桶）：
/// - 被打死时，对玩家方造成 2d8 点伤害，对怪物方造成 2d20 点伤害
///   （各方各掷一次，整方同值；2d20 部分不含已死的自己）；
/// - 死于自爆意图不触发（自爆动作执行前置 <see cref="SuppressExplosion"/>）；
/// - 当受到伤害使得剩余血量不大于 20 时，立即把意图变为自爆 16
///   （原版 StunInternal 的 SetMoveImmediate 同款机制；被眩晕打断则顺延一回合）。
/// </summary>
public sealed class OverloadMeltdownPower : CustomPowerModel
{
    /// <summary>自爆血量阈值。</summary>
    public const int SelfDestructHpThreshold = 20;

    private bool _hasExploded;
    private bool _selfDestructArmed;

    /// <summary>由自爆动作在击杀前置位：本次死亡是自爆，不应再触发熔毁。</summary>
    public bool SuppressExplosion { get; set; }

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    public override async Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature target, DamageResult result, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (target != Owner || _selfDestructArmed || Owner == null || !Owner.IsAlive)
        {
            return;
        }
        if (Owner.CurrentHp > SelfDestructHpThreshold)
        {
            return;
        }
        _selfDestructArmed = true;
        if (Owner.Monster is SteelWatcher watcher)
        {
            watcher.EnterSelfDestruct();
        }
    }

    public override async Task AfterDeath(PlayerChoiceContext choiceContext, Creature creature, bool wasRemovalPrevented, float deathAnimLength)
    {
        if (wasRemovalPrevented || creature != Owner || SuppressExplosion || _hasExploded)
        {
            return;
        }
        if (CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }
        ICombatState? combatState = Owner.CombatState;
        if (combatState == null)
        {
            return;
        }
        _hasExploded = true;

        // 对玩家方：掷一次 2d8，整方同值
        int playerDamage = Dice.Roll(combatState, 2, 8, 0);
        foreach (Creature player in combatState.PlayerCreatures)
        {
            if (!player.IsAlive) continue;
            await CreatureCmd.Damage(choiceContext, player, playerDamage, ValueProp.Unpowered, null, null);
        }

        // 对怪物方：掷一次 2d20，整方同值（不含已死的自己）
        int monsterDamage = Dice.Roll(combatState, 2, 20, 0);
        foreach (Creature other in combatState.HittableEnemies)
        {
            if (!other.IsAlive || other == Owner) continue;
            await CreatureCmd.Damage(choiceContext, other, monsterDamage, ValueProp.Unpowered, null, null);
        }
    }
}
