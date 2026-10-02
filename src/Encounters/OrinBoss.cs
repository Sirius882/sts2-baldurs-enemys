using System.Collections.Generic;
using BaseLib.Abstracts;
using BaseLib.Extensions;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using baldurs_enemys.src.Monsters;

namespace baldurs_enemys.src.Encounters;

/// <summary>
/// 「巴尔死亡使者」奥林 Boss 战（第三层 Boss 池）：[奥林, 人质]。
/// catch=true：本场发「拾取」卡，可拾取目标是人质（→ 原版遗物「布质果实」+「李家华夫饼」），
/// 但人质死亡/被拾取会触发奥林 +5 力量并进入杀戮者形态。
/// 地图图标暂用原版占位 the_kin_boss_icon。
/// </summary>
public sealed class OrinBoss : CustomEncounterModel, ICatchEncounter
{
    public bool CatchEnabled => true;

    public override bool IsWeak => false;

    public override bool IsValidForAct(ActModel act) => act.Index == 2;

    public OrinBoss() : base(RoomType.Boss)
    {
    }

    public override string BossNodePath => "res://baldurs_enemys/images/map/placeholder/orin_boss_icon";

    public override string? CustomRunHistoryIconPath => BossNodePath + ".png";

    public override string? CustomRunHistoryIconOutlinePath => BossNodePath + "_outline.png";

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
    [
        ModelDb.Monster<Orin>(),
        ModelDb.Monster<Hostage>()
    ];

    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters() =>
    [
        (ModelDb.Monster<Orin>().ToMutable(), null),
        (ModelDb.Monster<Hostage>().ToMutable(), null)
    ];
}
