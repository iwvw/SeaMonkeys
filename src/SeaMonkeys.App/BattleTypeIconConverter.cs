using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace SeaMonkeys.App;

/// <summary>战斗模式（gameType / matchGroup）→ 官方模式徽章（Assets/battles/*.png）。</summary>
public sealed class BattleTypeIconConverter : IValueConverter
{
    private static readonly Dictionary<string, string> Files = new(StringComparer.OrdinalIgnoreCase)
    {
        ["RandomBattle"] = "battle_type_standart.png",
        ["RankedBattle"] = "battle_type_rank.png",
        ["ClanBattle"] = "battle_type_clan.png",
        ["CooperativeBattle"] = "battle_type_pve.png",
        ["CoopBattle"] = "battle_type_pve.png",
        ["ScenarioBattle"] = "battle_type_scenario.png",
        ["OperationBattle"] = "battle_type_scenario.png",
        ["TrainingBattle"] = "battle_type_training.png",
    };

    private static readonly Dictionary<string, ImageSource> Cache = new();

    public object? Convert(object value, Type targetType, object parameter, string language)
    {
        string key = value as string ?? string.Empty;
        if (!Files.TryGetValue(key, out string? file))
        {
            file = "battle_type_standart.png";
        }

        if (Cache.TryGetValue(file, out ImageSource? cached))
        {
            return cached;
        }

        var image = new BitmapImage(new Uri($"ms-appx:///Assets/battles/{file}"));
        Cache[file] = image;
        return image;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}
