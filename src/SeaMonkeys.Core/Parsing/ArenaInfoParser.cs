using System.Globalization;
using System.Text;
using System.Text.Json;
using SeaMonkeys.Core.Models;

namespace SeaMonkeys.Core.Parsing;

public sealed class ArenaInfoParser
{
    private const int BotIdThreshold = 30;

    public Battle Parse(string filePath)
    {
        using FileStream stream = new(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);

        byte[] bytes = new byte[stream.Length];
        stream.ReadExactly(bytes);
        int headerOffset = ReadHeaderJsonOffset(bytes);
        int headerLength = ReadHeaderJsonLength(bytes);
        string headerJson = Encoding.UTF8.GetString(bytes, headerOffset, headerLength);

        // 莱斯塔（Korabli）版：头 JSON 之后紧跟 accountDBID 名单块（playersPublicInfo）。
        int lestaBlock = FindJson(bytes, headerOffset + headerLength, '{');
        if (lestaBlock > 0 && Contains(bytes, lestaBlock, "playersPublicInfo"))
        {
            return ParseLesta(bytes, headerJson, lestaBlock, filePath);
        }

        return ParseJson(headerJson, filePath);
    }

    public Battle ParseJson(string json, string sourceFile)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            throw new BattleParseException("FileFormatIncorrect", ex);
        }

        using (document)
        {
            JsonElement root = document.RootElement;

            string matchGroup = GetString(root, "matchGroup");
            string gameMode = GetString(root, "gameMode");
            string gameType = GetString(root, "gameType");
            string scenario = GetString(root, "scenario");
            string mapDisplayName = GetString(root, "mapDisplayName");
            int mapId = (int)GetLong(root, "mapId");
            string clientVersion = GetString(root, "clientVersionFromExe");
            DateTimeOffset startTime = ParseStartTime(GetString(root, "dateTime"));

            var participants = new List<Participant>();
            if (root.TryGetProperty("vehicles", out JsonElement vehicles) &&
                vehicles.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement vehicle in vehicles.EnumerateArray())
                {
                    long rawId = GetLong(vehicle, "id");
                    if (rawId <= BotIdThreshold)
                    {
                        continue;
                    }

                    participants.Add(new Participant
                    {
                        Name = GetString(vehicle, "name"),
                        Relation = (Relation)GetLong(vehicle, "relation"),
                        ShipId = GetLong(vehicle, "shipId").ToString(CultureInfo.InvariantCulture),
                        RawId = rawId,
                    });
                }
            }

            return new Battle
            {
                MatchGroup = matchGroup,
                GameMode = gameMode,
                GameType = gameType,
                Scenario = scenario,
                MapDisplayName = mapDisplayName,
                MapId = mapId,
                ClientVersion = clientVersion,
                StartTime = startTime,
                SourceFile = sourceFile,
                Participants = participants,
            };
        }
    }

    private static int ReadHeaderJsonOffset(byte[] bytes)
        => bytes.Length > 0 && bytes[0] == 0x7B ? 0 : 12;

    private static int ReadHeaderJsonLength(byte[] bytes)
    {
        if (bytes.Length == 0)
        {
            throw new BattleParseException("FileFormatIncorrect");
        }

        if (bytes[0] == 0x7B)
        {
            // 纯 JSON 起始（未压缩）：整个内容即 JSON。
            return bytes.Length;
        }

        if (bytes.Length < 12)
        {
            throw new BattleParseException("FileFormatIncorrect");
        }

        if (bytes[0] == 0x12)
        {
            ReadOnlySpan<byte> magic = new byte[] { 0x12, 0x32, 0x34, 0x11 };
            if (!bytes.AsSpan(0, 4).SequenceEqual(magic))
            {
                throw new BattleParseException("FileFormatIncorrect");
            }

            int length = BitConverter.ToInt32(bytes, 8);
            if (length <= 0 || length > 16 * 1024 * 1024 || 12 + length > bytes.Length)
            {
                throw new BattleParseException("FileFormatIncorrect");
            }

            return length;
        }

        throw new BattleParseException("FileFormatIncorrect");
    }

    /// <summary>从 start 开始查找第一个未转义的 JSON 起始（{）。</summary>
    private static int FindJson(byte[] bytes, int start, char ch)
    {
        for (int i = start; i < bytes.Length; i++)
        {
            if (bytes[i] == (byte)ch)
            {
                return i;
            }
        }

        return -1;
    }

    private static bool Contains(byte[] bytes, int start, string needle)
    {
        ReadOnlySpan<byte> n = System.Text.Encoding.UTF8.GetBytes(needle);
        for (int i = start; i < bytes.Length - n.Length + 1; i++)
        {
            if (bytes.AsSpan(i, n.Length).SequenceEqual(n))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 解析莱斯塔（Korabli）回放：头 JSON 提供对局基本信息，
    /// accountDBID 块提供玩家名单（playersPublicInfo）。
    /// </summary>
    private static Battle ParseLesta(byte[] bytes, string headerJson, int blockStart, string sourceFile)
    {
        using JsonDocument headerDoc = JsonDocument.Parse(headerJson);
        using JsonDocument blockDoc = ParseJsonObject(bytes, blockStart);

        JsonElement root = headerDoc.RootElement;
        JsonElement block = blockDoc.RootElement;

        string matchGroup = GetString(root, "matchGroup");
        string scenario = GetString(root, "scenario");
        string mapDisplayName = GetString(root, "mapDisplayName");
        int mapId = (int)GetLong(root, "mapId");
        string gameType = GetString(root, "gameType");
        string gameMode = GetString(root, "gameMode");
        DateTimeOffset startTime = ParseStartTime(GetString(root, "dateTime"));

        // block 结构根是 {"accountDBID": ..., "playersPublicInfo": {...}, ...}
        var participants = new List<Participant>();
        if (block.TryGetProperty("playersPublicInfo", out JsonElement ppi) &&
            ppi.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty item in ppi.EnumerateObject())
            {
                if (item.Value.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                JsonElement arr = item.Value;
                // [0]=账号ID, [1]=昵称, [3]=公会tag, [6]=队伍(0/1), [7]=船ID
                long accountId = arr.GetArrayLength() > 0 ? arr[0].GetInt64() : 0;
                string name = arr.GetArrayLength() > 1 ? (arr[1].ValueKind == JsonValueKind.String ? arr[1].GetString() ?? string.Empty : string.Empty) : string.Empty;
                string clan = arr.GetArrayLength() > 3 && arr[3].ValueKind == JsonValueKind.String ? arr[3].GetString() ?? string.Empty : string.Empty;
                long team = arr.GetArrayLength() > 6 ? arr[6].GetInt64() : 0;
                string ownVehicle = GetString(root, "playerName");
                long ownTeam = GetOwnTeam(block, ownVehicle);

                if (accountId <= 0)
                {
                    continue;
                }

                // 队伍归属：0 号队伍通常是敌军，1 号队伍是友军？需与 WOWS 对齐。
                // WOWS relation: 0=self, 1=ally, 2=enemy。
                // 这里以「自己所在队伍」为参照：同队为友军，异队为敌军。
                Relation relation = team == ownTeam ? Relation.Ally : Relation.Enemy;
                if (name == ownVehicle)
                {
                    relation = Relation.Self;
                }

                participants.Add(new Participant
                {
                    Name = name,
                    Relation = relation,
                    ShipId = arr.GetArrayLength() > 7 ? arr[7].GetInt64().ToString(CultureInfo.InvariantCulture) : string.Empty,
                    RawId = accountId,
                    AccountId = accountId.ToString(CultureInfo.InvariantCulture),
                    Server = Server.Ru,
                    ClanTag = string.IsNullOrWhiteSpace(clan) ? null : $"[{clan}]",
                });
            }
        }

        return new Battle
        {
            MatchGroup = matchGroup,
            GameMode = gameMode,
            GameType = gameType,
            Scenario = scenario,
            MapDisplayName = mapDisplayName,
            MapId = mapId,
            ClientVersion = GetString(root, "clientVersionFromExe"),
            StartTime = startTime,
            SourceFile = sourceFile,
            Participants = participants,
        };
    }

    /// <summary>从 block JSON 中解析出「自己」的队伍号（0 或 1）。</summary>
    private static long GetOwnTeam(JsonElement block, string ownName)
    {
        if (block.TryGetProperty("playersPublicInfo", out JsonElement ppi) &&
            ppi.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty item in ppi.EnumerateObject())
            {
                if (item.Value.ValueKind != JsonValueKind.Array || item.Value.GetArrayLength() <= 1)
                {
                    continue;
                }

                string name = item.Value[1].ValueKind == JsonValueKind.String ? item.Value[1].GetString() ?? string.Empty : string.Empty;
                if (name == ownName && item.Value.GetArrayLength() > 6)
                {
                    return item.Value[6].GetInt64();
                }
            }
        }

        return 0;
    }

    /// <summary>从指定偏移解析出一个完整 JSON 对象。</summary>
    private static JsonDocument ParseJsonObject(byte[] bytes, int start)
    {
        int depth = 0;
        bool inString = false;
        bool escaped = false;
        for (int i = start; i < bytes.Length; i++)
        {
            byte c = bytes[i];
            if (inString)
            {
                if (escaped)
                {
                    escaped = false;
                }
                else if (c == (byte)'\\')
                {
                    escaped = true;
                }
                else if (c == (byte)'"')
                {
                    inString = false;
                }
            }
            else
            {
                if (c == (byte)'"')
                {
                    inString = true;
                }
                else if (c == (byte)'{')
                {
                    depth++;
                }
                else if (c == (byte)'}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        string json = Encoding.UTF8.GetString(bytes, start, i - start + 1);
                        return JsonDocument.Parse(json);
                    }
                }
            }
        }

        throw new BattleParseException("FileFormatIncorrect");
    }


    private static DateTimeOffset ParseStartTime(string value)
    {
        if (DateTimeOffset.TryParseExact(
                value,
                "dd.MM.yyyy HH:mm:ss",
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeLocal,
                out DateTimeOffset exact))
        {
            return exact;
        }

        if (DateTimeOffset.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeLocal,
                out DateTimeOffset parsed))
        {
            return parsed;
        }

        return DateTimeOffset.MinValue;
    }

    private static string GetString(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out JsonElement value))
        {
            return string.Empty;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? string.Empty,
            JsonValueKind.Number => value.GetRawText(),
            _ => string.Empty,
        };
    }

    private static long GetLong(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out JsonElement value))
        {
            return 0;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number => value.TryGetInt64(out long l) ? l : (long)value.GetDouble(),
            JsonValueKind.String => long.TryParse(value.GetString(), out long l) ? l : 0,
            _ => 0,
        };
    }
}

public sealed class BattleParseException : Exception
{
    public BattleParseException(string message) : base(message)
    {
    }

    public BattleParseException(string message, Exception inner) : base(message, inner)
    {
    }
}
