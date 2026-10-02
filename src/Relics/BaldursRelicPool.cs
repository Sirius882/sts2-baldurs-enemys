using BaseLib.Abstracts;

namespace baldurs_enemys.src.Relics;

/// <summary>
/// mod 私有遗物池（IsShared=false → 不会注册进任何共享奖励池）。
///
/// 存在目的：新版 BaseLib 要求每个 CustomRelicModel 必须标 [Pool(池类型)]，
/// 否则 ModelDb 初始化直接抛异常。艾琳女士 / 博德安的巨人杀手的唯一来源是「拾取」，
/// 不能进任何会掉落的池 —— 所以给它们挂这个永不使用的私有池，语义等价于"不入池"。
/// </summary>
public class BaldursRelicPool : CustomRelicPoolModel
{
}
