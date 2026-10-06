namespace SeaMonkeys.Core.Models;

public enum Relation
{
    Self = 0,
    Ally = 1,
    Enemy = 2,
}

public sealed class Participant
{
    public required string Name { get; init; }
    public required Relation Relation { get; init; }
    public required string ShipId { get; init; }

    /// <summary>对局内标识，来自 replay 元数据，不可用于查询战绩。</summary>
    public required long RawId { get; init; }

    public string? AccountId { get; set; }
    public Server Server { get; set; } = Server.Auto;
    public string? ClanTag { get; set; }
    public PlayerStatistics Statistics { get; set; } = PlayerStatistics.Unavailable;
}

public sealed record PlayerStatistics
{
    public bool IsHidden { get; init; }
    public bool IsAvailable { get; init; }

    public double Battles { get; init; }
    public double Wins { get; init; }
    public double DamageDealt { get; init; }
    public double Experience { get; init; }

    public double BattlesSolo { get; init; }
    public double WinsSolo { get; init; }
    public double BattlesDiv2 { get; init; }
    public double WinsDiv2 { get; init; }
    public double BattlesDiv3 { get; init; }
    public double WinsDiv3 { get; init; }

    public double ShipBattles { get; init; }
    public double ShipWins { get; init; }
    public double ShipDamageDealt { get; init; }

    public double ShipBattlesSolo { get; init; }
    public double ShipWinsSolo { get; init; }
    public double ShipDamageDealtSolo { get; init; }
    public double ShipBattlesDiv2 { get; init; }
    public double ShipWinsDiv2 { get; init; }
    public double ShipDamageDealtDiv2 { get; init; }
    public double ShipBattlesDiv3 { get; init; }
    public double ShipWinsDiv3 { get; init; }
    public double ShipDamageDealtDiv3 { get; init; }

    public double WeightedWinrate { get; init; }

    public double Winrate => Battles <= 0 ? 0 : Wins / Battles;
    public double ShipWinrate => ShipBattles <= 0 ? 0 : ShipWins / ShipBattles;
    public double AvgDamage => Battles <= 0 ? 0 : DamageDealt / Battles;

    public static PlayerStatistics Unavailable { get; } = new() { IsAvailable = false };

    public static PlayerStatistics Hidden { get; } = new() { IsAvailable = false, IsHidden = true };
}
