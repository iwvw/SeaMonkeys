using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SeaMonkeys.Core.Models;

namespace SeaMonkeys.App.Pages;

public sealed partial class WatchlistPage : Page, INotifyPropertyChanged
{
    public WatchlistPage()
    {
        InitializeComponent();
        Loaded += (_, _) => Refresh();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string StatusText { get; private set; } = string.Empty;

    private void Refresh()
    {
        var entries = new List<object>();
        foreach (Server server in ServerExtensions.AllConcrete)
        {
            foreach ((string accountId, WatchStatus status) in WatchListRepository.Current.All(server))
            {
                entries.Add(new
                {
                    Server = server.ToDisplayName(),
                    AccountId = accountId,
                    Status = status switch
                    {
                        WatchStatus.Positive => "Positive",
                        WatchStatus.Negtive => "Negtive",
                        WatchStatus.Cheater => "Cheater",
                        _ => "-",
                    },
                });
            }
        }

        EntriesList.ItemsSource = entries;
        EmptyHint.Visibility = entries.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        StatusText = entries.Count == 0 ? "按服务器标记玩家，对局中高亮" : $"共 {entries.Count} 条记录";
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StatusText)));
    }
}
