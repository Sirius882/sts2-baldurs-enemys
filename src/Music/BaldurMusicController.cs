using System;
using System.Collections.Generic;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Audio;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;

namespace baldurs_enemys.src.Music;

/// <summary>
/// mod 战斗/特殊房间背景音乐控制器（2026-10-02）。
///
/// 规则（用户 2026-10-02 指定）：
/// 1. 指明战斗名称的曲目与对应战斗互相专属，不参与随机：
///    明萨拉 / 凯瑟里克 / 巴萨扎尔 / 奥林 / 安苏 / 拉斐尔。
/// 2. 无专属 BGM 的 mod Boss（戈塔什 / 君主 / 米尔寇使徒——米尔寇是 Boss 级、
///    不走非 boss 曲池）统一从「第三层boss1/2/3」中随机。
/// 3. 其他所有 mod 战斗（维康妮亚 / 卡扎多尔 / 尤格）从「其他非boss战斗1/2」中随机。
/// 4. 火堆 / 先古之民 / 通关结算（建筑师）房间：从「火堆、先古、通关bgm1/2/3」中随机，
///    **叠加**在原版音效之上——原版这三处本来只有进入/休息/锻造等音效、没有 BGM，
///    所以是「保留原版音效 + 额外加 BGM」，不是「替换原版音乐」。
///    实现上只把 FMOD 的 music 总线音量压到 0 来避免与原版 act 音乐叠加，**从不销毁任何
///    FMOD 事件、从不卸载 act bank**，因此原版的环境音与音效完全不受影响。
/// 5. 随机池的曲目播完后不是单曲循环，而是再在当前池内随机抽下一首（避免连续重复）；
///    专属曲目播完后重播自己（等价单曲循环）。
/// 6. 原版战斗与原版其他房间完全不受影响（原版 FMOD 音乐照常）。
///
/// 架构（2026-10-02 崩溃修复后的定型版，务必保持）：
/// 原版在「战斗开始/结束、进出房间」时都会调 NRunMusicController.UpdateTrack()，它是音乐
/// 上下文切换的天然时机。但**绝不能在这个 postfix 里直接接管**：
/// 接管要做的三件事——StopMusic()（停 FMOD 并卸载 act bank，GDScript + FMOD GDExtension 原生调用）、
/// new AudioStreamPlayer + AddChild（场景树变更）、GD.Load MP3（同步资源加载）——全部是
/// 托管↔原生边界操作。插在原版 UpdateTrack 栈中间执行时，v0.6.0 首次实测进火堆房即
/// 0xC0000005 访问违例硬崩（Windows 事件日志：SlayTheSpire2.exe 故障模块 coreclr.dll），
/// 表现是卡死黑屏后闪退，且因为崩在原生层，日志里连一行异常都没有。
///
/// 因此改成帧驱动：Harmony postfix 只做两件纯托管、无副作用的事——捕获 IRunState、置脏标记；
/// 真正的接管/交还由 SceneTree.ProcessFrame 回调执行。ProcessFrame 由引擎在主线程每帧派发，
/// 既保证在主线程，又保证已经退出原版的 UpdateTrack 调用栈。
/// 订阅在 mod Init（主线程）与 SetRunState（开局主线程）完成，postfix 里的订阅只是兜底。
///
/// 【2026-10-02 20:31 崩溃的真正根因（修正此前「postfix 内同步操作」的判断）】
/// 帧驱动本身没错，但**光有帧驱动还不够**：它并没有避开原版的资源预加载窗口。
/// RunManager.EnterRoom 的时序（GameBeta/src/Core/Runs/RunManager.cs）：
///   1220  State.PushRoom(room);        ← CurrentRoom 在这里就已换成新房间
///   1225  await room.Enter(...);        ← 之后才进 RestSiteRoom.EnterInternal 预加载
///   1228  NRunMusicController.Instance?.UpdateTrack();
/// 而 RestSiteRoom.EnterInternal 里 `await PreloadManager.LoadRoomRestSite(...)` 跨多帧，
/// 期间 NAssetLoader._Process 每帧调用 AssetLoadingSession.Process()，在**后台线程**跑
/// ResourceLoader.LoadThreadedRequest。原版在 AssetLoadingSession.cs:38-45 明确注释过
/// 「Godot 的线程加载器在共享子资源上会 race」，所以这段时间是明确的危险窗口。
///
/// 只要在这个窗口里置脏（上一首曲子自然播完的 Finished 回调、SetRunState、任何 UpdateTrack），
/// 下一帧 Resolve() 就会按**新房间**（RestSiteRoom）判定出 PeacePool 并立刻 TakeOver()：
///   StopMusic()  → GDScript → FMOD 原生调用 + 卸载 act bank
///   GD.Load(MP3) → 主线程同步加载 8MB 资源，与后台 LoadThreadedRequest 抢同一批资源
///   AddChild()   → 在房间场景构建期间插入节点
/// 三者叠加即 0xC0000005，故障模块 coreclr.dll（偏移 0x2e9302，4 次崩溃完全一致）。
/// 崩溃帧日志停在 `Preloading 'RestSite Room' assets...` 之后、`... Complete` 之前，
/// 且 [baldurs_enemys] music takeover 行**已经打印成功**——即接管本身完成了，是紧随其后的
/// 原版预加载收尾（FinalizeLoading / LoadThreadedGet）阶段撞上被破坏的加载状态才崩。
/// 所以崩点不在 mod 的 C# 代码里，而在 mod 污染了原版的加载状态机。
///
/// 修复 = 加预加载闸门：patch NAssetLoader._Process 作为「后台还在泵预加载」的探针
/// （postfix 里只改一个 int，是最纯的托管操作，不碰任何 Godot 节点）；帧回调在探针连续
/// RequiredIdleFrames 帧未触发前**保持脏标记不处理**，推迟到预加载真正结束后再接管。
/// 只延迟 2 帧，听不出延迟感。
///
/// 【2026-10-02 21:11「能进火堆、一锻造就卡死」的真正根因（修正此前全部判断）】
/// 上一轮把进火堆房崩溃归因于「预加载窗口内同步 GD.Load/AddChild 污染加载状态机」，并加了预加载闸门。
/// 闸门确实生效了（21:11 日志：takeover 打在 'RestSite Room' Complete 之后，不再在窗口内），
/// 进房崩溃随之消失。但**锻造卡死是另一个 bug，根因是调用了 NRunMusicController.StopMusic()**。
///
/// 先分清「音乐」与「音效」——原版这两者走的是不同 FMOD 事件：
///   music_controller_proxy.gd：_musicEv（BGM，update_music/stop_music）
///                              _ambienceEv（**环境音/音效**，update_ambience/stop_ambience）
///   NRunMusicController.StopMusic()（第 283-294 行）实际做了四件事：
///     ① update_global_parameter("Progress", 0)
///     ② stop_music()    → 停 _musicEv
///     ③ stop_ambience() → **停并 release() 掉 _ambienceEv，置 null**（这是音效！）
///     ④ UnloadActBanks()→ _bank_loader.free()，把 act bank 从 FMOD 里卸载
///
/// 而原版在火堆房**全程都在用 _ambienceEv**：
///   - UpdateTrack()（第 225-228 行）：只要 CurrentRoom 是 RestSiteRoom 就调
///     `update_campfire_ambience(0)`；music_controller_proxy.gd:53-59 该函数**直接对
///     _ambienceEv 调 set_parameter_by_name**，没有 null 检查——若 _ambienceEv 已被 release 成
///     null，这就是一次对已释放 FMOD 对象的原生访问 → 0xC0000005。
///   - 锻造完成时 NRestSiteRoom.ExtinguishFireIfAble()（第 438-457 行，在
///     AfterSelectingOptionAsync 第 382 行被调用）→ TriggerCampfireGoingOut()（第 271-281 行）
///     → 同样调 `update_campfire_ambience(1)`。
/// 这就精确解释了用户描述的症状：「能进火堆房」（进房时 _ambienceEv 还活着），
/// 「一锻造卡牌就卡死」（锻造触发 update_campfire_ambience(1)，此时 _ambienceEv 已是 null）。
/// 21:11 的崩溃日志正好终止于 `Player 1 chose cards [...]`，即锻造选卡那一步，之后无任何输出。
///
/// 另外 ③ 释放 ambience、④ 卸载 act bank 也直接违背「保留原版音效」的需求本身。
/// 故修复方向不是「更小心地调 StopMusic」，而是**根本不调它**。
///
/// 修复（2026-10-02 21:20）：
/// - TakeOver() 不再调 StopMusic()。改为调 NAudioManager.SetBgmVol(0) 把 FMOD 的
///   `bus:/master/music` 总线音量压到 0：原版 BGM 仍在播放、_musicEv / _ambienceEv / act bank
///   全部保持存活，只是听不到，从而与 mod BGM 叠加而不冲突。这是非破坏性的静音，不是停事件。
/// - RestoreVanilla 不再需要 UpdateMusic()+UpdateTrack() 重建（我们从没停过它），只需把
///   BGM 总线音量按玩家设置恢复回去。
/// - 保留原有预加载闸门（进房崩溃的修复依然有效且必要）。
///
/// 其它稳定性约定：
/// - AudioStreamPlayer.Finished 信号回调里也不做节点/资源操作，只置脏，统一由帧回调决策；
/// - 音频流按路径缓存，换曲不重复 GD.Load；
/// - 接管失败（资源缺失等）走 FailSafe：恢复原版 BGM 音量并记下失败池，同一池不再重试，
///   避免每帧重试；
/// - 纯客户端表现层，不参与联机锁步。
/// </summary>
[HarmonyPatch]
public static class BaldurMusicController
{
    // ---- 曲目清单（res://baldurs_enemys/music/ 下，文件名见仓库 musics/ 原始文件映射）----
    private const string Dir = "res://baldurs_enemys/music/";

    private const string TrackMinthara = Dir + "minthara.mp3";
    private const string TrackKetheric = Dir + "ketheric.mp3";
    private const string TrackBalthazar = Dir + "balthazar.mp3";
    private const string TrackOrin = Dir + "orin.mp3";
    private const string TrackAnsur = Dir + "ansur.mp3";
    private const string TrackRaphael = Dir + "raphael.mp3";

    private static readonly string[] Act3BossPool =
    [
        Dir + "act3_boss_1.mp3",
        Dir + "act3_boss_2.mp3",
        Dir + "act3_boss_3.mp3",
    ];

    private static readonly string[] NormalBattlePool =
    [
        Dir + "normal_battle_1.mp3",
        Dir + "normal_battle_2.mp3",
    ];

    private static readonly string[] PeacePool =
    [
        Dir + "peace_1.mp3",
        Dir + "peace_2.mp3",
        Dir + "peace_3.mp3",
    ];

    /// <summary>专属映射：遭遇类型 → 唯一曲目（不参与随机）。</summary>
    private static readonly Dictionary<Type, string> ExclusiveTracks = new()
    {
        [typeof(baldurs_enemys.src.Encounters.MintharaBoss)] = TrackMinthara,
        [typeof(baldurs_enemys.src.Encounters.KethericThormBoss)] = TrackKetheric,
        [typeof(baldurs_enemys.src.Encounters.BalthazarElite)] = TrackBalthazar,
        [typeof(baldurs_enemys.src.Encounters.OrinBoss)] = TrackOrin,
        [typeof(baldurs_enemys.src.Encounters.AnsurElite)] = TrackAnsur,
        [typeof(baldurs_enemys.src.Encounters.RaphaelBoss)] = TrackRaphael,
    };

    // ---- 运行时状态 ----
    private static IRunState? _runState;
    private static AudioStreamPlayer? _player;
    private static string[]? _currentPool;   // null = 未接管（原版音乐）
    private static string[]? _failedPool;    // 接管失败过的池：不再重试，避免每帧重试
    private static string? _lastTrack;       // 当前池内避免连续重复
    private static readonly Dictionary<string, AudioStream> StreamCache = new();
    private static readonly Random Rng = new();

    private static SceneTree? _tree;
    private static bool _subscribed;
    private static bool _dirty;
    private static bool _resolving;

    /// <summary>要求连续多少帧「NAssetLoader 没有在泵预加载会话」才允许动音乐。</summary>
    private const int RequiredIdleFrames = 2;

    /// <summary>连续未泵帧数；由 NAssetLoader._Process 的 postfix 归零。</summary>
    private static int _loadIdleFrames;

    /// <summary>当前是否由本控制器接管音乐。</summary>
    private static bool IsOurs => _player != null && GodotObject.IsInstanceValid(_player);

    /// <summary>
    /// mod 初始化时调用（主线程）：尽早订阅帧回调，避免首个音乐切换要等 postfix 兜底。
    /// </summary>
    public static void Initialize()
    {
        RequestResolve();
    }

    // ---- Harmony：只捕获状态与置脏，不做任何 Godot / 资源操作 ----

    [HarmonyPatch(typeof(NRunMusicController), nameof(NRunMusicController.SetRunState))]
    [HarmonyPostfix]
    private static void CaptureRunState(IRunState runState)
    {
        _runState = runState;
        RequestResolve();
    }

    // 显式指定无参重载：UpdateTrack(string, float) 是私有重载，按名匹配会 AmbiguousMatch
    [HarmonyPatch(typeof(NRunMusicController), nameof(NRunMusicController.UpdateTrack), new Type[] { })]
    [HarmonyPostfix]
    private static void OnUpdateTrack()
    {
        RequestResolve();
    }

    /// <summary>
    /// 预加载探针：原版每次在后台线程推进资源预加载会话时都会调用 NAssetLoader._Process。
    /// 只要它还在被调用，就说明 AssetLoadingSession 的 ResourceLoader.LoadThreadedRequest
    /// 还在跑——此时绝不能做同步 GD.Load / AddChild / FMOD 原生调用。
    /// 这里只改一个 int，是最纯的托管操作，绝不碰 Godot 节点。
    /// </summary>
    [HarmonyPatch(typeof(NAssetLoader), nameof(NAssetLoader._Process))]
    [HarmonyPostfix]
    private static void OnAssetLoaderProcess()
    {
        _loadIdleFrames = 0;
    }

    /// <summary>标记「音乐上下文可能变了」，实际处理延后到主线程的帧回调。</summary>
    private static void RequestResolve()
    {
        EnsureSubscribed();
        _dirty = true;
    }

    private static void EnsureSubscribed()
    {
        if (_subscribed) return;
        try
        {
            SceneTree? tree = Engine.GetMainLoop() as SceneTree;
            if (tree == null) return; // 引擎还没就绪，等下一次 postfix / Init 再试
            tree.ProcessFrame += OnProcessFrame;
            _tree = tree;
            _subscribed = true;
        }
        catch (Exception e)
        {
            GD.PushWarning($"[baldurs_enemys] music controller subscribe failed: {e.Message}");
        }
    }

    /// <summary>
    /// 主线程每帧回调：真正执行接管/交还。
    /// 放在这里而不是 Harmony postfix，是为了不插入原版 UpdateTrack 的调用栈（见类注释）。
    /// </summary>
    private static void OnProcessFrame()
    {
        // 资源预加载闸门：只要后台线程还在 LoadThreadedRequest，就保持脏标记推迟处理。
        // 原本的问题是置脏后下一帧立刻接管，而 RunManager.EnterRoom 在 PushRoom()（第 1220 行）
        // 就换了 CurrentRoom，之后才 await room.Enter 预加载（第 1225 行）；这个窗口里同步
        // GD.Load / AddChild / StopMusic(→FMOD) 会与原版的线程加载器抢同一批资源，
        // 结果是 coreclr.dll 0xC0000005 硬崩（日志无任何异常行）。
        _loadIdleFrames++;
        if (!_dirty || _resolving) return;
        if (_loadIdleFrames < RequiredIdleFrames) return;

        _dirty = false;
        _resolving = true;
        try
        {
            Resolve();
        }
        catch (Exception e)
        {
            // 音乐是表现层，任何异常都不能打断战斗/房间流程
            GD.PushWarning($"[baldurs_enemys] music controller error: {e}");
            SafeRestoreVanilla();
        }
        finally
        {
            _resolving = false;
        }
    }

    /// <summary>根据当前房间/战斗上下文决定：接管（选池播放）或交还（原版音乐）。</summary>
    private static void Resolve()
    {
        string[]? desired = ResolvePool();
        if (desired == null)
        {
            if (IsOurs)
            {
                RestoreVanilla();
            }
            return;
        }

        // 已在同一个池中 → 让播放列表自然继续（曲目结束回调会置脏续抽）。
        // 按内容比较而不是引用比较：专属曲目每次 ResolvePool 都会 new 一个单元素数组。
        if (IsOurs && SamePool(_currentPool, desired))
        {
            return;
        }

        TakeOver(desired);
    }

    /// <summary>
    /// 解析当前上下文应使用的曲目池；null 表示不属于 mod 音乐的场合（交还原版）。
    /// 判定顺序：火堆/先古/通关 → mod 专属遭遇 → 第三层 mod Boss 池 → 其他 mod 战斗池。
    /// </summary>
    private static string[]? ResolvePool()
    {
        AbstractRoom? room = _runState?.CurrentRoom;
        if (room == null) return null;

        // 火堆 / 先古之民 / 通关结算（建筑师）：原版这三处只有音效、没有 BGM，
        // 所以保留原版音效、直接叠加 mod BGM（接管时只把 BGM 总线音量压到 0）。
        if (room.RoomType == RoomType.RestSite
            || (room is EventRoom eventRoom && (eventRoom.CanonicalEvent is AncientEventModel || room.IsVictoryRoom)))
        {
            return PeacePool;
        }

        // mod 战斗：战斗进行中且遭遇属于本 mod
        if (CombatManager.Instance.IsInProgress && room is CombatRoom combatRoom)
        {
            EncounterModel? encounter = combatRoom.Encounter;
            if (encounter == null) return null;
            if (encounter.GetType().Assembly != typeof(BaldursEnemysEntry).Assembly) return null;

            if (ExclusiveTracks.TryGetValue(encounter.GetType(), out string? exclusive))
            {
                return [exclusive];
            }

            // 无专属的 mod Boss（戈塔什 / 君主 / 米尔寇使徒）：统一走 Boss 曲池
            if (encounter.RoomType == RoomType.Boss)
            {
                return Act3BossPool;
            }

            // 其他 mod 战斗
            return NormalBattlePool;
        }

        return null;
    }

    /// <summary>停掉自建播放器并把原版 FMOD 音乐接回来。</summary>
    private static void RestoreVanilla()
    {
        StopPlayer();
        SafeRestoreVanilla();
    }

    /// <summary>
    /// 恢复原版 BGM 音量。
    ///
    /// 关键：本控制器**从不销毁**原版 FMOD 事件（不调 StopMusic），只是把 music 总线音量压到 0
    /// 来避免与 mod BGM 叠加。所以「恢复」只是把音量按玩家设置调回去，不需要重建任何东西。
    /// 原版的环境音（_ambienceEv）与 act bank 全程存活，锻造/休息音效因此不受影响。
    /// </summary>
    private static void SafeRestoreVanilla()
    {
        try
        {
            NAudioManager.Instance?.SetBgmVol(BgmBusVolume());
        }
        catch (Exception e)
        {
            GD.PushWarning($"[baldurs_enemys] restore vanilla music failed: {e.Message}");
        }
    }

    /// <summary>
    /// FMOD 的 bus:/master/music 总线音量，按玩家设置取值（与原版 NAudioManager.SetBgmVol 同口径）。
    /// </summary>
    private static float BgmBusVolume()
    {
        try
        {
            return SaveManager.Instance.SettingsSave.VolumeBgm;
        }
        catch (Exception)
        {
            return 1f;
        }
    }

    private static void TakeOver(string[] pool)
    {
        if (SamePool(_failedPool, pool)) return; // 该池已失败过，不再重试

        try
        {
            // 只静音 BGM 总线，绝不调 NRunMusicController.StopMusic()。
            // StopMusic() 会 stop_ambience() 释放掉 _ambienceEv 并卸载 act bank，而原版在
            // 火堆房全程依赖 _ambienceEv（UpdateTrack / TriggerCampfireGoingOut 都会对它
            // set_parameter_by_name 且无 null 检查）——那会在锻造时对已释放的 FMOD 对象做原生
            // 访问，触发 0xC0000005。参见类注释【21:11 锻造卡死的真正根因】。
            NAudioManager.Instance?.SetBgmVol(0f);
        }
        catch (Exception e)
        {
            GD.PushWarning($"[baldurs_enemys] mute vanilla bgm bus failed: {e.Message}");
        }

        _currentPool = pool;
        _lastTrack = null;
        PlayNext();
    }

    /// <summary>在当前池内随机抽一首（避免与上一首重复）并播放。</summary>
    private static void PlayNext()
    {
        if (_currentPool == null || _currentPool.Length == 0) return;

        string track = PickFromPool(_currentPool);

        AudioStreamPlayer? player = EnsurePlayer();
        if (player == null)
        {
            FailTakeOver();
            return;
        }

        if (!TryGetStream(track, out AudioStream? stream))
        {
            FailTakeOver();
            return;
        }

        _lastTrack = track;
        try
        {
            player.Stream = stream;
            player.VolumeLinear = VolumeLinear();
            player.Play();
            GD.Print($"[baldurs_enemys] music takeover -> {track}");
        }
        catch (Exception e)
        {
            GD.PushWarning($"[baldurs_enemys] play '{track}' failed: {e.Message}");
            FailTakeOver();
        }
    }

    /// <summary>带缓存的流加载：换曲不重复 GD.Load。</summary>
    private static bool TryGetStream(string path, out AudioStream? stream)
    {
        if (StreamCache.TryGetValue(path, out AudioStream? cached) && GodotObject.IsInstanceValid(cached))
        {
            stream = cached;
            return true;
        }

        try
        {
            stream = GD.Load<AudioStream>(path);
        }
        catch (Exception e)
        {
            GD.PushWarning($"[baldurs_enemys] load '{path}' threw: {e.Message}");
            stream = null;
        }

        if (stream == null)
        {
            GD.PushWarning($"[baldurs_enemys] missing music resource: {path}");
            return false;
        }

        StreamCache[path] = stream;
        return true;
    }

    /// <summary>接管失败：恢复原版 BGM 音量并标记该池不可再试，避免原版音乐被永久静音。</summary>
    private static void FailTakeOver()
    {
        GD.PushWarning("[baldurs_enemys] music takeover failed; falling back to vanilla music");
        _failedPool = _currentPool;
        RestoreVanilla();
    }

    /// <summary>曲目池按内容比较（专属曲目每次都会新建单元素数组，不能用引用比较）。</summary>
    private static bool SamePool(string[]? a, string[]? b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a == null || b == null) return false;
        if (a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++)
        {
            if (a[i] != b[i]) return false;
        }
        return true;
    }

    private static string PickFromPool(string[] pool)
    {
        if (pool.Length == 1) return pool[0];
        if (pool.Length == 2 && pool[0] == _lastTrack) return pool[1];
        if (pool.Length == 2 && pool[1] == _lastTrack) return pool[0];

        string pick;
        do
        {
            pick = pool[Rng.Next(pool.Length)];
        } while (pick == _lastTrack);
        return pick;
    }

    private static AudioStreamPlayer? EnsurePlayer()
    {
        if (IsOurs) return _player;

        StopPlayer(); // 清理失效引用

        SceneTree? tree = _tree ?? Engine.GetMainLoop() as SceneTree;
        // 挂在 NRun 下（与 BaseLib ModAudio / Aemeath 音乐播放器同口径）；NRun 不在场时退到当前场景 / 根节点
        Node? parent = (Node?)NRun.Instance ?? (Node?)tree?.CurrentScene ?? (Node?)tree?.Root;
        if (parent == null) return null;

        _player = new AudioStreamPlayer { Bus = "Master" };
        AudioStreamPlayer captured = _player;
        captured.Finished += OnTrackFinished;
        captured.TreeExited += () =>
        {
            // 随 run 场景销毁时复位状态（播放列表随场景终止）。
            // 注意：不在这里反注册 Finished —— 那会再去操作一个正在销毁的节点。
            if (ReferenceEquals(_player, captured))
            {
                _player = null;
                _currentPool = null;
                _lastTrack = null;
                // 场景没了但我们可能仍处于「已把原版 BGM 总线静音」的状态，
                // 不恢复音量会让玩家下一次开局全程没 BGM。这只是设一个总线音量，
                // 不涉及节点/资源操作，销毁期调用是安全的。
                SafeRestoreVanilla();
            }
        };
        parent.AddChild(_player);
        return _player;
    }

    private static void StopPlayer()
    {
        if (_player == null) return;
        if (GodotObject.IsInstanceValid(_player))
        {
            _player.Stop();
            _player.QueueFree();
        }
        _player = null;
        _currentPool = null;
        _lastTrack = null;
        GD.Print("[baldurs_enemys] music player stopped");
    }

    /// <summary>曲目自然播完：只置脏，由帧回调决定续抽还是切换曲池（不在信号回调里做节点/资源操作）。</summary>
    private static void OnTrackFinished()
    {
        _dirty = true;
    }

    /// <summary>
    /// 音量跟随设置里的 BGM 滑条（与 BaseLib ModAudio 同一口径：Master 总线 + BGM 线性音量）；
    /// 主音量静音时不播。返回 0 表示静音。
    /// </summary>
    private static float VolumeLinear()
    {
        try
        {
            float master = SaveManager.Instance.SettingsSave.VolumeMaster;
            if (master <= 0f) return 0f;
            float bgm = SaveManager.Instance.SettingsSave.VolumeBgm;
            if (bgm <= 0f) return 0f;
            return Mathf.Clamp(bgm, 0f, 1f);
        }
        catch (Exception)
        {
            return 1f; // 设置不可用时按满音量播，避免整个 mod 静音
        }
    }
}
