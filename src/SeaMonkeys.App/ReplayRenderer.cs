using System.Diagnostics;
using System.Text.RegularExpressions;
using SeaMonkeys.Core.Servers;

namespace SeaMonkeys.App;

/// <summary>渲染进度。</summary>
public readonly record struct RenderProgress(string Stage, int Frame, int Total)
{
    public double Fraction => Total <= 0 ? 0 : Math.Clamp((double)Frame / Total, 0, 1);
}

/// <summary>
/// 回放可视化：调用 wows-toolkit 的 minimap_renderer 生成单帧预览或完整视频。
/// 工具为 MIT 许可的独立可执行文件，按需从 GitHub Releases 下载（支持加速前缀）。
/// </summary>
public static class ReplayRenderer
{
    /// <summary>已安装的工具路径；未安装返回 null。</summary>
    public static string? ToolPath => ToolkitTools.GetPath(ToolkitTools.MinimapRenderer);

    public static bool IsInstalled => ToolPath is not null;

    /// <summary>从 GitHub Releases 下载并解压渲染工具（支持加速前缀）。</summary>
    public static Task<bool> InstallAsync(IProgress<string>? log = null, CancellationToken token = default)
        => ToolkitTools.InstallAsync(log, token);

    /// <summary>渲染单帧预览图，返回 PNG 路径（失败返回 null）。</summary>
    public static async Task<string?> PreviewAsync(
        string replayPath,
        string outputDirectory,
        IProgress<string>? log = null,
        CancellationToken token = default)
    {
        string? tool = ToolPath;
        if (tool is null)
        {
            log?.Report("未找到渲染工具");
            return null;
        }

        Directory.CreateDirectory(outputDirectory);

        string? game = ResolveGamePath(replayPath);
        if (game is null)
        {
            log?.Report("未找到游戏目录，请在设置中填写游戏路径");
            return null;
        }

        // 不同回放的可用帧不同，mid 可能取不到帧；依次回退到 last、固定帧。
        foreach (string frame in new[] { "mid", "last", "100" })
        {
            string baseName = Path.Combine(outputDirectory, $"preview_{DateTime.Now:HHmmssfff}_{frame}");
            string expected = baseName + ".png";

            var args = new List<string> { "--game", game, "--dump-frame", frame, "-o", baseName };
            args.AddRange(BuildCommonArgs());
            args.Add(replayPath);

            int exit = await RunAsync(tool, args, null, token, log);
            if (exit == 0 && File.Exists(expected))
            {
                return expected;
            }

            if (token.IsCancellationRequested)
            {
                return null;
            }
        }

        log?.Report("未能从该回放生成预览帧");
        return null;
    }

    /// <summary>渲染完整视频，返回 MP4 路径（失败返回 null）。</summary>
    public static async Task<string?> RenderAsync(
        string replayPath,
        string outputPath,
        IProgress<RenderProgress>? progress = null,
        IProgress<string>? log = null,
        CancellationToken token = default)
    {
        string? tool = ToolPath;
        string? game = ResolveGamePath(replayPath);
        if (tool is null || game is null)
        {
            return null;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        var args = new List<string> { "--game", game, "--no-progress", "-o", outputPath };
        args.AddRange(BuildCommonArgs());
        args.Add(replayPath);

        int exit = await RunAsync(tool, args, progress, token, log);
        return exit == 0 && File.Exists(outputPath) ? outputPath : null;
    }

    /// <summary>根据设置拼接通用渲染参数。</summary>
    private static IEnumerable<string> BuildCommonArgs()
    {
        var s = AppSettings.Current;

        if (s.RenderCodec == 1)
        {
            yield return "--codec";
            yield return "h264";
        }
        else if (s.RenderCodec == 2)
        {
            yield return "--codec";
            yield return "h265";
        }

        if (s.RenderMaxSizeMiB > 0)
        {
            yield return "--max-size-mib";
            yield return s.RenderMaxSizeMiB.ToString();
        }

        if (s.RenderShowPlayerNames)
        {
            yield return "--show-player-names";
        }

        if (!s.RenderShowCapturePoints)
        {
            yield return "--no-capture-points";
        }

        if (!s.RenderShowBuildings)
        {
            yield return "--no-buildings";
        }

        if (!s.RenderShowCameraDirection)
        {
            yield return "--no-camera-direction";
        }

        if (!s.RenderShowArmament)
        {
            yield return "--no-armament";
        }

        if (!s.RenderShowKillFeed)
        {
            yield return "--no-kill-feed";
        }

        if (s.RenderShowSpeedTrails)
        {
            yield return "--show-speed-trails";
        }

        if (s.RenderShowShipConfig)
        {
            yield return "--show-ship-config";
        }
    }

    /// <summary>
    /// 定位游戏根目录：优先设置里的路径，其次从回放路径反推（回放位于 &lt;game&gt;\replays\...），
    /// 最后才用注册表探测（Steam 版通常探测不到）。
    /// </summary>
    private static string? ResolveGamePath(string? replayPath)
    {
        string game = AppSettings.Current.GamePath;
        if (!string.IsNullOrWhiteSpace(game) && Directory.Exists(game))
        {
            return game;
        }

        string? fromReplay = DeriveGamePathFromReplay(replayPath);
        if (fromReplay is not null)
        {
            return fromReplay;
        }

        return GameLocator.FindGamePath();
    }

    private static string? DeriveGamePathFromReplay(string? replayPath)
    {
        if (string.IsNullOrWhiteSpace(replayPath))
        {
            return null;
        }

        var directory = new DirectoryInfo(Path.GetDirectoryName(replayPath)!);
        while (directory is not null)
        {
            if (directory.Name.Equals("replays", StringComparison.OrdinalIgnoreCase))
            {
                string? root = directory.Parent?.FullName;
                if (root is not null && File.Exists(Path.Combine(root, "WorldOfWarships.exe")))
                {
                    return root;
                }

                return root;
            }

            directory = directory.Parent;
        }

        return null;
    }

    private static readonly Regex ProgressRegex = new(
        @"Progress\s+stage=(\S+)\s+frame=(\d+)\s+total=(\d+)",
        RegexOptions.Compiled);

    private static async Task<int> RunAsync(
        string exe,
        IEnumerable<string> args,
        IProgress<RenderProgress>? progress,
        CancellationToken token,
        IProgress<string>? log = null)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = exe,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (string arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };

        void HandleLine(string? line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return;
            }

            log?.Report(line);

            Match match = ProgressRegex.Match(line);
            if (match.Success
                && int.TryParse(match.Groups[2].Value, out int frame)
                && int.TryParse(match.Groups[3].Value, out int total))
            {
                progress?.Report(new RenderProgress(match.Groups[1].Value, frame, total));
            }
        }

        process.OutputDataReceived += (_, e) => HandleLine(e.Data);
        process.ErrorDataReceived += (_, e) => HandleLine(e.Data);

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
        return process.ExitCode;
    }
}
