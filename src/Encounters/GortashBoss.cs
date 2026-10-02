using System.Collections.Generic;
using BaseLib.Abstracts;
using BaseLib.Extensions;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using baldurs_enemys.src.Monsters;

namespace baldurs_enemys.src.Encounters;

/// <summary>
/// 「博德之门大公爵候选人」恩维尔·戈塔什大人 Boss 战（第三层 Boss 池）：
/// [钢铁卫士, 戈塔什, 钢铁卫士]。catch=false。
/// 地图图标暂用原版占位 kaiser_crab_boss_icon。
/// </summary>
public sealed class GortashBoss : CustomEncounterModel
{
    public override bool IsWeak => false;

    public override bool IsValidForAct(ActModel act) => act.Index == 2;

    public GortashBoss() : base(RoomType.Boss)
    {
    }

    public override string BossNodePath => "res://baldurs_enemys/images/map/placeholder/gortash_boss_icon";

    public override string? CustomRunHistoryIconPath => BossNodePath + ".png";

    public override string? CustomRunHistoryIconOutlinePath => BossNodePath + "_outline.png";

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
    [
        ModelDb.Monster<SteelWatcher>(),
        ModelDb.Monster<Gortash>()
    ];

    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters() =>
    [
        (ModelDb.Monster<SteelWatcher>().ToMutable(), null),
        (ModelDb.Monster<Gortash>().ToMutable(), null),
        (ModelDb.Monster<SteelWatcher>().ToMutable(), null)
    ];
}
