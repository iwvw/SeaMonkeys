using System.Text.Json;
using SeaMonkeys.Core.Models;

namespace SeaMonkeys.App;

public sealed class BattleHistoryEntry
{
    public DateTimeOffset StartTime { get; set; }
    public string MapName { get; set; } = string.Empty;
    public string Mode { get; set; } = string.Empty;
    public string Server { get; set; } = string.Empty;
    public int PlayerCount { get; set; }
    public double AllyAvgWeighted { get; set; }
    public double EnemyAvgWeighted { get; set; }

    /// <summary>该局对应的回放文件路径，用于后续渲染可视化。</summary>
    public string ReplayPath { get; set; } = string.Empty;

    public List<BattleHistoryPlayer> Players { get; set; } = new();
}

public sealed class BattleHistoryPlayer
{
    public string Name { get; set; } = string.Empty;
    public string Clan { get; set; } = string.Empty;
    public string Ship { get; set; } = string.Empty;
    public string Relation { get; set; } = string.Empty;
    public string Component { get; set; } = string.Empty;
    public double Weighted { get; set; }
    public double Winrate { get; set; }
}

/// <summary>本地对局历史（JSON 列表，最多保留 N 条）。</summary>
public sealed class BattleHistoryRepository
{
    public static BattleHistoryRepository Current { get; } = new();

    private const int MaxEntries = 200;

    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SeaMonkeys",
        "History.json");

    private List<BattleHistoryEntry> entries = new();

    private BattleHistoryRepository() => Load();

    public IReadOnlyList<BattleHistoryEntry> Entries => entries;

    private void Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                string json = File.ReadAllText(FilePath);
                entries = JsonSerializer.Deserialize<List<BattleHistoryEntry>>(json) ?? new();
            }
        }
        catch
        {
            entries = new();
        }
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
        }
    }

    public void Add(BattleHistoryEntry entry)
    {
        // 同一开战时间视为同一局，去重。
        entries.RemoveAll(e => e.StartTime == entry.StartTime);
        entries.Insert(0, entry);
        if (entries.Count > MaxEntries)
        {
            entries.RemoveRange(MaxEntries, entries.Count - MaxEntries);
        }
        Save();
    }

    public void Clear()
    {
        entries.Clear();
        Save();
    }
}
