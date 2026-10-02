using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Models.Encounters;
using baldurs_enemys.src.Encounters;

namespace baldurs_enemys.src.Patches;

/// <summary>
/// 把明萨拉 Boss 追加进第一幕（Overgrowth）的 BossDiscoveryOrder。
///
/// 机制（ActModel）：先 `rng.NextItem(AllBossEncounters)` 随机选 Boss，随后
/// `ApplyDiscoveryOrderModifications` 用 BossDiscoveryOrder 里**第一个未见过**的 Boss 覆盖结果。
/// 原版该数组写死为 [VantomBoss, CeremonialBeastBoss, TheKinBoss]，不含 mod Boss —— 不打这个补丁的话，
/// 玩家在集齐三个原版一层 Boss 之前几乎不可能遇到明萨拉。
///
/// 追加到**队尾**：原版 Boss 的"未见过优先"顺序保持不变；三个原版 Boss 都见过后，
/// 第一次生成会命中未过的明萨拉（保证首见），之后与其他 Boss 等概率随机。
/// </summary>
[HarmonyPatch(typeof(Overgrowth), nameof(Overgrowth.BossDiscoveryOrder), MethodType.Getter)]
internal static class OvergrowthBossDiscoveryOrderPatch
{
    [HarmonyPostfix]
    static void Postfix(ref IEnumerable<EncounterModel> __result)
    {
        __result = __result.Append(ModelDb.Encounter<MintharaBoss>());
    }
}
