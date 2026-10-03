using System.Collections.Generic;
using BaseLib.Abstracts;
using BaseLib.Extensions;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using baldurs_enemys.src.Monsters;

namespace baldurs_enemys.src.Encounters;

/// <summary>
/// 「博德之心」安苏精英战（第三层精英池）：[安苏, 博德安的巨人杀手]。
/// catch=true：本场发「交互」卡，可交互目标是巨人杀手（→ 遗物「博德安的巨人杀手」），
/// 但巨人杀手死亡（含被交互）会触发安苏的「被背叛」（+5 力量 + 半血自伤并转 6 连轻击）。
/// </summary>
public sealed class AnsurElite : CustomEncounterModel, ICatchEncounter
{
    public bool CatchEnabled => true;

    public override bool IsWeak => false;

    public override bool IsValidForAct(ActModel act) => act.Index == 2;

    public AnsurElite() : base(RoomType.Elite)
    {
    }

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
    [
        ModelDb.Monster<Ansur>(),
        ModelDb.Monster<BalduransGiantslayer>()
    ];

    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters() =>
    [
        (ModelDb.Monster<Ansur>().ToMutable(), null),
        (ModelDb.Monster<BalduransGiantslayer>().ToMutable(), null)
    ];
}
