using System.IO.Compression;
using System.Text.Json;

namespace SeaMonkeys.App;

/// <summary>
/// 船名表更新。支持两种上游格式：
/// 1. 直接指向 ships.json（根含 ships 字段）；
/// 2. ApeRadar 更新清单（含 shiplist_latest_url，指向打包了 ships.json 的 zip）。
/// 下载并校验后写入用户目录并热重载。
/// </summary>
/// <summary>船名表更新结果：成功与否及可读说明。</summary>
public readonly record struct ShipCatalogUpdateResult(bool Success, string Message)
{
    public static ShipCatalogUpdateResult Ok(string message) => new(true, message);
    public static ShipCatalogUpdateResult Fail(string message) => new(false, message);
}

public static class ShipCatalogUpdater
{
    public static async Task<ShipCatalogUpdateResult> TryUpdateAsync(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return ShipCatalogUpdateResult.Fail("未填写更新地址");
        }

        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("SeaMonkeys");

            string content;
            try
            {
                content = await http.GetStringAsync(url);
            }
            catch (Exception ex)
            {
                return ShipCatalogUpdateResult.Fail($"下载清单失败：{ex.Message}");
            }

            string json;
            if (IsDirectCatalog(content))
            {
                json = content;
            }
            else
            {
                string? zipUrl = ReadManifestZipUrl(content);
                if (string.IsNullOrWhiteSpace(zipUrl))
                {
                    return ShipCatalogUpdateResult.Fail("上游未提供船名表地址（清单缺少 shiplist_latest_url）");
                }

                try
                {
                    json = await DownloadCatalogFromZipAsync(http, zipUrl);
                }
                catch (Exception ex)
                {
                    return ShipCatalogUpdateResult.Fail($"下载船名表失败：{ex.Message}");
                }

                if (string.IsNullOrEmpty(json))
                {
                    return ShipCatalogUpdateResult.Fail("压缩包中未找到 ships.json");
                }
            }

            if (!IsDirectCatalog(json))
            {
                return ShipCatalogUpdateResult.Fail("下载内容不是有效的船名表");
            }

            string before = $"{ShipCatalog.Current.Version} ({ShipCatalog.Current.Date})";
            Directory.CreateDirectory(Path.GetDirectoryName(ShipCatalog.UserCatalogPath)!);
            await File.WriteAllTextAsync(ShipCatalog.UserCatalogPath, json);
            ShipCatalog.Current.Reload();

            string after = $"{ShipCatalog.Current.Version} ({ShipCatalog.Current.Date})";
            return ShipCatalogUpdateResult.Ok(
                before == after ? $"已是最新版本 {after}" : $"已更新：{before} → {after}");
        }
        catch (Exception ex)
        {
            return ShipCatalogUpdateResult.Fail($"更新失败：{ex.Message}");
        }
    }

    private static bool IsDirectCatalog(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.ValueKind == JsonValueKind.Object
                && doc.RootElement.TryGetProperty("ships", out _);
        }
        catch
        {
            return false;
        }
    }

    private static string? ReadManifestZipUrl(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind == JsonValueKind.Object
                && doc.RootElement.TryGetProperty("shiplist_latest_url", out JsonElement url))
            {
                return url.GetString();
            }
        }
        catch
        {
        }

        return null;
    }

    private static async Task<string> DownloadCatalogFromZipAsync(HttpClient http, string zipUrl)
    {
        byte[] bytes = await http.GetByteArrayAsync(zipUrl);
        using var stream = new MemoryStream(bytes);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

        ZipArchiveEntry? entry = archive.Entries.FirstOrDefault(
            e => e.Name.Equals("ships.json", StringComparison.OrdinalIgnoreCase))
            ?? archive.Entries.FirstOrDefault(
                e => e.Name.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                    && e.Name.Contains("ships", StringComparison.OrdinalIgnoreCase));

        if (entry is null)
        {
            return string.Empty;
        }

        using var reader = new StreamReader(entry.Open());
        return await reader.ReadToEndAsync();
    }
}
