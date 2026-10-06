using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Windows.UI.ViewManagement;

namespace SeaMonkeys.App.Services;

/// <summary>
/// 应用级主题跟随：WinUI 3 的 Application.RequestedTheme 默认是 Light，不会自动跟随系统。
/// 这里读注册表得到系统明暗并设置根元素 RequestedTheme，同时订阅系统主题变化实时刷新。
/// </summary>
public static class ThemeManager
{
    private static FrameworkElement? root;
    private static UISettings? uiSettings;
    private static DispatcherQueue? dispatcher;

    public static event EventHandler? ThemeChanged;

    public static bool IsDark { get; private set; }

    public static void Initialize(FrameworkElement rootElement, DispatcherQueue dispatcherQueue)
    {
        root = rootElement;
        dispatcher = dispatcherQueue;

        root.ActualThemeChanged += (_, _) => ThemeChanged?.Invoke(null, EventArgs.Empty);

        Apply();

        try
        {
            uiSettings = new UISettings();
            uiSettings.ColorValuesChanged += (_, _) =>
                dispatcher?.TryEnqueue(Apply);
        }
        catch
        {
        }
    }

    public static void Apply()
    {
        if (root is null)
        {
            return;
        }

        bool dark = IsSystemDark();
        IsDark = dark;
        root.RequestedTheme = dark ? ElementTheme.Dark : ElementTheme.Light;
        ThemeChanged?.Invoke(null, EventArgs.Empty);
    }

    private static bool IsSystemDark()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int value && value == 0;
        }
        catch
        {
            return true;
        }
    }
}
