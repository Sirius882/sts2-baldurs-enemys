using BaseLib.Patches.UI;
using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;
using baldurs_enemys.src.Cards;
using baldurs_enemys.src.Core;
using baldurs_enemys.src.Encounters;
using baldurs_enemys.src.Monsters;
using baldurs_enemys.src.Powers;
using baldurs_enemys.src.Relics;

namespace baldurs_enemys;

/// <summary>
/// 显式 mod 入口。
///
/// BaseLib 不会自动实例化任何模型 —— 它只在 <see cref="CustomEncounterModel"/> 的构造函数里把自己
/// 塞进 CustomContentDictionary，所以遭遇与单例必须在 Init 里显式 new 出来，否则：
/// - 遭遇：CustomEncounters 列表为空 → AddActContent postfix 拿不到任何东西 → 永远不入池；
/// - 单例：SubscribeForCombatStateHooks 没被调用 → 收不到战斗钩子 → 「拾取」卡永远不会被塞进手牌。
///
/// 怪物 / power / 卡牌由 ModelDb 通过反射发现（AllAbstractModelSubtypes 会扫 mod 程序集），
/// 显式 new 一遍是为了让 BaseLib 的登记（RegisterType / AddModel）在任何 ModelDb 查询之前完成，无害且稳妥。
///
/// 本 mod 打的 Harmony 补丁（PatchAll 自动发现）：
/// - src/Patches/OvergrowthBossDiscoveryOrderPatch.cs：把明萨拉追加进第一幕 BossDiscoveryOrder，
/// - src/Patches/HiveBossDiscoveryOrderPatch.cs：把凯瑟里克/米尔寇追加进第二幕 BossDiscoveryOrder，
/// - src/Patches/GloryBossDiscoveryOrderPatch.cs：把奥林/戈塔什/拉斐尔/君主追加进第三幕 BossDiscoveryOrder
///   （否则"未见过优先"机制会让自定义 Boss 几乎不出现）；
/// - src/Patches/PrimaryEnemyDeathTrackerPatch.cs：跟踪"主要敌人死亡处理帧"深度，
///   供死亡引爆排除爪牙连锁击败（设计稿：只排除爪牙效果，连锁爆炸保留）；
/// - src/Music/BaldurMusicController.cs：mod 战斗与火堆/先古/通关房间的自定义 BGM 接管/交还。
/// 入池、发牌、钩子全部走 BaseLib / 引擎公开扩展点，PatchAll 负责应用。
/// </summary>
[ModInitializer(nameof(Init))]
public static class BaldursEnemysEntry
{
    private static Harmony? _harmony;

    public static void Init()
    {
        _harmony = new Harmony("sts2.baldurs_enemys");
        _harmony.PatchAll(typeof(BaldursEnemysEntry).Assembly);

        // ---- 遭遇（必须显式构造，否则不入池）----
        _ = new MintharaBoss();      // 第一层 Boss
        _ = new YurgElite();         // 第二层精英
        _ = new BalthazarElite();    // 第二层精英
        _ = new KethericThormBoss(); // 第二层 Boss
        _ = new MyrkulBoss();        // 第二层 Boss
        _ = new ViconiaElite();      // 第三层精英
        _ = new CazadorElite();      // 第三层精英
        _ = new AnsurElite();        // 第三层精英
        _ = new OrinBoss();          // 第三层 Boss
        _ = new GortashBoss();       // 第三层 Boss
        _ = new RaphaelBoss();       // 第三层 Boss
        _ = new EmperorBoss();       // 第三层 Boss

        // ---- 单例驱动器 ----
        // 不在此显式构造：ModelDb 的反射扫描会构造所有 AbstractModel 子类型（含 CatchCombatDriver），
        // 而 CustomSingletonModel 构造函数本身即订阅战斗钩子 —— 显式 new 会导致重复订阅报错
        // （"hook id already been used"）。依赖 ModelDb 反射实例化一次即可。

        // ---- 怪物 ----
        _ = new GoblinMelee();
        _ = new GoblinRanged();
        _ = new OilBarrel();
        _ = new Minthara();
        _ = new Yurg();
        _ = new Skeleton();
        _ = new Balthazar();
        _ = new Nightsong();
        _ = new Isobel();
        _ = new KethericThorm();
        _ = new MyrkulApostle();
        _ = new Viconia();
        _ = new Cazador();
        _ = new Bat();
        _ = new Ansur();
        _ = new global::baldurs_enemys.src.Monsters.BalduransGiantslayer();
        _ = new Hostage();
        _ = new Orin();
        _ = new SteelWatcher();
        _ = new Gortash();
        _ = new SoulPillar();
        _ = new Raphael();
        _ = new DreamGuardian();
        _ = new Emperor();

        // ---- power ----
        _ = new PickpocketablePower();
        _ = new DeathExplosionPower();
        _ = new GuardAuraPower();
        _ = new ValorAuraPower();
        _ = new DeathCloudPower();
        _ = new PickpocketableBigPower();
        _ = new SelunesBlessingPower();
        _ = new ShroudVeilPower();
        _ = new BloodFeastPower();
        _ = new BloodArmorPower();
        _ = new BetrayedPower();
        _ = new OverloadMeltdownPower();
        _ = new BanesCommandPower();
        _ = new SoulSacrificePower();
        _ = new PsionicShieldPower();
        _ = new KarsusCompulsionPower();
        _ = new BhaalsBlessingPower();
        _ = new TimedDebuffExpiryPower();
        _ = new SkeletonImbalancedPower();

        // ---- 遗物（CustomRelicModel 构造时经 AddModel 登记；挂在 mod 专属池 BaldursRelicPool 上，
        //      该池不参与任何随机掉落，唯一来源只能是拾取暗夜之歌 / 拾取巨人杀手）----
        // 注意：BaldursRelicPool 内部 IsShared=>true 是【必须】的，否则图鉴会判定为「已锁定」。
        //      详见该池类注释里的完整证据链。
        _ = new BaldursRelicPool();
        _ = new DameAylin();
        _ = new global::baldurs_enemys.src.Relics.BalduransGiantslayer();

        // ---- 遗物图标覆盖（mod 自带美术，packed 64 / big 256 共用同一张原图，UI 自行缩放）----
        RelicIconData giantslayerIcon = new(
            "res://baldurs_enemys/images/relics/baldurans_giantslayer.png",
            "res://baldurs_enemys/images/relics/baldurans_giantslayer.png",
            "res://baldurs_enemys/images/relics/baldurans_giantslayer.png");
        RelicImageOverridePatch.AddOverride<global::baldurs_enemys.src.Relics.BalduransGiantslayer>(giantslayerIcon);
        RelicIconData dameAylinIcon = new(
            "res://baldurs_enemys/images/relics/dame_aylin.png",
            "res://baldurs_enemys/images/relics/dame_aylin.png",
            "res://baldurs_enemys/images/relics/dame_aylin.png");
        RelicImageOverridePatch.AddOverride<DameAylin>(dameAylinIcon);

        // ---- 卡牌 ----
        _ = new Pickup();

        // ---- 背景音乐控制器（订阅主线程帧回调，见 BaldurMusicController 类注释的架构约定）----
        baldurs_enemys.src.Music.BaldurMusicController.Initialize();
    }
}
