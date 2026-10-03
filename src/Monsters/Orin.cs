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
using MegaCrit.Sts2.Core.Nodes.Combat;
using Godot;
using baldurs_enemys.src.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Models.Powers;

namespace baldurs_enemys.src.Monsters;

/// <summary>
/// 「巴尔死亡使者」奥林（A10 血量 480，低进阶 457）。第三层 Boss，身旁是人质。
///
/// 原稿状态机：
///   int i=0;
///   while(人质存活)
///   {
///       攻击10*2;
///       减益：给所有玩家敏捷-1;   // 永久
///       if(i>2) break;
///       i++;
///   }
///   强化：获得3力量，并切换到杀戮者形态;
///   while(true)
///   {
///       攻击12*2;
///       攻击6*4;
///       减益：给所有玩家敏捷-1，持续一回合;
///   }
///
/// i 计数实现口径（条件会被预测重复求值，自增只能放执行体）：
/// 「敏捷-1」执行体末尾 i += 1；break 条件改为等价写法 i >= 4（C++ 原稿 4 次循环：
/// i=0,1,2 通过、i=3 break —— 自增提前后判 i>=4 完全等价）。
/// 人质中途死亡/被交互 → 两个 while 条件与循环内分支同时收敛到 EMPOWER。
/// </summary>
public sealed class Orin : BaldurMonsterModel
{
    public override int MinInitialHp => AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 480, 457);

    public override int MaxInitialHp => MinInitialHp;

    public override DamageSfxType TakeDamageSfxType => DamageSfxType.Fur;

    /// <summary>美术：mod 自带立绘，双形态切换（初始 orin.png / 杀戮者 orin_slayer.png）。</summary>
    public override NCreatureVisuals? CreateCustomVisuals()
    {
        NCreatureVisuals? visuals = BaldurVisuals.Create("res://baldurs_enemys/images/monsters/orin.png", 330);
        _visualSprite = visuals?.GetNode<Sprite2D>("Visuals");
        return visuals;
    }

    /// <summary>当前视觉节点上的立绘精灵（用于形态切换换图；进场前为 null）。</summary>
    private Sprite2D? _visualSprite;

    /// <summary>
    /// 切换到杀戮者形态立绘。由 <see cref="baldurs_enemys.src.Powers.BhaalsBlessingPower.EnterSlayerForm"/>
    /// 触发（转阶段 +3 力量 / 人质死亡或被交互）。视觉节点不存在（尚未进场/已离场）时静默跳过。
    /// </summary>
    public void ShowSlayerForm()
    {
        Texture2D? texture = GD.Load<Texture2D>("res://baldurs_enemys/images/monsters/orin_slayer.png");
        if (texture != null && _visualSprite != null && GodotObject.IsInstanceValid(_visualSprite))
        {
            _visualSprite.Texture = texture;
            // 杀戮者形态素材是横向的（908×723），保持底边对齐：按同一逻辑高度重新居中
            float targetHeight = 330f;
            float scale = targetHeight / texture.GetSize().Y;
            _visualSprite.Scale = new Vector2(scale, scale);
            _visualSprite.Position = new Vector2(0f, -targetHeight * 0.5f);
        }
    }

    private int _loopIndex;

    private int LoopIndex
    {
        get => _loopIndex;
        set
        {
            AssertMutable();
            _loopIndex = value;
        }
    }

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        await PowerCmd.Apply<BhaalsBlessingPower>(new ThrowingPlayerChoiceContext(), base.Creature, 1m, base.Creature, null);
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        // ---- 第一阶段：while(人质存活) { 攻击10*2 → 全体敏捷-1 → if(i>2) break; i++ } ----
        ConditionalBranchState entry = new("ENTRY"); // 首次条件判断（人质可能开场即死）
        MoveState stab = new("STAB", StabMove, new MultiAttackIntent(Balthazar.ScaleDeadly(10), 2));
        ConditionalBranchState postStab = new("POST_STAB");
        MoveState dexDown = new("DEX_DOWN", DexDownMove, new DebuffIntent());
        ConditionalBranchState postDex = new("POST_DEX");
        MoveState empower = new("EMPOWER", EmpowerMove, new BuffIntent());

        // ---- 第二阶段：杀戮者形态 ----
        MoveState slash = new("SLASH", SlashMove, new MultiAttackIntent(Balthazar.ScaleDeadly(12), 2));
        MoveState flurry = new("FLURRY", FlurryMove, new MultiAttackIntent(Balthazar.ScaleDeadly(6), 4));
        MoveState dexTemp = new("DEX_TEMP", DexTempMove, new DebuffIntent());

        entry.AddState(stab, HostageAlive);
        entry.AddState(empower, () => true);

        stab.FollowUpState = postStab;
        postStab.AddState(dexDown, HostageAlive);
        postStab.AddState(empower, () => true);

        dexDown.FollowUpState = postDex;
        // C++: if(i>2) break; i++; → 自增提前到 DEX_DOWN 执行体后，等价 break 条件为 i>=4
        postDex.AddState(empower, () => LoopIndex >= 4);
        postDex.AddState(stab, () => true);

        empower.FollowUpState = slash;
        slash.FollowUpState = flurry;
        flurry.FollowUpState = dexTemp;
        dexTemp.FollowUpState = slash;

        List<MonsterState> states =
        [
            entry, stab, postStab, dexDown, postDex, empower, slash, flurry, dexTemp
        ];
        return new MonsterMoveStateMachine(states, entry);
    }

    protected override bool ShouldShowMoveInBestiary(string moveStateId) =>
        moveStateId is "STAB" or "DEX_DOWN" or "EMPOWER" or "SLASH" or "FLURRY" or "DEX_TEMP";

    /// <summary>「人质存活」。</summary>
    private bool HostageAlive()
    {
        ICombatState? combatState = base.Creature.CombatState;
        return combatState != null && combatState.Enemies.Any(c => c.Monster is Hostage && c.IsAlive);
    }

    private async Task StabMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(Balthazar.ScaleDeadly(10)).WithHitCount(2).FromMonster(this)
            .OnlyPlayAnimOnce()
            .WithAttackerAnim("Attack", 0.3f)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(null);
    }

    /// <summary>全体玩家永久 -1 敏捷（原稿注明永久持续）；执行体末尾 i++。</summary>
    private async Task DexDownMove(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.TriggerAnim(base.Creature, "Cast", 0.25f);
        ICombatState? combatState = base.Creature.CombatState;
        if (combatState != null)
        {
            PlayerChoiceContext choiceContext = new ThrowingPlayerChoiceContext();
            foreach (Creature player in combatState.PlayerCreatures)
            {
                if (!player.IsAlive) continue;
                await PowerCmd.Apply<DexterityPower>(choiceContext, player, -1m, base.Creature, null);
            }
        }
        LoopIndex += 1;
    }

    /// <summary>转阶段：+3 力量并切换到杀戮者形态（巴尔的赐福随之改发活力）。</summary>
    private async Task EmpowerMove(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.TriggerAnim(base.Creature, "Cast", 0.25f);
        PlayerChoiceContext choiceContext = new ThrowingPlayerChoiceContext();
        await PowerCmd.Apply<StrengthPower>(choiceContext, base.Creature, 3m, base.Creature, null);
        BhaalsBlessingPower? blessing = base.Creature.GetPower<BhaalsBlessingPower>();
        blessing?.EnterSlayerForm();
    }

    private async Task SlashMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(Balthazar.ScaleDeadly(12)).WithHitCount(2).FromMonster(this)
            .OnlyPlayAnimOnce()
            .WithAttackerAnim("Attack", 0.3f)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(null);
    }

    private async Task FlurryMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(Balthazar.ScaleDeadly(6)).WithHitCount(4).FromMonster(this)
            .OnlyPlayAnimOnce()
            .WithAttackerAnim("Attack", 0.2f)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(null);
    }

    /// <summary>全体玩家 -1 敏捷，持续 1 回合（定时消散到期移除）。</summary>
    private async Task DexTempMove(IReadOnlyList<Creature> targets)
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
            await TimedDebuffExpiryPower.ApplyTimedDebuff<DexterityPower>(
                choiceContext, player, -1m, base.Creature, 1);
        }
    }
}
