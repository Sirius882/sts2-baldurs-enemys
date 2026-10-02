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
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Nodes.Combat;
using baldurs_enemys.src.Core;
using baldurs_enemys.src.Powers;
using MegaCrit.Sts2.Core.Models.Powers;

namespace baldurs_enemys.src.Monsters;

/// <summary>
/// 蝙蝠（A10 血量 30，低进阶 28）。卡扎多尔精英战的杂兵。
///
/// 原稿状态机：
///   while(true)
///   {
///       i = randint(1,3);   // 种子 RNG
///       if i==1  攻击3*2;
///       if i==2  攻击4*1;
///       if i==3  debuff 给所有玩家-1力量-1敏捷，持续2回合;
///   }
///
/// 随机意图实现口径（预测安全）：掷骰发生在**上一招的执行体内**并存入 _choice，
/// 分支条件只读取 _choice（纯函数，可被预测重复求值而不消耗 RNG）——
/// 意图展示与实际执行读到的是同一个已掷出的值。
/// 首招的 _choice 在进房时预掷（AfterAddedToRoom，战斗建立的确定性路径）。
/// </summary>
public sealed class Bat : CustomMonsterModel
{
    public override int MinInitialHp => AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 30, 28);

    public override int MaxInitialHp => MinInitialHp;

    public override DamageSfxType TakeDamageSfxType => DamageSfxType.Fur;

    /// <summary>美术：mod 自带立绘（baldurs_enemys/images/monsters/bat.png）。</summary>
    public override NCreatureVisuals? CreateCustomVisuals() =>
        BaldurVisuals.Create("res://baldurs_enemys/images/monsters/bat.png", 170);

    private int _choice = 1;

    private int Choice
    {
        get => _choice;
        set
        {
            AssertMutable();
            _choice = value;
        }
    }

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        // 首招预掷（战斗建立的确定性路径，不落在玩家操作之后）
        ICombatState? combatState = base.Creature.CombatState;
        if (combatState != null)
        {
            Choice = Dice.Roll(combatState, 1, 3, 0);
        }
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        ConditionalBranchState branch = new("BRANCH");
        MoveState doubleBite = new("DOUBLE_BITE", DoubleBiteMove, new MultiAttackIntent(Balthazar.ScaleDeadly(3), 2));
        MoveState bite = new("BITE", BiteMove, new SingleAttackIntent(Balthazar.ScaleDeadly(4)));
        MoveState screech = new("SCREECH", ScreechMove, new DebuffIntent());

        branch.AddState(doubleBite, () => Choice == 1);
        branch.AddState(bite, () => Choice == 2);
        branch.AddState(screech, () => true);

        doubleBite.FollowUpState = branch;
        bite.FollowUpState = branch;
        screech.FollowUpState = branch;

        List<MonsterState> states = [branch, doubleBite, bite, screech];
        return new MonsterMoveStateMachine(states, branch);
    }

    protected override bool ShouldShowMoveInBestiary(string moveStateId) =>
        moveStateId is "DOUBLE_BITE" or "BITE" or "SCREECH";

    /// <summary>每招执行体末尾为下一回合预掷（种子 RNG）。</summary>
    private async Task RerollForNextTurn()
    {
        ICombatState? combatState = base.Creature.CombatState;
        if (combatState != null)
        {
            Choice = Dice.Roll(combatState, 1, 3, 0);
        }
    }

    private async Task DoubleBiteMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(Balthazar.ScaleDeadly(3)).WithHitCount(2).FromMonster(this)
            .OnlyPlayAnimOnce()
            .WithAttackerAnim("Attack", 0.2f)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(null);
        await RerollForNextTurn();
    }

    private async Task BiteMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(Balthazar.ScaleDeadly(4)).FromMonster(this)
            .WithAttackerAnim("Attack", 0.3f)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(null);
        await RerollForNextTurn();
    }

    /// <summary>尖啸：所有玩家 -1 力量 -1 敏捷，持续 2 回合（定时消散辅助 power 到期移除）。</summary>
    private async Task ScreechMove(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.TriggerAnim(base.Creature, "Cast", 0.25f);
        ICombatState? combatState = base.Creature.CombatState;
        if (combatState == null)
        {
            return;
        }
        PlayerChoiceContext choiceContext = new ThrowingPlayerChoiceContext();
        foreach (Creature player in combatState.PlayerCreatures)
        {
            if (!player.IsAlive) continue;
            await TimedDebuffExpiryPower.ApplyTimedDebuff<StrengthPower>(
                choiceContext, player, -1m, base.Creature, TimedDebuffExpiryRounds);
            await TimedDebuffExpiryPower.ApplyTimedDebuff<DexterityPower>(
                choiceContext, player, -1m, base.Creature, TimedDebuffExpiryRounds);
        }
        await RerollForNextTurn();
    }

    /// <summary>尖啸的减益时长（回合）。</summary>
    private const int TimedDebuffExpiryRounds = 2;
}
