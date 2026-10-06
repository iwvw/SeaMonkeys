using System.Text.Json;

namespace SeaMonkeys.App;

public sealed class ShipCatalog
{
    private readonly Dictionary<string, ShipInfo> ships = new();

    public static ShipCatalog Current { get; } = new();

    public string Version { get; private set; } = "-";

    public string Date { get; private set; } = "-";

    /// <summary>用户目录下的可更新船名表路径（优先于程序内置表）。</summary>
    public static string UserCatalogPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SeaMonkeys",
        "ships.json");

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
                // 用户表损坏，回退内置表。
            }
        }

        Load(Path.Combine(AppContext.BaseDirectory, "Assets", "ships.json"));
    }

    public void Load(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        using FileStream stream = File.OpenRead(path);
        using JsonDocument document = JsonDocument.Parse(stream);
        JsonElement root = document.RootElement;

        Version = root.TryGetProperty("version", out JsonElement v) ? v.GetString() ?? "-" : "-";
        Date = root.TryGetProperty("date", out JsonElement d) ? d.GetString() ?? "-" : "-";

        if (!root.TryGetProperty("ships", out JsonElement shipsElement))
        {
            return;
        }

        ships.Clear();
        foreach (JsonProperty property in shipsElement.EnumerateObject())
        {
            string name = property.Value.TryGetProperty("name_zh-cn", out JsonElement zh)
                ? zh.GetString() ?? string.Empty
                : string.Empty;
            string type = property.Value.TryGetProperty("type", out JsonElement t)
                ? t.GetString() ?? string.Empty
                : string.Empty;
            int tier = property.Value.TryGetProperty("tier", out JsonElement tierElement)
                ? tierElement.GetInt32()
                : 0;

            ships[property.Name] = new ShipInfo(name, type, tier);
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
        11 => "XI",
        _ => value > 0 ? value.ToString() : string.Empty,
    };

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
