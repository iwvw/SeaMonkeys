using SeaMonkeys.Core.Models;

namespace SeaMonkeys.Core.Settings;

public sealed class SeaMonkeysSettings
{
    public string GamePath { get; set; } = string.Empty;
    public Server Server { get; set; } = Server.Auto;
    public bool SecondaryServerEnabled { get; set; }
    public Server SecondaryServer { get; set; } = Server.Auto;
    public string? ProxyBaseUrl { get; set; }
    public int RequestDelayMs { get; set; } = 20;
    public int MaximumParallelRequests { get; set; } = 12;
    public int RequestTimeoutSeconds { get; set; } = 20;
    public WeightedWinrateSettings WeightedWinrate { get; set; } = new();
}

public sealed class WeightedWinrateSettings
{
    public double AccountSoloWeightMultiplier { get; set; } = 1.0;
    public double AccountDiv2WeightMultiplier { get; set; } = 1.0;
    public double AccountDiv3WeightMultiplier { get; set; } = 1.0;
    public double ShipBattlesAtMaxWeight { get; set; } = 200;
    public double ShipMaxWeight { get; set; } = 100;
}
