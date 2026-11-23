using SeaMonkeys.Core.Models;
using SeaMonkeys.Core.Parsing;
using Xunit;

namespace SeaMonkeys.Core.Tests;

public sealed class ArenaInfoParserTests
{
    private static string WriteArena(string json)
    {
        string dir = Path.Combine(Path.GetTempPath(), "sm_tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "tempArenaInfo.json");
        File.WriteAllText(path, json);
        return path;
    }

    [Fact]
    public void ParsesWgJson_AndExcludesBots()
    {
        string path = WriteArena(
            """
            {
              "matchGroup": "pvp",
              "gameType": "RandomBattle",
              "mapId": 1,
              "mapDisplayName": "Shards",
              "dateTime": "01.01.2026 12:00:00",
              "playerName": "Me",
              "vehicles": [
                { "id": 100, "relation": 0, "name": "Me", "shipId": 4292851696 },
                { "id": 101, "relation": 2, "name": "Enemy", "shipId": 4292851696 },
                { "id": 5, "relation": 2, "name": "Bot", "shipId": 1 }
              ]
            }
            """);

        Battle battle = new ArenaInfoParser().Parse(path);

        Assert.Equal(2, battle.Participants.Count);
        Assert.Equal(1, battle.MapId);
        Assert.NotEqual(DateTimeOffset.MinValue, battle.StartTime);
        Assert.Single(battle.Allies);
        Assert.Single(battle.Enemies);
    }

    [Fact]
    public void ThrowsBattleParseException_OnGarbage()
    {
        string path = WriteArena("this is definitely not json");
        Assert.Throws<BattleParseException>(() => new ArenaInfoParser().Parse(path));
    }

    [Fact]
    public void MissingDateTime_YieldsMinValue()
    {
        string path = WriteArena(
            """
            {
              "matchGroup": "pvp",
              "vehicles": [ { "id": 100, "relation": 1, "name": "A", "shipId": 1 } ]
            }
            """);

        Battle battle = new ArenaInfoParser().Parse(path);
        Assert.Equal(DateTimeOffset.MinValue, battle.StartTime);
    }
}