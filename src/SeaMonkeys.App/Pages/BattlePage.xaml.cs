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
        RebuildAll();
    }

    public BattleState State => BattleState.Current;

    public async Task ReloadAsync() => await State.LoadLatestAsync();

    public void RebuildCards() => RebuildAll();

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

    private void RebuildAll()
    {
        // 先算缩放系数，再让卡片按系数渲染。
        double available = AlliesGrid.ActualHeight;
        int count = State.Allies.Count;
        double rowHeight = count > 0 && available > 1 ? available / count : BaseRowHeight;
        double s = Math.Clamp(rowHeight / BaseRowHeight, 0.72, 2.2);
        PlayerCard.RowScale = s;
        PlayerCard.RowHeight = rowHeight;

        Fill(AlliesGrid, State.Allies, allyPool);
        Fill(EnemiesGrid, State.Enemies, enemyPool);

        AllyAvgBattlesText.FontSize = AllyAvgWinrateText.FontSize = AllyAvgWeightedText.FontSize = 13 * s;
        EnemyAvgBattlesText.FontSize = EnemyAvgWinrateText.FontSize = EnemyAvgWeightedText.FontSize = 13 * s;
    }

    /// <summary>用卡片池填充网格：复用已有 PlayerCard 实例，只在数量不足时新建。</summary>
    private void Fill(Grid host, ObservableCollection<ParticipantRow> rows, List<PlayerCard> pool)
    {
        int count = rows.Count;
        host.Children.Clear();
        host.RowDefinitions.Clear();

        for (int i = 0; i < count; i++)
        {
            host.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            PlayerCard card = i < pool.Count ? pool[i] : CreateAndPool(pool);
            card.Apply(rows[i], PlayerCard.RowScale, double.NaN);
            card.VerticalAlignment = VerticalAlignment.Stretch;
            Grid.SetRow(card, i);
            host.Children.Add(card);
        }
    }

    private static PlayerCard CreateAndPool(List<PlayerCard> pool)
    {
        var card = new PlayerCard();
        pool.Add(card);
        return card;
    }
}
