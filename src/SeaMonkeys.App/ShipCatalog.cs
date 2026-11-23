using System.Text.Json;

namespace SeaMonkeys.App;

public sealed class ShipCatalog
{
    private readonly Dictionary<string, ShipInfo> ships = new();

    public string Version { get; private set; } = "-";

    public string Date { get; private set; } = "-";

    /// <summary>用户目录下的可更新船名表路径（优先于程序内置表）。</summary>
    public static string UserCatalogPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SeaMonkeys",
        "ships.json");

    // 注意:静态字段按声明顺序初始化,UserCatalogPath 必须先于 Current,
    // 否则 Current 构造时读不到用户船名表。
    public static ShipCatalog Current { get; } = new();

    public ShipCatalog()
    {
        Reload();
    }

    /// <summary>优先加载用户目录的表，缺失或损坏时回退到程序内置表。</summary>
    public void Reload()
    {
        if (File.Exists(UserCatalogPath))
        {
            try
            {
                Load(UserCatalogPath);
                return;
            }
            catch
            {
                AtomicFile.Backup(UserCatalogPath);
                // 用户表损坏，回退内置表。
            }
        }

        try
        {
            Load(Path.Combine(AppContext.BaseDirectory, "Assets", "ships.json"));
        }
        catch
        {
        }
    }

    public void Load(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("catalog not found", path);
        }

        using FileStream stream = File.OpenRead(path);
        using JsonDocument document = JsonDocument.Parse(stream);
        JsonElement root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("ships", out JsonElement shipsElement) ||
            shipsElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("catalog missing ships object");
        }

        var loaded = new Dictionary<string, ShipInfo>();
        foreach (JsonProperty property in shipsElement.EnumerateObject())
        {
            try
            {
                JsonElement value = property.Value;
                if (value.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                string name = value.TryGetProperty("name_zh-cn", out JsonElement zh) && zh.ValueKind == JsonValueKind.String
                    ? zh.GetString() ?? string.Empty
                    : string.Empty;
                string type = value.TryGetProperty("type", out JsonElement t) && t.ValueKind == JsonValueKind.String
                    ? t.GetString() ?? string.Empty
                    : string.Empty;
                int tier = value.TryGetProperty("tier", out JsonElement tierElement) && tierElement.ValueKind == JsonValueKind.Number
                    ? tierElement.GetInt32()
                    : 0;

                if (name.Length == 0)
                {
                    continue;
                }

                loaded[property.Name] = new ShipInfo(name, type, tier);
            }
            catch
            {
            }
        }

        if (loaded.Count == 0)
        {
            throw new InvalidDataException("catalog has no entries");
        }

        Version = root.TryGetProperty("version", out JsonElement v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "-" : "-";
        Date = root.TryGetProperty("date", out JsonElement d) && d.ValueKind == JsonValueKind.String ? d.GetString() ?? "-" : "-";

        ships.Clear();
        foreach (KeyValuePair<string, ShipInfo> kv in loaded)
        {
            ships[kv.Key] = kv.Value;
        }
    }

    public string GetName(string shipId)
    {
        if (ships.TryGetValue(shipId, out ShipInfo info) && !string.IsNullOrEmpty(info.Name))
        {
            return info.Name;
        }

        return shipId;
    }

    /// <summary>船名表里是否有该舰船（且带本地化名称）。</summary>
    public bool IsKnown(string shipId)
        => ships.TryGetValue(shipId, out ShipInfo info) && !string.IsNullOrEmpty(info.Name);

    public string GetTypeLabel(string shipId)
    {
        if (!ships.TryGetValue(shipId, out ShipInfo info))
        {
            return string.Empty;
        }

        string type = TypeName(info.Type);
        return info.Tier > 0 ? $"{type} {info.Tier}" : type;
    }

    public string GetTypeKey(string shipId)
        => ships.TryGetValue(shipId, out ShipInfo info) ? info.Type : string.Empty;

    public int GetTier(string shipId)
        => ships.TryGetValue(shipId, out ShipInfo info) ? info.Tier : 0;

    public int GetTypeOrder(string shipId)
        => ships.TryGetValue(shipId, out ShipInfo info) ? TypeOrder(info.Type) : 5;

    private static int TypeOrder(string type) => type switch
    {
        "AirCarrier" => 0,
        "Battleship" => 1,
        "Cruiser" => 2,
        "Destroyer" => 3,
        "Submarine" => 4,
        _ => 5,
    };

    public static string Roman(int value) => value switch
    {
        1 => "I",
        2 => "II",
        3 => "III",
        4 => "IV",
        5 => "V",
        6 => "VI",
        7 => "VII",
        8 => "VIII",
        9 => "IX",
        10 => "X",
        // 11 级（超级舰只）在游戏内用星标标识，不用罗马数字。
        11 => "★",
        _ => value > 0 ? value.ToString() : string.Empty,
    };

    /// <summary>是否为 11 级（超级舰只）。</summary>
    public static bool IsSuperShip(int tier) => tier == 11;

    private static string TypeName(string type) => type switch
    {
        "Destroyer" => "驱逐",
        "Cruiser" => "巡洋",
        "Battleship" => "战列",
        "AirCarrier" => "航母",
        "Submarine" => "潜艇",
        _ => type,
    };

    private readonly record struct ShipInfo(string Name, string Type, int Tier);
}
