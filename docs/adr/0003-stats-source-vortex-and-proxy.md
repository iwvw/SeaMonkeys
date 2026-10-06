# ADR-0003. 战绩源与代理：Vortex 为主，自建代理转发

日期：2026-10-04
状态：Accepted

## 背景

拿到参战名单（ADR-0002）后，需要用昵称换取账号 ID，再查账号与舰船战绩。

可选战绩源：

1. **Wargaming 公开开发者 API（PAI）**：官方文档化接口，需注册 `application_id`，限流严格（standalone 每 IP 10 req/s，server 20 req/s），**不完整服务国服与莱服**。
2. **Vortex**：各区域官网个人资料页背后的网页接口（`vortex.*`），无需 key，覆盖国际服三区、莱服与国服。未文档化。

已验证事实（见 `reference/wows-data-sources.md`）：用一份真实亚服 replay 的 24 名玩家，通过 `vortex.worldofwarships.asia` 全部查得账号数据（22 人成功、2 人隐藏档案），无需任何 key。

同时，由于国际服/莱服接口在境外，国内直连慢或失败，需要一个中转层。

## 决策

**以 Vortex 为主力战绩源，所有外部请求经自建代理转发。**

### 1. 战绩源

主力：**Vortex**（免 key、全服覆盖，含国服）。

区域主机：

| 区域 | Vortex 主机 |
|---|---|
| NA | `vortex.worldofwarships.com` |
| EU | `vortex.worldofwarships.eu` |
| ASIA | `vortex.worldofwarships.asia` |
| RU（莱服） | `vortex.korabli.su` |
| CN（国服） | `vortex.wowsgame.cn` |

端点（相对主机）：

```
GET /api/accounts/search/{name}                        → 换取 spa_id
GET /api/accounts/{id}/                                → 账号总数据
GET /api/accounts/{id}/clans/                          → 公会
GET /api/accounts/{id}/ships/{shipId}/pvp/             → 该船随机战
GET /api/accounts/{id}/ships/{shipId}/pvp_solo/        → 该船单野
GET /api/accounts/{id}/ships/{shipId}/pvp_div2/        → 该船双人
GET /api/accounts/{id}/ships/{shipId}/pvp_div3/        → 该船三人
```

必须携带请求头 `X-Requested-With: XMLHttpRequest`（网站 AJAX 后台特征）。

字段命名差异（与 PAI 不同，归一化时注意）：Vortex 用 `battles_count`、`original_exp`、`karma`、`spa_id`。

备选：**WG 公开 API（PAI）**，在需要官方文档化保证时启用，但受限于不覆盖国服/莱服，且需 `application_id`。

### 2. 代理

**客户端只请求自建代理，由代理转发到各区域战绩源。** 形态：自有服务器或 Cloudflare Worker，二者皆可。

代理职责：

1. **跨区探测**：客户端无法从 replay 得知服务器（ADR-0002/0004），由代理并行探测各区域，命中即返回区域与结果。
2. **请求节流**：把一局 24 人（含 search + 账号 + 公会 + 船只）的并发压到战绩源可接受范围。
3. **缓存**：玩家账号/公会数据缓存数小时，舰船目录按版本缓存；命中缓存不回源。
4. **密钥隔离**：即使未来启用 PAI，`application_id` 也只留在代理侧，客户端不硬编码任何密钥（CONTEXT 不变量）。
5. **隐藏档案降级**：对 `hidden` 账号返回统一的降级结果，不阻断其余玩家。

### 3. 归一化与降级

- 所有战绩源实现同一适配器接口（`IPlayerStatsAdapter`），返回归一化的 `PlayerStatistics`。
- 隐藏或不可得的战绩沿用负值哨兵语义，直到引入类型化结果状态（CONTEXT 不变量）。
- 源优先级与降级策略由 Core 集中决定，按区域选择：优先 Vortex；未来可按区域配置回退到 PAI。

### 4. 请求节流

- 单局玩家逐个 search 时插入固定间隔延迟（实测 60ms 量级即可稳定）。
- 账号/公会/船只查询尽可能批量或复用 search 返回值。
- 失败重试：格式错误与服务器识别失败不重试；网络类失败按退避重试。

## 备选与未选原因

- **只用 PAI**：不覆盖国服/莱服，且把 `application_id` 直接发给最终用户违反其 ToS（不得向第三方泄露密钥）。排除。
- **客户端直连 Vortex**：境外慢/失败，且暴露全部请求模式，无法统一节流与缓存。排除。
- **爬取官网页面**：脆弱且有 ToS 风险。排除。

## 后果

- 国服可用（`vortex.wowsgame.cn` 国内直连正常），打破"国服无公开数据"的常见说法。
- Vortex 未文档化，存在接口变更或被反滥用限制的风险。因此**必须把它封装在可替换的适配器层后**，接口一变只改这一层。
- 代理是必要基建，需承担运维成本（自有服务器）或部署成本（Cloudflare Worker 的 Worker + KV）。

## 关联

- ADR-0002（本地 replay 元数据）、ADR-0004（服务器识别）
- 实测记录与完整字段：`reference/wows-data-sources.md`
