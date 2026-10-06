using System.Text.Json;

namespace SeaMonkeys.App;

public sealed class AppSettings
{
    // 注意：静态字段初始化按声明顺序执行，FilePath 必须先于 Current，
    // 否则 Load() 执行时 FilePath 仍为 null，读取设置会静默失败并回退到默认值。
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SeaMonkeys",
        "settings.json");

    public static AppSettings Current { get; } = Load();

    private const int CurrentSettingsVersion = 4;

    public const string DefaultProxyBaseUrl = "https://momomi.dmuk.org";

    public const string DefaultShipCatalogUrl = "https://momomi.dmuk.org/ships.json";

    private const string LegacyShipCatalogUrl = "https://lxdev.org/aperadar/updateinfo/";

    private const int LegacyParallelRequests = 12;
    private const int NewParallelRequests = 64;

    /// <summary>设置结构版本，用于对旧用户做一次性默认值迁移。</summary>
    public int SettingsVersion { get; set; }

    /// <summary>0 = 左侧，1 = 顶部。</summary>
    public int NavigationStyle { get; set; } = 1;

    /// <summary>背景材质：0=亚克力，1=Mica，2=纯色。</summary>
    public int BackdropStyle { get; set; } = 1;

    /// <summary>配色风格：0=无颜色，1=4色，2=8色，3=彩虹。</summary>
    public int ColorStyle { get; set; } = 2;

    public string GamePath { get; set; } = string.Empty;

    /// <summary>0=自动，1=亚服，2=欧服，3=美服，4=莱服，5=国服。</summary>
    public int ServerIndex { get; set; }

    public string ProxyBaseUrl { get; set; } = DefaultProxyBaseUrl;

    public int RequestDelayMs { get; set; } = 20;

    public int ParallelRequests { get; set; } = 64;

    /// <summary>0=加权胜率，1=账号胜率，2=场次。</summary>
    public int SortMode { get; set; }

    public bool HighlightWatchlist { get; set; } = true;

    /// <summary>是否监视 replay 目录，出现新对局时自动加载。</summary>
    public bool ObserveReplays { get; set; } = true;

    public int WindowX { get; set; } = int.MinValue;
    public int WindowY { get; set; } = int.MinValue;
    public int WindowWidth { get; set; }
    public int WindowHeight { get; set; }

    /// <summary>船名表远端更新地址。默认指向自建代理上的 ships.json；
    /// 也接受 ApeRadar 更新清单地址。留空则不更新。</summary>
    public string ShipCatalogUrl { get; set; } = DefaultShipCatalogUrl;

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
                    bool migrated = loaded.Migrate();
                    if (migrated)
                    {
                        loaded.Save();
                    }

                    return loaded;
                }
            }
        }
        catch
        {
        }

        return new AppSettings();
    }

    /// <summary>对旧版本设置做一次性迁移，返回是否发生了变更（需要回写）。</summary>
    private bool Migrate()
    {
        bool changed = false;

        if (SettingsVersion < 1)
        {
            if (string.IsNullOrWhiteSpace(ProxyBaseUrl))
            {
                ProxyBaseUrl = DefaultProxyBaseUrl;
            }

            SettingsVersion = 1;
            changed = true;
        }

        if (SettingsVersion < 2)
        {
            // 并发上限默认值从 12 提到 64；仅在用户未自定义（仍是旧默认）时升级。
            if (ParallelRequests is LegacyParallelRequests or 32)
            {
                ParallelRequests = NewParallelRequests;
            }

            SettingsVersion = 2;
            changed = true;
        }

        if (SettingsVersion < 3)
        {
            // 背景材质默认值从亚克力(0)改为 Mica(1)；仅在用户未自定义时升级。
            if (BackdropStyle == 0)
            {
                BackdropStyle = 1;
            }

            SettingsVersion = 3;
            changed = true;
        }

        if (SettingsVersion < 4)
        {
            // 船名表更新地址默认值从 ApeRadar 改为自建代理；仅在用户未自定义时升级。
            if (string.IsNullOrWhiteSpace(ShipCatalogUrl) || ShipCatalogUrl == LegacyShipCatalogUrl)
            {
                ShipCatalogUrl = DefaultShipCatalogUrl;
            }

            SettingsVersion = 4;
            changed = true;
        }

        if (SettingsVersion != CurrentSettingsVersion)
        {
            SettingsVersion = CurrentSettingsVersion;
            changed = true;
        }

        return changed;
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
