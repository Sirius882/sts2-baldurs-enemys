using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Audio;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Nodes.Combat;
using baldurs_enemys.src.Core;
using baldurs_enemys.src.Powers;
using MegaCrit.Sts2.Core.Helpers;

namespace baldurs_enemys.src.Monsters;

/// <summary>
/// 梦境守护者（A10 血量 200，低进阶 190）。君主 Boss 战的护卫（附带爪牙，MinionPower）。
///
/// 原稿状态机：每回合，从下列 debuff 中**随机**选择一个，给所有玩家施加，都只持续 2 回合：
/// - 魂缚锁链 3 层（原版女王 boss 战的 ChainsOfBindingPower）
/// - -5 力量
/// - -3 敏捷
/// - 恶咒 + 抑制（原版三骑士精英战的 HexPower + DampenPower）
///
/// 随机选择走种子 RNG 掷 1d4（在招式执行体内掷，意图恒为减益、无需预测一致性处理）；
/// "持续 2 回合"由 TimedDebuffExpiryPower（定时消散）挂倒计时到期移除。
/// </summary>
public sealed class DreamGuardian : BaldurMonsterModel
{
    public override int MinInitialHp => AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 200, 190);

    public override int MaxInitialHp => MinInitialHp;

    public override DamageSfxType TakeDamageSfxType => DamageSfxType.Magic;

    /// <summary>美术：mod 自带立绘（baldurs_enemys/images/monsters/dream_guardian.png）。</summary>
    public override NCreatureVisuals? CreateCustomVisuals() =>
        BaldurVisuals.Create("res://baldurs_enemys/images/monsters/dream_guardian.png", 300);

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        // 附带爪牙（照抄油桶的 MinionPower 挂法）
        await PowerCmd.Apply<MinionPower>(new ThrowingPlayerChoiceContext(), base.Creature, 1m, base.Creature, null);
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        MoveState nightmare = new("NIGHTMARE", NightmareMove, new DebuffIntent());
        nightmare.FollowUpState = nightmare;
        List<MonsterState> states = [nightmare];
        return new MonsterMoveStateMachine(states, nightmare);
    }

    protected override bool ShouldShowMoveInBestiary(string moveStateId) => true;

    private async Task NightmareMove(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.TriggerAnim(base.Creature, "Cast", 0.25f);
        ICombatState? combatState = base.Creature.CombatState;
        if (combatState == null)
        {
            return;
        }
        int roll = Dice.Roll(combatState, 1, 4, 0);
        PlayerChoiceContext choiceContext = new ThrowingPlayerChoiceContext();
        foreach (Creature player in combatState.PlayerCreatures)
        {
            if (!player.IsAlive) continue;
            switch (roll)
            {
                case 1:
                    // 魂缚锁链 3 层
                    await TimedDebuffExpiryPower.ApplyTimedDebuff<ChainsOfBindingPower>(
                        choiceContext, player, 3m, base.Creature, 2);
                    break;
                case 2:
                    // -5 力量
                    await TimedDebuffExpiryPower.ApplyTimedDebuff<StrengthPower>(
                        choiceContext, player, -5m, base.Creature, 2);
                    break;
                case 3:
                    // -3 敏捷
                    await TimedDebuffExpiryPower.ApplyTimedDebuff<DexterityPower>(
                        choiceContext, player, -3m, base.Creature, 2);
                    break;
                default:
                    // 恶咒 + 抑制
                    await TimedDebuffExpiryPower.ApplyTimedDebuff<HexPower>(
                        choiceContext, player, 1m, base.Creature, 2);
                    await TimedDebuffExpiryPower.ApplyTimedDebuff<DampenPower>(
                        choiceContext, player, 1m, base.Creature, 2);
                    break;
            }
        }
    }
}
