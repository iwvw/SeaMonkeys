# SeaMonkeys Domain Context

## Product

SeaMonkeys 是《战舰世界》（World of Warships）的 Windows 桌面实时战绩助手。它读取游戏写出的对局元数据，解析出双方参战名单，再通过外部战绩源获取每位玩家的账号与舰船数据，并在悬浮面板上展示团队构成与战力对比。全程只读文件与公开接口，不注入、不读游戏进程内存。

## Domain Language

### Battle（对局）

一次战斗，由 replay 文件头部的明文 JSON 元数据描述。包含战斗类型、开战时间、地图、双方参与者。来源是游戏目录 `replays/` 下的 `tempArenaInfo.json` 或 `.wowsreplay` 文件的元数据前缀。

### Participant（参与者）

对局元数据里列出的玩家或 bot。在取到战绩前，参与者只有关系（自己/友军/敌军）、账号 ID、舰船 ID 和昵称。

### Player Statistics（玩家战绩）

针对某个参与者归一化后的账号、舰船、单野/组队、经验、伤害、公会、隐藏档案、加权胜率数据。

### Statistics Source（战绩源）

获取玩家战绩的外部提供方。当前规划：Vortex 网页接口、Wargaming 公开 API（PAI）。全部经自建代理转发。

### Battle Intake（对局接入）

完整流程：解析对局 → 识别服务器 → 选择战绩源 → 获取玩家战绩 → 应用观察名单 → 计算战场视图 → 生成输出文本。

### Battlefield（战场视图）

对局的计算后呈现：友军、敌军、队伍聚合、排序投影、图表输入。

### Watch List（观察名单）

按服务器存储的用户数据，把玩家标记为 Positive / Negative / Cheater / None。历史拼写 `Negtive` 为兼容契约，不得改动。

### Ship Catalog（舰船目录）

版本化的本地元数据，把舰船 ID 映射到本地化名称、类型、等级。可由本地游戏数据（wowsunpack 导出 GameParams + 游戏自带 localizations/global.mo）离线生成，官方译名且覆盖最全；发布前用 build/update-ships.ps1 刷新内置表。

### Output Text（输出文本）

从战场视图统计生成、可配置的文本，用于复制或展示。模板标签与格式为兼容契约。

### Battle Observation（对局观察）

检测到配置的游戏 replay 目录下出现新的 `tempArenaInfo.json`。观察与解析是两个独立职责。

### Proxy（代理）

自建的中转服务（自有服务器或 Cloudflare Worker）。客户端只请求代理，由代理转发到各区域战绩源。承担跨区探测、请求节流、缓存与隐藏档案降级。

### Software Update（软件更新）

应用本体与舰船目录的下载、哈希校验、解压与替换，且保留运行时用户数据。发布流程见 `docs/release-checklist.md`。

## Invariants

- 对局文件以兼容游戏正在写入或替换的共享模式打开。
- 既有标识规则排除的 bot 继续排除（`id <= 30`）。
- 服务器选择与战绩源降级保持各区域当前行为。
- 隐藏或不可得的战绩沿用既有的负值哨兵语义，直到引入类型化结果状态。
- 观察名单、设置、窗口位置、截图与日志位于既定用户数据目录。
- 软件更新绝不覆盖观察名单、窗口位置、日志、截图与下载运行数据。
- 客户端不硬编码任何战绩源的密钥；所有外部请求经代理转发。
