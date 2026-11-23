using SeaMonkeys.Core.Models;
using SeaMonkeys.Core.Settings;
using Xunit;

namespace SeaMonkeys.Core.Tests;

public sealed class WeightedWinrateCalculatorTests
{
    private static PlayerStatistics Stats() => new()
    {
        IsAvailable = true,
        Battles = 100,
        Wins = 55,
        BattlesSolo = 40,
        WinsSolo = 24,
        ShipBattles = 100,
        ShipWins = 60,
    };

    [Fact]
    public void ShipMaxWeight_IsClampedToUnitInterval()
    {
        double value = WeightedWinrateCalculator.Calculate(
            Stats(),
            new WeightedWinrateSettings { ShipMaxWeight = 500, ShipBattlesAtMaxWeight = 200 });

        Assert.InRange(value, 0.0, 1.0);
    }

    [Fact]
    public void Unavailable_ReturnsZero()
    {
        Assert.Equal(0, WeightedWinrateCalculator.Calculate(PlayerStatistics.Unavailable, new WeightedWinrateSettings()));
    }

    [Fact]
    public void UnclassifiedBattles_StillProduceFiniteValue()
    {
        double value = WeightedWinrateCalculator.Calculate(
            Stats(),
            new WeightedWinrateSettings { ShipMaxWeight = 100, ShipBattlesAtMaxWeight = 200 });

        Assert.True(double.IsFinite(value));
        Assert.InRange(value, 0.0, 1.0);
    }
}