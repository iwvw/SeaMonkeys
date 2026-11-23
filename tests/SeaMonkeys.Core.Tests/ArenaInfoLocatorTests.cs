using SeaMonkeys.Core.Observation;
using Xunit;

namespace SeaMonkeys.Core.Tests;

public sealed class ArenaInfoLocatorTests
{
    [Fact]
    public void Rollback_AllowsRetry_OfSameFile()
    {
        string gamePath = Path.Combine(Path.GetTempPath(), "sm_tests", Guid.NewGuid().ToString("N"));
        string replayDir = Path.Combine(gamePath, "replays");
        Directory.CreateDirectory(replayDir);
        string file = Path.Combine(replayDir, "tempArenaInfo.json");
        File.WriteAllText(file, "{}");

        var locator = new ArenaInfoLocator();

        Assert.Equal(file, locator.FindLatest(gamePath, requireNewer: true));
        Assert.Equal(string.Empty, locator.FindLatest(gamePath, requireNewer: true));

        locator.RollbackLast();
        Assert.Equal(file, locator.FindLatest(gamePath, requireNewer: true));
    }

    [Fact]
    public void Reset_AllowsRefire()
    {
        string gamePath = Path.Combine(Path.GetTempPath(), "sm_tests", Guid.NewGuid().ToString("N"));
        string replayDir = Path.Combine(gamePath, "replays");
        Directory.CreateDirectory(replayDir);
        string file = Path.Combine(replayDir, "tempArenaInfo.json");
        File.WriteAllText(file, "{}");

        var locator = new ArenaInfoLocator();
        Assert.Equal(file, locator.FindLatest(gamePath, requireNewer: true));
        locator.Reset();
        Assert.Equal(file, locator.FindLatest(gamePath, requireNewer: true));
    }

    [Fact]
    public void MissingDirectory_ReturnsEmpty()
    {
        string gamePath = Path.Combine(Path.GetTempPath(), "sm_tests", Guid.NewGuid().ToString("N"), "nope");
        var locator = new ArenaInfoLocator();
        Assert.Equal(string.Empty, locator.FindLatest(gamePath, requireNewer: true));
    }
}