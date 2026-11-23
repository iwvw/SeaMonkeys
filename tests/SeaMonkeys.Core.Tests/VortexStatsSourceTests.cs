using SeaMonkeys.Core.Models;
using SeaMonkeys.Core.Vortex;
using Xunit;

namespace SeaMonkeys.Core.Tests;

public sealed class VortexStatsSourceTests
{
    private sealed class FakeTransport(Func<string, string> handler) : IVortexTransport
    {
        public string BuildUrl(string host, string path) => host + path;

        public Task<string> GetAsync(string url, CancellationToken cancellationToken)
            => Task.FromResult(handler(url));

        public Task<string?> GetOptionalAsync(string url, CancellationToken cancellationToken)
            => Task.FromResult<string?>(handler(url));
    }

    [Fact]
    public async Task NonJsonBody_IsTreatedAsNoData()
    {
        var source = new VortexStatsSource(new FakeTransport(_ => "<html>gateway error</html>"));

        Assert.Equal(0, await source.SearchAccountIdAsync(Server.Ru, "x", default));

        PlayerStatistics stats = await source.GetAccountStatisticsAsync(Server.Ru, 1, default);
        Assert.False(stats.IsAvailable);
        Assert.False(stats.IsHidden);
    }

    [Fact]
    public async Task MissingStatistics_IsUnavailable_NotHidden()
    {
        var source = new VortexStatsSource(new FakeTransport(
            _ => """{"status":"ok","data":{"1":{"hidden":false}}}"""));

        PlayerStatistics stats = await source.GetAccountStatisticsAsync(Server.Ru, 1, default);
        Assert.False(stats.IsAvailable);
        Assert.False(stats.IsHidden);
    }

    [Fact]
    public async Task HiddenProfile_IsHidden()
    {
        var source = new VortexStatsSource(new FakeTransport(
            _ => """{"status":"ok","data":{"1":{"hidden":true}}}"""));

        PlayerStatistics stats = await source.GetAccountStatisticsAsync(Server.Ru, 1, default);
        Assert.True(stats.IsHidden);
    }

    [Fact]
    public async Task Search_SkipsFirstMismatch_AndMatchesCaseInsensitively()
    {
        var source = new VortexStatsSource(new FakeTransport(
            _ => """{"status":"ok","data":[{"name":"Other","spa_id":9},{"name":"PLAYER","spa_id":42}]}"""));

        Assert.Equal(42, await source.SearchAccountIdAsync(Server.Ru, "player", default));
    }

    [Fact]
    public async Task BattlesCount_IsNotDoubleCounted()
    {
        var source = new VortexStatsSource(new FakeTransport(
            _ => """
            {"status":"ok","data":{"1":{"statistics":{"pvp":{"battles_count":100,"battles":100,"wins":55}}}}}
            """));

        PlayerStatistics stats = await source.GetAccountStatisticsAsync(Server.Ru, 1, default);
        Assert.True(stats.IsAvailable);
        Assert.Equal(100, stats.Battles);
        Assert.Equal(55, stats.Wins);
    }
}