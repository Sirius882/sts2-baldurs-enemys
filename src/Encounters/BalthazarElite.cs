using System.Collections.Generic;
using BaseLib.Abstracts;
using BaseLib.Extensions;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using baldurs_enemys.src.Monsters;

namespace baldurs_enemys.src.Encounters;

/// <summary>
/// 「巴萨扎尔」精英战（第二层精英池）：[骷髅, 巴萨扎尔, 暗夜之歌]。
///
/// - catch=true：本场战斗会发「拾取」卡（拾取暗夜之歌是遗物「艾琳女士」的唯一来源）。
/// - 3 怪无自定义场景 → 走引擎默认排位（原版多数遭遇同样无场景，3 怪默认位存在）。
/// - 无自定义背景 → 用第二层默认背景。
/// </summary>
public sealed class BalthazarElite : CustomEncounterModel, ICatchEncounter
{
    public override bool IsWeak => false;

    /// <summary>仅第二层（Index 从 0 起）。</summary>
    public override bool IsValidForAct(ActModel act) => act.Index == 1;

    public BalthazarElite() : base(RoomType.Elite)
    {
    }

    /// <summary>本场战斗会发「拾取」卡。</summary>
    public bool CatchEnabled => true;

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
    [
        ModelDb.Monster<Skeleton>(),
        ModelDb.Monster<Balthazar>(),
        ModelDb.Monster<Nightsong>()
    ];

    /// <summary>原稿敌怪排列：[骷髅, 巴萨扎尔, 暗夜之歌]。每只单独 ToMutable()。</summary>
    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters() =>
    [
        (ModelDb.Monster<Skeleton>().ToMutable(), null),
        (ModelDb.Monster<Balthazar>().ToMutable(), null),
        (ModelDb.Monster<Nightsong>().ToMutable(), null)
    ];
}
