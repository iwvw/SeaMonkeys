using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace SeaMonkeys.App.Pages;

public sealed partial class HistoryPage : Page, INotifyPropertyChanged
{
    public HistoryPage()
    {
        InitializeComponent();
        Loaded += (_, _) => Refresh();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string StatusText { get; private set; } = string.Empty;

    private void Refresh()
    {
        var entries = BattleHistoryRepository.Current.Entries
            .Select(e => new
            {
                Time = e.StartTime.ToLocalTime().ToString("MM-dd HH:mm"),
                Title = $"{e.Server} · {e.MapName} · {e.Mode} · {e.PlayerCount} 人",
                Averages = $"友军 {e.AllyAvgWeighted:P1} / 敌军 {e.EnemyAvgWeighted:P1}",
            })
            .ToList();

        EntriesList.ItemsSource = entries;
        EmptyHint.Visibility = entries.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        StatusText = entries.Count == 0 ? "本地留存的对局记录" : $"共 {entries.Count} 局记录";
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StatusText)));
    }
}
