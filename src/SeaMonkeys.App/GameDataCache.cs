using System.Diagnostics;

namespace SeaMonkeys.App;

/// <summary>渲染数据缓存位置。</summary>
public readonly record struct RenderCacheLocation(string ExtractedDir, string? OverrideDir);

/// <summary>
/// 用 wows-data-mgr 预提取游戏数据。渲染时改用 --extracted-dir,
/// 可跳过每次调用重新解析 GameParams 的约 15 秒开销。
/// 首次提取约 30 秒,占用约 350MB/版本。
/// </summary>
public static class GameDataCache
{
    private static readonly string Root = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SeaMonkeys",
        "render-cache");

    private const string LocaleFileName = "global.mo";

    /// <summary>缓存根目录。</summary>
    public static string Directory => Root;

    /// <summary>返回该 build 的缓存位置;未启用、失败或取消时返回 null。</summary>
    public static async Task<RenderCacheLocation?> EnsureAsync(
        string gameDir,
        string build,
        bool chineseNames,
        IProgress<string>? log,
        CancellationToken token)
    {
        string? manager = ToolkitTools.GetPath(ToolkitTools.WowsDataMgr);
        if (manager is null || !File.Exists(Path.Combine(ToolkitTools.Directory, "game_versions.toml")))
        {
            return null;
        }

        string? buildDir = FindBuildDir(build);
        if (buildDir is null)
        {
            log?.Report("首次生成游戏数据缓存(约 350MB,30 秒)…");
            if (!await DumpAsync(manager, gameDir, build, log, token))
            {
                return null;
            }

            buildDir = FindBuildDir(build);
            if (buildDir is null)
            {
                return null;
            }
        }

        return new RenderCacheLocation(Root, EnsureChineseOverride(buildDir, chineseNames));
    }

    private static string? FindBuildDir(string build)
    {
        try
        {
            if (!System.IO.Directory.Exists(Root))
            {
                return null;
            }

            foreach (string dir in System.IO.Directory.GetDirectories(Root))
            {
                if (Path.GetFileName(dir).EndsWith("_" + build, StringComparison.Ordinal)
                    && File.Exists(Path.Combine(dir, "metadata.toml"))
                    && System.IO.Directory.Exists(Path.Combine(dir, "vfs")))
                {
                    return dir;
                }
            }
        }
        catch
        {
        }

        return null;
    }

    private static async Task<bool> DumpAsync(
        string manager,
        string gameDir,
        string build,
        IProgress<string>? log,
        CancellationToken token)
    {
        try
        {
            System.IO.Directory.CreateDirectory(Root);

            var startInfo = new ProcessStartInfo
            {
                FileName = manager,
                WorkingDirectory = ToolkitTools.Directory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            foreach (string arg in new[]
            {
                "--data-dir", Root,
                "dump-renderer-data",
                "--game-dir", gameDir,
                "--build", build,
                "--no-armor",
                "-o", Root,
            })
            {
                startInfo.ArgumentList.Add(arg);
            }

            using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
            process.OutputDataReceived += (_, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                {
                    log?.Report(e.Data);
                }
            };
            process.ErrorDataReceived += (_, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                {
                    log?.Report(e.Data);
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            using var registration = token.Register(() =>
            {
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill(entireProcessTree: true);
                    }
                }
                catch
                {
                }
            });

            await process.WaitForExitAsync(CancellationToken.None);
            return process.ExitCode == 0 && FindBuildDir(build) is not null;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 渲染工具固定读取 translations/en。这里不改动缓存本体,而是在覆盖目录
    /// 放一份中文 global.mo,通过 --game-params-cache 让工具优先读取它。
    /// </summary>
    private static string? EnsureChineseOverride(string buildDir, bool chineseNames)
    {
        if (!chineseNames)
        {
            return null;
        }

        try
        {
            string source = Path.Combine(buildDir, "translations", "zh_sg", "LC_MESSAGES", LocaleFileName);
            if (!File.Exists(source))
            {
                return null;
            }

            string dir = Path.Combine(Root, "overrides", Path.GetFileName(buildDir));
            string target = Path.Combine(dir, "translations", "en", "LC_MESSAGES", LocaleFileName);
            System.IO.Directory.CreateDirectory(Path.GetDirectoryName(target)!);

            byte[] content = File.ReadAllBytes(source);
            if (!File.Exists(target) || new FileInfo(target).Length != content.LongLength)
            {
                File.WriteAllBytes(target, content);
            }

            return dir;
        }
        catch
        {
            return null;
        }
    }
}