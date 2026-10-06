# 参考：战舰世界数据来源与实测记录

> 本文记录 SeaMonkeys 依赖的两条数据链路的格式、接口与实测结果。
> 实测日期：2026-10-04。实测样本：本地真实 replay 一份。

## 一、样本

```
D:\Game\Steam\steamapps\common\World of Warships\replays\15.8.0.0\20261004_013852_PASS710-Archerfish_14_Atlantic.wowsreplay
大小：1,954,208 字节
```

头部字节：

```
12 32 34 11 | 03 00 00 00 | 79 0A 00 00 | 7B 22 6D 61 ...
magic(4)    | 未知(4)      | JSON长度=0x0A79=2681 | {"ma...
```

解析方式：跳过前 8 字节，读 4 字节小端长度，再读该长度的 UTF-8 JSON。

## 二、对局元数据（replay 头部明文 JSON）

样本解析结果：

```
matchGroup        = pvp
gameMode          = 11
scenario          = domination_2point
mapDisplayName    = 14_Atlantic
playersPerTeam    = 12
teamsCount        = 2
dateTime          = 04.10.2026 01:38:52
clientVersionFromExe = 15,8,0,13187581
playerName        = salist_s
playerVehicle     = PASS710-Archerfish
playerID          = 0
vehicles          = 24 条
```

`vehicles[]` 结构：

```json
{ "shipId": 4276041424, "relation": 2, "id": 671953152, "name": "Logicphile_LY" }
```

`relation`：0=自己，1=友军，2=敌军。

### 重要发现

1. **`vehicles[].id` 不是账号 ID**。样本里是 `537xxxxxx` / `671xxxxxx`，而真实账号 ID（Vortex 的 `spa_id`）是 `2038493617` 这类。该字段只在对局内有效，不能用于查战绩。必须用 `name` 换取。
2. **元数据不含服务器信息**。没有区域字段，必须靠 ADR-0004 的机制判定。

## 三、战绩源实测（Vortex）

### 3.1 区域连通性

对同一昵称 `salist_s` 调各区域 `/api/accounts/search/{name}`：

| 主机 | 结果 |
|---|---|
| `vortex.worldofwarships.asia` | **命中** `spa_id=2038493617` |
| `vortex.wowsgame.cn`（国服） | 200，空 |
| `vortex.worldofwarships.eu` | 200，空 |
| `vortex.worldofwarships.com` | 200，空 |
| `vortex.korabli.su`（莱服） | 200，空 |

结论：样本是亚服；同昵称只在目标区命中，跨区探测有效。

请求头（必需）：

```
X-Requested-With: XMLHttpRequest
User-Agent: Mozilla/5.0 ...
```

### 3.2 端点与返回

```
GET /api/accounts/search/salist_s
→ {"status":"ok","data":[{"spa_id":2038493617,"name":"salist_s","hidden":false,
    "statistics":{"pvp":{"battles_count":12458,"wins":6767,...}, ...}}]}

GET /api/accounts/2038493617/
→ {"status":"ok","data":{"2038493617":{"statistics":{"pvp":{...},
    "pvp_solo":{...}, "pvp_div2":{...}, "pvp_div3":{...}}}}}

GET /api/accounts/2038493617/clans/
→ {"status":"ok","data":{"clan":{"name":"460的后花园","tag":"AG-"},
    "clan_id":2000041261,"role":"executive_officer"}}

GET /api/accounts/2038493617/ships/3549870064/pvp_solo/
→ {"status":"ok","data":{"2038493617":{"statistics":{"3549870064":{
    "pvp_solo":{"battles_count":484,"wins":...,"damage_dealt":...}}}}}}
```

字段命名与 WG 官方 PAI 不同，归一化时注意：

| Vortex | 说明 |
|---|---|
| `spa_id` | 账号 ID（PAI 里叫 `account_id`） |
| `battles_count` | 场次（PAI 里叫 `battles`） |
| `original_exp` | 原始经验（PAI 里叫 `exp`） |
| `karma` | 业力值（PAI 无此字段） |
| `hidden` | 隐藏档案标记 |

### 3.3 全名单批量实测

对样本 24 名玩家逐个 `search`（间隔 60ms），结果：

```
Logicphile_LY     id=2051262800  hidden=False  battles=6085    wr=48.7%
iSpeTsNaz_XeNos   id=2031695138  hidden=False  battles=5575    wr=45.5%
Y1silent          id=2027153670  hidden=False  battles=5514    wr=46.0%
HawTeaN_Ak1Kaze   id=3007572399  hidden=False  battles=5197    wr=57.1%
THEDEERS          id=2037191720  hidden=False  battles=6752    wr=50.6%
becauseyouare     id=3008384255  hidden=False  battles=2442    wr=49.6%
2365596663        id=2033036686  hidden=False  battles=1889    wr=42.9%
samuel_cz         id=2019045090  hidden=True   battles=-       wr=-
UnicornBF         id=2021945765  hidden=False  battles=5622    wr=49.0%
FinalScorpio      id=2030907540  hidden=True   battles=-       wr=-
1917822617        id=2048440526  hidden=False  battles=2489    wr=49.7%
InsaneEvil        id=2020327317  hidden=False  battles=3916    wr=46.9%
jibensugela       id=3015621799  hidden=False  battles=152     wr=48.0%
3378222136        id=2028253027  hidden=False  battles=4552    wr=49.8%
salist_s          id=2038493617  hidden=False  battles=12458   wr=54.3%   ← 房主
luxurymouse       id=2051714561  hidden=False  battles=10148   wr=40.1%
wuzang_2014       id=2051308916  hidden=False  battles=1105    wr=45.9%
606_2024          id=2050462790  hidden=False  battles=3068    wr=49.2%
fyjvt             id=2048257260  hidden=False  battles=7652    wr=48.6%
Dreamer0315       id=2023289586  hidden=False  battles=8820    wr=51.0%
yufn00            id=2025371759  hidden=False  battles=3826    wr=47.1%
3136132480        id=2032527414  hidden=False  battles=16508   wr=53.1%
wwwSumchangewww   id=2044838232  hidden=False  battles=6771    wr=45.1%
3222878852        id=2027603259  hidden=False  battles=2780    wr=45.6%
```

结论：24 人全部查得账号 ID；22 人拿到完整数据，2 人隐藏档案。整条链路（名单 → search → 账号/公会/船只）在免 key 条件下跑通。

## 四、完整接入链路

```
读 replays/ 下最新 tempArenaInfo.json（共享模式）
  → 解析 vehicles[]：24 人 name / relation / shipId
  → 排除 id <= 30 的 bot
  → 判定区域（clientrunner.log，回退跨区探测）
  → 逐个 search 换 spa_id（间隔节流）
  → 批量查 account / clans / ships/{shipId}/pvp*
  → 归一化 + 加权胜率
  → 应用观察名单
  → 计算战场视图 + 输出文本
```

## 五、风险

1. **Vortex 未文档化**：接口可能变更或加反滥用限制。必须封装在适配器层后。
2. **隐藏档案**：样本中 2/24。需降级展示，不阻断其余玩家。
3. **跨区探测成本**：回退路径需并行探测多区，由代理承担（ADR-0003）。
4. **请求节流**：单局 24 人，务必节流 + 缓存，避免触发限流。
