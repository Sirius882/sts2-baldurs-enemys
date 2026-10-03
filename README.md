# Baldur's Gate 3 Enemies for Slay the Spire 2

为《杀戮尖塔2》（Slay the Spire 2）加入《博德之门3》主题的敌人与遭遇战的游戏 Mod。

13 场专属遭遇战覆盖三层，24 种敌人各有独立立绘与配色，14 首原创 BGM，中英双语本地化。

---

## 特性

### 13 场专属遭遇战

| 幕 | 类别 | 敌人 |
|---|---|---|
| 第一幕 | Boss | 「夜影守望者」明萨拉 |
| 第二幕 | 精英 | 尤格、巴萨扎尔 |
| 第二幕 | Boss | 凯瑟里克·索姆、「死亡之神的面容」米尔寇的使徒 |
| 第三幕 | 精英 | 维康妮亚、卡扎多尔、安苏 |
| 第三幕 | Boss | 奥林、恩维尔·戈塔什、拉斐尔、君主 |

### 24 种敌人

- 第一幕：地精·近战、地精·远程、骷髅
- 第二幕：油桶、暗夜之歌、伊索贝尔、人质、灵魂柱
- 通用：博德安的巨人杀手、米尔寇的使徒、凯瑟里克·索姆
- 第三幕：维康妮亚、卡扎多尔、安苏、奥林、戈塔什、拉斐尔、君主

### 原创「交互」机制

- 专属卡牌「交互」：把带有「可被交互」的敌人直接收入囊中。
- 油桶可被交互后变成原版爆炸药水；暗夜之歌需要交互两次才生效。
- 被交互获得的遗物**无法**从奖励池或商店获得，只能靠这套机制取得。

### 2 件专属遗物

- **艾琳女士**：每场战斗开始获得 3 层人工制品，每回合开始获得 1d4 格挡。
- **博德安的巨人杀手**：按战斗等级提供 1d4 / 1d8 / 2d20 力量。

### 14 首原创 BGM

每场专属战斗都有对应的战斗曲或探索曲，与原版曲目不冲突。

---

## 安装

### 依赖

| 依赖 | 版本 | 说明 |
|---|---|---|
| [BaseLib](https://steamcommunity.com/sharedfiles/filedetails/?id=3737335127) | ≥ 3.3.0 | 必需 |
| Slay the Spire 2 | ≥ 0.111.0 | — |

### 步骤

1. 安装 [BaseLib](https://steamcommunity.com/sharedfiles/filedetails/?id=3737335127)。
2. 从本仓库 [Releases](https://github.com/Sirius882/sts2-baldurs-enemys/releases) 下载最新产物。
3. 解压到游戏目录 `Slay the Spire 2/mods/baldurs_enemys/`。

也可以直接在 Steam 创意工坊订阅（如果已上架）。

---

## 从源码构建

### 前置

- [.NET 9 SDK](https://dotnet.microsoft.com/download)
- Godot 4.5.1 **mono** 版本（用于导出 PCK）

### 1. 编译 DLL

```bash
dotnet build -c Debug -t:Rebuild
```

产物输出到 `.godot/mono/temp/bin/Debug/baldurs_enemys.dll`。

> `Sts2Dll` 按以下顺序解析：环境变量 `STS2_DLL` → 已安装的游戏目录
> `F:\SteamLibrary\steamapps\common\Slay the Spire 2\data_sts2_windows_x86_64\sts2.dll`
> → 工作区内`GameBeta` 调试产物。可用 `/p:Sts2Dll=<路径>` 强制覆盖。

### 2. 导出 PCK

```bash
Godot_v4.5.1-stable_mono_win64_console.exe \
  --headless --path . \
  --export-pack "Windows Desktop" ./baldurs_enemys.pck
```

首次添加新资源后需先跑一次 `--headless --import` 生成 `.import`。
**替换已有资源内容时**，必须先删除 `.godot/imported/` 下对应的缓存再 `--import`，否则改动不生效。

仓库内也提供了 `build_baldurs_enemys.bat` / `pack_baldurs_enemys.bat` 一键脚本。

---

## 仓库结构

```
├── src/                        C# 源码
│   ├── Monsters/               24 种敌人的状态机
│   ├── Powers/                 自定义 Power（含图标路径覆写）
│   ├── Patches/                Harmony 补丁（Boss 发现顺序等）
│   └── BaldursEnemysEntry.cs   Mod 入口与模型注册
├── baldurs_enemys/             资源本体
│   ├── music/                  14 首 BGM
│   ├── images/                 立绘、图标、卡面
│   ├── sounds/                 音效
│   ├── localization/           zhs / eng 本地化
│   └── scenes/                 遭遇战场景
├── scenes/                     Godot 场景
├── docs/                       设计文档与制作清单
├── baldurs_enemys.csproj
├── baldurs_enemys.json         Mod manifest
└── export_presets.cfg          打包参数（勿删，pack 脚本依赖它）
```

---

## 文档

| 文档 | 内容 |
|---|---|
| [`docs/敌人与战斗mod制作清单.md`](docs/敌人与战斗mod制作清单.md) | 引擎机制踩坑清单（状态机、意图、钩子、图鉴解锁链等） |
| [`docs/待打包清单_v0.8.0.md`](docs/待打包清单_v0.8.0.md) | 当前版本打包与验证清单 |
| [`docs/明萨拉精英战_设计规格.md`](docs/明萨拉精英战_可开工设计规格.md) | 明萨拉遭遇战设计规格 |
| [`AGENTS.md`](AGENTS.md) | AI 辅助开发入口约定 |

---

## 本地化

`baldurs_enemys/localization/` 下按语言分目录（`zhs` / `eng`），每个语言一份
`monsters.json` / `powers.json` / `relics.json` / `cards.json`。

模型文本的 key 段 = `StringHelper.Slugify(类名)`（每个非首位大写字母前插下划线并转大写），
例如 `DameAylin` → `DAME_AYLIN`。新增模型后请先跑 key 比对脚本再打包。

---

## 许可

代码与资源以 [MIT License](LICENSE) 发布。《博德之门3》及其角色为
Larian Studios / Wizards of the Coast 的商标；本项目为非官方粉丝作品，
与上述公司无隶属或背书关系。
