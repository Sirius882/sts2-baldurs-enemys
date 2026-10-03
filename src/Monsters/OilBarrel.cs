using System.Collections.Generic;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Audio;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Nodes.Combat;
using baldurs_enemys.src.Powers;

namespace baldurs_enemys.src.Monsters;

/// <summary>
/// 油桶（血量 5，进阶不变）。
///
/// "一个没有生命、没有自我意识，也没有手脚的油桶，它当然只是在那杵着，什么都不会做。"
///
/// 意图状态机：while{ 自己眩晕 } —— 实现为自循环的 StunIntent 节点（同地精·远程的"自己眩晕"写法）。
///
/// 附带三个 power：
/// - 爪牙：直接用原版 <see cref="MinionPower"/>，挂法照抄原版 GasBomb（在 AfterAddedToRoom 里 Apply）；
/// - 可被交互：<see cref="InteractablePower"/>；
/// - 死亡引爆：<see cref="DeathExplosionPower"/>。
///
/// 结构上直接以原版 GasBomb 为模板：同为"不会动的爆炸物"，ShouldFadeAfterDeath=false 留尸。
/// </summary>
public sealed class OilBarrel : BaldurMonsterModel
{
    /// <summary>血量 5，÷1.06 后四舍五入仍为 5 → 不随进阶变化。</summary>
    public override int MinInitialHp => 5;

    public override int MaxInitialHp => MinInitialHp;

    public override DamageSfxType TakeDamageSfxType => DamageSfxType.Magic;

    /// <summary>留尸：死亡后本体不淡出（照抄 GasBomb）。</summary>
    public override bool ShouldFadeAfterDeath => false;

    /// <summary>
    /// 5 点血条在 150 点血条旁边会退化成一个点，压一下血条高度。
    /// 原版先例：DecimillipedeSegment 用 35f（血条基准 24f，负值会被钳制）。
    /// </summary>
    public override float HpBarSizeReduction => 10f;

    /// <summary>美术：mod 自带立绘（baldurs_enemys/images/monsters/oil_barrel.png）。
    /// 原先借用原版 gas_bomb 场景，现已换为mod 自有立绘。</summary>
    public override NCreatureVisuals? CreateCustomVisuals() =>
        BaldurVisuals.Create("res://baldurs_enemys/images/monsters/oil_barrel.png", 170);

    /// <summary>
    /// 爪牙必须在进房时挂（照抄原版 GasBomb），而不是预先写进遭遇 ——
    /// 这样动态生成的油桶也能正确获得爪牙。
    /// </summary>
    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        await PowerCmd.Apply<MinionPower>(new ThrowingPlayerChoiceContext(), base.Creature, 1m, base.Creature, null);
        await PowerCmd.Apply<InteractablePower>(new ThrowingPlayerChoiceContext(), base.Creature, 1m, base.Creature, null);
        await PowerCmd.Apply<DeathExplosionPower>(new ThrowingPlayerChoiceContext(), base.Creature, 1m, base.Creature, null);
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        MoveState idle = new("IDLE", IdleMove, new StunIntent());
        idle.FollowUpState = idle;
        List<MonsterState> states = [idle];
        return new MonsterMoveStateMachine(states, idle);
    }

    protected override bool ShouldShowMoveInBestiary(string moveStateId) => true;

    /// <summary>油桶什么都不会做。</summary>
    private Task IdleMove(IReadOnlyList<Creature> targets) => Task.CompletedTask;
}
