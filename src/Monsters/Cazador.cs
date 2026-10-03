using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Audio;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Nodes.Combat;
using baldurs_enemys.src.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Models.Powers;

namespace baldurs_enemys.src.Monsters;

/// <summary>
/// 「吸血鬼领主」卡扎多尔·扎尔（A10 血量 220，低进阶 209）。第三层精英，两侧各一蝙蝠。
///
/// 原稿状态机：
///   强化：获得鲜血盛宴;
///   for i in range(2) { 攻击16*1; 攻击3*3; }
///   强化：获得3力量;
///   while(true) { 攻击16*1; 攻击3*3; 强化获得1力量; }
///
/// 开场自带「血护甲」（溢出治疗转格挡 + 格挡跨回合保留）；
/// 「鲜血盛宴」由第一招主动获得（攻击吸血 + 任意单位死亡回血 2d8）。
/// </summary>
public sealed class Cazador : BaldurMonsterModel
{
    public override int MinInitialHp => AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 220, 209);

    public override int MaxInitialHp => MinInitialHp;

    public override DamageSfxType TakeDamageSfxType => DamageSfxType.Fur;

    /// <summary>美术：mod 自带立绘（baldurs_enemys/images/monsters/cazador.png）。</summary>
    public override NCreatureVisuals? CreateCustomVisuals() =>
        BaldurVisuals.Create("res://baldurs_enemys/images/monsters/cazador.png", 320);

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        // 开场自带：血护甲
        await PowerCmd.Apply<BloodArmorPower>(new ThrowingPlayerChoiceContext(), base.Creature, 1m, base.Creature, null);
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        MoveState feast = new("FEAST", FeastMove, new BuffIntent());
        MoveState bite1 = new("BITE1", BiteMove, new SingleAttackIntent(Balthazar.ScaleDeadly(16)));
        MoveState swarm1 = new("SWARM1", SwarmMove, new MultiAttackIntent(Balthazar.ScaleDeadly(3), 3));
        MoveState bite2 = new("BITE2", BiteMove, new SingleAttackIntent(Balthazar.ScaleDeadly(16)));
        MoveState swarm2 = new("SWARM2", SwarmMove, new MultiAttackIntent(Balthazar.ScaleDeadly(3), 3));
        MoveState empower3 = new("EMPOWER3", Empower3Move, new BuffIntent());
        MoveState biteLoop = new("BITE_LOOP", BiteMove, new SingleAttackIntent(Balthazar.ScaleDeadly(16)));
        MoveState swarmLoop = new("SWARM_LOOP", SwarmMove, new MultiAttackIntent(Balthazar.ScaleDeadly(3), 3));
        MoveState empower1 = new("EMPOWER1", Empower1Move, new BuffIntent());

        feast.FollowUpState = bite1;
        bite1.FollowUpState = swarm1;
        swarm1.FollowUpState = bite2;
        bite2.FollowUpState = swarm2;
        swarm2.FollowUpState = empower3;
        empower3.FollowUpState = biteLoop;
        biteLoop.FollowUpState = swarmLoop;
        swarmLoop.FollowUpState = empower1;
        empower1.FollowUpState = biteLoop;

        List<MonsterState> states =
        [
            feast, bite1, swarm1, bite2, swarm2, empower3, biteLoop, swarmLoop, empower1
        ];
        return new MonsterMoveStateMachine(states, feast);
    }

    protected override bool ShouldShowMoveInBestiary(string moveStateId) =>
        moveStateId is "FEAST" or "BITE1" or "BITE2" or "BITE_LOOP" or "SWARM1" or "SWARM2" or "SWARM_LOOP"
            or "EMPOWER3" or "EMPOWER1";

    private async Task FeastMove(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.TriggerAnim(base.Creature, "Cast", 0.25f);
        await PowerCmd.Apply<BloodFeastPower>(new ThrowingPlayerChoiceContext(), base.Creature, 1m, base.Creature, null);
    }

    private async Task BiteMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(Balthazar.ScaleDeadly(16)).FromMonster(this)
            .WithAttackerAnim("Attack", 0.3f)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(null);
    }

    private async Task SwarmMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(Balthazar.ScaleDeadly(3)).WithHitCount(3).FromMonster(this)
            .OnlyPlayAnimOnce()
            .WithAttackerAnim("Attack", 0.2f)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(null);
    }

    private async Task Empower3Move(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.TriggerAnim(base.Creature, "Cast", 0.25f);
        await PowerCmd.Apply<StrengthPower>(new ThrowingPlayerChoiceContext(), base.Creature, 3m, base.Creature, null);
    }

    private async Task Empower1Move(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.TriggerAnim(base.Creature, "Cast", 0.25f);
        await PowerCmd.Apply<StrengthPower>(new ThrowingPlayerChoiceContext(), base.Creature, 1m, base.Creature, null);
    }
}
