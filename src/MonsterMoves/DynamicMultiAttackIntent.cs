using System;
using System.Collections.Generic;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;

namespace baldurs_enemys.src.MonsterMoves;

/// <summary>
/// 伤害数值运行期计算的多段攻击意图。
/// 原版 <see cref="MultiAttackIntent"/> 只支持"固定伤害 + 动态段数"（Func&lt;int&gt; repeatCalc），
/// 而凯瑟里克·索姆二阶段的攻击伤害随计数 i 增长（攻击(i+3)*3 / 攻击(i+3)*2），
/// 需要动态伤害 + 固定段数 —— 本类补上这个组合。实现完全照抄 MultiAttackIntent。
/// </summary>
public sealed class DynamicMultiAttackIntent : AttackIntent
{
    private readonly int _repeat;

    protected override LocString IntentLabelFormat => new LocString("intents", "FORMAT_DAMAGE_MULTI");

    public override int Repeats => _repeat;

    public DynamicMultiAttackIntent(Func<decimal> damageCalc, int repeat)
    {
        base.DamageCalc = damageCalc;
        _repeat = repeat;
    }

    public override int GetTotalDamage(IEnumerable<Creature> targets, Creature owner)
    {
        return GetSingleDamage(targets, owner) * Repeats;
    }

    public override LocString GetIntentLabel(IEnumerable<Creature> targets, Creature owner)
    {
        LocString intentLabelFormat = IntentLabelFormat;
        float num = GetSingleDamage(targets, owner);
        intentLabelFormat.Add("Damage", (int)num);
        intentLabelFormat.Add("Repeat", Repeats);
        return intentLabelFormat;
    }
}
