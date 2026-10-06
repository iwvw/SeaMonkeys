using Microsoft.UI.Xaml.Data;

namespace SeaMonkeys.App;

/// <summary>布尔取反，用于"忙碌时禁用按钮"。</summary>
public sealed class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => value is bool b ? !b : true;

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => value is bool b ? !b : true;
}
