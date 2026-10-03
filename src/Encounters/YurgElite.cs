using System.Collections.Generic;
using BaseLib.Abstracts;
using BaseLib.Extensions;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using baldurs_enemys.src.Monsters;

namespace baldurs_enemys.src.Encounters;

/// <summary>
/// 「尤格」精英战（第二层精英池）：单怪。
///
/// - catch=false：本场不发「交互」卡（不实现 ICatchEncounter）。
/// - 单怪、无自定义场景（不设 CustomScenePath）→ 走引擎默认排位，与原版 TestSubjectBoss 同款处理。
/// - 无自定义背景 → 用第二层默认背景。
/// </summary>
public sealed class YurgElite : CustomEncounterModel
{
    public override bool IsWeak => false;

    /// <summary>仅第二层（Index 从 0 起）。</summary>
    public override bool IsValidForAct(ActModel act) => act.Index == 1;

    public YurgElite() : base(RoomType.Elite)
    {
    }

    public override IEnumerable<MonsterModel> AllPossibleMonsters => [ModelDb.Monster<Yurg>()];

    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters() =>
    [
        (ModelDb.Monster<Yurg>().ToMutable(), null)
    ];
}
