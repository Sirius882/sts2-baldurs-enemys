using System.Collections.Generic;
using BaseLib.Abstracts;
using BaseLib.Extensions;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using baldurs_enemys.src.Monsters;

namespace baldurs_enemys.src.Encounters;

/// <summary>
/// 「死亡之神的面容」米尔寇的使徒 Boss 战（第二层 Boss 池）：[米尔寇的使徒]。catch=false。
/// 地图图标暂用原版占位 soul_fysh_boss_icon。
/// </summary>
public sealed class MyrkulBoss : CustomEncounterModel
{
    public override bool IsWeak => false;

    public override bool IsValidForAct(ActModel act) => act.Index == 1;

    public MyrkulBoss() : base(RoomType.Boss)
    {
    }

    public override string BossNodePath => "res://baldurs_enemys/images/map/placeholder/myrkul_boss_icon";

    public override string? CustomRunHistoryIconPath => BossNodePath + ".png";

    public override string? CustomRunHistoryIconOutlinePath => BossNodePath + "_outline.png";

    public override IEnumerable<MonsterModel> AllPossibleMonsters => [ModelDb.Monster<MyrkulApostle>()];

    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters() =>
    [
        (ModelDb.Monster<MyrkulApostle>().ToMutable(), null)
    ];
}
