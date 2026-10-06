using System.Text.Json;
using SeaMonkeys.Core.Models;

namespace SeaMonkeys.App;

public enum WatchStatus
{
    None,
    Positive,
    Negtive,
    Cheater,
}

/// <summary>按服务器持久化的观察名单（JSON）。历史拼写 Negtive 为兼容契约。</summary>
public sealed class WatchListRepository
{
    public static WatchListRepository Current { get; } = new();

    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SeaMonkeys",
        "WatchList.json");

    // serverCode -> (accountId -> status)
    private Dictionary<string, Dictionary<string, WatchStatus>> data = new();

    private WatchListRepository() => Load();

    private void Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                string json = File.ReadAllText(FilePath);
                data = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, WatchStatus>>>(json)
                    ?? new();
            }
        }
        catch
        {
            data = new();
        }
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
        }
    }

    public WatchStatus Get(Server server, string? accountId)
    {
        if (string.IsNullOrEmpty(accountId))
        {
            return WatchStatus.None;
        }

        string key = server.ToCode();
        return data.TryGetValue(key, out var map) && map.TryGetValue(accountId, out WatchStatus status)
            ? status
            : WatchStatus.None;
    }

    public void Set(Server server, string accountId, WatchStatus status)
    {
        string key = server.ToCode();
        if (!data.TryGetValue(key, out var map))
        {
            map = new();
            data[key] = map;
        }

        if (status == WatchStatus.None)
        {
            map.Remove(accountId);
        }
        else
        {
            map[accountId] = status;
        }

        Save();
    }

    public IReadOnlyList<(string AccountId, WatchStatus Status)> All(Server server)
    {
        string key = server.ToCode();
        if (!data.TryGetValue(key, out var map))
        {
            return Array.Empty<(string, WatchStatus)>();
        }

        return map.Select(kv => (kv.Key, kv.Value)).ToList();
    }

    public int TotalCount => data.Values.Sum(m => m.Count);
}
