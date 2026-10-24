using System.Collections.Generic;
using BaseLib.Abstracts;
using BaseLib.Extensions;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using baldurs_enemys.src.Monsters;

namespace baldurs_enemys.src.Encounters;

/// <summary>
/// 「枭熊」精英战（第一层精英池）：[大枭熊, 小枭熊]。
///
/// catch=true：本场每回合发「交互」卡（见 CatchCombatDriver），两个目标都带「可被交互」：
/// - 大枭熊：掷 1d20，≥ 14 则玩家直接赢得本场战斗；检定失败可在后续回合继续尝试；
/// - 小枭熊：立即死亡，然后掷 1d20，&gt; 12 获得遗物「佩尔的士兵」。
///
/// 说明：
/// - `IsValidForAct(act.Index == 0)` ⇒ 第一层（Overgrowth 与 Underdocks 的 Index 都是 0，
///   与明萨拉 Boss 战同一口径，会同时进入两幕的池）。
/// - 不覆写 `Tags`：EncounterTag 是 enum，mod 不能新增值，复用原版 tag 会与本 mod 之外的
///   原版遭遇产生"相邻不重复"的跨干涉（与 MintharaBoss 同策）。
/// - 不覆写 `Slots` / `CustomScenePath`：走默认遭遇场景的自动排位（同 AnsurElite），
///   2 个目标不需要缩小镜头。
/// </summary>
public sealed class OwlbearElite : CustomEncounterModel, ICatchEncounter
{
    public bool CatchEnabled => true;

    public override bool IsWeak => false;

    /// <summary>仅第一层（Index 从 0 起）。</summary>
    public override bool IsValidForAct(ActModel act) => act.Index == 0;

    public OwlbearElite() : base(RoomType.Elite)
    {
    }

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
    [
        ModelDb.Monster<Owlbear>(),
        ModelDb.Monster<OwlbearCub>()
    ];

    /// <summary>原稿敌怪排列：[大枭熊, 小枭熊]。</summary>
    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters() =>
    [
        (ModelDb.Monster<Owlbear>().ToMutable(), null),
        (ModelDb.Monster<OwlbearCub>().ToMutable(), null)
    ];
}
