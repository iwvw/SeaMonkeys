using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SeaMonkeys.Core.Servers;

namespace SeaMonkeys.App.Pages;

public sealed partial class SettingsPage : Page
{
    private bool syncing;

    public SettingsPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        syncing = true;
        var s = AppSettings.Current;
        GamePathBox.Text = s.GamePath;
        ServerBox.SelectedIndex = Math.Clamp(s.ServerIndex, 0, 5);
        ProxyBox.Text = s.ProxyBaseUrl;
        DelayBox.Text = s.RequestDelayMs.ToString();
        ParallelBox.Text = s.ParallelRequests.ToString();
        NavStyleBox.SelectedIndex = s.NavigationStyle == 1 ? 1 : 0;
        BackdropStyleBox.SelectedIndex = Math.Clamp(s.BackdropStyle, 0, 2);
        ColorStyleBox.SelectedIndex = Math.Clamp(s.ColorStyle, 0, 3);
        SortBox.SelectedIndex = Math.Clamp(s.SortMode, 0, 2);
        ObserveSwitch.IsOn = s.ObserveReplays;
        HighlightSwitch.IsOn = s.HighlightWatchlist;
        CatalogUrlBox.Text = s.ShipCatalogUrl;
        CatalogVersionText.Text = $"{ShipCatalog.Current.Version} ({ShipCatalog.Current.Date})";
        AcceleratorBox.Text = s.GitHubAccelerator;
        RendererPathBox.Text = s.RendererToolPath;
        AboutVersionText.Text = $"v{GetVersion()}";
        syncing = false;
    }

    private static string GetVersion()
    {
        var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        return version is null ? "0.0.0" : $"{version.Major}.{version.Minor}.{version.Build}";
    }

    private void CatalogUrl_Changed(object sender, TextChangedEventArgs e)
    {
        if (syncing) return;
        AppSettings.Current.ShipCatalogUrl = CatalogUrlBox.Text;
        AppSettings.Current.Save();
    }

    private async void UpdateCatalog_Click(object sender, RoutedEventArgs e)
    {
        string url = CatalogUrlBox.Text;
        if (string.IsNullOrWhiteSpace(url))
        {
            CatalogStatusText.Text = "请先填写船名表更新地址";
            return;
        }

        CatalogUpdateButton.IsEnabled = false;
        CatalogStatusText.Text = "正在更新…";

        ShipCatalogUpdateResult result = await ShipCatalogUpdater.TryUpdateAsync(url);

        CatalogUpdateButton.IsEnabled = true;
        ShowCatalogResult(result);
    }

    private async void GenerateCatalog_Click(object sender, RoutedEventArgs e)
    {
        string gamePath = GamePathBox.Text;
        if (string.IsNullOrWhiteSpace(gamePath) || !Directory.Exists(gamePath))
        {
            string? detected = GameLocator.FindGamePath();
            if (detected is null)
            {
                CatalogStatusText.Text = "未找到游戏目录，请先在上方设置游戏路径";
                CatalogStatusText.Foreground = CriticalBrush();
                return;
            }

            gamePath = detected;
            GamePathBox.Text = detected;
            AppSettings.Current.GamePath = detected;
            AppSettings.Current.Save();
        }

        CatalogGenerateButton.IsEnabled = false;
        CatalogStatusText.Text = "正在从游戏生成…";

        var log = new Progress<string>(t => CatalogStatusText.Text = t);
        ShipCatalogUpdateResult result = await ShipDataExtractor.GenerateAsync(gamePath, log);

        CatalogGenerateButton.IsEnabled = true;
        ShowCatalogResult(result);
    }

    private void ShowCatalogResult(ShipCatalogUpdateResult result)
    {
        CatalogStatusText.Text = result.Message;
        CatalogStatusText.Foreground = result.Success ? SuccessBrush() : CriticalBrush();
        CatalogVersionText.Text = $"{ShipCatalog.Current.Version} ({ShipCatalog.Current.Date})";

        if (result.Success && App.Current.MainWindow is MainWindow window)
        {
            window.RefreshBattleView();
        }
    }

    private static Microsoft.UI.Xaml.Media.Brush SuccessBrush()
        => (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SystemFillColorSuccessBrush"];

    private static Microsoft.UI.Xaml.Media.Brush CriticalBrush()
        => (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SystemFillColorCriticalBrush"];

    private void GamePath_Changed(object sender, TextChangedEventArgs e)
    {
        if (syncing) return;
        AppSettings.Current.GamePath = GamePathBox.Text;
        AppSettings.Current.Save();
        BattleState.Current.RestartObservation();
    }

    private void DetectGamePath_Click(object sender, RoutedEventArgs e)
    {
        string? path = GameLocator.FindGamePath();
        if (path is not null)
        {
            GamePathBox.Text = path;
            AppSettings.Current.GamePath = path;
            AppSettings.Current.Save();
            BattleState.Current.RestartObservation();
        }
    }

    private void Observe_Toggled(object sender, RoutedEventArgs e)
    {
        if (syncing) return;
        AppSettings.Current.ObserveReplays = ObserveSwitch.IsOn;
        AppSettings.Current.Save();
        BattleState.Current.RestartObservation();
    }

    private void Server_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (syncing) return;
        AppSettings.Current.ServerIndex = ServerBox.SelectedIndex;
        AppSettings.Current.Save();
    }

    private void Proxy_Changed(object sender, TextChangedEventArgs e)
    {
        if (syncing) return;
        AppSettings.Current.ProxyBaseUrl = ProxyBox.Text;
        AppSettings.Current.Save();
    }

    private void Delay_Changed(object sender, TextChangedEventArgs e)
    {
        if (syncing) return;
        if (int.TryParse(DelayBox.Text, out int value))
        {
            AppSettings.Current.RequestDelayMs = Math.Max(0, value);
            AppSettings.Current.Save();
        }
    }

    private void Parallel_Changed(object sender, TextChangedEventArgs e)
    {
        if (syncing) return;
        if (int.TryParse(ParallelBox.Text, out int value))
        {
            AppSettings.Current.ParallelRequests = Math.Max(1, value);
            AppSettings.Current.Save();
        }
    }

    private void NavStyle_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (syncing) return;
        int style = NavStyleBox.SelectedIndex == 1 ? 1 : 0;
        AppSettings.Current.NavigationStyle = style;
        AppSettings.Current.Save();

        if (App.Current.MainWindow is MainWindow window)
        {
            window.ApplyNavigationStyle(style);
        }
    }

    private void BackdropStyle_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (syncing) return;
        AppSettings.Current.BackdropStyle = BackdropStyleBox.SelectedIndex;
        AppSettings.Current.Save();

        if (App.Current.MainWindow is MainWindow window)
        {
            window.ApplyBackdrop(AppSettings.Current.BackdropStyle);
        }
    }

    private void ColorStyle_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (syncing) return;
        AppSettings.Current.ColorStyle = ColorStyleBox.SelectedIndex;
        AppSettings.Current.Save();

        if (App.Current.MainWindow is MainWindow window)
        {
            window.RefreshBattleView();
        }
    }

    private void Sort_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (syncing) return;
        AppSettings.Current.SortMode = SortBox.SelectedIndex;
        AppSettings.Current.Save();
    }

    private void Highlight_Toggled(object sender, RoutedEventArgs e)
    {
        if (syncing) return;
        AppSettings.Current.HighlightWatchlist = HighlightSwitch.IsOn;
        AppSettings.Current.Save();
    }

    private void Accelerator_Changed(object sender, TextChangedEventArgs e)
    {
        if (syncing) return;
        AppSettings.Current.GitHubAccelerator = AcceleratorBox.Text;
        AppSettings.Current.Save();
    }

    private void RendererPath_Changed(object sender, TextChangedEventArgs e)
    {
        if (syncing) return;
        AppSettings.Current.RendererToolPath = RendererPathBox.Text;
        AppSettings.Current.Save();
    }

    private async void PickRenderer_Click(object sender, RoutedEventArgs e)
    {
        var picker = new Windows.Storage.Pickers.FileOpenPicker();
        picker.FileTypeFilter.Add(".exe");
        picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.ComputerFolder;

        IntPtr hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.Current.MainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

        Windows.Storage.StorageFile? file = await picker.PickSingleFileAsync();
        if (file is null) return;

        RendererPathBox.Text = file.Path;
        AppSettings.Current.RendererToolPath = file.Path;
        AppSettings.Current.Save();
    }
}
