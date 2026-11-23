using System.Text.Json;
using SeaMonkeys.Core.Models;

namespace SeaMonkeys.App;

public enum WatchStatus
{
    None = 0,
    Positive = 1,
    Negtive = 2,
    Cheater = 3,
}

/// <summary>按服务器持久化的观察名单（JSON）。历史拼写 Negtive 为兼容契约。</summary>
public sealed class WatchListRepository
{
    // 注意:静态字段按声明顺序初始化,FilePath 必须先于 Current,
    // 否则 Current 构造时 FilePath 为 null,启动时不会加载用户观察名单。
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SeaMonkeys",
        "WatchList.json");

    public static WatchListRepository Current { get; } = new();

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
            AtomicFile.Backup(FilePath);
            data = new();
        }
    }

    private void Save()
    {
        try
        {
            AtomicFile.WriteAllText(FilePath, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
        }
    }

    public WatchStatus Get(Server server, string? accountId)
    {
        if (string.IsNullOrEmpty(accountId) || server == Server.Auto)
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
        if (string.IsNullOrEmpty(accountId) || server == Server.Auto)
        {
            return;
        }

        if (status == WatchStatus.None)
        {
            if (data.TryGetValue(server.ToCode(), out var existing))
            {
                existing.Remove(accountId);
            }
        }
        else
        {
            string key = server.ToCode();
            if (!data.TryGetValue(key, out var map))
            {
                map = new();
                data[key] = map;
            }

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
