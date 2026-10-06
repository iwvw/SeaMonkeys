# PRD（后端）：SeaMonkeys Core / Data / Proxy

> 版本：v1（2026-10-04）
> 范围：`SeaMonkeys.Core`、`SeaMonkeys.Data` 与代理服务。对应主 PRD `docs/PRD.md`。
> 关联 ADR：0002 / 0003 / 0004 / 0005。

## 定位

后端负责"从文件拿到名单、从外部源拿到战绩、算好结果"，**零 UI 依赖**（不得 `using Microsoft.UI.*` 或任何 UI 命名空间），可被 App 引用，也可独立测试。

```
SeaMonkeys.Core    对局解析、服务器识别、战绩源适配、加权胜率、舰船目录、输出文本
SeaMonkeys.Data    SQLite：设置、观察名单、对局历史
Proxy（独立部署）  跨区探测、请求节流、缓存、隐藏档案降级、密钥隔离
```

## 领域模型（Core）

### Battle

| 字段 | 类型 | 说明 |
|---|---|---|
| MatchGroup | string | `pvp` 等 |
| GameMode / Scenario | string | 模式与脚本 |
| MapDisplayName | string | 地图名 |
| DateTime | DateTimeOffset | 开战时间（`dd.MM.yyyy HH:mm:ss`） |
| Participants | Participant[] | 双方参战者 |

### Participant

| 字段 | 说明 |
|---|---|
| Name | 昵称 |
| Relation | 0=自己，1=友军，2=敌军 |
| ClanTag | 公会标签（查询后填充） |
| ShipId | 舰船 ID（数字字符串） |
| AccountId | 真实账号 ID（查询后填充，非元数据的 `id`） |
| Server | 所属区域 |
| Statistics | `PlayerStatistics` |

### PlayerStatistics

账号（总/单野/双人/三人）与当前舰船的场次、胜率、经验、伤害、公会、隐藏标记、加权胜率。

## 功能需求

### BE-01 对局观察

- 监听配置的游戏 replay 目录，检测 `tempArenaInfo.json` 出现或更新。
- 判定"新对局"依据 `LastWriteTime` 是否较上次更新。
- 观察与解析分离：观察只负责发现，解析只负责结构化。
- 支持手动指定文件（跳过"需更新"判定）。

验收：对目录内新增/更新的 `tempArenaInfo.json` 能在 1 个轮询周期内触发一次解析回调，且同一文件不重复触发。

### BE-02 对局解析

- 读取 `tempArenaInfo.json` 或 `.wowsreplay` 头部。
- 兼容两种格式：首字节 `0x7B` 直接 JSON；首字节 `0x12` 跳过 12 字节头 + 4 字节长度前缀。
- 打开必须用 `FileShare.ReadWrite | FileShare.Delete`（ADR-0002）。
- 解析失败语义：格式不符抛"文件格式错误"（不重试）；瞬时读取失败按可重试处理。

验收：用真实 replay 与 `tempArenaInfo.json` 各能解析出正确的地图、时间、模式与 24 名参与者。

### BE-03 bot 排除

- `id <= 30` 的参与者排除（沿用既有标识规则，CONTEXT 不变量）。

验收：剧情/护航模式中的 bot 不出现在结果里。

### BE-04 服务器识别

- 主路径：读 `<game>/profile/clientrunner.log` 最后一次 `Selected realm: <mode>`。
- 回退：跨区探测（向各区域战绩源 search 名单中的昵称，命中即定区）。
- 支持手动指定；`AUTO` 且读取失败时才回退。
- 彻底失败时抛"服务器识别失败"，不重试。

验收：给定游戏目录能正确判区；日志缺失时回退探测能在有限请求内定区；两者都失败时给出明确错误而非静默。

### BE-05 战绩源适配

统一接口 `IPlayerStatsAdapter`，可替换实现：

- `VortexStatsAdapter`（主力）：`/api/accounts/search/{name}` → `spa_id` → 账号 / 公会 / 舰船战绩（`pvp`、`pvp_solo`、`pvp_div2`、`pvp_div3`）。
- `WgPublicStatsAdapter`（备选）：官方 PAI，需 `application_id`（只放代理侧）。

归一化：不同源字段差异在适配器内消化（如 `battles_count`↔`battles`、`original_exp`↔`exp`、`spa_id`↔`account_id`、`karma`）。

验收：给定 24 名玩家名单，能返回归一化的 `PlayerStatistics`；字段命名差异不外泄到 Core 其他模块。

### BE-06 请求节流与批量

- 逐个 search 时插入固定间隔（实测 60ms 量级）。
- 账号/公会/船只查询尽量批量或复用 search 返回值。
- 重试：格式错误与识别失败不重试；网络类失败按退避重试（间隔 + 上限次数）。

验收：单局 24 人查询不触发源限流；网络抖动时能有限重试成功。

### BE-07 隐藏档案降级

- `hidden=true` 或战绩不可得的玩家，返回统一降级结果（负值哨兵），不阻断其余玩家（CONTEXT 不变量）。

验收：样本中 2 名隐藏档案玩家显示为降级态，其余 22 人正常。

### BE-08 加权胜率

- 账号权重：单野 / 双人 / 三人按场次与配置权重合成。
- 舰船权重：按该船场次相对于阈值（如 200 场）线性提升，达到上限后完全采用舰船胜率。
- 参数走设置，可调。

验收：给定账号与舰船数据，加权胜率符合配置公式；无舰船数据时回退为账号胜率。

### BE-09 舰船目录

- 本地版本化 JSON（`version` / `date` / `ships[id] = {name_zh-cn, name_en-us, type, tier}`）。
- 查询：按 ID 取本地化名称（按语言回退）、类型、等级；缺失返回"未知船只"。
- 更新：与远端清单比对 `date`，下载 + 哈希校验 + 原子替换；更新不触碰用户数据（ADR-0005）。

验收：新船在目录更新后正确显示名称；更新失败不破坏现有目录。

### BE-10 观察名单（Data）

- 按服务器存储，标记 Positive / Negtive / Cheater / None。
- 拼写 `Negtive` 为兼容契约（CONTEXT 不变量）。
- 应用：对局结果中命中标记的玩家附上状态，供 UI 高亮。

验收：同一玩家在不同服务器可分别标记；重装/更新后观察名单不丢失。

### BE-11 对局历史（Data）

- 留存每局的名单与统计（本地 SQLite）。
- 更新应用绝不覆盖历史数据（CONTEXT 不变量）。

### BE-12 输出文本

- 从战场视图统计生成可配置文本（模板标签为兼容契约）。
- 支持复制用途的纯文本格式。

验收：模板改动即时生效；输出与战场视图数据一致。

## 代理服务（独立部署）

### BE-13 代理契约

客户端只请求代理，由代理转发到各区域战绩源（自有服务器或 Cloudflare Worker，二者皆可，ADR-0003）。

职责与契约：

| 职责 | 说明 |
|---|---|
| 跨区探测 | 接受"昵称列表"，并行探测各区域，返回命中区域与结果 |
| 转发 | 按区域转发 Vortex（或 PAI）请求，统一响应结构 |
| 节流 | 汇聚多客户端请求，控制在源限流以内 |
| 缓存 | 账号/公会缓存数小时；舰船目录按版本缓存；命中不回源 |
| 隐藏档案降级 | `hidden` 统一返回降级结构 |
| 密钥隔离 | 任何 `application_id` 只存在代理侧 |

验收：客户端在无任何密钥配置下，通过代理完成一次完整对局查询；代理层可在不改变客户端的情况下切换/新增数据源。

### BE-14 缓存与失效

- 缓存键：`区域 + 账号ID + 数据类型（账号/公会/船只+shipId）`。
- TTL：账号与公会数小时；舰船目录按版本。
- 缓存不缓存"隐藏档案"的瞬时失败超过必要时长，避免误判。

## 非功能需求

| ID | 需求 | 目标 |
|---|---|---|
| NFR-BE-01 | Core 零 UI 依赖 | 不引用任何 UI 命名空间，可独立单测 |
| NFR-BE-02 | 对局检测到回调 | ≤ 1 个轮询周期 |
| NFR-BE-03 | 全名单查询 | 受源限流约束，靠节流与缓存达成 |
| NFR-BE-04 | 文件并发安全 | 游戏写入/替换时读取不崩溃 |
| NFR-BE-05 | 更新安全 | 更新绝不触碰观察名单/历史/设置 |
| NFR-BE-06 | 可替换性 | 战绩源与代理均可替换而不动上层 |

## 接口契约（供前端）

Core 对 App 暴露的调用面（示意，最终以代码为准）：

```
BattleObservation.Start(gamePath, onNewBattle)
BattleParser.Parse(filePath) -> Battle
ServerResolver.Resolve(configuredServer, gamePath) -> Server
StatsSource.GetPlayersAsync(battle, server, options) -> Participant[] (含 Statistics)
ShipCatalog.GetName(id, language) / GetType(id) / GetTier(id)
WatchListRepository.Read() / Apply(participants)
HistoryRepository.Save(battle)
OutputText.Generate(battlefield, template) -> string
BattlefieldFactory.Create(battle) -> Battlefield
```

## 里程碑（后端）

| 阶段 | 内容 |
|---|---|
| M1 | BE-01/02/03/04 + Vortex 客户端（BE-05）+ 加权胜率（BE-08）；控制台跑通一份真实 replay |
| M2 | BE-06/07 + 代理（BE-13/14）接入 |
| M3 | BE-09 舰船目录 + 更新 |
| M4 | BE-10/11/12 数据层与输出文本 |

## 待定

1. 战绩源优先级与降级细节（Vortex 与 PAI 的切换条件）
2. 代理形态最终选择（自有服务器 vs Cloudflare Worker）
3. 缓存 TTL 具体取值
4. 首发是否含莱服 / 国服
