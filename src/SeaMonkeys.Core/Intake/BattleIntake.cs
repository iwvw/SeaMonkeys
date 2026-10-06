using SeaMonkeys.Core.Models;
using SeaMonkeys.Core.Servers;
using SeaMonkeys.Core.Settings;
using SeaMonkeys.Core.Vortex;

namespace SeaMonkeys.Core.Intake;

public sealed class BattleIntake
{
    private readonly VortexStatsSource source;
    private readonly SeaMonkeysSettings settings;
    private readonly ServerResolver serverResolver = new();

    public BattleIntake(VortexStatsSource source, SeaMonkeysSettings settings)
    {
        this.source = source;
        this.settings = settings;
    }

    public async Task<Server> ResolveServerAsync(
        Battle battle,
        string gamePath,
        CancellationToken cancellationToken = default)
    {
        if (settings.Server != Server.Auto)
        {
            return settings.Server;
        }

        try
        {
            return serverResolver.Resolve(settings.Server, gamePath);
        }
        catch (ServerResolutionException)
        {
            Server detected = await DetectServerByProbeAsync(battle, cancellationToken);
            if (detected == Server.Auto)
            {
                throw;
            }

            return detected;
        }
    }

    public async Task<Server> DetectServerByProbeAsync(
        Battle battle,
        CancellationToken cancellationToken)
    {
        string? sampleName = battle.Participants
            .Select(p => p.Name)
            .FirstOrDefault(n => !string.IsNullOrWhiteSpace(n) && !n.StartsWith(':'));

        if (sampleName is null)
        {
            return Server.Auto;
        }

        var probes = ServerExtensions.AllConcrete.Select(async server =>
        {
            try
            {
                long id = await source.SearchAccountIdAsync(server, sampleName, cancellationToken);
                return (server, id);
            }
            catch
            {
                return (server, 0L);
            }
        });

        (Server server, long id)[] results = await Task.WhenAll(probes);
        (Server server, long id) hit = results.FirstOrDefault(r => r.id > 0);
        return hit.server;
    }

    public async Task EnrichAsync(
        Battle battle,
        Server server,
        IProgress<int>? progress = null,
        IProgress<Participant>? onParticipantReady = null,
        CancellationToken cancellationToken = default)
    {
        int completed = 0;

        var tasks = battle.Participants.Select(async participant =>
        {
            try
            {
                await EnrichParticipantAsync(participant, server, cancellationToken);

                // 加权胜率在单个玩家就绪时即算好，便于实时上屏。
                if (participant.Statistics.IsAvailable)
                {
                    double weighted = WeightedWinrateCalculator.Calculate(
                        participant.Statistics,
                        settings.WeightedWinrate);
                    participant.Statistics = WithWeightedWinrate(participant.Statistics, weighted);
                }

                onParticipantReady?.Report(participant);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                if (!participant.Statistics.IsAvailable)
                {
                    participant.Statistics = PlayerStatistics.Unavailable;
                }
            }
            finally
            {
                progress?.Report(Interlocked.Increment(ref completed));
            }
        });

        await Task.WhenAll(tasks);
    }

    private async Task EnrichParticipantAsync(
        Participant participant,
        Server server,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(participant.Name) || participant.Name.StartsWith(':'))
        {
            participant.Statistics = PlayerStatistics.Unavailable;
            return;
        }

        // 单玩家失败自动重试最多 3 次。
        Exception? last = null;
        for (int attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                await EnrichOnceAsync(participant, server, cancellationToken);
                return;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                last = ex;
                if (attempt < 2)
                {
                    await Task.Delay(300 * (attempt + 1), cancellationToken);
                }
            }
        }

        if (last is not null)
        {
            throw last;
        }
    }

    private async Task EnrichOnceAsync(
        Participant participant,
        Server server,
        CancellationToken cancellationToken)
    {
        long accountId = await source.SearchAccountIdAsync(server, participant.Name, cancellationToken);
        if (accountId <= 0)
        {
            participant.Statistics = PlayerStatistics.Unavailable;
            return;
        }

        participant.AccountId = accountId.ToString(System.Globalization.CultureInfo.InvariantCulture);
        participant.Server = server;

        PlayerStatistics account = await source.GetAccountStatisticsAsync(server, accountId, cancellationToken);
        if (!account.IsAvailable)
        {
            participant.Statistics = account.IsHidden ? PlayerStatistics.Hidden : PlayerStatistics.Unavailable;
            return;
        }

        // 账号已就绪，公会标签与舰船战绩互不依赖，并行取。
        Task<string?> clanTask = source.GetClanTagAsync(server, accountId, cancellationToken);
        Task<ShipStatistics> shipTask = source.GetShipStatisticsAsync(
            server,
            accountId,
            participant.ShipId,
            cancellationToken);

        await Task.WhenAll(clanTask, shipTask);
        participant.ClanTag = await clanTask;
        ShipStatistics ship = await shipTask;

        participant.Statistics = account with
        {
            ShipBattles = ship.Battles,
            ShipWins = ship.Wins,
            ShipDamageDealt = ship.DamageDealt,
            ShipBattlesSolo = ship.SoloBattles,
            ShipWinsSolo = ship.SoloWins,
            ShipDamageDealtSolo = ship.SoloDamage,
            ShipBattlesDiv2 = ship.Div2Battles,
            ShipWinsDiv2 = ship.Div2Wins,
            ShipDamageDealtDiv2 = ship.Div2Damage,
            ShipBattlesDiv3 = ship.Div3Battles,
            ShipWinsDiv3 = ship.Div3Wins,
            ShipDamageDealtDiv3 = ship.Div3Damage,
        };
    }

    private static PlayerStatistics WithWeightedWinrate(PlayerStatistics stats, double weighted) => new()
    {
        IsAvailable = stats.IsAvailable,
        IsHidden = stats.IsHidden,
        Battles = stats.Battles,
        Wins = stats.Wins,
        DamageDealt = stats.DamageDealt,
        Experience = stats.Experience,
        BattlesSolo = stats.BattlesSolo,
        WinsSolo = stats.WinsSolo,
        BattlesDiv2 = stats.BattlesDiv2,
        WinsDiv2 = stats.WinsDiv2,
        BattlesDiv3 = stats.BattlesDiv3,
        WinsDiv3 = stats.WinsDiv3,
        ShipBattles = stats.ShipBattles,
        ShipWins = stats.ShipWins,
        ShipDamageDealt = stats.ShipDamageDealt,
        ShipBattlesSolo = stats.ShipBattlesSolo,
        ShipWinsSolo = stats.ShipWinsSolo,
        ShipDamageDealtSolo = stats.ShipDamageDealtSolo,
        ShipBattlesDiv2 = stats.ShipBattlesDiv2,
        ShipWinsDiv2 = stats.ShipWinsDiv2,
        ShipDamageDealtDiv2 = stats.ShipDamageDealtDiv2,
        ShipBattlesDiv3 = stats.ShipBattlesDiv3,
        ShipWinsDiv3 = stats.ShipWinsDiv3,
        ShipDamageDealtDiv3 = stats.ShipDamageDealtDiv3,
        WeightedWinrate = weighted,
    };
}
