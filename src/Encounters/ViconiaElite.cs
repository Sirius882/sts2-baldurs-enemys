using System.Collections.Generic;
using BaseLib.Abstracts;
using BaseLib.Extensions;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using baldurs_enemys.src.Monsters;

namespace baldurs_enemys.src.Encounters;

/// <summary>
/// 「莎尔的女院长」维康妮亚精英战（第三层精英池）：[油桶, 维康妮亚, 油桶]。
/// catch=false：本场不发「交互」卡。油桶是解题钥匙（引爆绕过暗影帷幕）。
/// </summary>
public sealed class ViconiaElite : CustomEncounterModel
{
    public override bool IsWeak => false;

    /// <summary>仅第三层（Index 从 0 起）。</summary>
    public override bool IsValidForAct(ActModel act) => act.Index == 2;

    public ViconiaElite() : base(RoomType.Elite)
    {
    }

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
    [
        ModelDb.Monster<OilBarrel>(),
        ModelDb.Monster<Viconia>()
    ];

    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters() =>
    [
        (ModelDb.Monster<OilBarrel>().ToMutable(), null),
        (ModelDb.Monster<Viconia>().ToMutable(), null),
        (ModelDb.Monster<OilBarrel>().ToMutable(), null)
    ];
}
