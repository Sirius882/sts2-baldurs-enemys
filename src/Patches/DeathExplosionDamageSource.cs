using System;

namespace baldurs_enemys.src.Patches;

/// <summary>
/// 油桶「死亡引爆」伤害来源标记（不是 Harmony 补丁，只是一个标志位持有者）。
///
/// 为什么不能用 dealer 来识别爆炸来源：
/// <see cref="MegaCrit.Sts2.Core.Commands.CreatureCmd"/> 的集合重载 Damage 开头有一道早退
/// ——`if (dealer != null && dealer.IsDead) return ...;`。而「死亡引爆」挂在 AfterDeath 上，
/// 引擎时序（KillWithoutCheckingWinCondition）是先 LoseHpInternal 把 HP 归零、再 await
/// Hook.AfterDeath，所以爆炸结算时油桶必然已死。若以油桶自身作 dealer，伤害会被这道早退
/// 全部跳过（表现为「爆炸特效播了但一点伤害都没有」）。
/// 传 dealer: null 可绕开早退，且这正是同源的「过载熔毁」（钢铁卫士）一直在用的写法。
/// 但 dealer 变成 null 后，「油桶易伤」（明萨拉 ×3）就失去了识别爆炸的依据，
/// 故用本标志位在爆炸伤害结算窗口内补上来源标记。
///
/// 时序要点（与 <see cref="ExplosiveAmpouleDamagePatch"/> 同源，但实现更简单）：
/// 标志位由 <see cref="baldurs_enemys.src.Powers.DeathExplosionPower"/> 在方法体内
/// 手动置位 / 清位，不需要像 Harmony Postfix 那样包装 Task —— 因为置位方就是 async 方法
/// 本身的持有者，try/finally 即可精确覆盖「整个爆炸伤害结算窗口」。
///
/// 必须用深度计数而非 bool：油桶连环引爆时，B 桶的 AfterDeath 会嵌套在 A 桶的伤害结算
/// 内部触发（设计稿明确要求链式引爆）。若用 bool，B 桶清位后 A 桶的剩余伤害就会丢失标记。
///
/// 【2026-10-02 22:40 现状】原为「油桶易伤」（明萨拉 ×3）提供来源识别，但该 power 因
/// 数值超标（油桶有 3/5 概率直接秒杀明萨拉）已被用户要求删除，故本类目前**没有消费者**。
/// 仍然保留并继续置位的原因：它是「死亡爆炸必须传 dealer: null」这一修复的配套说明载体，
/// 且未来若再加「按爆炸来源判定」的效果（如伤害类型的区分），可直接复用，无需重新设计时序。
/// </summary>
public static class DeathExplosionDamageSource
{
    private static int _depth;

    /// <summary>当前是否正在结算「死亡引爆」造成的伤害。仅主线程读写。</summary>
    public static bool IsOilBarrelExplosion => _depth > 0;

    /// <summary>进入爆炸伤害结算窗口。返回 IDisposable 以便用 using 保证配对清位。</summary>
    public static IDisposable Enter()
    {
        _depth++;
        return new Scope();
    }

    private sealed class Scope : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            if (_depth > 0)
            {
                _depth--;
            }
        }
    }
}
