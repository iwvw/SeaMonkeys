using System.IO.Compression;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace SeaMonkeys.App;

/// <summary>更新检查结果：分别给出安装版与便携版的下载地址，按运行形态二选一。</summary>
public readonly record struct UpdateInfo(
    bool HasUpdate,
    string LatestVersion,
    string SetupUrl,
    string PortableUrl,
    string ReleaseUrl)
{
    public static UpdateInfo None => new(false, string.Empty, string.Empty, string.Empty, string.Empty);
}

/// <summary>
/// 通过 GitHub Releases 检查并自更新。请求与下载走镜像链（用户配置的加速前缀优先），
/// 更新时生成 cmd 脚本：等待本进程退出 → 静默安装（安装版）或原地覆盖（便携版）→ 重启。
/// 方案参考 Sox / Momomi 的 AppUpdateService。
/// </summary>
public static class UpdateService
{
    private const string Repo = "iwvw/SeaMonkeys";

    /// <summary>镜像前缀链，末尾空串表示直连兜底。</summary>
    private static readonly string[] Mirrors =
    [
        "https://gh-proxy.org/",
        "https://ghproxy.net/",
        "https://ghfast.top/",
        string.Empty,
    ];

    /// <summary>当前是否为安装版（Inno 会在安装目录放 unins000.exe）。</summary>
    public static bool IsInstalled =>
        File.Exists(Path.Combine(AppContext.BaseDirectory, "unins000.exe"));

    /// <summary>当前是否为合并版（自包含，自带 coreclr.dll）。</summary>
    private static bool IsMerged => File.Exists(Path.Combine(AppContext.BaseDirectory, "coreclr.dll"));

    private static string Arch => RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "arm64" : "x64";

    /// <summary>查询最新 Release。任何失败都返回 None（静默）。</summary>
    public static async Task<UpdateInfo> CheckAsync(CancellationToken token = default)
    {
        string apiUrl = $"https://api.github.com/repos/{Repo}/releases/latest";
        string? json = await GetStringWithMirrorsAsync(apiUrl, token);
        if (json is null)
        {
            return UpdateInfo.None;
        }

        try
        {
            using JsonDocument doc = JsonDocument.Parse(json);
            JsonElement root = doc.RootElement;

            string tag = root.TryGetProperty("tag_name", out JsonElement t) ? t.GetString() ?? string.Empty : string.Empty;
            string releaseUrl = root.TryGetProperty("html_url", out JsonElement h) ? h.GetString() ?? string.Empty : string.Empty;
            if (string.IsNullOrWhiteSpace(tag))
            {
                return UpdateInfo.None;
            }

            string latest = tag.TrimStart('v', 'V');
            if (CompareVersions(latest, CurrentVersion()) <= 0)
            {
                return UpdateInfo.None;
            }

            (string setup, string portable) = FindAssets(root, latest);
            if (string.IsNullOrWhiteSpace(setup) && string.IsNullOrWhiteSpace(portable))
            {
                return UpdateInfo.None;
            }

            return new UpdateInfo(true, latest, setup, portable, releaseUrl);
        }
        catch
        {
            return UpdateInfo.None;
        }
    }

    /// <summary>下载更新包并生成应用脚本，成功后硬退出本进程。</summary>
    public static async Task<bool> ApplyAsync(
        UpdateInfo info,
        IProgress<double>? progress = null,
        CancellationToken token = default)
    {
        try
        {
            string version = info.LatestVersion;
            string workDir = Path.Combine(Path.GetTempPath(), "SeaMonkeys", $"update-{version}");
            CleanStaleDownloads();
            Directory.CreateDirectory(workDir);

            bool installed = IsInstalled;
            string? downloaded;
            if (installed && !string.IsNullOrWhiteSpace(info.SetupUrl))
            {
                downloaded = await DownloadWithMirrorsAsync(
                    info.SetupUrl, Path.Combine(workDir, Path.GetFileName(new Uri(info.SetupUrl).AbsolutePath)), progress, token);
            }
            else if (!string.IsNullOrWhiteSpace(info.PortableUrl))
            {
                downloaded = await DownloadWithMirrorsAsync(
                    info.PortableUrl, Path.Combine(workDir, Path.GetFileName(new Uri(info.PortableUrl).AbsolutePath)), progress, token);
            }
            else
            {
                return false;
            }

            if (downloaded is null)
            {
                return false;
            }

            string script = installed
                ? WriteInstallerScript(downloaded, workDir)
                : WritePortableScript(downloaded, workDir);

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c \"\"{script}\"\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden,
            });

            // 硬退出：Application.Exit() 会等消息循环收尾，更新脚本的等待轮询可能一直看不到进程退出。
            Environment.Exit(0);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>安装版：等待退出后静默安装并重启。</summary>
    private static string WriteInstallerScript(string setup, string workDir)
    {
        string app = AppContext.BaseDirectory.TrimEnd('\\');
        string script = Path.Combine(workDir, "updater.cmd");
        var sb = new StringBuilder();
        sb.AppendLine("@echo off");
        sb.AppendLine("chcp 65001 >nul");
        sb.AppendLine(":WAIT");
        sb.AppendLine("tasklist /FI \"IMAGENAME eq SeaMonkeys.exe\" 2>nul | find /I \"SeaMonkeys.exe\" >nul");
        sb.AppendLine("if not errorlevel 1 ( timeout /t 1 /nobreak >nul & goto WAIT )");
        sb.AppendLine($"start /wait \"\" \"{setup}\" /SILENT /SP- /NORESTART");
        sb.AppendLine($"del /f /q \"{setup}\" >nul 2>&1");
        sb.AppendLine($"start \"\" \"{app}\\SeaMonkeys.exe\"");
        sb.AppendLine("del \"%~f0\"");
        File.WriteAllText(script, sb.ToString(), Encoding.UTF8);
        return script;
    }

    /// <summary>便携版：等待退出后解压覆盖程序目录（用户数据在 %LocalAppData%，不受影响）。</summary>
    private static string WritePortableScript(string zip, string workDir)
    {
        string app = AppContext.BaseDirectory.TrimEnd('\\');
        string extract = Path.Combine(workDir, "extract");
        string script = Path.Combine(workDir, "updater.cmd");
        var sb = new StringBuilder();
        sb.AppendLine("@echo off");
        sb.AppendLine("chcp 65001 >nul");
        sb.AppendLine(":WAIT");
        sb.AppendLine("tasklist /FI \"IMAGENAME eq SeaMonkeys.exe\" 2>nul | find /I \"SeaMonkeys.exe\" >nul");
        sb.AppendLine("if not errorlevel 1 ( timeout /t 1 /nobreak >nul & goto WAIT )");
        sb.AppendLine($"powershell -NoProfile -ExecutionPolicy Bypass -Command \"Expand-Archive -LiteralPath '{zip}' -DestinationPath '{extract}' -Force\"");
        sb.AppendLine($"robocopy \"{extract}\\SeaMonkeys\" \"{app}\" /E /NFL /NDL /NJH /NJS /R:1 /W:1 >nul");
        sb.AppendLine($"rmdir /s /q \"{extract}\"");
        sb.AppendLine($"del /f /q \"{zip}\" >nul 2>&1");
        sb.AppendLine($"start \"\" \"{app}\\SeaMonkeys.exe\"");
        sb.AppendLine("del \"%~f0\"");
        File.WriteAllText(script, sb.ToString(), Encoding.UTF8);
        return script;
    }

    /// <summary>清理历史更新下载，避免临时目录越堆越大。</summary>
    private static void CleanStaleDownloads()
    {
        try
        {
            string root = Path.Combine(Path.GetTempPath(), "SeaMonkeys");
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
        catch
        {
        }
    }

    private static (string Setup, string Portable) FindAssets(JsonElement release, string version)
    {
        string setup = string.Empty;
        string portable = string.Empty;

        if (release.TryGetProperty("assets", out JsonElement assets) && assets.ValueKind == JsonValueKind.Array)
        {
            string arch = Arch;
            string variant = IsMerged ? "merged" : "split";
            string setupName = $"SeaMonkeys-{version}-{arch}-{variant}-setup.exe";
            string portableName = $"SeaMonkeys-{version}-{arch}-{variant}-portable.zip";

            foreach (JsonElement asset in assets.EnumerateArray())
            {
                string name = asset.TryGetProperty("name", out JsonElement n) ? n.GetString() ?? string.Empty : string.Empty;
                string url = asset.TryGetProperty("browser_download_url", out JsonElement u) ? u.GetString() ?? string.Empty : string.Empty;
                if (string.IsNullOrWhiteSpace(url))
                {
                    continue;
                }

                if (name.Equals(setupName, StringComparison.OrdinalIgnoreCase))
                {
                    setup = url;
                }
                else if (name.Equals(portableName, StringComparison.OrdinalIgnoreCase))
                {
                    portable = url;
                }
                else if (string.IsNullOrEmpty(setup) && name.Contains(arch) && name.EndsWith("-setup.exe", StringComparison.OrdinalIgnoreCase))
                {
                    setup = url;
                }
                else if (string.IsNullOrEmpty(portable) && name.Contains(arch) && name.EndsWith("-portable.zip", StringComparison.OrdinalIgnoreCase))
                {
                    portable = url;
                }
            }
        }

        return (setup, portable);
    }

    private static IEnumerable<string> MirrorChain()
    {
        string custom = AppSettings.Current.GitHubAccelerator?.Trim() ?? string.Empty;
        if (!string.IsNullOrEmpty(custom))
        {
            yield return custom.EndsWith('/') ? custom : custom + "/";
        }

        foreach (string mirror in Mirrors)
        {
            yield return mirror;
        }
    }

    private static string ApplyMirror(string mirror, string url)
        => string.IsNullOrEmpty(mirror) ? url : mirror + url;

    private static async Task<string?> GetStringWithMirrorsAsync(string url, CancellationToken token)
    {
        foreach (string mirror in MirrorChain())
        {
            try
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
                http.DefaultRequestHeaders.UserAgent.ParseAdd("SeaMonkeys");
                http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
                return await http.GetStringAsync(ApplyMirror(mirror, url), token);
            }
            catch
            {
            }
        }

        return null;
    }

    private static async Task<string?> DownloadWithMirrorsAsync(
        string url,
        string target,
        IProgress<double>? progress,
        CancellationToken token)
    {
        foreach (string mirror in MirrorChain())
        {
            try
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(15) };
                http.DefaultRequestHeaders.UserAgent.ParseAdd("SeaMonkeys");

                using HttpResponseMessage response = await http.GetAsync(
                    ApplyMirror(mirror, url), HttpCompletionOption.ResponseHeadersRead, token);
                response.EnsureSuccessStatusCode();

                long total = response.Content.Headers.ContentLength ?? -1;
                await using Stream source = await response.Content.ReadAsStreamAsync(token);
                await using FileStream dest = File.Create(target);

                byte[] buffer = new byte[81920];
                long read = 0;
                int n;
                while ((n = await source.ReadAsync(buffer, token)) > 0)
                {
                    await dest.WriteAsync(buffer.AsMemory(0, n), token);
                    read += n;
                    if (total > 0)
                    {
                        progress?.Report((double)read / total);
                    }
                }

                if (total > 0 && read < total)
                {
                    continue;
                }

                return target;
            }
            catch
            {
            }
        }

        return null;
    }

    private static string CurrentVersion()
    {
        var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        return version is null ? "0.0.0" : $"{version.Major}.{version.Minor}.{version.Build}";
    }

    private static int CompareVersions(string a, string b)
    {
        int[] pa = ParseParts(a);
        int[] pb = ParseParts(b);
        for (int i = 0; i < 3; i++)
        {
            int cmp = pa[i].CompareTo(pb[i]);
            if (cmp != 0)
            {
                return cmp;
            }
        }

        return 0;
    }

    private static int[] ParseParts(string v)
    {
        string[] parts = v.Split('.');
        int[] result = new int[3];
        for (int i = 0; i < 3; i++)
        {
            result[i] = i < parts.Length && int.TryParse(parts[i], out int n) ? n : 0;
        }

        return result;
    }
}
