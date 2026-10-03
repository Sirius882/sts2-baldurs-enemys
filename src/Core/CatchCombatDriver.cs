using System.Threading.Tasks;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using baldurs_enemys.src.Cards;
using baldurs_enemys.src.Encounters;

namespace baldurs_enemys.src.Core;

/// <summary>
/// catch 战斗的"发牌驱动器"。
///
/// 为什么需要它：EncounterModel **不是**战斗钩子监听者 ——
/// CombatState.IterateHookListeners 只收集 生物的 power / MonsterModel / 玩家的遗物·药水·充能球·
/// 所有牌堆里的卡 / Modifiers / Badges，再加上 ModHelper 订阅者。遭遇不在其中，
/// 所以写在遭遇里的 AfterPlayerTurnStart 永远不会被调用。
///
/// 解法：CustomSingletonModel(HookType.Combat) 会让 BaseLib 替我们调用
/// ModHelper.SubscribeForCombatStateHooks，从而在每场战斗里收到全部战斗钩子。
/// 这里在 AfterPlayerTurnStart（= 抽牌完成之后的确切位置）判断当前战斗是否为 catch 战斗，是则塞一张「交互」。
/// </summary>
public sealed class CatchCombatDriver : CustomSingletonModel
{
    public CatchCombatDriver() : base(HookType.Combat)
    {
    }

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (!IsCatchCombat(player)) return;

        // 手牌满（上限 10）时不塞，静默跳过 —— 与引擎自己抽牌的截断行为一致
        if (player.PlayerCombatState == null) return;
        if (player.PlayerCombatState.Hand.Cards.Count >= CardPile.MaxCardsInHand) return;

        // 目标传玩家的 Creature；pileType=Hand 时引擎只做 0.1s 等待，不走卡牌预览
        await CardPileCmd.AddToCombatAndPreview<Interact>(player.Creature, PileType.Hand, 1, null);
    }

    private static bool IsCatchCombat(Player player)
    {
        ICombatState? combatState = player.Creature?.CombatState;
        if (combatState == null) return false;

        return combatState.Encounter is ICatchEncounter { CatchEnabled: true };
    }
}
