using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Random;

namespace baldurs_enemys.src.Core;

/// <summary>
/// DnD 骰子工具（设计稿总则第 5 条：骰子的随机数计算要严格绑定种子 RNG，
/// 确保多人游戏中所有玩家的随机数一致、每次 SL 的结果一致）。
///
/// 实现口径：所有掷骰走 `IRunState.Rng`（RunRngSet）—— 以 run 种子派生的按用途分通道 RNG，
/// 随存档序列化（SL 后从存档恢复状态），多人共享同一 run 种子。
/// 通道选 **Niche**（原版注释："niche one-off RNG stuff that we don't care about interacting"）：
/// 消耗它的随机数序列不会扰动原版 Shuffle/MonsterAi 等通道的行为。
///
/// 掷骰顺序即消耗顺序：所有逐单位掷骰都必须按 combatState.Players / Enemies 的固定列表顺序进行，
/// 保证同步（锁步）下各端一致。
/// </summary>
public static class Dice
{
    /// <summary>取当前战斗所属 run 的骰子通道（Niche）。</summary>
    public static Rng GetRng(ICombatState combatState) => combatState.RunState.Rng.Niche;

    /// <summary>掷XdY+Z（例如 X=1,Y=4,Z=3 即 1d4+3）。</summary>
    public static int Roll(ICombatState combatState, int diceCount, int sides, int bonus)
    {
        Rng rng = GetRng(combatState);
        int total = bonus;
        for (int i = 0; i < diceCount; i++)
        {
            total += rng.NextInt(1, sides + 1);
        }
        return total;
    }
}
