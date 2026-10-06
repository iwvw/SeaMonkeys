using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SeaMonkeys.Core.Ships;

/// <summary>
/// 从游戏本体数据构建船名表：解析 gettext .mo 本地化，join GameParams 的数值ID/index，
/// 产出与 <c>ships.json</c> 兼容的 JSON。纯逻辑，不依赖 UI，供应用与构建脚本复用。
/// </summary>
public static class ShipCatalogBuilder
{
    private static readonly Regex ShipIndexPattern = new(@"^P[A-Z]{2,3}[A-Z]\d+$", RegexOptions.Compiled);
    private static readonly Regex MarkupPattern = new(@"<[^>]+>", RegexOptions.Compiled);
    private static readonly Regex SuffixPattern = new(@"\s*\(<[^)]*\)\s*$", RegexOptions.Compiled);

    /// <summary>解析 gettext .mo 文件，返回 msgid → msgstr。</summary>
    public static Dictionary<string, string> ParseMo(string path)
    {
        var result = new Dictionary<string, string>();
        byte[] data = File.ReadAllBytes(path);
        if (data.Length < 28)
        {
            return result;
        }

        uint magic = BitConverter.ToUInt32(data, 0);
        bool littleEndian = magic == 0x950412de;
        if (!littleEndian && magic != 0xde120495)
        {
            return result;
        }

        uint ReadUInt(int offset)
        {
            uint v = BitConverter.ToUInt32(data, offset);
            return littleEndian ? v : BinaryPrimitives.ReverseEndianness(v);
        }

        uint count = ReadUInt(8);
        uint origTable = ReadUInt(12);
        uint transTable = ReadUInt(16);

        for (uint i = 0; i < count; i++)
        {
            uint origLen = ReadUInt((int)(origTable + i * 8));
            uint origOff = ReadUInt((int)(origTable + i * 8 + 4));
            uint transLen = ReadUInt((int)(transTable + i * 8));
            uint transOff = ReadUInt((int)(transTable + i * 8 + 4));

            if (origOff + origLen > data.Length || transOff + transLen > data.Length)
            {
                continue;
            }

            string key = Encoding.UTF8.GetString(data, (int)origOff, (int)origLen);
            string value = Encoding.UTF8.GetString(data, (int)transOff, (int)transLen);
            result[key] = value;
        }

        return result;
    }

    /// <summary>用中文（必需）与英文（可选）本地化构建 ships.json 内容。</summary>
    public static string Build(
        string gameParamsPath,
        Dictionary<string, string> zh,
        Dictionary<string, string>? en,
        string version,
        string date)
    {
        var ships = new Dictionary<string, object>();

        using FileStream stream = File.OpenRead(gameParamsPath);
        using JsonDocument document = JsonDocument.Parse(stream);

        foreach (JsonProperty property in document.RootElement.EnumerateObject())
        {
            JsonElement value = property.Value;
            if (value.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            if (!value.TryGetProperty("id", out JsonElement idElement) || idElement.ValueKind != JsonValueKind.Number)
            {
                continue;
            }

            if (!value.TryGetProperty("index", out JsonElement indexElement) || indexElement.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            string index = indexElement.GetString() ?? string.Empty;
            if (!ShipIndexPattern.IsMatch(index))
            {
                continue;
            }

            if (!value.TryGetProperty("typeinfo", out JsonElement typeInfo) || typeInfo.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            string species = typeInfo.TryGetProperty("species", out JsonElement sp) ? sp.GetString() ?? string.Empty : string.Empty;
            string nation = typeInfo.TryGetProperty("nation", out JsonElement nt) ? nt.GetString() ?? string.Empty : string.Empty;
            if (string.IsNullOrEmpty(species))
            {
                continue;
            }

            string zhName = LookupName(zh, index);
            if (string.IsNullOrEmpty(zhName))
            {
                continue;
            }

            string enName = en is null ? string.Empty : LookupName(en, index);
            int tier = value.TryGetProperty("level", out JsonElement lv) && lv.ValueKind == JsonValueKind.Number
                ? lv.GetInt32()
                : 0;
            string id = idElement.GetInt64().ToString(CultureInfo.InvariantCulture);

            ships[id] = new Dictionary<string, object>
            {
                ["name_en-us"] = enName,
                ["name_zh-cn"] = zhName,
                ["nation"] = nation.ToLowerInvariant(),
                ["tier"] = tier,
                ["type"] = species,
            };
        }

        var root = new Dictionary<string, object>
        {
            ["version"] = version,
            ["date"] = date,
            ["ships"] = ships,
        };

        return JsonSerializer.Serialize(root, new JsonSerializerOptions { WriteIndented = true });
    }

    private static string LookupName(Dictionary<string, string> mo, string index)
    {
        // 优先普通键（干净），其次 _FULL（可能带 "(< 日期)" 后缀）。
        if (mo.TryGetValue($"IDS_{index}", out string? plain))
        {
            string cleaned = Clean(plain);
            if (!string.IsNullOrEmpty(cleaned))
            {
                return cleaned;
            }
        }

        if (mo.TryGetValue($"IDS_{index}_FULL", out string? full))
        {
            return Clean(full);
        }

        return string.Empty;
    }

    private static string Clean(string value)
    {
        string cleaned = MarkupPattern.Replace(value, string.Empty).Trim();
        // 去掉历史版本后缀，如 "凤 (< 23.01.2019)"。
        cleaned = SuffixPattern.Replace(cleaned, string.Empty).Trim();
        return cleaned;
    }
}
