using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace SeaMonkeys.Core.Servers;

public static class GameLocator
{
    private static readonly string[] Publishers =
    {
        "Wargaming.net",
        "Wargaming Group Limited",
        "360.cn",
        "Lesta Games",
    };

    private static readonly string[] CommonPaths =
    {
        @"C:\Games\World_of_Warships",
        @"C:\Program Files\World of Warships",
        @"C:\Program Files (x86)\World of Warships",
        @"D:\Games\World_of_Warships",
        @"D:\World of Warships",
        @"E:\Games\World_of_Warships",
    };

    public static string? FindGamePath()
    {
        // 1. 注册表卸载项（WGC / 直装版）。
        foreach (RegistryKey root in new[] { Registry.CurrentUser, Registry.LocalMachine })
        {
            string? found = SearchUninstallKey(
                root.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"));
            if (found is not null)
            {
                return found;
            }

            found = SearchUninstallKey(
                root.OpenSubKey(@"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"));
            if (found is not null)
            {
                return found;
            }
        }

        // 2. Steam 库。
        string? steam = FindInSteamLibraries();
        if (steam is not null)
        {
            return steam;
        }

        // 3. 常见安装目录。
        foreach (string path in CommonPaths)
        {
            if (IsGameRoot(path))
            {
                return path;
            }
        }

        return null;
    }

    /// <summary>校验目录是否为游戏根（含 WorldOfWarships.exe）。</summary>
    public static bool IsGameRoot(string path)
        => !string.IsNullOrWhiteSpace(path)
            && Directory.Exists(path)
            && File.Exists(Path.Combine(path, "WorldOfWarships.exe"));

    /// <summary>遍历 Steam 各库，查找 World of Warships。</summary>
    public static string? FindInSteamLibraries()
    {
        foreach (string steamRoot in EnumerateSteamRoots())
        {
            string libraryFile = Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf");
            foreach (string library in ReadSteamLibraries(libraryFile).Append(steamRoot))
            {
                string candidate = Path.Combine(
                    library, "steamapps", "common", "World of Warships");
                if (IsGameRoot(candidate))
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    private static IEnumerable<string> EnumerateSteamRoots()
    {
        var roots = new List<string>();

        void AddFromRegistry(RegistryKey root, string subKey, string valueName)
        {
            try
            {
                using RegistryKey? key = root.OpenSubKey(subKey);
                if (key?.GetValue(valueName) is string value && !string.IsNullOrWhiteSpace(value))
                {
                    roots.Add(value);
                }
            }
            catch
            {
            }
        }

        AddFromRegistry(Registry.CurrentUser, @"SOFTWARE\Valve\Steam", "SteamPath");
        AddFromRegistry(Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath");
        AddFromRegistry(Registry.LocalMachine, @"SOFTWARE\Valve\Steam", "InstallPath");

        return roots.Distinct(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>解析 libraryfolders.vdf 中的库路径。</summary>
    public static IEnumerable<string> ReadSteamLibraries(string libraryFile)
    {
        string content;
        try
        {
            if (!File.Exists(libraryFile))
            {
                return Array.Empty<string>();
            }

            content = File.ReadAllText(libraryFile);
        }
        catch
        {
            return Array.Empty<string>();
        }

        // 兼容新旧格式："path"  "D:\\SteamLibrary" 以及数字键。
        var paths = new List<string>();
        foreach (Match match in Regex.Matches(content, "\"path\"\\s+\"([^\"]+)\""))
        {
            string path = match.Groups[1].Value.Replace("\\\\", "\\");
            if (Directory.Exists(path))
            {
                paths.Add(path);
            }
        }

        return paths;
    }

    public static string? FindLatestReplay()
    {
        string? gamePath = FindGamePath();
        return gamePath is null ? null : FindLatestReplay(gamePath);
    }

    public static string? FindLatestReplay(string gamePath)
    {
        string replayDirectory = Path.Combine(gamePath, "replays");
        if (!Directory.Exists(replayDirectory))
        {
            return null;
        }

        return Directory
            .EnumerateFiles(replayDirectory, "tempArenaInfo.json", SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles(replayDirectory, "*.wowsreplay", SearchOption.AllDirectories))
            .Select(path => new FileInfo(path))
            .OrderByDescending(file => file.LastWriteTime)
            .Select(file => file.FullName)
            .FirstOrDefault();
    }

    private static string? SearchUninstallKey(RegistryKey? uninstall)
    {
        if (uninstall is null)
        {
            return null;
        }

        using (uninstall)
        {
            foreach (string subKeyName in uninstall.GetSubKeyNames())
            {
                using RegistryKey? key = uninstall.OpenSubKey(subKeyName);
                string publisher = (key?.GetValue("Publisher") ?? string.Empty).ToString() ?? string.Empty;
                if (!Publishers.Contains(publisher))
                {
                    continue;
                }

                string installLocation = (key?.GetValue("InstallLocation") ?? string.Empty).ToString() ?? string.Empty;
                if (IsGameRoot(installLocation))
                {
                    return installLocation;
                }
            }
        }

        return null;
    }
}
