namespace SeaMonkeys.Core.Observation;

/// <summary>
/// 定位游戏目录下最新的对局元数据文件（tempArenaInfo.json）。
/// 通过记录上次的写入时间判断是否有新对局，用于轮询式对局观察。
/// </summary>
public sealed class ArenaInfoLocator
{
    private DateTimeOffset lastWriteTime = DateTimeOffset.MinValue;

    /// <summary>
    /// 返回最新的 tempArenaInfo.json 路径。
    /// requireNewer 为 true 时，仅当该文件写入时间比上次记录更新才返回，否则返回空串。
    /// </summary>
    public string FindLatest(string gamePath, bool requireNewer)
    {
        if (string.IsNullOrWhiteSpace(gamePath))
        {
            return string.Empty;
        }

        string replayDirectory = Path.Combine(gamePath, "replays");
        if (!Directory.Exists(replayDirectory))
        {
            lastWriteTime = DateTimeOffset.MinValue;
            return string.Empty;
        }

        string[] candidates;
        try
        {
            candidates = Directory.GetFiles(
                replayDirectory, "tempArenaInfo.json", SearchOption.AllDirectories);
        }
        catch
        {
            return string.Empty;
        }

        if (candidates.Length == 0)
        {
            lastWriteTime = DateTimeOffset.MinValue;
            return string.Empty;
        }

        string latest = candidates
            .Select(path => new FileInfo(path))
            .OrderByDescending(file => file.LastWriteTime)
            .First()
            .FullName;

        DateTimeOffset latestWriteTime = new FileInfo(latest).LastWriteTime;
        bool isNewer = latestWriteTime > lastWriteTime;
        lastWriteTime = latestWriteTime;

        return isNewer || !requireNewer ? latest : string.Empty;
    }

    /// <summary>重置记录，使下一次查询无视时间戳。</summary>
    public void Reset() => lastWriteTime = DateTimeOffset.MinValue;
}
