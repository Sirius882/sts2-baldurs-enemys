# 敌人与战斗 mod 制作清单

> 目的：回答「往弱怪池 / 强怪池 / 精英怪池 / boss 怪池里加备选战，需要准备哪些**设计稿**和哪些**美术资源**」。
>
> 结论来源：`GameBeta/src`（游戏测试版 v0.111.0 解包源码）、`BaseLib/`（Alchyr.Sts2.BaseLib 3.3.0 源码）、游戏本体 PCK（`SlayTheSpire2.pck`，2 GB，F 盘）实测扫描。
> 本文只写机制事实与需求清单，不写具体数值设计。

---

## 0. 一句话结论

技术锚点是 **两个基类 + 一个判定函数**：

```
CustomMonsterModel   → 一只怪（血量 / 意图状态机 / 动画 / 音效）
CustomEncounterModel → 一场战斗（幕判定 / 池归属 / 站位 / 出场怪）
   IsValidForAct(act) → 这场战斗能出现在哪一幕
```

**四个池子不是可配置的数据结构**，而是引擎对同一张「本幕全部遭遇」列表按 `RoomType` + `IsWeak` 现场过滤出来的视图（`ActModel.cs:144-164`）。所以：**你不需要向任何池子注册任何东西**，只要让遭遇在正确的幕返回 `true`、并把 `RoomType` / `IsWeak` 写对，它自己就会落进对应的池子。BaseLib 已经在启动时给所有 `ActModel` 的 `GenerateAllEncounters()` 打好了 postfix，连 Harmony 补丁都不用自己写。

真正的工作量在两件事上：

1. **每个战斗一份设计稿**（编成 / 数值 / 意图时序 / 进阶曲线 / 文案 / 验收用例）；
2. **每只怪一套美术 + 音频**（Spine 骨骼动画 + Godot 场景 + 意图依赖 + 音效）。

---

## 1. 池子机制（先搞清楚，否则设计稿会白写）

### 1.1 过滤规则

`ActModel.cs:144-169`，`AllEncounters` = `GenerateAllEncounters()`，其余四个池全是它的过滤视图：

| 池子 | 判定条件 | 备注 |
|---|---|---|
| 弱怪池 | `RoomType == Monster && IsWeak == true` | 只出现在每幕**开局前几层** |
| 强怪池（普通池） | `RoomType == Monster && IsWeak == false` | |
| 精英池 | `RoomType == Elite` | |
| Boss 池 | `RoomType == Boss` | 与精英/普通不共用抽取逻辑 |

### 1.2 每幕参数

| 幕 | 类 | `Index` | `NumberOfWeakEncounters` | `BaseNumberOfRooms` | 精英池预抽数 |
|---|---|---|---|---|---|
| 第一幕（默认） | `Overgrowth` | 0 | **3** | 15 | 15 |
| 第一幕（替代） | `Underdocks` | 0 | **3** | 15 | 15 |
| 第二幕 | `Hive` | 1 | **2** | 14 | 15 |
| 第三幕 | `Glory` | 2 | **2** | 13 | 15 |

- 多人模式：`GetNumberOfRooms()` 会 **-1**。
- `ActNumber()`（BaseLib 扩展）= `Index + 1`。注意 **Overgrowth 与 Underdocks 的 Index 都是 0**，`ActNumber()==1` 会同时命中两个第一幕；只想进 Overgrowth 要写 `act is Overgrowth` 或比对 `act.Id.Entry`。

### 1.3 抽取规则（`ActModel.GenerateRooms:331-386`）

1. 弱怪与强怪**塞进同一个列表** `normalEncounters`：前 N 个从弱怪池抽，接着一直抽到房间数满为止，从强怪池抽。
2. 用 `GrabBag<EncounterModel>` 抽取，**但每项权重被硬编码为 `1.0`**。
3. 相邻两场不会共享 `EncounterTag`（`AddWithoutRepeatingTags`，`ActModel.cs:419-430`）。
4. 精英池一次性抽 **15 个**（允许重复，`GrabBag3` 空了就重填）。
5. Boss：`rng.NextItem(AllBossEncounters)` —— **纯等概率抽一个**。
6. 房间按顺序消费：`RoomSet.NextNormalEncounter = normalEncounters[visited % count]`（`RoomSet.cs:75`）——一次 Rogue 里同一场遭遇基本不会连着重复。

### 1.4 由机制推出的硬约束（设计稿必须遵守）

| # | 约束 | 后果 |
|---|---|---|
| C1 | **没有权重入口** | 加 1 个普通遭遇 = 等概率稀释原版（第一幕原有 11 个普通遭遇）。想"稀有出场"只能：拆多个同类遭遇 / 自己 Harmony 补 `GenerateRooms` 改权重 |
| C2 | 弱怪池只在开局 2~3 层生效 | 往弱怪池加 1 个 = 直接改动玩家开局三连的分布，手感影响最大、要最谨慎 |
| C3 | `EncounterTag` 是 `enum`，mod **不能新增枚举值** | 自定义遭遇要么复用现有 tag（Burrower/Chomper/Nibbit/Slimes/Thieves/…），要么返回空集合（= 不受"相邻不重复"约束） |
| C4 | `NumberOfWeakEncounters` 是 `protected virtual` | 只能在自己的 `CustomActModel` 里覆写；给**原版幕**改弱怪数量必须打补丁 |
| C5 | Boss 有"未见过优先"逻辑 | `ActModel.ApplyDiscoveryOrderModifications` 会把 `BossDiscoveryOrder` 里第一个没见过的 Boss 顶上去。**加 Boss 必须同时改幕的 `BossDiscoveryOrder`**，否则首次通关仍走原版 Boss，你的 Boss 只会在之后的局出现 |
| C6 | `AllEncounters` 有缓存字段 `_allEncounters` | 入池结果在首次访问后固定，运行中再改列表不生效 |
| C7 | 存档会序列化遭遇 ID | 删/改遭遇 id 会让老存档指向不存在的遭遇（引擎有 `DeprecatedEncounter` 兜底，但战史里会显示占位图） |

---

## 2. 代码侧要准备的东西

### 2.1 工程骨架（照 `myModresources/denia` 抄即可）

| 项 | 说明 |
|---|---|
| `baldurs_enemys.csproj` | `Godot.NET.Sdk/4.5.1`、`net90`、引用 `sts2` / `BaseLib` / `0Harmony` |
| `project.godot` | Godot 4.5 工程，`config/name` 与程序集名一致 |
| `export_presets.cfg` | `export_filter="all_resources"`，导出 PCK 用 |
| `baldurs_enemys.json` | manifest：`id` / `version` / `min_game_version` / `has_dll` / `has_pck` / `dependencies:[{id:"BaseLib"}]` / **`affects_gameplay: true`** |
| 入口 | `[ModInitializer(nameof(Init))]` + `new Harmony("sts2.baldurs_enemys").PatchAll(...)` |
| 本地化 | `localization/zhs/` 与 `localization/eng/` 下的 `monsters.json`、`encounters.json`（目录名 = manifest 的 `id`） |

**ID 规则**（`BaseLib/Patches/Content/PrefixIdPatch.cs` + `Extensions/TypePrefix.cs`）：

```
Entry  = StringHelper.Slugify(类名)                  // GoblinMelee → GOBLIN_MELEE
Prefix = 根命名空间.ToUpperInvariant() + "-"          // BaldursEnemys → BALDURS_ENEMYS-
本地化键 = Prefix + Entry + "." + 字段                 // BALDURS_ENEMYS-GOBLIN_MELEE.name
```

> 根命名空间取「命名空间的第一段」。类名与界面文案要提前和根命名空间对齐，改名会连带改所有本地化键。

**自动发现**：`ModelDb` 用 `ReflectionHelper.GetSubtypesInMods<AbstractModel>()` 反射扫描 mod 程序集（`ModelDb.cs:81`），怪物与遭遇不需要手工注册进池子；`CustomEncounterModel` 的构造函数会自动把自己登记进 `CustomContentDictionary.CustomEncounters`。

### 2.2 每只怪：`CustomMonsterModel` 子类

| 成员 | 作用 | 必需 |
|---|---|---|
| `MinInitialHp` / `MaxInitialHp` | 血量区间，用 `AscensionHelper.GetValueIfAscension(...)` 挂进阶 | ✅ |
| `GenerateMoveStateMachine()` | 意图状态机（见 2.3） | ✅ |
| `CustomVisualPath` | 战斗内视觉场景路径（`res://scenes/creature_visuals/xxx.tscn`） | ✅ |
| `GenerateAnimator` / `SetupCustomAnimationStates` | 动画状态映射；BaseLib 提供 `SetupAnimationState(...)` 便捷版 | 视 spine 动画完整度 |
| `AttackSfx` / `CastSfx` / `DeathSfx` | FMOD 事件路径 | 建议 |
| `TakeDamageSfxType` | armor / fur / insect / magic / plant / slime / stone | 建议 |
| `AfterAddedToRoom()` | 开场给自己上 buff / 播放对白 | 可选 |
| `ShouldFadeAfterDeath` `ShouldDisappearFromDoom` `ExtraDeathVfxPadding` | 死亡表现微调 | 可选 |
| `HpBarSizeReduction` | 怪物挨太近时血条重叠 | 可选 |
| `HasPhobiaSpineSkin` + `SetupSkins` | 恐惧模式皮肤 | 可选 |
| `ShouldShowInCompendium` | 是否进图鉴 | 可选 |
| `GenerateBestiaryMoveList` | 图鉴里的动作列表 | 可选 |

`Title` 自动走 `monsters.json`；`AssetPaths` 会自动带上视觉场景 + 全部意图图标，用于预加载。

### 2.3 意图状态机可用的积木

**意图类型**（`Core/MonsterMoves/Intents/`，图标全部来自原版 `atlases/intent_atlas.sprites/intent_*.tres`，**不需要美术**）：

| 意图 | 用途 |
|---|---|
| `SingleAttackIntent(dmg)` | 单击 |
| `MultiAttackIntent(dmg, times)` | 多段（可传 `Func<int>` 动态段数） |
| `DeathBlowIntent(dmg)` | 处决式重击 |
| `DefendIntent` | 获得格挡 |
| `BuffIntent` / `DebuffIntent(strong)` | 强化 / 弱化 |
| `StatusIntent(count)` | 塞状态牌 |
| `StunIntent` | 眩晕 |
| `SleepIntent` | 睡眠 |
| `SummonIntent` | 召唤 |
| `HealIntent` | 治疗 |
| `EscapeIntent` | 逃跑 |
| `CardDebuffIntent` | 卡牌弱化 |
| `UnknownIntent` / `HiddenIntent` | 未知 / 不显示 |

**状态机节点**（`Core/MonsterMoves/MonsterMoveStateMachine/`）：

- `MoveState(id, onPerform, params intents[])` —— 一个具体行动，`FollowUpState` 指定下一个状态；
- `ConditionalBranchState(id)` —— 条件分支（原版 `Nibbit` 用 `_isFront` / `_isAlone` 决定起手）；
- `RandomBranchState(id)` —— 随机分支；
- `MonsterMoveStateMachine(states, initialState)` —— 组装；`ForceCurrentState` / `MonsterModel.SetMoveImmediate` 可在战斗中强插状态。

**「被眩晕打断则顺延一回合」的确切语义**（`Creature.StunInternal:525-545` + `CreatureCmd.Stun`）：

```csharp
await CreatureCmd.Stun(creature, nextMoveId);   // nextMoveId 缺省 = 重复上一次已执行的行动
```

引擎会插入一个 `MoveState("STUNNED", ..., new StunIntent())`，`FollowUpStateId = nextMoveId`、`MustPerformOnceBeforeTransitioning = true`。
→ **注意**：缺省语义是「重复上一次已执行的行动」，而你的设计稿写的是「顺延一回合」（即执行"本该这回合执行的行动"）。二者不等价，**设计稿必须显式写明打断后接哪一招**，代码里对应传显式 `nextMoveId`。这是本文档发现的第一个需要你拍板的设计点。

### 2.4 每场战斗：`CustomEncounterModel` 子类

| 成员 | 说明 | 必需 |
|---|---|---|
| `base(RoomType.Monster / Elite / Boss)` | 决定落进哪个池 | ✅ |
| `IsWeak` | 弱怪池开关 | 弱怪 ✅ |
| `IsValidForAct(ActModel act)` | **抽象方法**，进哪一幕 | ✅ |
| `AllPossibleMonsters` | 可能出现怪的并集（图鉴/成就用） | ✅ |
| `GenerateMonsters()` | 实际出场怪，**每个实例必须 `.ToMutable()`** | ✅ |
| `CustomScenePath` | 战斗场景（多怪 / 需要站位时） | 多怪 ✅ |
| `Slots` | 槽位名列表，须与场景里 Marker2D 名一致 | 多怪 ✅ |
| `GetCameraScaling` / `GetCameraOffset` / `FullyCenterPlayers` | 镜头适配 | 建议 |
| `Tags` | 相邻去重（见 C3） | 可选 |
| `CustomBgm` / `AmbientSfx` | 战斗音乐 / 环境音 | Boss 建议 |
| `BossNodePath` / `CustomRunHistoryIconPath` | 地图节点 / 战史图标 | Boss ✅ |
| `CustomEncounterBackground` | 自定义战斗背景 | 可选 |

> 忘记 `.ToMutable()` 的典型症状：同一只怪在多场战斗之间状态串台（血量/力量残留）。

### 2.5 Boss 额外要做的事

1. **地图节点**：默认路径 `res://animations/map/<entry>/<entry>_node_skel_data.tres`。没有 Spine 版就在子类里覆写：

```csharp
public override MegaSkeletonDataResource? BossNodeSpineResource => null;
public override string BossNodePath => "res://images/map/placeholder/" + Id.Entry.ToLowerInvariant() + "_icon";
```

引擎会自动去找 `<path>.png` 与 `<path>_outline.png`（`EncounterModel.MapNodeAssetPaths`）。

2. **`BossDiscoveryOrder`**：注意它在 `ActModel` 里，不在遭遇里。见 C5。
3. **BGM**：`CustomBgm` 走 FMOD 事件路径（`EncounterModel.HasBgm`）。

### 2.6 「拾取」系统（本 mod 专属，需要额外一套设计）

| 部件 | 基类 / 挂点 |
|---|---|
| 「可被拾取」power | `CustomPowerModel` |
| 「死亡引爆」power | `CustomPowerModel`，挂 `Hook.BeforeDeath` / `AfterDeath` 区分"被拾取杀死"与"其他方式杀死" |
| 「拾取」卡 | `CustomCardModel`（建议继承项目里既有的卡基类以复用卡框/悬停提示） |
| 每回合抽牌后自动塞牌 | `Hook.AfterHandEmptied` / `Hook.BeforeHandDraw` / `Hook.AfterCardDrawn` |
| 只在 `catch=true` 的战斗里生效 | 建议用「遭遇 Id 白名单」或自定义遭遇子类上的标记位，避免读取战斗场景 |
| 拾取产出物 | 原版就是 `ExplosiveAmpoule`（爆炸安瓿：`TargetType.AllEnemies`，10 点无来源伤害，`Potions/ExplosiveAmpoule.cs`），图标可直接复用 |
| 强制结束回合 | 检查 `CardModel` / `Cmd` 里结束回合的现成调用（`EndTurn` 类命令） |

### 2.7 联机与版本（本工程已踩过的坑，必须遵守）

- manifest `affects_gameplay: true`。
- **每次增删改注册进 ModelDb 的模型（怪物 / 遭遇 / power / 卡），必须递增 manifest 的 `version`**。否则两边 DLL 不一致但版本号相同，联机会报成**误导性的 `VersionMismatch`**（真实原因是 `ModelIdSerializationCache.Hash` 不同），而不是语义清晰的 `ModMismatch`。
- 怪物 AI 随机走 `RunRng.MonsterAi`，遭遇内随机用 `this.Rng`（种子 = `runRng.Seed + TotalFloor + hash(entry)`）→ 天然确定性，联机安全。**不要在怪物里用 `Rng.Chaotic` 或 `DateTime` 做决策**。
- 调试命令：控制台 `fight <遭遇Entry>`（`FightConsoleCmd`，`IsNetworked = true`），可直接跳进指定遭遇，联机自测也用它。

---

## 3. 设计稿清单（要写的东西）

> 建议每份都是**表格**，一屏能看完；数值与文案分开，文案单独一份方便本地化。

| 编号 | 设计稿 | 必须包含的字段 | 交付给谁 |
|---|---|---|---|
| **D0** | 幕与池位总表 | 每个战斗：名字 / 目标幕 / 池（弱·强·精英·Boss）/ `IsWeak` / `RoomType` / 备注 | 自己 + 编程 |
| **D1** | 怪物表（每只怪一张卡） | 名称 / 内部英文名（→ 类名）/ 基础血量 / 进阶血量 / 定位（近战·远程·环境物）/ 稀有度 / 是否可被拾取 / 是否进图鉴 | 编程 + 美术 |
| **D2** | 意图状态机表 | 循环序列（如 `攻击2 - 攻击3 - 强化+1力量 ···`）/ 每个行动的伤害·格挡·增益数值 / **被打断后接哪一招**（见 2.3）/ 分支条件（若 A 则 B） | 编程 |
| **D3** | 遭遇编成表（核心） | 战斗名 / 池 / 目标幕 / **敌人排列顺序 + 站位**（左→右）/ 槽位数 / 是否为 `catch` 战斗 / 期望难度（对比原版同池战斗）/ `Tags` 复用哪些原版 tag | 编程 + 美术 |
| **D4** | 数值与进阶曲线 | 每只怪的基础值 + 进阶 8 / 进阶 10 的取值（`DeadlyEnemies` / `ToughEnemies` / `Poverty`）；每场战斗的期望回合数、期望掉血 | 自己 |
| **D5** | 关键词与 power 说明 | power 名 / 图标需求 / 层数语义 / 可见性（是否显示在血条下）/ 是否可被驱散 / 是否影响 AI | 编程 + 美术 |
| **D6** | 「拾取」卡设计稿 | 费用 / 文本 / 关键词（消耗·虚无）/ 目标合法性 / 与 power 的交互 / 强制结束回合的时机 / 自动塞牌的时机与上限 | 编程 |
| **D7** | 拾取产出映射表 | 敌人 → 产出物（药水 / 遗物）/ 产出物名称与图标来源 | 编程 |
| **D8** | 本地化文案表 | 中英双语，一次写全：怪物名、意图名（`moves.<ID>.title`）、战斗标题（`encounters.title`）、**战败文案**（`encounters.loss`）、战斗内语音（可选） | 自己 |
| **D9** | 美术需求单 | 见第 4 节清单，逐项勾选并附参考图 | 美术 |
| **D10** | 音效需求单 | 每只怪的 attack / cast / die / hurt，标注「复用原版」还是「自制」 | 音频 |
| **D11** | 调试与验收用例 | 见第 6 节 | 自己 |

### D8 本地化键名模板

```jsonc
// localization/zhs/monsters.json
{
  "BALDURS_ENEMYS-GOBLIN_MELEE.name": "地精·近战",
  "BALDURS_ENEMYS-GOBLIN_MELEE.moves.ATTACK_2.title": "挥砍",
  "BALDURS_ENEMYS-GOBLIN_MELEE.moves.BUFF_1.title": "战吼",
  "BALDURS_ENEMYS-GOBLIN_MELEE.moves.ATTACK_2.banter": "[jitter]呃啊！[/jitter]"
}

// localization/zhs/encounters.json
{
  "BALDURS_ENEMYS-MINTHARA_ELITE.title": "夜影守望者",
  "BALDURS_ENEMYS-MINTHARA_ELITE.loss": "{character}倒在了[gold]{encounter}[/gold]的剑下。"
}
```

- `moves.<MOVE_ID>.title` 的 `MOVE_ID` = `MoveState` 构造时的字符串 id（**大小写敏感**）；
- `.banter` 只有调用 `TalkCmd.Play(...)` 时才需要；
- 战死文案里 `{character}` / `{encounter}` 由引擎替换；
- 意图名缺失只会在日志里 warn，不会崩，但图鉴会显示裸 id。

---

## 4. 美术资源清单

### 4.1 每只怪（必需，一次性）

| # | 资源 | 目标路径 / 命名 | 格式 | 规格 |
|---|---|---|---|---|
| 1 | Spine 图集贴图 | `res://animations/monsters/<entry>/<entry>.png` | PNG | 一般 1024/2048 级图集，按导出设置 |
| 2 | Spine 图集描述 | `.../<entry>.atlas` | 文本 | Spine 导出 |
| 3 | Spine 骨骼数据 | `.../<entry>.skel` | 二进制 | Spine 导出（二进制，非 json） |
| 4 | Godot 骨骼资源 | `.../<entry>_skel_data.tres` | `.tres` | 引用上面两个，`default_mix` 一般 0.15 |
| 5 | 战斗内视觉场景 | `res://scenes/creature_visuals/<entry>.tscn` | `.tscn` | 见下 |
| 6 | `.import` 文件 | 每个资源旁边 | Godot 自动生成 | **不要手写**，用 Godot 编辑器导入 |

`<entry>` 一律用小写 slug（= `Id.Entry.ToLowerInvariant()`）。

**Spine 动画名必须与代码一一对应**（`MonsterModel.GenerateAnimator` / `BaseLib.SetupAnimationState`）：

| 动画名 | 用途 | 必需 |
|---|---|---|
| `idle_loop` | 待机（**循环**） | ✅ |
| `attack` | 攻击 | ✅ |
| `cast` | 施法 / 强化 / 上 buff | ✅ |
| `hurt` | 受击 | ✅ |
| `die` | 死亡 | ✅ |
| `revive` | 复活（少量怪） | 可选 |
| `stun` | 眩晕表现（图鉴动作列表会读） | 可选 |
| skin `normal` / `phobia` | 恐惧模式开关皮肤 | 可选 |

动画名写错 = 怪物卡在默认姿势 / 死亡不消失。

**战斗内视觉场景 `creature_visuals/<entry>.tscn` 结构**（实测原版所有怪物一致）：

```
<Node2D>                       ← 根节点，mod 里用普通 Node2D，BaseLib 会在运行时自动转换成 NCreatureVisuals
├── Visuals   (SpineSprite)    % 唯一名   skeleton_data_res 指向 <entry>_skel_data.tres
├── Bounds    (Control)        % 唯一名   可点选判定区（血条长度参考它）
├── CenterPos (Marker2D)       % 唯一名   视觉中心 / 镜头对焦点
└── IntentPos (Marker2D)       % 唯一名   意图图标位置
```

- 名字**不能改**，四个节点都要勾「作为唯一名称访问」；
- `Visuals` 里的 `scale` / `position` 决定怪在屏幕上的大小与落点（原版怪 scale 大致 0.22~0.5）；
- 场景根用普通 `Node2D` 即可，BaseLib 的 `NodeFactory` 会自动转换（见 `BaseLib/docs/auto_conversion.md`）。

### 4.2 每场多怪战斗（必需）

`res://scenes/encounters/<encounter_entry>.tscn`：

```
<Control>                       ← 1920×1080，Full Rect 锚点，mouse_filter = 2 (Ignore)
├── <slot_1> (Marker2D)
├── <slot_2> (Marker2D)
└── ...
```

- Marker 名字必须与代码 `Slots` 返回的字符串**完全一致**；
- 站位实测参考（原版单排 4 位）：`x ≈ 1100 / 1310 / 1530 / 1740`，`y ≈ 675 ~ 715`；
- **两排**（前排放脚下、后排放稍上）：上排 `y ≈ 370 ~ 400`；
- 5 个怪的场景可参考原版（`slots` 数量没有硬上限，但血条会挤）；
- 换行/镜头：场景太宽时用 `GetCameraScaling()` 缩小。

> 单怪战斗**不需要**这个场景，`Slots` 也不必要（`GenerateMonsters` 里槽位传 `null` 由引擎自动分配）。

### 4.3 Boss 专用

| 资源 | 路径 | 说明 |
|---|---|---|
| 地图节点图标（无 Spine 版） | `res://images/map/placeholder/<entry>_icon.png` + `_icon_outline.png` | 覆写 `BossNodePath` 去掉后缀；**轮廓图必需**（选中态用） |
| 地图节点 Spine 版（可选，更酷） | `res://animations/map/<entry>/<entry>_node.{png,atlas,skel}` + `<entry>_node_skel_data.tres` | 默认路径即此，不覆写就走这条 |
| 战史图标（可选） | `res://images/ui/run_history/<entry>.png` + `_outline.png` | 覆写 `CustomRunHistoryIconPath` |
| 专属 BGM | FMOD 事件或自备音频 | `CustomBgm` |

### 4.4 战斗背景（可选）

默认继承所在幕的背景（`ActModel.GenerateBackgroundAssets`）。只有想做「专属战场」才需要：
`CustomEncounterBackground(...)` 返回 `CustomBackgroundAssets`，配 `res://scenes/backgrounds/<名>/<名>_background.tscn` + `layers/` 下的层场景。

### 4.5 「拾取」系统所需美术

| 资源 | 规格 | 来源 |
|---|---|---|
| 「拾取」卡卡面 | 参考 Denia：约 2500×1600（各卡尺寸不强制统一） | 新画 |
| 卡框 | 若沿用 Denia 双形态风格：粉 429×632 / 黑 436×663 | 复用 Denia |
| 「可被拾取」power 图标 | **64×64**（`CustomPackedIconPath`）+ **256×256**（`CustomBigIconPath`） | 新画 |
| 「死亡引爆」power 图标 | 同上 | 新画 |
| 拾取产出药水图标 | 复用原版 `ExplosiveAmpoule` | 复用 |
| 油桶「可被拾取」视觉提示（可选） | 高亮描边 / 光标 | 新画 |

### 4.6 **不需要**画的东西（省下来的工作量）

- 全部意图图标（原版 `atlases/intent_atlas.sprites/intent_*.tres`）；
- 通用打击 VFX（`vfx/vfx_attack_slash`、`vfx/vfx_attack_blunt`）、火焰烟雾（`NFireSmokePuffVfx`）；
- 血条、意图框、目标箭头、图鉴 UI、战史 UI；
- 通用受击音效（按材质自动选：`event:/sfx/enemy/enemy_impact_enemy_size/enemy_impact_{armor|fur|insect|magic|plant|slime|stone}`）；
- 眩晕特效（`NStunnedVfx`）。

### 4.7 音频

| 方案 | 成本 | 做法 |
|---|---|---|
| **A. 纯复用** | 0 | 不覆写 `AttackSfx` / `DeathSfx`，走原版通用音；`TakeDamageSfxType` 选材质 |
| **B. 自备文件（推荐）** | 低 | wav/ogg 放 mod 的 `sounds/`，用 BaseLib `FmodAudio.PlayFile` / `CreatePool` 播放，**不需要 FMOD Studio** |
| **C. 完整 FMOD 事件** | 高 | FMOD Studio **2.03.x**（版本必须匹配）+ `sts2-fmod-tools` 生成工程 → 导出 `.bank` → `FmodAudio.LoadBank` → 事件命名 `event:/mods/baldurs_enemys/...` |

- 每只怪建议至少 3 条：attack / cast / die（hurt 可复用受击音）；
- **Godot 4 的 wav 必须是标准 PCM（fmt tag `0x0001`）**；`WAVE_FORMAT_EXTENSIBLE`（`0xFFFE`）会导致导入只生成 `.md5`、不生成 `.sample`，运行时 `Failed loading resource`。本工程已踩过这个坑。
- 资源路径**不要用中文**（Godot 导入 + 本仓库的批量删除工具都会出问题）。

---

## 5. 建议的制作顺序

```
1. D0 / D3 / D4 设计稿定稿（先定池位与编成，别的都能后补）
2. D9 / D10 美术音频需求单交出去（美术是最长的一条关键路径，尽早并行）
3. 建工程骨架（csproj / project.godot / manifest / entry），先不写内容
4. 打通链路：1 只怪 + 1 场战斗，跑通「编译 → 打包 → 进游戏 → 用 fight 命令跳进去」
       ↑ 这一步必须最先做完。链路不通的话后面所有设计都是空转
5. 本地化（D8）—— 第 4 步的怪先起个占位名也行
6. 美术 / 音频入包（Godot 编辑器导入 → 重新导出 PCK）
7. 全量铺开剩余怪物与战斗
8. 回归：进阶难度 / 联机 / 老存档 / 图鉴 / 战史
```

工程骨架可直接从 `myModresources/denia` 复制改造（`csproj` / `project.godot` / `export_presets.cfg` / `denia.json` / `pack_*.bat` 的结构）。

---

## 6. 陷阱清单与验收用例

### 6.1 陷阱（按踩坑概率排序）

1. **以为池子有权重** → 见 C1。
2. **想加自定义 `EncounterTag`** → 见 C3，enum 不可扩展。
3. **加了 Boss 忘了改 `BossDiscoveryOrder`** → 见 C5。
4. **`GenerateMonsters()` 忘了 `.ToMutable()`** → 状态串台。
5. **模型增删没递增 manifest `version`** → 联机报假 `VersionMismatch`（详见第 7 节与项目记忆）。
6. **槽位名与场景 Marker 名不一致** → 怪叠在一起或直接不生成。
7. **Spine 动画名与代码不一致** → 卡姿势 / 死亡不消失。
8. **资源路径用中文** → 导入失败 + 仓库工具删不掉。
9. **wav 是 EXTENSIBLE 格式** → 音效静默丢失。
10. **正式版 / 测试版输出目录互相覆盖**：`Game` 与 `GameBeta` 构建会写同一个 `.godot/mono/temp/bin/Debug/`，交叉验证后必须重建目标版本产物，否则部署的是错的那个。
11. **改了遭遇 id 让老存档失效** → 见 C7。

### 6.2 验收用例（D11）

| # | 用例 | 通过标准 |
|---|---|---|
| 1 | `fight <遭遇Entry>` 跳进每场战斗 | 站位正确、怪不重叠、镜头不裁切 |
| 2 | 逐回合空过，记录意图序列 | 与 D2 状态机表逐项一致；打断后接的招也对 |
| 3 | 每种击杀方式各来一次 | 普通击杀 / 拾取击杀 / 死亡引爆，行为与 D5·D6 一致 |
| 4 | 全场打完看奖励 | 金币区间符合 `MinGoldReward`/`MaxGoldReward`；精英/Boss 掉落正常 |
| 5 | 开 10 局，统计遭遇分布 | 弱怪池每局出现次数 = 幕的 `NumberOfWeakEncounters`；自定义遭遇实际出现率符合预期（**这是验证 C1 稀释效应的唯一手段**） |
| 6 | 进图鉴翻到自定义怪 | 名字、动作名、动画都正常 |
| 7 | 战史 / 地图节点（Boss） | 图标与轮廓图都在，不是占位红叉 |
| 8 | 进阶 8 / 进阶 10 各跑一场 | 血量与伤害按 D4 曲线生效 |
| 9 | 二楼联机各一场 | 怪物 AI 两边一致、无回滚、无 `VersionMismatch` |
| 10 | 中途存档退出再进 | 房间状态与遭遇不丢、不重复 |

---

## 7. 正式版 vs 测试版差异速查（对敌人 mod 的影响）

实测 `Game/src` 与 `GameBeta/src` 的差异：

| 文件 | 差异 | 对敌人 mod 的影响 |
|---|---|---|
| `EncounterModel.cs` | beta 新增 `public virtual bool IsDebugEncounter => false;`；`Rng` 种子类型 `uint` → `ulong` | 几乎无影响（不覆写、不自己 `new Rng`） |
| `MonsterModel.cs` | beta 新增 `HurtAnimationTrackOffsetForDoom`；`CombatState` 加了 `?? NullCombatState.Instance`；日志包了一层 `TestMode.IsOff` | 无影响 |
| `ActModel.cs` | **完全一致** | 无 |
| `MoveState.cs` / `AbstractIntent.cs` | **完全一致** | 无 |

**结论：敌人与战斗 mod 的双版本适配成本很低**——真正会撞上差异的是伤害计算类接口（`ModifyDamageMultiplicative` / `ModifyDamageCap` 的 `CardPlay?` 参数等，见 Denia 的 beta 适配记录），而怪物 mod 基本不碰那些。但构建时仍要留意第 6.1 节第 10 条的输出目录覆盖问题。

**另需注意（环境事实）**：游戏当前安装在 **`F:\SteamLibrary\steamapps\common\Slay the Spire 2\`**（`release_info.json` = `v0.111.0`），而仓库里的 `build_denia.bat` / `pack_denia.bat` 默认路径仍写的是 `E:\SteamLibrary\...`。新 mod 的构建脚本要么直接用 F 盘路径，要么靠 `STS2_MODS_DIR` 环境变量覆盖。

---

## 8. 参考入口（源码坐标）

| 主题 | 位置 |
|---|---|
| 池子过滤规则 | `GameBeta/src/Core/Models/ActModel.cs:144-169` |
| 抽取算法（GrabBag / 弱怪数 / Boss 抽取 / 相邻 tag 去重） | `GameBeta/src/Core/Models/ActModel.cs:331-430` |
| Boss 未见过优先 | `GameBeta/src/Core/Models/ActModel.cs:404-415` |
| 各幕参数 | `GameBeta/src/Core/Models/Acts/{Overgrowth,Underdocks,Hive,Glory}.cs` |
| 房间消费顺序 | `GameBeta/src/Core/Rooms/RoomSet.cs:70-80` |
| 遭遇基类 | `GameBeta/src/Core/Models/EncounterModel.cs` |
| 怪物基类 | `GameBeta/src/Core/Models/MonsterModel.cs` |
| 眩晕语义 | `GameBeta/src/Core/Entities/Creatures/Creature.cs:525-545`、`Core/Commands/CreatureCmd.cs:918-950` |
| 意图类型全表 | `GameBeta/src/Core/MonsterMoves/Intents/` |
| 状态机节点 | `GameBeta/src/Core/MonsterMoves/MonsterMoveStateMachine/` |
| 抽取袋实现（权重机制） | `GameBeta/src/Core/Helpers/GrabBag.cs` |
| mod 入池补丁 | `BaseLib/Patches/Content/ContentPatches.cs:348-410` |
| 遭遇抽象基类 | `BaseLib/Abstracts/CustomEncounterModel.cs` |
| 怪物抽象基类 | `BaseLib/Abstracts/CustomMonsterModel.cs` |
| 意图/行动构造助手 | `BaseLib/Monsters/MoveBuilder.cs` |
| ID 前缀规则 | `BaseLib/Patches/Content/PrefixIdPatch.cs`、`BaseLib/Extensions/TypePrefix.cs` |
| 幕数判定 | `BaseLib/Extensions/ActModelExtensions.cs` |
| Boss 图标路径覆写 | `BaseLib/Patches/UI/RoomIconPathPatch.cs` |
| 场景自动转换 | `BaseLib/docs/auto_conversion.md` |
| 自定义音频 | `BaseLib/docs/FmodAudio.md` |
| 官方教程（怪物 + 遭遇） | `SlayTheSpire2ModdingTutorials/BaseLib/11 - 添加新怪物/README.md` |
| 调试命令 | `GameBeta/src/Core/DevConsole/ConsoleCommands/FightConsoleCmd.cs` |
