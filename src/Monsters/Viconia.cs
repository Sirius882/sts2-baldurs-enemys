using System;
using System.Collections.Generic;
using System.Linq;
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
using baldurs_enemys.src.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace baldurs_enemys.src.Monsters;

/// <summary>
/// 「莎尔的女院长」维康妮亚（A10 血量 180，低进阶 171）。第三层精英，两侧各一油桶。
///
/// 原稿状态机：
///   while(true) { 攻击5*2; 强化：暗影帷幕(刷新至3层); if(任一侧油桶存活) 攻击7*1; else 强化获得2力量; }
///
/// 设计说明：暗影帷幕 = BG3 悲伤之屋的黑暗遮蔽（掷 1d4 闪避，见 ShroudVeilPower）；
/// 两侧油桶是明牌解法——油桶爆炸是未被格挡的无来源伤害，绕过帷幕直接炸掉大量血量。
/// </summary>
public sealed class Viconia : BaldurMonsterModel
{
    public override int MinInitialHp => AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 180, 171);

    public override int MaxInitialHp => MinInitialHp;

    public override DamageSfxType TakeDamageSfxType => DamageSfxType.Magic;

    /// <summary>美术：mod 自带立绘（baldurs_enemys/images/monsters/viconia.png）。
    /// 原先借用原版 damp_cultist 场景，现已换为 mod 自有立绘。</summary>
    public override NCreatureVisuals? CreateCustomVisuals() =>
        BaldurVisuals.Create("res://baldurs_enemys/images/monsters/viconia.png", 320);

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        MoveState strike = new("STRIKE", StrikeMove, new MultiAttackIntent(Balthazar.ScaleDeadly(5), 2));
        MoveState veil = new("VEIL", VeilMove, new BuffIntent());
        ConditionalBranchState branchOil = new("BRANCH_OIL");
        MoveState strikeSingle = new("STRIKE_SINGLE", StrikeSingleMove, new SingleAttackIntent(Balthazar.ScaleDeadly(7)));
        MoveState empower = new("EMPOWER", EmpowerMove, new BuffIntent());

        strike.FollowUpState = veil;
        veil.FollowUpState = branchOil;
        branchOil.AddState(strikeSingle, OilBarrelAlive);
        branchOil.AddState(empower, () => true);
        strikeSingle.FollowUpState = strike;
        empower.FollowUpState = strike;

        List<MonsterState> states = [strike, veil, branchOil, strikeSingle, empower];
        return new MonsterMoveStateMachine(states, strike);
    }

    protected override bool ShouldShowMoveInBestiary(string moveStateId) =>
        moveStateId is "STRIKE" or "VEIL" or "STRIKE_SINGLE" or "EMPOWER";

    /// <summary>「任一侧油桶存活」。</summary>
    private bool OilBarrelAlive()
    {
        ICombatState? combatState = base.Creature.CombatState;
        return combatState != null && combatState.Enemies.Any(c => c.Monster is OilBarrel && c.IsAlive);
    }

    private async Task StrikeMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(Balthazar.ScaleDeadly(5)).WithHitCount(2).FromMonster(this)
            .OnlyPlayAnimOnce()
            .WithAttackerAnim("Attack", 0.3f)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(null);
    }

    /// <summary>暗影帷幕：施加/刷新至 3 层（ApplyInternal SetAmount 语义即"刷新"）。</summary>
    private async Task VeilMove(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.TriggerAnim(base.Creature, "Cast", 0.25f);
        await PowerCmd.Apply<ShroudVeilPower>(
            new ThrowingPlayerChoiceContext(), base.Creature, ShroudVeilPower.ApplyAmount, base.Creature, null);
    }

    private async Task StrikeSingleMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(Balthazar.ScaleDeadly(7)).FromMonster(this)
            .WithAttackerAnim("Attack", 0.3f)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(null);
    }

    private async Task EmpowerMove(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.TriggerAnim(base.Creature, "Cast", 0.25f);
        await PowerCmd.Apply<StrengthPower>(new ThrowingPlayerChoiceContext(), base.Creature, 2m, base.Creature, null);
    }
}
