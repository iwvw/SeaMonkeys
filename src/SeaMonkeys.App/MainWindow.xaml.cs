using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using SeaMonkeys.App.Pages;

namespace SeaMonkeys.App;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Title = "SeaMonkeys";
        AppVersionText.Text = $"v{GetVersion()}";

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico"));
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.PreferredMinimumWidth = 1280;
            presenter.PreferredMinimumHeight = 820;
        }

        RestoreWindowPlacement();
        ApplyBackdrop();
        ApplyNavigationStyle(AppSettings.Current.NavigationStyle);

        RootGrid.DataContext = State;
        State.Changed += (_, _) => BattleSummaryText.Text = State.Summary;
        State.InitializeDispatcher(DispatcherQueue);
        NotificationService.Attach(RootGrid);

        Services.ThemeManager.Initialize((FrameworkElement)Content, DispatcherQueue);
        Services.ThemeManager.ThemeChanged += (_, _) => ShipTypeIconConverter.Invalidate();
        NavFrame.Navigate(typeof(BattlePage));

        // 拖拽加载：把 replay 文件拖进窗口即导入。
        RootGrid.AllowDrop = true;
        RootGrid.DragOver += OnRootDragOver;
        RootGrid.Drop += OnRootDrop;

        Closed += OnWindowClosed;
        AppWindow.Changed += (_, _) =>
        {
            if (AppWindow.Presenter is OverlappedPresenter p && p.State == OverlappedPresenterState.Minimized)
            {
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Optimized, blocking: false);
                BattleState.TrimWorkingSetPublic();
            }
        };

        _ = UpdateShipCatalogAsync();
    }

    private async Task UpdateShipCatalogAsync()
    {
        string url = AppSettings.Current.ShipCatalogUrl;
        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        string before = ShipCatalog.Current.Date;
        ShipCatalogUpdateResult result = await ShipCatalogUpdater.TryUpdateAsync(url);
        if (result.Success && ShipCatalog.Current.Date != before)
        {
            RefreshBattleView();
        }
    }

    private void OnRootDragOver(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.StorageItems))
        {
            e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy;
        }
    }

    private async void OnRootDrop(object sender, DragEventArgs e)
    {
        if (!e.DataView.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.StorageItems))
        {
            return;
        }

        var items = await e.DataView.GetStorageItemsAsync();
        var file = items.OfType<Windows.Storage.StorageFile>()
            .FirstOrDefault(f => f.Name.EndsWith(".wowsreplay", StringComparison.OrdinalIgnoreCase)
                || f.Name.EndsWith(".json", StringComparison.OrdinalIgnoreCase));
        if (file is null)
        {
            NotificationService.Warn("请拖入 .wowsreplay 回放文件");
            return;
        }

        if (NavFrame.Content is not BattlePage)
        {
            NavFrame.Navigate(typeof(BattlePage));
        }

        await BattleState.Current.LoadFileAsync(file.Path);
    }

    private void RestoreWindowPlacement()
    {
        var s = AppSettings.Current;
        if (s.WindowWidth > 0 && s.WindowHeight > 0 && s.WindowX != int.MinValue)
        {
            AppWindow.MoveAndResize(new Windows.Graphics.RectInt32(
                s.WindowX, s.WindowY, s.WindowWidth, s.WindowHeight));
        }
        else
        {
            CenterWindow(1520, 1000);
        }
    }

    private void SaveWindowPlacement()
    {
        try
        {
            var s = AppSettings.Current;
            var pos = AppWindow.Position;
            var size = AppWindow.Size;
            s.WindowX = pos.X;
            s.WindowY = pos.Y;
            s.WindowWidth = size.Width;
            s.WindowHeight = size.Height;
            s.Save();
        }
        catch
        {
        }
    }

    private void OnWindowClosed(object sender, WindowEventArgs args) => SaveWindowPlacement();

    private static string GetVersion()
    {
        var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        return version is null ? "0.1.0" : $"{version.Major}.{version.Minor}.{version.Build}";
    }

    public void ApplyNavigationStyle(int style)    {
        NavView.PaneDisplayMode = style == 1
            ? NavigationViewPaneDisplayMode.Top
            : NavigationViewPaneDisplayMode.Left;
    }

    public void RefreshBattleView()
    {
        if (NavFrame.Content is BattlePage battle)
        {
            battle.RebuildCards();
        }
    }

    private void CenterWindow(int width, int height)
    {
        DisplayArea display = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary);
        Windows.Graphics.RectInt32 work = display.WorkArea;

        int w = Math.Min(width, work.Width);
        int h = Math.Min(height, work.Height);
        int x = work.X + (work.Width - w) / 2;
        int y = work.Y + (work.Height - h) / 2;

        AppWindow.MoveAndResize(new Windows.Graphics.RectInt32(x, y, w, h));
    }

    public void ApplyBackdrop()
    {
        ApplyBackdrop(AppSettings.Current.BackdropStyle);
    }

    /// <summary>背景材质：0=亚克力，1=Mica，2=纯色。切换后立即生效。</summary>
    public void ApplyBackdrop(AppSettings appSettings)
    {
        ApplyBackdrop(appSettings.BackdropStyle);
    }

    public void ApplyBackdrop(int style)
    {
        try
        {
            SystemBackdrop? next = style switch
            {
                1 when MicaController.IsSupported() => new MicaBackdrop { Kind = MicaKind.Base },
                2 => null,
                _ => DesktopAcrylicController.IsSupported()
                    ? new Controls.AlwaysActiveAcrylicBackdrop()
                    : (MicaController.IsSupported() ? new MicaBackdrop { Kind = MicaKind.Base } : null),
            };

            var previous = SystemBackdrop;
            SystemBackdrop = next;
            if (!ReferenceEquals(previous, next) && previous is IDisposable disposable)
            {
                disposable.Dispose();
            }

            // 纯色：给根容器一个不透明背景。
            RootGrid.Background = style == 2
                ? (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["ApplicationPageBackgroundThemeBrush"]
                : null;
        }
        catch
        {
        }
    }

    public BattleState State => BattleState.Current;

    public void SetStatus(string text, bool busy)
    {
        // 顶栏改为进度条展示，无需状态文本。
    }

    private async void Reload_Click(object sender, RoutedEventArgs e)
    {
        // 确保回到战场页再加载。
        if (NavFrame.Content is not BattlePage)
        {
            NavFrame.Navigate(typeof(BattlePage));
        }

        await BattleState.Current.LoadLatestAsync();
    }

    private async void OpenReplay_Click(object sender, RoutedEventArgs e)
    {
        var picker = new Windows.Storage.Pickers.FileOpenPicker();
        picker.FileTypeFilter.Add(".wowsreplay");
        picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.ComputerFolder;

        IntPtr hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

        Windows.Storage.StorageFile? file = await picker.PickSingleFileAsync();
        if (file is null)
        {
            return;
        }

        // 导入到本程序：走自己的解析链路加载该回放并展示。
        if (NavFrame.Content is not BattlePage)
        {
            NavFrame.Navigate(typeof(BattlePage));
        }

        await BattleState.Current.LoadFileAsync(file.Path);
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.IsSettingsSelected)
        {
            NavFrame.Navigate(typeof(SettingsPage));
            return;
        }

        if (args.SelectedItem is not NavigationViewItem item)
        {
            return;
        }

        switch (item.Tag)
        {
            case "battle": NavFrame.Navigate(typeof(BattlePage)); break;
            case "history": NavFrame.Navigate(typeof(HistoryPage)); break;
            case "watchlist": NavFrame.Navigate(typeof(WatchlistPage)); break;
            case "render": NavFrame.Navigate(typeof(RenderPage)); break;
        }
    }
}
