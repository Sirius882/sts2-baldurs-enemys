using System;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;

namespace baldurs_enemys.src.Patches;

/// <summary>
/// 追踪「主要敌人死亡处理中」的嵌套深度，用于识别油桶的"被爪牙效果击败"。
///
/// 引擎机制（CreatureCmd.KillWithoutCheckingWinCondition）：主要敌人死亡时，
/// 若剩余队友全是次要敌人（爪牙），会 `await Kill(teammates)` 连带处决 —— 这发生在
/// 主要敌人的死亡处理帧内。本补丁在进入"主要敌人"的死亡处理时深度 +1、退出 -1。
/// 连带处决中的油桶（AfterDeath 触发时）会看到深度 &gt; 0；而玩家直接击杀、
/// 油桶连环爆炸等路径深度为 0，互不误伤。
///
/// 为什么不用 `Kill(IReadOnlyCollection)` 批量特征（全次要敌人）判断：伤害致死的路径
/// （DamageCmd → Kill(killedCreatures)，force=false）也走同一重载，单独击杀一个爪牙时
/// 无法与爪牙连锁区分 —— 深度计数则严格对应"主要敌人的死亡处理帧"。
/// </summary>
[HarmonyPatch(typeof(CreatureCmd), "KillWithoutCheckingWinCondition", MethodType.Normal)]
internal static class PrimaryEnemyDeathTrackerPatch
{
    /// <summary>&gt; 0 表示当前调用栈内正在处理某个主要敌人的死亡（爪牙连锁正在进行）。</summary>
    public static int PrimaryEnemyDeathDepth;

    [HarmonyPrefix]
    static void Prefix(Creature creature, ref bool __state)
    {
        __state = false;
        if (creature.Side == CombatSide.Enemy && creature.IsPrimaryEnemy)
        {
            PrimaryEnemyDeathDepth++;
            __state = true;
        }
    }

    [HarmonyFinalizer]
    static Exception? Finalizer(bool __state)
    {
        if (__state)
        {
            PrimaryEnemyDeathDepth--;
        }
        return null;
    }
}
