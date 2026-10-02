using System.Collections.Generic;
using BaseLib.Abstracts;
using BaseLib.Extensions;
using Godot;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using baldurs_enemys.src.Monsters;

namespace baldurs_enemys.src.Encounters;

/// <summary>
/// 「明萨拉」Boss 战（2026-10-02 起从精英移入**第一层 Boss 池**）：
/// [地精·近战, 油桶, 明萨拉, 地精·远程, 油桶]。
///
/// - Tags 取空集合：EncounterTag 是 enum，mod 不能新增值；复用原版 tag 会与本 mod 之外的原版遭遇
///   产生"相邻不重复"的跨干涉。留空 = 不受相邻去重约束，最干净。
/// - 镜头 0.85 / Down*25：取自同为 5 槽位的原版 TwoTailedRatsNormal 实测值。
/// - Boss 地图图标：本 mod 暂无自绘图标，复用原版占位图标 waterfall_giant_boss_icon。
///   BossNodePath 指向真实存在的图标基名（引擎的 MapNodeAssetPaths 会拼 ".png" 与 "_outline.png"
///   做预加载，指到不存在的路径会在日志刷错误）；BossNodeSpineResource 保持默认 null（无 Spine）。
/// </summary>
public sealed class MintharaBoss : CustomEncounterModel, ICatchEncounter
{
    public override bool IsWeak => false;

    /// <summary>仅第一层（Index 从 0 起）。</summary>
    public override bool IsValidForAct(ActModel act) => act.Index == 0;

    /// <summary>战斗场景：Control(1920x1080, Full Rect, mouse_filter=2) + 5 个 Marker2D 槽位。</summary>
    public override string? CustomScenePath => "res://scenes/encounters/baldurs_enemys-minthara_boss.tscn";

    public override IReadOnlyList<string> Slots => ["first", "second", "third", "fourth", "fifth"];

    public override float GetCameraScaling() => 0.85f;

    public override Vector2 GetCameraOffset() => Vector2.Down * 25f;

    /// <summary>原版 Boss 地图图标基名（无扩展名；预加载时由引擎拼 .png / _outline.png）。</summary>
    public override string BossNodePath => "res://baldurs_enemys/images/map/placeholder/minthara_boss_icon";

    public override string? CustomRunHistoryIconPath => BossNodePath + ".png";

    public override string? CustomRunHistoryIconOutlinePath => BossNodePath + "_outline.png";

    /// <summary>本场战斗会发「拾取」卡。</summary>
    public bool CatchEnabled => true;

    public MintharaBoss() : base(RoomType.Boss)
    {
    }

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
    [
        ModelDb.Monster<GoblinMelee>(),
        ModelDb.Monster<OilBarrel>(),
        ModelDb.Monster<Minthara>(),
        ModelDb.Monster<GoblinRanged>()
    ];

    /// <summary>
    /// 原稿敌怪排列：[地精·近战, 油桶, 明萨拉, 地精·远程, 油桶]。
    /// 每只都必须单独 ToMutable()（同一只不能复用两个槽位）。
    /// </summary>
    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters() =>
    [
        (ModelDb.Monster<GoblinMelee>().ToMutable(), Slots[0]),
        (ModelDb.Monster<OilBarrel>().ToMutable(), Slots[1]),
        (ModelDb.Monster<Minthara>().ToMutable(), Slots[2]),
        (ModelDb.Monster<GoblinRanged>().ToMutable(), Slots[3]),
        (ModelDb.Monster<OilBarrel>().ToMutable(), Slots[4])
    ];
}
