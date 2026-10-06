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

    /// <summary>设置里服务器下拉框的索引：0=自动，1=亚服，2=欧服，3=美服，4=莱服，5=国服。</summary>
    public static int ToSettingsIndex(this Server server) => server switch
    {
        Server.Asia => 1,
        Server.Eu => 2,
        Server.Na => 3,
        Server.Ru => 4,
        Server.Cn => 5,
        _ => 0,
    };

    public static Server FromSettingsIndex(int index) => index switch
    {
        1 => Server.Asia,
        2 => Server.Eu,
        3 => Server.Na,
        4 => Server.Ru,
        5 => Server.Cn,
        _ => Server.Auto,
    };

    public static IReadOnlyList<Server> AllConcrete { get; } = new[]
    {
        Server.Ru, Server.Eu, Server.Na, Server.Asia, Server.Cn,
    };
}
