using System.Diagnostics;

namespace SeaMonkeys.App;

/// <summary>
/// 渲染工具只读取游戏安装里的 en 本地化,舰船名因此固定为英文。
/// 这里按回放版本构造一个影子游戏目录:用 junction 复用真实的包与索引,
/// 但把 res/texts/en 指向 zh_sg,从而输出中文舰船名。
/// </summary>
public static class GameDataShadow
{
    private static readonly string Root = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SeaMonkeys",
        "render-game");

    private const string MarkerFile = "source.txt";

    /// <summary>返回该回放可用的中文影子目录;版本不匹配或缺少中文本地化时返回 null。</summary>
    public static string? EnsureChinese(string gameDir, string? clientVersion)
    {
        try
        {
            string? build = ParseBuild(clientVersion);
            if (build is null || string.IsNullOrWhiteSpace(gameDir) || !Directory.Exists(gameDir))
            {
                return null;
            }

            string sourceBuild = Path.Combine(gameDir, "bin", build);
            string sourceIdx = Path.Combine(sourceBuild, "idx");
            string sourceZh = Path.Combine(sourceBuild, "res", "texts", "zh_sg");
            string sourcePackages = Path.Combine(gameDir, "res_packages");
            if (!Directory.Exists(sourceIdx)
                || !File.Exists(Path.Combine(sourceZh, "LC_MESSAGES", "global.mo"))
                || !Directory.Exists(sourcePackages))
            {
                return null;
            }

            string shadow = Path.Combine(Root, build);
            string marker = Path.Combine(shadow, MarkerFile);
            string stamp = $"{gameDir}\n{build}";
            if (Directory.Exists(shadow)
                && File.Exists(marker)
                && File.ReadAllText(marker) == stamp
                && Directory.Exists(Path.Combine(shadow, "bin", build, "idx"))
                && Directory.Exists(Path.Combine(shadow, "bin", build, "res", "texts", "en"))
                && Directory.Exists(Path.Combine(shadow, "res_packages")))
            {
                return shadow;
            }

            if (Directory.Exists(shadow))
            {
                RemoveShadow(shadow, build);
            }

            Directory.CreateDirectory(Path.Combine(shadow, "bin", build, "res", "texts"));
            CreateJunction(Path.Combine(shadow, "res_packages"), sourcePackages);
            CreateJunction(Path.Combine(shadow, "bin", build, "idx"), sourceIdx);
            CreateJunction(Path.Combine(shadow, "bin", build, "res", "texts", "en"), sourceZh);
            File.WriteAllText(marker, stamp);
            return shadow;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 先逐个删除 junction 链接(不会跟随到真实安装),再清理剩余空目录,
    /// 避免对含 junction 的目录做递归删除时误伤游戏本体。
    /// </summary>
    private static void RemoveShadow(string shadow, string build)
    {
        foreach (string link in new[]
        {
            Path.Combine(shadow, "bin", build, "res", "texts", "en"),
            Path.Combine(shadow, "bin", build, "idx"),
            Path.Combine(shadow, "res_packages"),
        })
        {
            try
            {
                if (Directory.Exists(link))
                {
                    Directory.Delete(link);
                }
            }
            catch
            {
            }
        }

        try
        {
            Directory.Delete(shadow, recursive: true);
        }
        catch
        {
        }
    }

    private static string? ParseBuild(string? clientVersion)
    {
        if (string.IsNullOrWhiteSpace(clientVersion))
        {
            return null;
        }

        string[] parts = clientVersion.Split(
            ',',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length >= 4 && uint.TryParse(parts[^1], out _) ? parts[^1] : null;
    }

    private static void CreateJunction(string link, string target)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(link)!);
        if (Directory.Exists(link))
        {
            return;
        }

        var startInfo = new ProcessStartInfo("cmd.exe", $"/c mklink /J \"{link}\" \"{target}\"")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        using var process = Process.Start(startInfo);
        process?.WaitForExit();
    }
}