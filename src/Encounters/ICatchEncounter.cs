namespace baldurs_enemys.src.Encounters;

/// <summary>
/// 标记接口：表示"这场战斗会每回合发「交互」卡"。
/// 引擎没有"战斗参数"这种字段，所以用接口承载（规格 5.3）。
/// 后续若再加别的 catch 战斗，实现本接口即可复用发牌逻辑。
/// </summary>
public interface ICatchEncounter
{
    /// <summary>是否在本场战斗中给玩家发「交互」卡。</summary>
    bool CatchEnabled { get; }
}
