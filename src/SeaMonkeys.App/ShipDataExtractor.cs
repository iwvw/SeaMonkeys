using System.Diagnostics;
using System.Globalization;
using SeaMonkeys.Core.Ships;

namespace SeaMonkeys.App;

/// <summary>
/// 从本地游戏本体生成船名表：用 wowsunpack 导出 GameParams，配合游戏自带的多语言
/// global.mo 资源，得到「数值ID → 中文名/英文名/等级/舰种/国家」的 ships.json。
/// 完全离线，且为官方译名、覆盖全。
/// </summary>
public static class ShipDataExtractor
{
    /// <summary>从本地游戏生成船名表并写入用户目录、热重载。返回结果说明。</summary>
    public static async Task<ShipCatalogUpdateResult> GenerateAsync(
        string gamePath,
        IProgress<string>? log = null,
        CancellationToken token = default)
    {
        if (string.IsNullOrWhiteSpace(gamePath) || !Directory.Exists(gamePath))
        {
            return ShipCatalogUpdateResult.Fail("未找到游戏目录，请先在设置里填写游戏路径");
        }

        try
        {
            log?.Report("准备解包工具…");
            string? unpack = await ToolkitTools.EnsureAsync(ToolkitTools.WowsUnpack, log, token);
            if (unpack is null)
            {
                return ShipCatalogUpdateResult.Fail("解包工具下载失败");
            }

            string tempDir = Path.Combine(Path.GetTempPath(), "SeaMonkeys", "shipgen");
            Directory.CreateDirectory(tempDir);
            string gameParamsPath = Path.Combine(tempDir, "GameParams.json");

            log?.Report("导出 GameParams（约 20 秒）…");
            if (!await RunUnpackAsync(unpack, gamePath, gameParamsPath, token))
            {
                return ShipCatalogUpdateResult.Fail("导出 GameParams 失败");
            }

            log?.Report("读取游戏本地化…");
            string? primaryMo = FindGlobalMo(gamePath, "zh_sg");
            string? fallbackMo = FindGlobalMo(gamePath, "zh");
            string? enMo = FindGlobalMo(gamePath, "en");
            if (primaryMo is null && fallbackMo is null)
            {
                return ShipCatalogUpdateResult.Fail("未找到游戏中文语言资源（global.mo）");
            }

            // zh_sg 为正常简中船名（zh 含反和谐代号），故 zh_sg 优先、zh 兜底。
            Dictionary<string, string> zh = MergeZh(primaryMo, fallbackMo);
            Dictionary<string, string>? en = enMo is null ? null : ShipCatalogBuilder.ParseMo(enMo);

            log?.Report("生成船名表…");
            string version = ReadGameVersion(gamePath);
            string json = ShipCatalogBuilder.Build(
                gameParamsPath, zh, en, version,
                DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture));

            int count = CountShips(json);
            if (count == 0)
            {
                return ShipCatalogUpdateResult.Fail("未能从游戏数据中解析出舰船");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(ShipCatalog.UserCatalogPath)!);
            await File.WriteAllTextAsync(ShipCatalog.UserCatalogPath, json, token);
            ShipCatalog.Current.Reload();

            try
            {
                File.Delete(gameParamsPath);
            }
            catch
            {
            }

            return ShipCatalogUpdateResult.Ok($"已从本地游戏生成 {count} 条舰船（{version}）");
        }
        catch (OperationCanceledException)
        {
            return ShipCatalogUpdateResult.Fail("已取消");
        }
        catch (Exception ex)
        {
            return ShipCatalogUpdateResult.Fail($"生成失败：{ex.Message}");
        }
    }

    /// <summary>合并主/兜底本地化：主语言优先，缺项用兜底补齐。</summary>
    private static Dictionary<string, string> MergeZh(string? primary, string? fallback)
    {
        var merged = new Dictionary<string, string>();

        if (fallback is not null)
        {
            foreach (var pair in ShipCatalogBuilder.ParseMo(fallback))
            {
                merged[pair.Key] = pair.Value;
            }
        }

        if (primary is not null)
        {
            foreach (var pair in ShipCatalogBuilder.ParseMo(primary))
            {
                merged[pair.Key] = pair.Value;
            }
        }

        return merged;
    }

    private static int CountShips(string json)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty("ships", out System.Text.Json.JsonElement ships)
                ? ships.EnumerateObject().Count()
                : 0;
        }
        catch
        {
            return 0;
        }
    }

    private static async Task<bool> RunUnpackAsync(string exe, string gamePath, string outPath, CancellationToken token)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = exe,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        startInfo.ArgumentList.Add("-g");
        startInfo.ArgumentList.Add(gamePath);
        startInfo.ArgumentList.Add("game-params");
        startInfo.ArgumentList.Add(outPath);

        using var process = new Process { StartInfo = startInfo };
        process.Start();
        using var reg = token.Register(() =>
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
        return process.ExitCode == 0 && File.Exists(outPath);
    }

    /// <summary>在 &lt;game&gt;\bin\&lt;build&gt;\res\texts\&lt;lang&gt;\LC_MESSAGES\global.mo 中查找语言文件。</summary>
    public static string? FindGlobalMo(string gamePath, params string[] languages)
    {
        string binDir = Path.Combine(gamePath, "bin");
        if (!Directory.Exists(binDir))
        {
            return null;
        }

        IEnumerable<string> builds = Directory.GetDirectories(binDir)
            .OrderByDescending(d => Path.GetFileName(d).Length)
            .ThenByDescending(d => Path.GetFileName(d));

        foreach (string build in builds)
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

    /// <summary>用游戏最新构建号作为版本标识。</summary>
    private static string ReadGameVersion(string gamePath)
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
}
