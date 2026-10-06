using SeaMonkeys.Core.Models;
using SeaMonkeys.Core.Settings;

namespace SeaMonkeys.Core;

public static class WeightedWinrateCalculator
{
    public static double Calculate(
        PlayerStatistics stats,
        WeightedWinrateSettings settings)
    {
        if (!stats.IsAvailable)
        {
            return 0;
        }

        double soloWeight = stats.BattlesSolo * settings.AccountSoloWeightMultiplier;
        double div2Weight = stats.BattlesDiv2 * settings.AccountDiv2WeightMultiplier;
        double div3Weight = stats.BattlesDiv3 * settings.AccountDiv3WeightMultiplier;
        double totalWeight = soloWeight + div2Weight + div3Weight;

        double accountWinrate;
        if (totalWeight <= 0)
        {
            accountWinrate = stats.Winrate;
        }
        else
        {
            double solo = stats.BattlesSolo <= 0 ? 0 : stats.WinsSolo / stats.BattlesSolo;
            double div2 = stats.BattlesDiv2 <= 0 ? 0 : stats.WinsDiv2 / stats.BattlesDiv2;
            double div3 = stats.BattlesDiv3 <= 0 ? 0 : stats.WinsDiv3 / stats.BattlesDiv3;
            accountWinrate = (solo * soloWeight + div2 * div2Weight + div3 * div3Weight) / totalWeight;
        }

        double shipWeight = settings.ShipBattlesAtMaxWeight <= 0
            ? 0
            : Math.Min(1.0, stats.ShipBattles / settings.ShipBattlesAtMaxWeight)
                * (settings.ShipMaxWeight / 100.0);

        if (stats.ShipBattles <= 0)
        {
            return accountWinrate;
        }

        return accountWinrate * (1 - shipWeight) + stats.ShipWinrate * shipWeight;
    }

    public static bool IsBot(Participant participant, int botIdThreshold = 30)
        => participant.RawId <= botIdThreshold;
}
