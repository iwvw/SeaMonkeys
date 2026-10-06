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

        string json = ReadMetadataJson(stream);
        return ParseJson(json, filePath);
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

    private static string ReadMetadataJson(FileStream stream)
    {
        int firstByte = stream.ReadByte();
        stream.Seek(0, SeekOrigin.Begin);

        if (firstByte == 0x7B)
        {
            using var reader = new StreamReader(stream, Encoding.UTF8);
            return reader.ReadToEnd();
        }

        if (firstByte == 0x12)
        {
            Span<byte> header = stackalloc byte[12];
            stream.ReadExactly(header);

            ReadOnlySpan<byte> magic = new byte[] { 0x12, 0x32, 0x34, 0x11 };
            if (!header[..4].SequenceEqual(magic))
            {
                throw new BattleParseException("FileFormatIncorrect");
            }

            int length = BitConverter.ToInt32(header[8..12]);
            if (length <= 0 || length > 16 * 1024 * 1024)
            {
                throw new BattleParseException("FileFormatIncorrect");
            }

            byte[] buffer = new byte[length];
            stream.ReadExactly(buffer);
            return Encoding.UTF8.GetString(buffer);
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
