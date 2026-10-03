using System.Collections.Generic;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.MonsterMoves;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;

namespace baldurs_enemys.src.Monsters;

/// <summary>
/// 本 mod 所有怪物的公共基类。存在的**唯一目的**是修掉一处引擎/BaseLib 的缺口：
///走<see cref="CustomMonsterModel.CreateCustomVisuals"/> 自建立绘路线的怪物，
/// 不该被引擎去预加载一个根本不存在的 `.tscn` 战斗视觉场景。
///
/// 【问题根源】
/// 引擎 <c>MonsterModel.AssetPaths</c>（MonsterModel.cs:77-94）**无条件**把
/// <c>VisualsPath</c> 塞进预加载清单：
/// ```csharp
/// public virtual IEnumerable<string> AssetPaths
/// {
///     get
///     {
///         List&lt;string&gt; list = new(1);
///         ...
///         span[0] = VisualsPath;          // ← 无条件
///         foreach (AbstractIntent intent in GetIntents())
///             list.AddRange(intent.AssetPaths);
///         return list;
///     }
/// }
/// ```
/// 而 <c>VisualsPath</c>（MonsterModel.cs:75）默认按 id 拼出
/// <c>res://scenes/creature_visuals/&lt;Id.Entry.ToLowerInvariant()&gt;.tscn</c>。
///
/// BaseLib 的 <c>CustomMonsterModel</c> 考虑到了这个缺口，但**只补了一半**：
/// - <c>CreateVisuals</c>（:115-126）prefix：构建时改走 <c>CreateCustomVisuals()</c>，OK；
/// - <c>RegisterSceneConversions</c>（:109-112）用 <c>CustomVisualPath?.RegisterSceneForConversion…</c>
///   —— 空安全，<c>CustomVisualPath == null</c> 时不注册，OK；
/// - **唯独没有覆写 <c>AssetPaths</c>**，于是预加载阶段仍去加载那个不存在的路径。
///
/// BaseLib 自己的注释其实点明了这个要求（CustomMonsterModel.cs:24-36）：
/// > Use if you want to generate creature visuals entirely yourself.
/// > **If you do, you'll also need to override AssetPaths to not include VisualPath.**
///
/// 【实际症状】
/// 预加载阶段 <c>AssetLoadingSession.CheckLoadingStatus()</c> 去
/// <c>ResourceLoader.Load</c> 一个不存在的 tscn，日志台连报：
/// ```
/// [ERROR] Cannot open file 'res://scenes/creature_visuals/baldurs_enemys-isobel.tscn'.
/// [ERROR] Failed to load resource: res://scenes/creature_visuals/baldurs_enemys-ketheric_thorm.tscn
/// ```
/// 附带的 C# backtrace 落在 <c>AssetLoadingSession.CheckLoadingStatus → Process
/// → NAssetLoader._Process_Patch1</c>。注意 mod 的 ModelId 是
/// <c>baldurs_enemys-&lt;类名&gt;</c>，所以拼出的路径带 <c>baldurs_enemys-</c> 前缀
/// （报错里的 <c>ketheric_thorm</c> 正是下划线形式，引擎用 <c>Id.Entry</c> 原样小写）。
///
/// 【为什么必须修，而不是"无害的日志噪音"】
/// 1. 该 tscn 会进入 <c>PreloadManager</c> 的预加载会话，失败项会让
///    <c>AssetLoadingSession</c> 的完成判定走异常分支（同步 Load 失败即抛），
///    属于在**原版加载状态机里制造失败项**——这正是 21:05 那次「预加载窗口内
///    做原生操作 → 后续 0xC0000005」的同类风险面，不该留；
/// 2. <c>MonsterModel.CreateVisuals</c>（:278-290）虽有 try/catch 回退到
///    <c>creature_visuals/fallback</c>，但那只兜住"构建"这一刻；
/// 3. 同理，日志台每次进战斗都刷一批 ERROR 会掩盖真正的错误。
///
/// 【本类如何修】
/// 覆写 <see cref="AssetPaths"/>：原版第一项固定是 <c>VisualsPath</c>，
/// 直接跳过它、只保留各 <c>AbstractIntent</c> 自己的资源即可。
/// 因为 <c>AssetPaths</c> 是 <c>virtual</c>，子类无需任何改动即可受益。
/// </summary>
public abstract class BaldurMonsterModel : CustomMonsterModel
{
    /// <summary>
    /// 去掉原版无条件包含的 <c>VisualsPath</c>（一个我们从不提供的 <c>.tscn</c>），
    /// 只保留各招式的资源。子类若将来真的要提供 <c>.tscn</c>，需自行把
    /// <c>CustomVisualPath</c> 加回来。
    /// </summary>
    public override IEnumerable<string> AssetPaths
    {
        get
        {
            List<string> assetPaths = [];
            foreach (AbstractIntent intent in CollectIntents())
            {
                assetPaths.AddRange(intent.AssetPaths);
            }

            return assetPaths;
        }
    }

    /// <summary>
    /// 等价复刻原版 <c>MonsterModel.GetIntents()</c>（:297-309）——后者是 private，
    /// 子类无法调用，故此处用可访问的 <c>GenerateMoveStateMachine()</c> 走同一份逻辑。
    /// 原版只收集「是 move 的状态」上的 intents。
    /// </summary>
    private List<AbstractIntent> CollectIntents()
    {
        List<AbstractIntent> intents = [];
        MonsterMoveStateMachine stateMachine = GenerateMoveStateMachine();
        foreach (MonsterState state in stateMachine.States.Values)
        {
            if (state.IsMove && state is MoveState moveState)
            {
                intents.AddRange(moveState.Intents);
            }
        }

        return intents;
    }
}
