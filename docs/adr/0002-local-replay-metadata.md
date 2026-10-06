# ADR-0002. 数据来源：本地 replay 元数据 + 外部战绩源

日期：2026-10-04
状态：Accepted

## 背景

需要回答"当前对局有哪些玩家"和"这些玩家战绩如何"两个问题，涉及两条独立数据链路。

已知事实：

- Wargaming 官方开发者 API **不提供**当前对局参战名单。
- 游戏会把当前对局写成本地文件，位置在游戏根目录 `replays/` 下。
- 对局进行中为 `tempArenaInfo.json`，结束后为 `.wowsreplay`；二者头部都带同一段明文 JSON 元数据。

## 决策

**分两条链路取数：对局名单走本地 replay 元数据，玩家战绩走外部战绩源。**

### 1. 对局名单：本地 replay 元数据

来源：游戏目录 `replays/` 下的 `tempArenaInfo.json`，或 `.wowsreplay` 头部。

文件结构（两种）：

- 首字节 `0x7B`（`{`）：纯 JSON，直接读取。
- 首字节 `0x12`：12 字节头（4 字节 magic `12 32 34 11` + 4 字节未知 + 4 字节 JSON 长度）+ JSON。

关键字段：

```
matchGroup        pvp / ...
gameMode, scenario, mapDisplayName, clientVersionFromExe, dateTime
playersPerTeam    每队人数
vehicles[]        { name, relation, id, shipId }
```

`relation`：0=自己，1=友军，2=敌军。

关键结论（已用真实 replay 验证，见 `reference/wows-data-sources.md`）：

- 元数据里 `id` 字段**不是**账号 ID（spa_id），只是对局内标识，不可用于查战绩。必须用 `name` 去战绩源换取真实账号 ID。
- 元数据**不含服务器信息**，需要另行判定（见 ADR-0004）。

### 2. 玩家战绩：外部战绩源

见 ADR-0003。

### 3. bot 排除

沿用既有标识规则：`id <= 30` 的条目视为 bot 排除（剧情/护航模式）。

## 文件打开约束

游戏可能正在写入或替换该文件，必须以共享模式打开：

```
FileShare.ReadWrite | FileShare.Delete
```

读取失败（写一半、格式不符）视为可重试，不视为致命错误。

## 观察与解析分离

"检测到新对局"（观察）与"解析对局"（解析）是两个独立职责，分别可测：

- 观察：轮询目录，按 `LastWriteTime` 判断是否出现更新的文件。
- 解析：对给定文件路径读取并结构化。

## 后果

- 名单获取不依赖解析压缩 packet 流，工作量远低于完整 replay 解析器（ApeRadar 走的就是这条轻量路线）。
- 必须实现服务器识别（ADR-0004）与账号 ID 换取（ADR-0003）。
- 需要在日志与 UI 上区分"文件格式错误"（不重试）与"读取瞬时失败"（重试）。

## 关联

- ADR-0003（战绩源）、ADR-0004（服务器识别）
- 实测记录：`reference/wows-data-sources.md`
