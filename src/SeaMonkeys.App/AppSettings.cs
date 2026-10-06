using System.Text.Json;

namespace SeaMonkeys.App;

public sealed class AppSettings
{
    public static AppSettings Current { get; } = Load();

    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SeaMonkeys",
        "settings.json");

    /// <summary>0 = 左侧，1 = 顶部。</summary>
    public int NavigationStyle { get; set; } = 1;

    public int BackdropStyle { get; set; }

    /// <summary>配色风格：0=无颜色，1=4色，2=8色，3=彩虹。</summary>
    public int ColorStyle { get; set; } = 2;

    public string GamePath { get; set; } = string.Empty;

    /// <summary>0=自动，1=亚服，2=欧服，3=美服，4=莱服，5=国服。</summary>
    public int ServerIndex { get; set; }

    public string ProxyBaseUrl { get; set; } = string.Empty;

    public int RequestDelayMs { get; set; } = 20;

    public int ParallelRequests { get; set; } = 12;

    /// <summary>0=加权胜率，1=账号胜率，2=场次。</summary>
    public int SortMode { get; set; }

    public bool HighlightWatchlist { get; set; } = true;

    /// <summary>是否监视 replay 目录，出现新对局时自动加载。</summary>
    public bool ObserveReplays { get; set; } = true;

    public int WindowX { get; set; } = int.MinValue;
    public int WindowY { get; set; } = int.MinValue;
    public int WindowWidth { get; set; }
    public int WindowHeight { get; set; }

    /// <summary>船名表远端更新地址。默认指向 ApeRadar 官方更新清单；
    /// 也接受直接指向 ships.json 的地址。留空则不更新。</summary>
    public string ShipCatalogUrl { get; set; } = "https://lxdev.org/aperadar/updateinfo/";

    /// <summary>GitHub 下载加速前缀。留空则直连 github.com。下载工具包时拼接在该地址之后。</summary>
    public string GitHubAccelerator { get; set; } = "https://ghfast.top/";

    /// <summary>回放渲染工具（minimap_renderer.exe）路径。留空则在用户数据目录 tools/ 下查找。</summary>
    public string RendererToolPath { get; set; } = string.Empty;

    /// <summary>渲染编码器：0=自动（GPU 优先），1=H.264，2=H.265。</summary>
    public int RenderCodec { get; set; }

    /// <summary>渲染目标体积上限（MiB），0 表示不限制（走默认码率）。</summary>
    public int RenderMaxSizeMiB { get; set; } = 60;

    /// <summary>渲染是否显示玩家名。</summary>
    public bool RenderShowPlayerNames { get; set; } = true;

    /// <summary>渲染是否显示占点。</summary>
    public bool RenderShowCapturePoints { get; set; } = true;

    /// <summary>渲染是否显示建筑标记。</summary>
    public bool RenderShowBuildings { get; set; } = true;

    /// <summary>渲染是否显示相机方向。</summary>
    public bool RenderShowCameraDirection { get; set; } = true;

    /// <summary>渲染是否显示武器/弹药类型。</summary>
    public bool RenderShowArmament { get; set; } = true;

    /// <summary>渲染是否显示击杀信息。</summary>
    public bool RenderShowKillFeed { get; set; } = true;

    /// <summary>渲染是否显示航速轨迹（蓝慢红快）。</summary>
    public bool RenderShowSpeedTrails { get; set; }

    /// <summary>渲染是否显示射程/点亮圈。</summary>
    public bool RenderShowShipConfig { get; set; }

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                string json = File.ReadAllText(FilePath);
                AppSettings? loaded = JsonSerializer.Deserialize<AppSettings>(json);
                if (loaded is not null)
                {
                    return loaded;
                }
            }
        }
        catch
        {
        }

        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
        }
    }
}
