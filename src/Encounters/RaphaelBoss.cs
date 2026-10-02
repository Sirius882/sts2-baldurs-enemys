using System.Collections.Generic;
using BaseLib.Abstracts;
using BaseLib.Extensions;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using baldurs_enemys.src.Monsters;

namespace baldurs_enemys.src.Encounters;

/// <summary>
/// 拉斐尔 Boss 战（第三层 Boss 池）：[灵魂柱, 灵魂柱, 拉斐尔, 灵魂柱, 灵魂柱]。catch=false。
/// 地图图标暂用原版占位 lagavulin_matriarch_boss_icon。
/// </summary>
public sealed class RaphaelBoss : CustomEncounterModel
{
    public override bool IsWeak => false;

    public override bool IsValidForAct(ActModel act) => act.Index == 2;

    public RaphaelBoss() : base(RoomType.Boss)
    {
    }

    public override string BossNodePath => "res://baldurs_enemys/images/map/placeholder/raphael_boss_icon";

    public override string? CustomRunHistoryIconPath => BossNodePath + ".png";

    public override string? CustomRunHistoryIconOutlinePath => BossNodePath + "_outline.png";

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
    [
        ModelDb.Monster<SoulPillar>(),
        ModelDb.Monster<Raphael>()
    ];

    /// <summary>原稿敌怪排列：[灵魂柱, 灵魂柱, 拉斐尔, 灵魂柱, 灵魂柱]。</summary>
    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters() =>
    [
        (ModelDb.Monster<SoulPillar>().ToMutable(), null),
        (ModelDb.Monster<SoulPillar>().ToMutable(), null),
        (ModelDb.Monster<Raphael>().ToMutable(), null),
        (ModelDb.Monster<SoulPillar>().ToMutable(), null),
        (ModelDb.Monster<SoulPillar>().ToMutable(), null)
    ];
}
