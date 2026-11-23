using System.Globalization;
using System.Text.Json;
using SeaMonkeys.Core.Models;

namespace SeaMonkeys.Core.Vortex;

public sealed class VortexStatsSource
{
    private readonly IVortexTransport transport;

    public VortexStatsSource(IVortexTransport transport)
    {
        this.transport = transport;
    }

    public async Task<long> SearchAccountIdAsync(
        Server server,
        string name,
        CancellationToken cancellationToken)
    {
        string host = server.ToVortexHost();
        string path = $"/api/accounts/search/{Uri.EscapeDataString(name)}";
        string body = await transport.GetAsync(transport.BuildUrl(host, path), cancellationToken);

        if (!TryParseJson(body, out JsonDocument? document))
        {
            return 0;
        }

        using (document)
        {
            JsonElement root = document!.RootElement;
            if (!IsOk(root) || !root.TryGetProperty("data", out JsonElement data) ||
                data.ValueKind != JsonValueKind.Array || data.GetArrayLength() == 0)
            {
                return 0;
            }

            foreach (JsonElement candidate in data.EnumerateArray())
            {
                if (!candidate.TryGetProperty("name", out JsonElement nickname) ||
                    nickname.ValueKind != JsonValueKind.String ||
                    !string.Equals(nickname.GetString(), name, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!candidate.TryGetProperty("spa_id", out JsonElement spaId))
                {
                    continue;
                }

                return spaId.ValueKind switch
                {
                    JsonValueKind.Number => spaId.TryGetInt64(out long l) ? l : 0,
                    JsonValueKind.String => long.TryParse(spaId.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long parsed) ? parsed : 0,
                    _ => 0,
                };
            }

            return 0;
        }
    }

    public async Task<PlayerStatistics> GetAccountStatisticsAsync(
        Server server,
        long accountId,
        CancellationToken cancellationToken)
    {
        string host = server.ToVortexHost();
        string id = accountId.ToString(CultureInfo.InvariantCulture);
        string path = $"/api/accounts/{id}/";
        string body = await transport.GetAsync(transport.BuildUrl(host, path), cancellationToken);

        if (!TryParseJson(body, out JsonDocument? document))
        {
            return PlayerStatistics.Unavailable;
        }

        using (document)
        {
            JsonElement root = document!.RootElement;
            if (!IsOk(root) || !root.TryGetProperty("data", out JsonElement data) ||
                data.ValueKind != JsonValueKind.Object ||
                !data.TryGetProperty(id, out JsonElement account))
            {
                return PlayerStatistics.Unavailable;
            }

            if (account.TryGetProperty("hidden", out JsonElement hidden) && hidden.ValueKind == JsonValueKind.True)
            {
                return PlayerStatistics.Hidden;
            }

            if (account.TryGetProperty("hidden_profile", out JsonElement hiddenProfile) &&
                hiddenProfile.ValueKind is JsonValueKind.True)
            {
                return PlayerStatistics.Hidden;
            }

            if (!account.TryGetProperty("statistics", out JsonElement statistics) ||
                statistics.ValueKind != JsonValueKind.Object)
            {
                return PlayerStatistics.Unavailable;
            }

            double battles = SumBattles(statistics, "pvp");
            double wins = SumWins(statistics, "pvp");

            return new PlayerStatistics
            {
                IsAvailable = true,
                Battles = battles,
                Wins = wins,
                DamageDealt = GetDouble(statistics, "pvp", "damage_dealt"),
                Experience = GetDouble(statistics, "pvp", "original_exp"),
                BattlesSolo = SumBattles(statistics, "pvp_solo"),
                WinsSolo = SumWins(statistics, "pvp_solo"),
                BattlesDiv2 = SumBattles(statistics, "pvp_div2"),
                WinsDiv2 = SumWins(statistics, "pvp_div2"),
                BattlesDiv3 = SumBattles(statistics, "pvp_div3"),
                WinsDiv3 = SumWins(statistics, "pvp_div3"),
            };
        }
    }

    public async Task<string?> GetClanTagAsync(
        Server server,
        long accountId,
        CancellationToken cancellationToken)
    {
        string host = server.ToVortexHost();
        string id = accountId.ToString(CultureInfo.InvariantCulture);
        string path = $"/api/accounts/{id}/clans/";
        string? body = await transport.GetOptionalAsync(transport.BuildUrl(host, path), cancellationToken);

        if (body is null)
        {
            return null;
        }

        if (!TryParseJson(body, out JsonDocument? document))
        {
            return null;
        }

        using (document)
        {
            JsonElement root = document!.RootElement;
            if (!IsOk(root) || !root.TryGetProperty("data", out JsonElement data) ||
                data.ValueKind != JsonValueKind.Object ||
                !data.TryGetProperty("clan", out JsonElement clan) ||
                clan.ValueKind != JsonValueKind.Object ||
                !clan.TryGetProperty("tag", out JsonElement tag) ||
                tag.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            string? value = tag.GetString();
            return string.IsNullOrWhiteSpace(value) ? null : $"[{value}]";
        }
    }

    public async Task<ShipStatistics> GetShipStatisticsAsync(
        Server server,
        long accountId,
        string shipId,
        CancellationToken cancellationToken)
    {
        Task<ShipStatistics> pvpTask = GetShipModeAsync(server, accountId, shipId, "pvp", cancellationToken);
        Task<ShipStatistics> soloTask = GetShipModeAsync(server, accountId, shipId, "pvp_solo", cancellationToken);
        Task<ShipStatistics> div2Task = GetShipModeAsync(server, accountId, shipId, "pvp_div2", cancellationToken);
        Task<ShipStatistics> div3Task = GetShipModeAsync(server, accountId, shipId, "pvp_div3", cancellationToken);

        // 任一 mode 失败都由 WhenAll 聚合抛出,交由上层按可重试处理(不静默吞掉)。
        await Task.WhenAll(pvpTask, soloTask, div2Task, div3Task);

        ShipStatistics pvp = await pvpTask;
        ShipStatistics solo = await soloTask;
        ShipStatistics div2 = await div2Task;
        ShipStatistics div3 = await div3Task;

        return new ShipStatistics(
            pvp.Battles, pvp.Wins, pvp.DamageDealt,
            solo.Battles, solo.Wins, solo.DamageDealt,
            div2.Battles, div2.Wins, div2.DamageDealt,
            div3.Battles, div3.Wins, div3.DamageDealt);
    }

    private async Task<ShipStatistics> GetShipModeAsync(
        Server server,
        long accountId,
        string shipId,
        string mode,
        CancellationToken cancellationToken)
    {
        string host = server.ToVortexHost();
        string id = accountId.ToString(CultureInfo.InvariantCulture);
        string path = $"/api/accounts/{id}/ships/{shipId}/{mode}/";
        string body = await transport.GetAsync(transport.BuildUrl(host, path), cancellationToken);

        if (!TryParseJson(body, out JsonDocument? document))
        {
            return ShipStatistics.Empty;
        }

        using (document)
        {
            JsonElement root = document!.RootElement;
            if (!IsOk(root) || !root.TryGetProperty("data", out JsonElement data) ||
                data.ValueKind != JsonValueKind.Object ||
                !data.TryGetProperty(id, out JsonElement account) ||
                !account.TryGetProperty("statistics", out JsonElement statistics) ||
                !statistics.TryGetProperty(shipId, out JsonElement ship) ||
                !ship.TryGetProperty(mode, out JsonElement modeElement))
            {
                return ShipStatistics.Empty;
            }

            return new ShipStatistics(
                SumBattlesFlat(modeElement),
                SumWinsFlat(modeElement),
                GetDouble(modeElement, "damage_dealt"),
                0, 0, 0, 0, 0, 0, 0, 0, 0);
        }
    }

    private static bool TryParseJson(string body, out JsonDocument? document)
    {
        try
        {
            document = JsonDocument.Parse(body);
            return true;
        }
        catch (JsonException)
        {
            document = null;
            return false;
        }
    }

    private static bool IsOk(JsonElement root)
        => root.TryGetProperty("status", out JsonElement status) &&
           status.ValueKind == JsonValueKind.String &&
           status.GetString() == "ok";

    private static double SumBattles(JsonElement statistics, string mode)
    {
        if (!statistics.TryGetProperty(mode, out JsonElement modeElement) ||
            modeElement.ValueKind != JsonValueKind.Object)
        {
            return 0;
        }

        double count = GetDouble(modeElement, "battles_count");
        return count > 0 ? count : GetDouble(modeElement, "battles");
    }

    private static double SumWins(JsonElement statistics, string mode)
        => GetDouble(statistics, mode, "wins");

    private static double SumBattlesFlat(JsonElement mode)
    {
        double count = GetDouble(mode, "battles_count");
        return count > 0 ? count : GetDouble(mode, "battles");
    }

    private static double SumWinsFlat(JsonElement mode)
        => GetDouble(mode, "wins");

    private static double GetDouble(JsonElement statistics, string mode, string field)
    {
        if (!statistics.TryGetProperty(mode, out JsonElement modeElement) ||
            modeElement.ValueKind != JsonValueKind.Object)
        {
            return 0;
        }

        return GetDouble(modeElement, field);
    }

    private static double GetDouble(JsonElement element, string field)
    {
        if (!element.TryGetProperty(field, out JsonElement value))
        {
            return 0;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number => value.GetDouble(),
            JsonValueKind.String => double.TryParse(
                value.GetString(),
                NumberStyles.Any,
                CultureInfo.InvariantCulture,
                out double parsed) ? parsed : 0,
            _ => 0,
        };
    }
}

public readonly record struct ShipStatistics(
    double Battles, double Wins, double DamageDealt,
    double SoloBattles, double SoloWins, double SoloDamage,
    double Div2Battles, double Div2Wins, double Div2Damage,
    double Div3Battles, double Div3Wins, double Div3Damage)
{
    public static ShipStatistics Empty => new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
}
