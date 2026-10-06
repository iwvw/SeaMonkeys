using Windows.UI;

namespace SeaMonkeys.App;

/// <summary>
/// 配色风格：0=无颜色，1=4色，2=分类色（按等级 6 色），3=彩虹（连续 HSL）。
/// 色值取自 ApeRadar 的定义，「诗人」为新增档位的插值色。
/// </summary>
public static class ColorSchemes
{
    public static string[] Names { get; } = { "无颜色", "4 色", "分类色", "彩虹" };

    /// <summary>各等级对应的分类色（与印章颜色一致）。</summary>
    public static Color GradeColor(string grade) => grade switch
    {
        "神佬" => Rgb(0xA0, 0x0D, 0xC5),
        "大佬" => Rgb(0x02, 0xC9, 0xB3),
        "诗人" => Rgb(0x1F, 0xC7, 0x5A),
        "正常" => Rgb(0x44, 0xB3, 0x00),
        "路边一条" => Rgb(0xFF, 0xC7, 0x1F),
        "区" => Rgb(0xFE, 0x0E, 0x00),
        _ => Rgb(0x88, 0x88, 0x88),
    };

    public static Color? GetColor(int style, double winrate, string grade)
    {
        if (winrate < 0)
        {
            return null;
        }

        return style switch
        {
            1 => FourColor(winrate),
            2 => GradeColor(grade),
            3 => Rainbow(winrate),
            _ => null,
        };
    }

    private static Color FourColor(double wr) => wr switch
    {
        > 0.60 => Rgb(0xD0, 0x42, 0xF3),
        > 0.52 => Rgb(0x31, 0x80, 0x00),
        > 0.47 => Rgb(0xFF, 0xC7, 0x1F),
        _ => Rgb(0xFE, 0x0E, 0x00),
    };

    private static Color Rainbow(double wr)
    {
        // 饱和度略降（1.0 → 0.82），亮度略提，观感更柔和。
        const double sat = 0.82;
        const double lum = 0.52;

        if (wr > 0.65)
        {
            return Hsl2Rgb(0.8, sat, lum);
        }

        if (wr < 0.47)
        {
            return Hsl2Rgb(0, sat, lum);
        }

        return Hsl2Rgb((wr - 0.47) / 0.18 * 0.8, sat, lum);
    }

    private static Color Rgb(byte r, byte g, byte b) => Color.FromArgb(255, r, g, b);

    private static Color Hsl2Rgb(double h, double s, double l)
    {
        double v = l <= 0.5 ? l * (1.0 + s) : l + s - l * s;
        double m = l + l - v;
        double sv = v == 0 ? 0 : (v - m) / v;
        h *= 6.0;
        int sextant = (int)h;
        double fract = h - sextant;
        double vsf = v * sv * fract;
        double mid1 = m + vsf;
        double mid2 = v - vsf;

        (double r, double g, double b) = sextant switch
        {
            0 => (v, mid1, m),
            1 => (mid2, v, m),
            2 => (m, v, mid1),
            3 => (m, mid2, v),
            4 => (mid1, m, v),
            5 => (v, m, mid2),
            _ => (m, m, m),
        };

        return Color.FromArgb(255, (byte)(r * 255), (byte)(g * 255), (byte)(b * 255));
    }
}
