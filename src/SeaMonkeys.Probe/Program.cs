using System.Diagnostics;
using System.Globalization;
using SeaMonkeys.Core.Intake;
using SeaMonkeys.Core.Models;
using SeaMonkeys.Core.Parsing;
using SeaMonkeys.Core.Settings;
using SeaMonkeys.Core.Ships;
using SeaMonkeys.Core.Vortex;

namespace SeaMonkeys.Probe;

internal static class Program
{
    private const string DefaultReplay =
        @"D:\Game\Steam\steamapps\common\World of Warships\replays\15.8.0.0\20261004_013852_PASS710-Archerfish_14_Atlantic.wowsreplay";

    private static async Task<int> Main(string[] args)
    {
        if (args.Length >= 2 && args[0] == "shipgen")
        {
            return await ShipGenAsync(args[1], args.Length >= 3 ? args[2] : null);
        }

        string replayPath = args.Length > 0 ? args[0] : DefaultReplay;

        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.WriteLine($"replay: {replayPath}");
        if (!File.Exists(replayPath))
        {
            Console.WriteLine("replay file not found");
            return 1;
        }

        var parser = new ArenaInfoParser();
        Battle battle = parser.Parse(replayPath);

        Console.WriteLine($"matchGroup={battle.MatchGroup} mode={battle.GameMode} scenario={battle.Scenario}");
        Console.WriteLine($"map={battle.MapDisplayName} time={battle.StartTime:u} version={battle.ClientVersion}");
        Console.WriteLine($"participants={battle.Participants.Count}");
        Console.WriteLine();

        var settings = new SeaMonkeysSettings
        {
            Server = Server.Auto,
            RequestDelayMs = 60,
            MaximumParallelRequests = 6,
        };

        using var transport = new HttpVortexTransport(settings);
        var source = new VortexStatsSource(transport, settings);
        var intake = new BattleIntake(source, settings);

        Console.WriteLine("resolving server by probing regions...");
        Server server = await intake.ResolveServerAsync(battle, string.Empty);
        Console.WriteLine($"server={server.ToDisplayName()}");
        Console.WriteLine();

        Console.WriteLine("fetching statistics...");
        var progress = new Progress<int>(done => Console.WriteLine($"  {done}/{battle.Participants.Count}"));
        await intake.EnrichAsync(battle, server, progress);

        Console.WriteLine();
        PrintTeam("ALLIES", battle.Allies);
        Console.WriteLine();
        PrintTeam("ENEMIES", battle.Enemies);
        return 0;
    }

    /// <summary>
    /// shipgen &lt;gamePath&gt; [outJson] [unpackExe]
    /// 用 wowsunpack 导出 GameParams，配合游戏本地化生成 ships.json。构建期刷新内置表用。
    /// </summary>
    private static async Task<int> ShipGenAsync(string gamePath, string? outJson)
    {
        outJson ??= Path.Combine(AppContext.BaseDirectory, "ships.json");
        string unpack = Environment.GetEnvironmentVariable("WOWSUNPACK")
            ?? Path.Combine(AppContext.BaseDirectory, "wowsunpack.exe");

        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.WriteLine($"game={gamePath}");
        Console.WriteLine($"out={outJson}");
        Console.WriteLine($"unpack={unpack}");

        if (!File.Exists(unpack))
        {
            Console.WriteLine("wowsunpack.exe not found (set WOWSUNPACK or place beside probe)");
            return 1;
        }

        string gameParams = Path.Combine(Path.GetTempPath(), $"SeaMonkeys_gp_{Guid.NewGuid():N}.json");
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = unpack,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            psi.ArgumentList.Add("-g");
            psi.ArgumentList.Add(gamePath);
            psi.ArgumentList.Add("game-params");
            psi.ArgumentList.Add(gameParams);

            Console.WriteLine("exporting GameParams...");
            using (var p = Process.Start(psi)!)
            {
                await p.WaitForExitAsync();
                if (p.ExitCode != 0 || !File.Exists(gameParams))
                {
                    Console.WriteLine("game-params export failed");
                    return 1;
                }
            }

            // zh_sg（正常简中）优先，zh（含反和谐代号）兜底。
            string? primaryMo = FindLatestMo(gamePath, "zh_sg");
            string? fallbackMo = FindLatestMo(gamePath, "zh");
            string? enMo = FindLatestMo(gamePath, "en");
            if (primaryMo is null && fallbackMo is null)
            {
                Console.WriteLine("zh global.mo not found");
                return 1;
            }

            Console.WriteLine($"zh_sg={primaryMo}");
            Console.WriteLine($"zh={fallbackMo}");
            var zh = new Dictionary<string, string>();
            if (fallbackMo is not null)
            {
                foreach (var kv in ShipCatalogBuilder.ParseMo(fallbackMo)) zh[kv.Key] = kv.Value;
            }
            if (primaryMo is not null)
            {
                foreach (var kv in ShipCatalogBuilder.ParseMo(primaryMo)) zh[kv.Key] = kv.Value;
            }
            var en = enMo is null ? null : ShipCatalogBuilder.ParseMo(enMo);

            string version = ReadVersion(gamePath);
            string date = DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
            string json = ShipCatalogBuilder.Build(gameParams, zh, en, version, date);
            File.WriteAllText(outJson, json);

            int count = 0;
            using (var doc = System.Text.Json.JsonDocument.Parse(json))
            {
                if (doc.RootElement.TryGetProperty("ships", out var ships))
                {
                    count = ships.EnumerateObject().Count();
                }
            }

            Console.WriteLine($"wrote {count} ships to {outJson}");
            return count > 0 ? 0 : 2;
        }
        finally
        {
            try { File.Delete(gameParams); } catch { }
        }
    }

    private static string? FindLatestMo(string gamePath, params string[] languages)
    {
        string binDir = Path.Combine(gamePath, "bin");
        if (!Directory.Exists(binDir))
        {
            return null;
        }

        foreach (string build in Directory.GetDirectories(binDir)
            .OrderByDescending(d => Path.GetFileName(d).Length)
            .ThenByDescending(d => Path.GetFileName(d)))
        {
            foreach (string lang in languages)
            {
                string path = Path.Combine(build, "res", "texts", lang, "LC_MESSAGES", "global.mo");
                if (File.Exists(path))
                {
                    return path;
                }
            }
        }

        return null;
    }

    private static string ReadVersion(string gamePath)
    {
        try
        {
            string? build = Directory.GetDirectories(Path.Combine(gamePath, "bin"))
                .OrderByDescending(d => Path.GetFileName(d).Length)
                .ThenByDescending(d => Path.GetFileName(d))
                .FirstOrDefault();
            return build is null ? "local" : Path.GetFileName(build);
        }
        catch
        {
            return "local";
        }
    }

    private static void PrintTeam(string title, IEnumerable<Participant> participants)
    {
        Console.WriteLine($"=== {title} ===");
        Console.WriteLine($"{"name",-24}{"clan",-8}{"ship",-14}{"battles",10}{"wr",8}{"weighted",10}");
        foreach (Participant p in participants.OrderByDescending(p => p.Statistics.WeightedWinrate))
        {
            var s = p.Statistics;
            string battles = s.IsAvailable ? s.Battles.ToString("0", CultureInfo.InvariantCulture) : "-";
            string wr = s.IsAvailable ? s.Winrate.ToString("P1", CultureInfo.InvariantCulture) : "-";
            string weighted = s.IsAvailable ? s.WeightedWinrate.ToString("P1", CultureInfo.InvariantCulture) : "-";
            string state = !s.IsAvailable ? (s.IsHidden ? " (hidden)" : " (n/a)") : string.Empty;
            Console.WriteLine(
                $"{p.Name,-24}{p.ClanTag ?? "-",-8}{p.ShipId,-14}{battles,10}{wr,8}{weighted,10}{state}");
        }
    }
}
