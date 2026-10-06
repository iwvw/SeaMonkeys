using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;

namespace SeaMonkeys.App.Pages;

public sealed partial class BattlePage : Page
{
    private const double BaseRowHeight = 72.0;
    private bool loaded;
    private int lastDataVersion = -1;
    private double lastScale = -1;
    private int lastFillVersion = -1;
    private Microsoft.UI.Dispatching.DispatcherQueueTimer? resizeTimer;
    private readonly List<PlayerCard> allyPool = new();
    private readonly List<PlayerCard> enemyPool = new();

    public BattlePage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;

        // 尺寸变化防抖：拖动窗口时只重建一次，避免逐帧重建整棵树。
        resizeTimer = DispatcherQueue.CreateTimer();
        resizeTimer.Interval = TimeSpan.FromMilliseconds(90);
        resizeTimer.IsRepeating = false;
        resizeTimer.Tick += (_, _) => RebuildAll();

        AlliesGrid.SizeChanged += (_, _) => ScheduleRebuild();
        EnemiesGrid.SizeChanged += (_, _) => ScheduleRebuild();
        Services.ThemeManager.ThemeChanged += OnThemeChanged;
    }

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        PlayerCard.InvalidateBrushes();
        RebuildAll(force: true);
    }

    public BattleState State => BattleState.Current;

    public async Task ReloadAsync() => await State.LoadLatestAsync();

    public void RebuildCards() => RebuildAll(force: true);

    /// <summary>把战场视图渲染成位图，供复制到剪贴板。
    /// 截图时临时铺一层不透明底色：页面本身透明（透出 Mica 材质），
    /// 直接截会得到透明背景，粘到别处会发黑。</summary>
    public async Task<RenderTargetBitmap?> CaptureAsync()
    {
        Microsoft.UI.Xaml.Media.Brush? original = CaptureRoot.Background;
        try
        {
            CaptureRoot.Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SolidBackgroundFillColorBaseBrush"];
            CaptureRoot.UpdateLayout();

            var bitmap = new RenderTargetBitmap();
            await bitmap.RenderAsync(CaptureRoot);
            return bitmap;
        }
        catch
        {
            return null;
        }
        finally
        {
            CaptureRoot.Background = original;
        }
    }

    private void ScheduleRebuild()
    {
        resizeTimer?.Stop();
        resizeTimer?.Start();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        State.Changed += OnStateChanged;

        if (XamlRoot is not null)
        {
            XamlRoot.Changed += OnXamlRootChanged;
        }

        // 已有对局数据时不重复拉取；切标签后返回只重建视图。
        if (!loaded)
        {
            loaded = true;
            if (State.DataVersion == 0)
            {
                await State.LoadLatestAsync();
            }
        }

        RebuildAll();
    }

    private void OnXamlRootChanged(XamlRoot sender, XamlRootChangedEventArgs args) => ScheduleRebuild();

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        State.Changed -= OnStateChanged;
        if (XamlRoot is not null)
        {
            XamlRoot.Changed -= OnXamlRootChanged;
        }
    }

    private void OnStateChanged(object? sender, EventArgs e)
    {
        if (App.Current.MainWindow is MainWindow window)
        {
            window.SetStatus(State.Status, State.IsBusy);
        }

        // 仅当对局数据变化时才重建卡片；进度/状态回调不触发重建。
        if (State.DataVersion != lastDataVersion)
        {
            lastDataVersion = State.DataVersion;
            RebuildAll();
        }
    }

    private void RebuildAll(bool force = false)
    {
        // 先算缩放系数，再让卡片按系数渲染。
        double available = AlliesGrid.ActualHeight;
        int count = State.Allies.Count;
        double rowHeight = count > 0 && available > 1 ? available / count : BaseRowHeight;
        double s = Math.Clamp(rowHeight / BaseRowHeight, 0.72, 2.2);

        // 缩放系数与数据都未变时跳过重排：拖动窗口只改宽度时不会白白重建整棵树。
        bool scaleChanged = Math.Abs(s - lastScale) > 0.001;
        bool dataChanged = lastFillVersion != State.DataVersion;
        if (!force && !scaleChanged && !dataChanged)
        {
            return;
        }

        lastScale = s;
        lastFillVersion = State.DataVersion;
        PlayerCard.RowScale = s;
        PlayerCard.RowHeight = rowHeight;

        Fill(AlliesGrid, State.Allies, allyPool);
        Fill(EnemiesGrid, State.Enemies, enemyPool);

        AllyAvgBattlesText.FontSize = AllyAvgWinrateText.FontSize = AllyAvgWeightedText.FontSize = 13 * s;
        EnemyAvgBattlesText.FontSize = EnemyAvgWinrateText.FontSize = EnemyAvgWeightedText.FontSize = 13 * s;
    }

    /// <summary>用卡片池填充网格：复用已有 PlayerCard 实例与容器子元素，
    /// 数量不变时只重新 Apply，不重建可视树（拖动/缩放窗口时明显更快）。</summary>
    private void Fill(Grid host, ObservableCollection<ParticipantRow> rows, List<PlayerCard> pool)
    {
        int count = rows.Count;
        bool sameCount = host.Children.Count == count && host.RowDefinitions.Count == count;

        if (!sameCount)
        {
            host.Children.Clear();
            host.RowDefinitions.Clear();
            for (int i = 0; i < count; i++)
            {
                host.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            }
        }

        for (int i = 0; i < count; i++)
        {
            PlayerCard card = i < pool.Count ? pool[i] : CreateAndPool(pool);
            card.Apply(rows[i], PlayerCard.RowScale, double.NaN);
            card.VerticalAlignment = VerticalAlignment.Stretch;

            if (!sameCount)
            {
                Grid.SetRow(card, i);
                host.Children.Add(card);
            }
        }
    }

    private static PlayerCard CreateAndPool(List<PlayerCard> pool)
    {
        var card = new PlayerCard();
        pool.Add(card);
        return card;
    }
}
