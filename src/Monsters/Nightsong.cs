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

namespace baldurs_enemys.src.Monsters;

/// <summary>
/// 暗夜之歌（A10 血量 200，低进阶 190 = floor(200/1.05)）。巴萨扎尔精英战的三号位。
///
/// 意图状态机：while(true) 自己眩晕 —— 一个没有行动能力的"场景物"（与油桶同款写法：
/// onPerform 为空 + StunIntent）。
///
/// 附带 power（AfterAddedToRoom 施加）：
/// - 可被拾取·大：需要两次「拾取」才触发拾取效果（Counter = 2，逻辑在拾取卡里）；
/// - 塞伦涅的庇佑：被拾取成功时清除所有玩家的死云术（标记，效果在拾取卡里）。
///
/// 被拾取成功后变成遗物「艾琳女士」（见 DameAylin）。
/// 美术占位：原版神秘骑士（mysterious_knight，甲胄人形体量感接近）。
/// </summary>
public sealed class Nightsong : BaldurMonsterModel
{
    public override int MinInitialHp => AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 200, 190);

    public override int MaxInitialHp => MinInitialHp;

    public override DamageSfxType TakeDamageSfxType => DamageSfxType.Armor;

    /// <summary>美术：mod 自带立绘（baldurs_enemys/images/monsters/nightsong.png）。</summary>
    public override NCreatureVisuals? CreateCustomVisuals() =>
        BaldurVisuals.Create("res://baldurs_enemys/images/monsters/nightsong.png", 320);

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        await PowerCmd.Apply<PickpocketableBigPower>(
            new ThrowingPlayerChoiceContext(), base.Creature, PickpocketableBigPower.PickupsRequired, base.Creature, null);
        await PowerCmd.Apply<SelunesBlessingPower>(
            new ThrowingPlayerChoiceContext(), base.Creature, 1m, base.Creature, null);
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        // 与油桶同款：自眩晕是"这回合不行动"，不是被外部眩晕
        MoveState idle = new("IDLE", IdleMove, new StunIntent());
        idle.FollowUpState = idle;
        List<MonsterState> states = [idle];
        return new MonsterMoveStateMachine(states, idle);
    }

    private Task IdleMove(IReadOnlyList<Creature> targets) => Task.CompletedTask;
}
