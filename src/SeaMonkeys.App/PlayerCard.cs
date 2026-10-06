using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.UI;
using Windows.UI.Text;

namespace SeaMonkeys.App;

/// <summary>
/// 单张玩家卡片。构造时一次性建好可视化树，之后只通过 <see cref="Apply"/> 更新内容与尺寸，
/// 供 ItemsRepeater 复用容器，避免每次数据变化都重建元素。
/// </summary>
public sealed class PlayerCard : Grid
{
    private static readonly Dictionary<string, Brush> BrushCache = new();

    /// <summary>主题切换时清空缓存，避免沿用旧主题画刷。</summary>
    public static void InvalidateBrushes() => BrushCache.Clear();


    /// <summary>由页面设置的行缩放系数，容器复用时按此渲染。</summary>
    public static double RowScale { get; set; } = 1.0;

    /// <summary>由页面设置的行高（像素）。</summary>
    public static double RowHeight { get; set; } = 72.0;

    private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
    {
        if (args.NewValue is ParticipantRow row)
        {
            Apply(row, RowScale, RowHeight);
        }
    }

    private readonly Border bar;
    private readonly Grid playerCell;
    private readonly StackPanel shipLine;
    private readonly TextBlock tier;
    private readonly Image icon;
    private readonly TextBlock ship;
    private readonly StackPanel nameLine;
    private readonly TextBlock clan;
    private readonly TextBlock name;
    private readonly StackPanel accountCol;
    private readonly TextBlock account;
    private readonly TextBlock accountRate;
    private readonly StackPanel shipCol;
    private readonly TextBlock shipRate;
    private readonly TextBlock shipWinrate;
    private readonly StackPanel weightedCol;
    private readonly TextBlock weighted;
    private readonly TextBlock component;
    private readonly TextBlock stateText;
    private readonly Image stamp;
    private ParticipantRow? currentRow;
    private Flyout? detailFlyout;
    private MenuFlyout? contextMenu;

    public PlayerCard()
    {
        DataContextChanged += OnDataContextChanged;
        Tapped += (_, _) => ToggleTooltip();
        RightTapped += (_, _) => EnsureContextMenu();
        ColumnSpacing = 10;
        Padding = new Thickness(0);
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2.2, GridUnitType.Star) });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.4, GridUnitType.Star) });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.4, GridUnitType.Star) });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.0, GridUnitType.Star) });

        bar = new Border { CornerRadius = new CornerRadius(7, 0, 0, 7) };
        Grid.SetColumn(bar, 0);
        Grid.SetColumnSpan(bar, 4);
        Children.Add(bar);

        // 玩家列
        playerCell = new Grid { RowSpacing = 2 };
        playerCell.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        playerCell.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        shipLine = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        tier = Text(18, new FontWeight { Weight = 600 }, "TextFillColorSecondaryBrush");
        shipLine.Children.Add(tier);
        icon = new Image { VerticalAlignment = VerticalAlignment.Center, Stretch = Stretch.Uniform };
        shipLine.Children.Add(icon);
        ship = Text(16, new FontWeight { Weight = 600 }, null);
        ship.VerticalAlignment = VerticalAlignment.Center;
        shipLine.Children.Add(ship);
        Grid.SetRow(shipLine, 0);
        playerCell.Children.Add(shipLine);

        nameLine = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        clan = Text(17, new FontWeight { Weight = 600 }, "TextFillColorSecondaryBrush");
        clan.VerticalAlignment = VerticalAlignment.Center;
        nameLine.Children.Add(clan);
        name = Text(17, new FontWeight { Weight = 600 }, null);
        name.VerticalAlignment = VerticalAlignment.Center;
        nameLine.Children.Add(name);
        Grid.SetRow(nameLine, 1);
        playerCell.Children.Add(nameLine);

        Grid.SetColumn(playerCell, 0);
        Children.Add(playerCell);

        // 账号列
        accountCol = Column(out account, out accountRate);
        Grid.SetColumn(accountCol, 1);
        Children.Add(accountCol);

        // 单船列
        shipCol = Column(out shipRate, out shipWinrate);
        Grid.SetColumn(shipCol, 2);
        Children.Add(shipCol);

        // 加权列
        weightedCol = Column(out weighted, out component);
        Grid.SetColumn(weightedCol, 3);
        Children.Add(weightedCol);

        // 隐藏档案：合并右侧三列居中
        stateText = Text(16, new FontWeight { Weight = 600 }, "TextFillColorSecondaryBrush");
        stateText.HorizontalAlignment = HorizontalAlignment.Center;
        stateText.VerticalAlignment = VerticalAlignment.Center;
        stateText.Visibility = Visibility.Collapsed;
        Grid.SetColumn(stateText, 1);
        Grid.SetColumnSpan(stateText, 3);
        Children.Add(stateText);

        // 印章（右上角，叠加不占列宽）
        stamp = new Image
        {
            Stretch = Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            IsHitTestVisible = false,
            Opacity = 0.92,
            Visibility = Visibility.Collapsed,
        };
        Grid.SetColumn(stamp, 0);
        Grid.SetColumnSpan(stamp, 4);
        Children.Add(stamp);
    }

    private StackPanel Column(out TextBlock top, out TextBlock bottom)
    {
        var panel = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        top = Text(16, new FontWeight { Weight = 600 }, null);
        bottom = Text(17, new FontWeight { Weight = 600 }, null);
        panel.Children.Add(top);
        panel.Children.Add(bottom);
        return panel;
    }
    public void Apply(ParticipantRow row, double s, double height)
    {
        Background = Brush("CardBackgroundFillColorDefaultBrush");
        BorderBrush = Brush("CardStrokeColorDefaultBrush");
        BorderThickness = new Thickness(1);
        CornerRadius = new CornerRadius(8);
        ColumnSpacing = 10 * s;
        Height = double.IsNaN(height) ? double.NaN : height;
        VerticalAlignment = VerticalAlignment.Stretch;

        playerCell.Padding = new Thickness(12 * s, 6 * s, 0, 6 * s);
        shipLine.Spacing = 6 * s;
        nameLine.Spacing = 6 * s;

        Color? barColor = row.IsAvailable
            ? ColorSchemes.GetColor(AppSettings.Current.ColorStyle, row.WinrateValue, row.Grade)
            : (row.State.Length > 0 ? Color.FromArgb(255, 0x88, 0x88, 0x88) : (Color?)null);
        bar.Background = barColor is Color c ? BuildGradient(c) : null;

        // 主题画刷在构造时被缓存，切换主题后必须重新取，否则沿用旧主题颜色。
        tier.Foreground = Brush("TextFillColorSecondaryBrush");
        clan.Foreground = Brush("TextFillColorSecondaryBrush");
        stateText.Foreground = Brush("TextFillColorSecondaryBrush");

        tier.Text = row.Tier;
        tier.FontSize = 18 * s;
        icon.Height = 15 * s;
        icon.Width = 15 * s * ShipAspect(row.ShipTypeKey);
        icon.Source = ShipIcon(row.ShipTypeKey);
        ship.Text = row.Ship;
        ship.FontSize = 16 * s;

        clan.Text = row.Clan;
        clan.FontSize = 17 * s;
        clan.Visibility = row.Clan.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        name.Text = row.Name;
        name.FontSize = 17 * s;

        // 观察名单高亮：命中时用状态色显示昵称并附图标。
        if (AppSettings.Current.HighlightWatchlist && row.Watch != WatchStatus.None)
        {
            name.Foreground = new SolidColorBrush(WatchColor(row.Watch));
            name.Text = $"{row.Name} {row.WatchLabel}";
        }
        else
        {
            name.ClearValue(TextBlock.ForegroundProperty);
            name.Foreground = Brush("TextFillColorPrimaryBrush");
        }

        // 详细浮窗与右键菜单惰性构建：仅在首次交互时创建，避免每卡每次刷新都建整棵树。
        currentRow = row;
        detailFlyout?.Hide();
        detailFlyout = null;
        contextMenu = null;
        ToolTipService.SetToolTip(this, null);
        ContextFlyout = null;


        bool hidden = row.State.Length > 0;
        accountCol.Visibility = hidden ? Visibility.Collapsed : Visibility.Visible;
        shipCol.Visibility = hidden ? Visibility.Collapsed : Visibility.Visible;
        weightedCol.Visibility = hidden ? Visibility.Collapsed : Visibility.Visible;
        stateText.Visibility = hidden ? Visibility.Visible : Visibility.Collapsed;
        stateText.Text = row.State;
        stateText.FontSize = 16 * s;

        account.Text = row.Account;
        account.FontSize = 16 * s;
        accountRate.Text = row.AccountRate;
        accountRate.FontSize = 17 * s;
        shipRate.Text = row.ShipRate;
        shipRate.FontSize = 16 * s;
        shipWinrate.Text = row.ShipWinrate;
        shipWinrate.FontSize = 17 * s;
        weighted.Text = row.Weighted;
        weighted.FontSize = 16 * s;
        component.Text = row.Component;
        component.FontSize = 17 * s;

        // 隐藏档案用「棍母」标记；其余按分级。
        string? stampFile = row.State == "隐藏档案"
            ? "hidden.png"
            : row.Grade switch
            {
                "神佬" => "god.png",
                "大佬" => "pro.png",
                "诗人" => "poet.png",
                "正常" => "normal.png",
                "路边一条" => "roadside.png",
                "区" => "seamonkey.png",
                _ => null,
            };

        stamp.Visibility = stampFile is not null ? Visibility.Visible : Visibility.Collapsed;
        if (stampFile is not null)
        {
            stamp.Source = new BitmapImage(new Uri($"ms-appx:///Assets/stamps/{stampFile}"));
            stamp.Width = 75 * s;
            stamp.Height = 75 * s;
            stamp.Margin = new Thickness(0, -14 * s, -12 * s, 0);
            stamp.RenderTransform = new Microsoft.UI.Xaml.Media.RotateTransform
            {
                Angle = -12,
                CenterX = 37.5 * s,
                CenterY = 37.5 * s,
            };
        }
    }

    /// <summary>左键点击切换详细浮窗；用 Flyout 而非 ToolTip，避免被窗口边界裁切。</summary>
    private void ToggleTooltip()
    {
        if (currentRow is null)
        {
            return;
        }

        if (detailFlyout is null)
        {
            detailFlyout = new Flyout
            {
                Content = BuildDetailTooltip(currentRow, RowScale),
                Placement = Microsoft.UI.Xaml.Controls.Primitives.FlyoutPlacementMode.BottomEdgeAlignedLeft,
                FlyoutPresenterStyle = (Style)Application.Current.Resources["OpaqueFlyoutPresenterStyle"],
            };
        }

        if (detailFlyout.IsOpen)
        {
            detailFlyout.Hide();
        }
        else
        {
            detailFlyout.ShowAt(this);
        }
    }

    private void EnsureContextMenu()
    {
        if (currentRow is not null && contextMenu is null)
        {
            contextMenu = BuildContextMenu(currentRow);
            ContextFlyout = contextMenu;
        }
    }


    private static Color WatchColor(WatchStatus status) => status switch
    {
        WatchStatus.Positive => Color.FromArgb(255, 0x3C, 0xB0, 0x4A),
        WatchStatus.Negtive => Color.FromArgb(255, 0xE5, 0x48, 0x4D),
        WatchStatus.Cheater => Color.FromArgb(255, 0xD0, 0x70, 0x00),
        _ => Color.FromArgb(255, 0x88, 0x88, 0x88),
    };

    /// <summary>玩家详细 tooltip：账号与单船的 单野/双人/三人 场次、胜率、场均伤害。</summary>
    private static StackPanel BuildDetailTooltip(ParticipantRow row, double s)
    {
        var root = new StackPanel { Spacing = 6, MinWidth = 360 };
        root.Children.Add(Header($"{row.Clan} {row.Name} · {row.Tier} {row.Ship}"));

        root.Children.Add(DetailTable(
            "账号",
            ("场次", row.AccountSolo, row.AccountDiv2, row.AccountDiv3),
            ("胜率", row.AccountSoloWr, row.AccountDiv2Wr, row.AccountDiv3Wr)));
        root.Children.Add(DetailTable(
            "单船",
            ("场次", row.ShipSolo, row.ShipDiv2, row.ShipDiv3),
            ("胜率", row.ShipSoloWr, row.ShipDiv2Wr, row.ShipDiv3Wr),
            ("场均伤害", row.ShipSoloDmg, row.ShipDiv2Dmg, row.ShipDiv3Dmg)));

        return root;
    }

    private static TextBlock Header(string text) => new()
    {
        Text = text,
        FontWeight = new FontWeight { Weight = 600 },
        FontSize = 14,
    };

    private static Grid DetailTable(string title, params (string Label, string Solo, string Div2, string Div3)[] rows)
    {
        var grid = new Grid { ColumnSpacing = 12, RowSpacing = 2 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(64) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(72) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(72) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(72) });

        var titleBlock = new TextBlock { Text = title, FontWeight = new FontWeight { Weight = 600 }, FontSize = 13 };
        Grid.SetRow(titleBlock, 0);
        Grid.SetColumn(titleBlock, 0);
        grid.Children.Add(titleBlock);

        string[] heads = { "单野", "双人", "三人" };
        for (int c = 0; c < 3; c++)
        {
            var head = new TextBlock { Text = heads[c], FontSize = 11, Opacity = 0.7, HorizontalAlignment = HorizontalAlignment.Right };
            Grid.SetRow(head, 0);
            Grid.SetColumn(head, c + 1);
            grid.Children.Add(head);
        }

        for (int r = 0; r < rows.Length; r++)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var label = new TextBlock { Text = rows[r].Label, FontSize = 12, Opacity = 0.7 };
            Grid.SetRow(label, r + 1);
            Grid.SetColumn(label, 0);
            grid.Children.Add(label);

            string[] vals = { rows[r].Solo, rows[r].Div2, rows[r].Div3 };
            for (int c = 0; c < 3; c++)
            {
                var val = new TextBlock { Text = vals[c], FontSize = 12, HorizontalAlignment = HorizontalAlignment.Right };
                Grid.SetRow(val, r + 1);
                Grid.SetColumn(val, c + 1);
                grid.Children.Add(val);
            }
        }

        grid.RowDefinitions.Insert(0, new RowDefinition { Height = GridLength.Auto });
        return grid;
    }

    private static MenuFlyout BuildContextMenu(ParticipantRow row)
    {
        var flyout = new MenuFlyout();

        void Add(string text, WatchStatus status)
        {
            var item = new MenuFlyoutItem { Text = text };
            item.Click += (_, _) =>
            {
                var server = ParseServer(row);
                WatchListRepository.Current.Set(server, row.AccountId, status);
                NotificationService.Success($"{row.Name} 已标记为 {text}");
                BattleState.Current.RefreshWatchHighlight();
            };
            flyout.Items.Add(item);
        }

        Add("标记为 Positive", WatchStatus.Positive);
        Add("标记为 Negtive", WatchStatus.Negtive);
        Add("标记为 Cheater", WatchStatus.Cheater);
        flyout.Items.Add(new MenuFlyoutSeparator());
        var remove = new MenuFlyoutItem { Text = "移出观察名单" };
        remove.Click += (_, _) =>
        {
            WatchListRepository.Current.Set(ParseServer(row), row.AccountId, WatchStatus.None);
            NotificationService.Info($"{row.Name} 已移出观察名单");
            BattleState.Current.RefreshWatchHighlight();
        };
        flyout.Items.Add(remove);

        return flyout;
    }

    private static SeaMonkeys.Core.Models.Server ParseServer(ParticipantRow row)
    {
        if (SeaMonkeys.Core.Models.ServerExtensions.TryParseCode(row.ServerCode, out var server))
        {
            return server;
        }
        return SeaMonkeys.Core.Models.Server.Auto;
    }

    private static LinearGradientBrush BuildGradient(Color c)
    {
        double peak = Services.ThemeManager.IsDark ? 0x8C : 0xB4;
        var gradient = new LinearGradientBrush
        {
            StartPoint = new Windows.Foundation.Point(0, 0.5),
            EndPoint = new Windows.Foundation.Point(1, 0.5),
        };
        gradient.GradientStops.Add(new GradientStop { Color = Color.FromArgb((byte)peak, c.R, c.G, c.B), Offset = 0.0 });
        gradient.GradientStops.Add(new GradientStop { Color = Color.FromArgb((byte)(peak * 0.45), c.R, c.G, c.B), Offset = 0.5 });
        gradient.GradientStops.Add(new GradientStop { Color = Color.FromArgb((byte)(peak * 0.12), c.R, c.G, c.B), Offset = 0.8 });
        gradient.GradientStops.Add(new GradientStop { Color = Color.FromArgb(0x00, c.R, c.G, c.B), Offset = 1.0 });
        return gradient;
    }

    private static TextBlock Text(double size, FontWeight weight, string? brushKey)
    {
        var block = new TextBlock
        {
            FontSize = size,
            FontWeight = weight,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        if (brushKey is not null)
        {
            block.Foreground = Brush(brushKey);
        }
        return block;
    }

    private static Brush Brush(string key)
    {
        if (BrushCache.TryGetValue(key, out Brush? cached))
        {
            return cached;
        }

        var brush = (Brush)Application.Current.Resources[key];
        BrushCache[key] = brush;
        return brush;
    }

    private static ImageSource? ShipIcon(string key)
        => (ImageSource?)new ShipTypeIconConverter().Convert(key, typeof(ImageSource), null!, string.Empty);

    private static double ShipAspect(string key) => key switch
    {
        "Destroyer" => 235.0 / 168.0,
        "Submarine" => 293.0 / 203.0,
        _ => 320.0 / 168.0,
    };
}


