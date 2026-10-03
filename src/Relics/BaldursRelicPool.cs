using BaseLib.Abstracts;

namespace baldurs_enemys.src.Relics;

/// <summary>
/// mod 私有遗物池。
///
/// 存在目的：新版 BaseLib 要求每个 CustomRelicModel 必须标 [Pool(池类型)]，
/// 否则 ModelDb 初始化直接抛异常。艾琳女士 / 博德安的巨人杀手的唯一来源是「交互」，
/// 不能进任何会掉落的池——所以给它们挂这个专属池。
///
/// 【2026-10-03 重要修正】本池必须 IsShared =&gt; true，否则图鉴里两款遗物永远显示为「已锁定」。
///
/// 曾经的错误认知：「IsShared=false 的私有池 ≡ 不入池」，于是靠它实现「不会随机掉落」。
/// 这个假设是错的，因为图鉴的解锁判定链根本不经过「掉落池」，而只认「模型注册链」：
///   UnlockState.Relics
///     =&gt; ModelDb.AllRelicPools.Select(p =&gt; p.GetUnlockedRelics(this))
///   而 ModelDb.AllRelicPools（ModelDb.cs:268）
///     =&gt; CharacterRelicPools.Concat(AllSharedRelicPools)
///   AllSharedRelicPools 是 private static，只会被 BaseLib 的 ModelDbSharedRelicPoolsPatch
///   （postfix ModelDb.AllSharedRelicPools）追加，而该 patch 的 Register 只在
///   CustomRelicPoolModel 构造时 IsShared==true 才被调用（CustomRelicPoolModel.cs:14）。
///   ⇒ IsShared=false 的池压根不在 AllRelicPools 里 ⇒ UnlockState.Relics 枚举不到它
///   ⇒ NRelicCollectionCategory.cs:178 判 !unlockedRelics.Contains(relic)
///   ⇒ ModelVisibility.Locked ⇒ 悬停显示 main_menu_ui 表的通用「已锁定」提示，
///      而不是遗物自己的 title/description（这正是用户反馈的症状）。
/// 连带风险：RelicModel.Pool（RelicModel.cs:174）实现为
///   AllRelicPools.First(p =&gt; p.AllRelicIds.Contains(Id))，
///  遗物不属于任何池时会抛 InvalidOperationException（EnergyIconHelper.cs:81 会走到）。
///
/// 为什么 IsShared=true 不会导致遗物被随机掉落：
/// 随机掉落的来源是硬编码指定某一个池，不存在「遍历所有池」：
///   - RelicGrabBag.cs:75 与 RunManager.cs:522 都写死 ModelDb.RelicPool&lt;SharedRelicPool&gt;()
///   - 角色专属池由 CharacterModel.RelicPool 指向（IroncladRelicPool 等），与本池无关
///   - EventRelicPool 虽是 shared，但引擎里没有任何掉落路径引用它（全仓库仅 ModelDb 的
///     AllSharedRelicPools 列表与 AbstractModelSubtypes 引用它）
/// 也就是说 AllSharedRelicPools 的成员资格只影响「模型注册 / 图鉴 / 存档」，不影响掉落。
///
/// 双重保险仍然保留：两个遗物各自还有 IsAllowedInShops=&gt;false + RelicRarity.Event。
/// </summary>
public class BaldursRelicPool : CustomRelicPoolModel
{
    /// <summary>
    /// 必须为 true，否则图鉴判定为「已锁定」。理由见类注释的完整证据链。
    /// </summary>
    public override bool IsShared => true;

    /// <summary>
    /// 图鉴默认标记为「已见过」。这两款遗物只能靠特定交互卡获得，若保持未发现状态，
    /// 玩家在图鉴里点开只能看到 UnknownHoverTip（通用「未知遗物」提示），
    /// 仍然读不到实际效果说明——等同于没有说明文档。设为 true 后图鉴直接显示完整说明。
    /// </summary>
    public override bool SeenByDefault => true;
}
