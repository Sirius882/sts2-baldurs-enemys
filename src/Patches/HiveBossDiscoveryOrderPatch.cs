using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Models.Encounters;
using baldurs_enemys.src.Encounters;

namespace baldurs_enemys.src.Patches;

/// <summary>
/// 把第二层自定义 Boss 追加进 Hive.BossDiscoveryOrder（凯瑟里克·索姆、米尔寇的使徒）。
///
/// 机制（ActModel）：先 `rng.NextItem(AllBossEncounters)` 随机选 Boss，随后
/// `ApplyDiscoveryOrderModifications` 用 BossDiscoveryOrder 里**第一个未见过**的 Boss 覆盖结果。
/// 原版该数组写死且不含 mod Boss —— 不打这个补丁的话，玩家在集齐原版二层 Boss 之前
/// 几乎不可能遇到自定义 Boss。追加到队尾：原版顺序不变，集齐后保证首见一次，此后等概率随机。
/// （与 OvergrowthBossDiscoveryOrderPatch 同构。）
/// </summary>
[HarmonyPatch(typeof(Hive), nameof(Hive.BossDiscoveryOrder), MethodType.Getter)]
internal static class HiveBossDiscoveryOrderPatch
{
    [HarmonyPostfix]
    static void Postfix(ref IEnumerable<EncounterModel> __result)
    {
        __result = __result
            .Append(ModelDb.Encounter<KethericThormBoss>())
            .Append(ModelDb.Encounter<MyrkulBoss>());
    }
}
