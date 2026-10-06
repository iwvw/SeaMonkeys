namespace SeaMonkeys.Core.Models;

public enum Server
{
    Auto,
    Ru,
    Eu,
    Na,
    Asia,
    Cn,
}

public static class ServerExtensions
{
    public static string ToCode(this Server server) => server switch
    {
        Server.Ru => "ru",
        Server.Eu => "eu",
        Server.Na => "na",
        Server.Asia => "asia",
        Server.Cn => "cn",
        _ => throw new ArgumentOutOfRangeException(nameof(server), server, "AUTO has no code"),
    };

    public static string ToVortexHost(this Server server) => server switch
    {
        Server.Ru => "vortex.korabli.su",
        Server.Eu => "vortex.worldofwarships.eu",
        Server.Na => "vortex.worldofwarships.com",
        Server.Asia => "vortex.worldofwarships.asia",
        Server.Cn => "vortex.wowsgame.cn",
        _ => throw new ArgumentOutOfRangeException(nameof(server), server, "AUTO has no host"),
    };

    public static string ToDisplayName(this Server server) => server switch
    {
        Server.Auto => "AUTO",
        Server.Ru => "RU",
        Server.Eu => "EU",
        Server.Na => "NA",
        Server.Asia => "ASIA",
        Server.Cn => "CN",
        _ => server.ToString(),
    };

    public static bool TryParseCode(string? code, out Server server)
    {
        switch (code?.Trim().ToLowerInvariant())
        {
            case "ru": server = Server.Ru; return true;
            case "eu": server = Server.Eu; return true;
            case "na": server = Server.Na; return true;
            case "asia": server = Server.Asia; return true;
            case "cn": server = Server.Cn; return true;
            default: server = Server.Auto; return false;
        }
    }

    public static IReadOnlyList<Server> AllConcrete { get; } = new[]
    {
        Server.Ru, Server.Eu, Server.Na, Server.Asia, Server.Cn,
    };
}
