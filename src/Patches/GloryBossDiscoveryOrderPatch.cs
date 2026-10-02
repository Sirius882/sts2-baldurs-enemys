using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Models.Encounters;
using baldurs_enemys.src.Encounters;

namespace baldurs_enemys.src.Patches;

/// <summary>
/// 把第三层四个自定义 Boss 追加进 Glory.BossDiscoveryOrder 队尾。
///
/// 机制与第一/二幕补丁同构（Overgrowth/Hive）：原版数组写死且不含 mod Boss，
/// 不打这个补丁的话，"未见过优先"机制会让奥林/戈塔什/拉斐尔/君主几乎不出现。
/// 追加到队尾：原版顺序不变，集齐后保证各自首见一次，此后等概率随机。
/// </summary>
[HarmonyPatch(typeof(Glory), nameof(Glory.BossDiscoveryOrder), MethodType.Getter)]
internal static class GloryBossDiscoveryOrderPatch
{
    [HarmonyPostfix]
    static void Postfix(ref IEnumerable<EncounterModel> __result)
    {
        __result = __result
            .Append(ModelDb.Encounter<OrinBoss>())
            .Append(ModelDb.Encounter<GortashBoss>())
            .Append(ModelDb.Encounter<RaphaelBoss>())
            .Append(ModelDb.Encounter<EmperorBoss>());
    }
}
