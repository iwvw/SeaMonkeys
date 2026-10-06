using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using SeaMonkeys.App.Services;

namespace SeaMonkeys.App;

/// <summary>舰种 key（Destroyer 等）→ 官方舰种图标（Assets/ships/*.png），浅色主题用深色描边变体。</summary>
public sealed class ShipTypeIconConverter : IValueConverter
{
    private static readonly Dictionary<string, string> Files = new()
    {
        ["Destroyer"] = "destroyer.png",
        ["Cruiser"] = "cruiser.png",
        ["Battleship"] = "battleship.png",
        ["AirCarrier"] = "carrier.png",
        ["Submarine"] = "submarine.png",
    };

    private static readonly Dictionary<string, ImageSource> Cache = new();

    /// <summary>供代码侧复用的共享实例，避免每次转换都新建转换器。</summary>
    public static ShipTypeIconConverter Shared { get; } = new();

    public static void Invalidate() => Cache.Clear();

    public object? Convert(object value, Type targetType, object parameter, string language)
    {
        string key = value as string ?? string.Empty;
        if (!Files.TryGetValue(key, out string? file))
        {
            return null;
        }

        bool dark = ThemeManager.IsDark;
        string variant = dark ? file : Path.GetFileNameWithoutExtension(file) + "-dark.png";
        string cacheKey = variant;

        if (Cache.TryGetValue(cacheKey, out ImageSource? cached))
        {
            return cached;
        }

        var image = new BitmapImage(new Uri($"ms-appx:///Assets/ships/{variant}"));
        Cache[cacheKey] = image;
        return image;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}
