using System.Collections.Generic;
using BaseLib.Abstracts;
using BaseLib.Extensions;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using baldurs_enemys.src.Monsters;

namespace baldurs_enemys.src.Encounters;

/// <summary>
/// 「凯瑟里克·索姆」Boss 战（第二层 Boss 池）：[伊索贝尔, 凯瑟里克·索姆]。
///
/// - catch=false：本场不发「交互」卡。
/// - 2 怪无自定义场景 → 走引擎默认排位。
/// - Boss 地图图标：暂复用原版占位图标 knowledge_demon_boss_icon（本 mod 暂无自绘图标）。
///   BossNodePath 指向真实存在的图标基名（引擎 MapNodeAssetPaths 拼 ".png"/"_outline.png" 预加载）。
/// - 入池：BaseLib 把本遭遇注入 AllEncounters（IsValidForAct），但原版 Hive.BossDiscoveryOrder
///   写死且不含 mod Boss —— 由 HiveBossDiscoveryOrderPatch 追加，保证"未见过优先"机制下也能遇到。
/// </summary>
public sealed class KethericThormBoss : CustomEncounterModel
{
    public override bool IsWeak => false;

    /// <summary>仅第二层（Index 从 0 起）。</summary>
    public override bool IsValidForAct(ActModel act) => act.Index == 1;

    public KethericThormBoss() : base(RoomType.Boss)
    {
    }

    /// <summary>原版 Boss 地图图标基名（无扩展名；预加载时由引擎拼 .png / _outline.png）。</summary>
    public override string BossNodePath => "res://baldurs_enemys/images/map/placeholder/ketheric_thorm_boss_icon";

    public override string? CustomRunHistoryIconPath => BossNodePath + ".png";

    public override string? CustomRunHistoryIconOutlinePath => BossNodePath + "_outline.png";

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
    [
        ModelDb.Monster<Isobel>(),
        ModelDb.Monster<KethericThorm>()
    ];

    /// <summary>原稿敌怪排列：[伊索贝尔, 凯瑟里克·索姆]。每只单独 ToMutable()。</summary>
    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters() =>
    [
        (ModelDb.Monster<Isobel>().ToMutable(), null),
        (ModelDb.Monster<KethericThorm>().ToMutable(), null)
    ];
}
