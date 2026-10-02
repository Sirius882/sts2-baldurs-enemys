using System.Collections.Generic;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Audio;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace baldurs_enemys.src.Monsters;

/// <summary>
/// 骷髅：与原版「盛碗虫（石）」(BowlbugRock) 完全一样，就是换皮，全套照抄（设计稿原文）。
///
/// 数据/逻辑 1:1 复制自 GameBeta/src/Core/Models/Monsters/BowlbugRock.cs（2026-10-02 版）：
/// - 血量 A10 46~49 / 低进阶 45~48；头槌 16/15；
/// - 进场自带「失衡」(ImbalancedPower)：头槌后若自己失衡则眩晕一回合（DIZZY）；
/// - 美术：mod 自带立绘（baldurs_enemys/images/monsters/skeleton.png），
///   原先借用原版 bowlbug_rock 场景 + Spine 皮肤("rock") 的占位写法已随之移除
///   （静态贴图无 Spine，SetupSkins 覆写一并删除）。
/// 音效沿用原版 workbug_rock 事件与 Insect 受击音（占位）。
/// </summary>
public sealed class Skeleton : CustomMonsterModel
{
    private const string _stunTrigger = "Stun";

    private bool _isOffBalance;

    private const string _stunSfx = "event:/sfx/enemy/enemy_attacks/workbug_rock/workbug_rock_stun";

    public override int MinInitialHp => AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 46, 45);

    public override int MaxInitialHp => AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 49, 48);

    public static int HeadbuttDamage => AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 16, 15);

    public bool IsOffBalance
    {
        get
        {
            return _isOffBalance;
        }
        set
        {
            AssertMutable();
            _isOffBalance = value;
        }
    }

    public override string DeathSfx => "event:/sfx/enemy/enemy_attacks/workbug_rock/workbug_rock_die";

    protected override string AttackSfx => "event:/sfx/enemy/enemy_attacks/workbug_rock/workbug_rock_attack";

    public override DamageSfxType TakeDamageSfxType => DamageSfxType.Insect;

    /// <summary>美术：mod 自带立绘（baldurs_enemys/images/monsters/skeleton.png）。</summary>
    public override NCreatureVisuals? CreateCustomVisuals() =>
        BaldurVisuals.Create("res://baldurs_enemys/images/monsters/skeleton.png", 280);

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        await PowerCmd.Apply<ImbalancedPower>(new ThrowingPlayerChoiceContext(), base.Creature, 1m, base.Creature, null);
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        MoveState headbutt = new("HEADBUTT", HeadbuttMove, new SingleAttackIntent(HeadbuttDamage));
        MoveState dizzy = new("DIZZY", DizzyMove, new StunIntent());
        ConditionalBranchState postHeadbutt = (ConditionalBranchState)(headbutt.FollowUpState = new ConditionalBranchState("POST_HEADBUTT"));
        dizzy.FollowUpState = headbutt;
        postHeadbutt.AddState(dizzy, () => IsOffBalance);
        postHeadbutt.AddState(headbutt, () => !IsOffBalance);
        List<MonsterState> list = [dizzy, postHeadbutt, headbutt];
        return new MonsterMoveStateMachine(list, headbutt);
    }

    /// <summary>图鉴：头槌 + 眩晕（照抄原版把 Stun 也塞进图鉴列表的做法）。</summary>
    protected override bool ShouldShowMoveInBestiary(string moveStateId) =>
        moveStateId is "HEADBUTT" or "DIZZY";

    private async Task HeadbuttMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(HeadbuttDamage).FromMonster(this).WithAttackerAnim("Attack", 0.3f)
            .WithAttackerFx(null, AttackSfx)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(null);
        if (IsOffBalance)
        {
            await Stun();
        }
    }

    private async Task Stun()
    {
        SfxCmd.Play(_stunSfx);
        await CreatureCmd.TriggerAnim(base.Creature, _stunTrigger, 0.6f);
        await CreatureCmd.Stun(base.Creature, "DIZZY");
    }

    private async Task DizzyMove(IReadOnlyList<Creature> targets)
    {
        IsOffBalance = false;
        await CreatureCmd.TriggerAnim(base.Creature, "Unstun", 0.6f);
    }
}
