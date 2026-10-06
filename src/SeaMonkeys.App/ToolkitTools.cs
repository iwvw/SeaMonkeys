using System.IO.Compression;

namespace SeaMonkeys.App;

/// <summary>
/// wows-toolkit 工具包管理：统一负责从 GitHub Releases 下载、解压、定位其中的可执行文件
/// （minimap_renderer、wowsunpack 等）。下载走可配置的加速前缀。
/// </summary>
public static class ToolkitTools
{
    private const string ReleaseTag = "v1.0.1";
    private const string ToolsFileName = "wows_toolkit_tools_v1.0.1_win64.zip";

    public const string MinimapRenderer = "minimap_renderer.exe";
    public const string WowsUnpack = "wowsunpack.exe";

    private static readonly string ToolsDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SeaMonkeys",
        "tools");

    /// <summary>工具下载目录。</summary>
    public static string Directory => ToolsDirectory;

    /// <summary>某工具是否已就绪。</summary>
    public static bool IsInstalled(string exeName) => GetPath(exeName) is not null;

    /// <summary>工具可执行文件路径；缺失返回 null。渲染工具允许用户指定路径覆盖。</summary>
    public static string? GetPath(string exeName)
    {
        if (exeName.Equals(MinimapRenderer, StringComparison.OrdinalIgnoreCase))
        {
            string configured = AppSettings.Current.RendererToolPath;
            if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured))
            {
                return configured;
            }
        }

        string bundled = Path.Combine(ToolsDirectory, exeName);
        return File.Exists(bundled) ? bundled : null;
    }

    /// <summary>下载并安装整个工具包，返回日志。</summary>
    public static async Task<bool> InstallAsync(IProgress<string>? log = null, CancellationToken token = default)
    {
        try
        {
            System.IO.Directory.CreateDirectory(ToolsDirectory);

            string url = $"https://github.com/landaire/wows-toolkit/releases/download/{ReleaseTag}/{ToolsFileName}";
            string prefix = AppSettings.Current.GitHubAccelerator?.Trim() ?? string.Empty;
            string downloadUrl = string.IsNullOrEmpty(prefix) ? url : prefix + url;

            log?.Report($"下载 {downloadUrl}");

            using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("SeaMonkeys");
            byte[] bytes = await http.GetByteArrayAsync(downloadUrl, token);

            log?.Report("解压工具…");

            int extracted = 0;
            using (var stream = new MemoryStream(bytes))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
            {
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    if (!entry.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    string target = Path.Combine(ToolsDirectory, entry.Name);
                    entry.ExtractToFile(target, overwrite: true);
                    extracted++;
                }
            }

            log?.Report(extracted > 0 ? "安装完成" : "压缩包中未找到可执行文件");
            return extracted > 0;
        }
        catch (Exception ex)
        {
            log?.Report($"下载失败：{ex.Message}");
            return false;
        }
    }

    /// <summary>确保指定工具存在，缺失时自动下载工具包。</summary>
    public static async Task<string?> EnsureAsync(string exeName, IProgress<string>? log = null, CancellationToken token = default)
    {
        string? path = GetPath(exeName);
        if (path is not null)
        {
            return path;
        }

        if (!await InstallAsync(log, token))
        {
            return null;
        }

        return GetPath(exeName);
    }
}
