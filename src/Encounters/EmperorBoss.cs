using System.Collections.Generic;
using BaseLib.Abstracts;
using BaseLib.Extensions;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using baldurs_enemys.src.Monsters;

namespace baldurs_enemys.src.Encounters;

/// <summary>
/// 君主 Boss 战（第三层 Boss 池）：[梦境守护者, 君主, 梦境守护者]。catch=false。
/// 地图图标暂用原版占位 aeonglass_boss_icon。
/// </summary>
public sealed class EmperorBoss : CustomEncounterModel
{
    public override bool IsWeak => false;

    public override bool IsValidForAct(ActModel act) => act.Index == 2;

    public EmperorBoss() : base(RoomType.Boss)
    {
    }

    public override string BossNodePath => "res://baldurs_enemys/images/map/placeholder/emperor_boss_icon";

    public override string? CustomRunHistoryIconPath => BossNodePath + ".png";

    public override string? CustomRunHistoryIconOutlinePath => BossNodePath + "_outline.png";

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
    [
        ModelDb.Monster<DreamGuardian>(),
        ModelDb.Monster<Emperor>()
    ];

    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters() =>
    [
        (ModelDb.Monster<DreamGuardian>().ToMutable(), null),
        (ModelDb.Monster<Emperor>().ToMutable(), null),
        (ModelDb.Monster<DreamGuardian>().ToMutable(), null)
    ];
}
