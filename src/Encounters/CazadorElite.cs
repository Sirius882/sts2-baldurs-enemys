using System.Collections.Generic;
using BaseLib.Abstracts;
using BaseLib.Extensions;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using baldurs_enemys.src.Monsters;

namespace baldurs_enemys.src.Encounters;

/// <summary>
/// 「吸血鬼领主」卡扎多尔精英战（第三层精英池）：[蝙蝠, 卡扎多尔, 蝙蝠]。catch=false。
/// </summary>
public sealed class CazadorElite : CustomEncounterModel
{
    public override bool IsWeak => false;

    public override bool IsValidForAct(ActModel act) => act.Index == 2;

    public CazadorElite() : base(RoomType.Elite)
    {
    }

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
    [
        ModelDb.Monster<Bat>(),
        ModelDb.Monster<Cazador>()
    ];

    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters() =>
    [
        (ModelDb.Monster<Bat>().ToMutable(), null),
        (ModelDb.Monster<Cazador>().ToMutable(), null),
        (ModelDb.Monster<Bat>().ToMutable(), null)
    ];
}
