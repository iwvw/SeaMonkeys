using System.Text.Json;
using SeaMonkeys.Core.Intake;
using SeaMonkeys.Core.Models;
using SeaMonkeys.Core.Settings;
using SeaMonkeys.Core.Vortex;
using Xunit;

namespace SeaMonkeys.Core.Tests;

public sealed class BattleIntakeTests
{
    private sealed class FlakyTransport(int failFirstCalls) : IVortexTransport
    {
        private int calls;

        public int Calls => calls;

        public string BuildUrl(string host, string path) => host + path;

        public Task<string> GetAsync(string url, CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref calls) <= failFirstCalls)
            {
                throw new HttpRequestException("simulated transient failure");
            }

            return Task.FromResult(Route(url));
        }

        public Task<string?> GetOptionalAsync(string url, CancellationToken cancellationToken)
            => Task.FromResult<string?>(null);

        private static string Route(string url)
        {
            if (url.Contains("/search/", StringComparison.Ordinal))
            {
                string name = Uri.UnescapeDataString(url[(url.IndexOf("/search/", StringComparison.Ordinal) + 8)..]).TrimEnd('/');
                return JsonSerializer.Serialize(new
                {
                    status = "ok",
                    data = new[] { new { spa_id = 4242L, name } },
                });
            }

            if (url.Contains("/ships/", StringComparison.Ordinal))
            {
                return """{"status":"ok","data":{}}""";
            }

            return """{"status":"ok","data":{"4242":{"statistics":{"pvp":{"battles_count":10,"wins":5}}}}}""";
        }
    }

    private static Battle OnePlayerBattle() => new()
    {
        MatchGroup = "pvp",
        GameMode = "pvp",
        GameType = "RandomBattle",
        Scenario = "scenario",
        MapDisplayName = "Test",
        MapId = 1,
        StartTime = DateTimeOffset.UtcNow,
        ClientVersion = "1.0",
        SourceFile = "test",
        Participants = new[]
        {
            new Participant
            {
                Name = "Player",
                Relation = Relation.Self,
                ShipId = "1",
                RawId = 100,
            },
        },
    };

    [Fact]
    public async Task Enrich_RecoversFromTransientTransportFailures()
    {
        // 首轮三次尝试全部失败 → 触发整局重试,第二轮成功。
        var transport = new FlakyTransport(failFirstCalls: 3);
        Battle battle = OnePlayerBattle();
        var intake = new BattleIntake(new VortexStatsSource(transport), new SeaMonkeysSettings());

        await intake.EnrichAsync(battle, Server.Asia);

        Participant participant = battle.Participants[0];
        Assert.True(participant.Statistics.IsAvailable);
        Assert.False(participant.Statistics.IsError);
        Assert.Equal("4242", participant.AccountId);
    }

    [Fact]
    public async Task Enrich_MarksRequestFailed_WhenRetriesExhausted()
    {
        var transport = new FlakyTransport(failFirstCalls: int.MaxValue);
        Battle battle = OnePlayerBattle();
        var intake = new BattleIntake(new VortexStatsSource(transport), new SeaMonkeysSettings());

        await intake.EnrichAsync(battle, Server.Asia, maxPasses: 1);

        Participant participant = battle.Participants[0];
        Assert.False(participant.Statistics.IsAvailable);
        Assert.True(participant.Statistics.IsError);
    }
}