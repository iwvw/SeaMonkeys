using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;

namespace SeaMonkeys.App.Pages;

public sealed partial class RenderPage : Page, INotifyPropertyChanged
{
    private bool syncing;
    private bool busy;
    private bool previewValid;
    private string? selectedReplayPath;
    private string? lastResultPath;
    private CancellationTokenSource? cts;
    private readonly List<HistoryOption> historyOptions = new();

    public RenderPage()
    {
        InitializeComponent();
        Loaded += (_, _) => OnLoaded();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string StatusText { get; private set; } = string.Empty;

    private void OnLoaded()
    {
        syncing = true;

        var s = AppSettings.Current;
        CodecBox.SelectedIndex = Math.Clamp(s.RenderCodec, 0, 2);
        MaxSizeBox.Value = s.RenderMaxSizeMiB;
        PlayerNamesSwitch.IsOn = s.RenderShowPlayerNames;
        CapturePointsSwitch.IsOn = s.RenderShowCapturePoints;
        BuildingsSwitch.IsOn = s.RenderShowBuildings;
        CameraSwitch.IsOn = s.RenderShowCameraDirection;
        ArmamentSwitch.IsOn = s.RenderShowArmament;
        KillFeedSwitch.IsOn = s.RenderShowKillFeed;
        SpeedTrailsSwitch.IsOn = s.RenderShowSpeedTrails;
        ShipConfigSwitch.IsOn = s.RenderShowShipConfig;

        historyOptions.Clear();
        foreach (BattleHistoryEntry entry in BattleHistoryRepository.Current.Entries)
        {
            historyOptions.Add(new HistoryOption(
                $"{entry.StartTime.ToLocalTime():MM-dd HH:mm} · {entry.MapName} · {entry.Mode}",
                entry.ReplayPath));
        }

        HistoryBox.ItemsSource = historyOptions;
        previewValid = false;
        syncing = false;

        RefreshToolStatus();
        SetStatus(historyOptions.Count == 0 ? "可导入回放，或先打一局" : $"共 {historyOptions.Count} 局历史可选");

        // 自动选中第一个带有效回放的历史对局；SelectionChanged 会触发预览。
        HistoryOption? first = historyOptions.FirstOrDefault(
            o => !string.IsNullOrWhiteSpace(o.ReplayPath) && File.Exists(o.ReplayPath));
        if (first is not null)
        {
            HistoryBox.SelectedItem = first;
        }
        else
        {
            selectedReplayPath = null;
            UpdateActionState();
        }
    }

    private void SetStatus(string text)
    {
        StatusText = text;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StatusText)));
    }

    private void RefreshToolStatus()
    {
        if (ReplayRenderer.IsInstalled)
        {
            ToolStatusText.Text = $"已安装：{ReplayRenderer.ToolPath}";
            DownloadToolButton.Content = "重新下载";
        }
        else
        {
            ToolStatusText.Text = "未安装。渲染工具约 24MB，从 GitHub 下载（受加速前缀影响）。";
            DownloadToolButton.Content = "下载渲染工具";
        }
    }

    /// <summary>根据工具/回放/预览状态刷新按钮可用性。</summary>
    private void UpdateActionState()
    {
        bool installed = ReplayRenderer.IsInstalled;
        bool hasReplay = !string.IsNullOrWhiteSpace(selectedReplayPath) && File.Exists(selectedReplayPath);

        PreviewButton.IsEnabled = installed && hasReplay && !busy;
        RenderButton.IsEnabled = installed && hasReplay && !busy && previewValid;
        CancelButton.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        HistoryBox.IsEnabled = !busy;
        DownloadToolButton.IsEnabled = !busy;
    }

    /// <summary>参数或来源变化后，已生成的预览失效，需重新预览才能导出。</summary>
    private void InvalidatePreview()
    {
        previewValid = false;
        RenderStatusText.Text = "参数已更改，请重新预览后再导出";
        UpdateActionState();
    }

    private void History_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (syncing || HistoryBox.SelectedItem is not HistoryOption option)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(option.ReplayPath) || !File.Exists(option.ReplayPath))
        {
            selectedReplayPath = null;
            SourceText.Text = "该记录未保存回放路径，请手动导入";
            InvalidatePreview();
            return;
        }

        selectedReplayPath = option.ReplayPath;
        SourceText.Text = option.ReplayPath;
        InvalidatePreview();
        _ = PreviewAsync();
    }

    private async void ImportReplay_Click(object sender, RoutedEventArgs e)
    {
        var picker = new Windows.Storage.Pickers.FileOpenPicker();
        picker.FileTypeFilter.Add(".wowsreplay");
        picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.ComputerFolder;

        IntPtr hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.Current.MainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

        Windows.Storage.StorageFile? file = await picker.PickSingleFileAsync();
        if (file is null)
        {
            return;
        }

        selectedReplayPath = file.Path;
        HistoryBox.SelectedItem = null;
        SourceText.Text = file.Path;
        InvalidatePreview();
        await PreviewAsync();
    }

    private void Codec_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (syncing)
        {
            return;
        }

        AppSettings.Current.RenderCodec = Math.Clamp(CodecBox.SelectedIndex, 0, 2);
        AppSettings.Current.Save();
        InvalidatePreview();
    }

    private void MaxSize_Changed(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (syncing || double.IsNaN(args.NewValue))
        {
            return;
        }

        AppSettings.Current.RenderMaxSizeMiB = (int)args.NewValue;
        AppSettings.Current.Save();
        InvalidatePreview();
    }

    private void Toggle_Changed(object sender, RoutedEventArgs e)
    {
        if (syncing)
        {
            return;
        }

        var s = AppSettings.Current;
        s.RenderShowPlayerNames = PlayerNamesSwitch.IsOn;
        s.RenderShowCapturePoints = CapturePointsSwitch.IsOn;
        s.RenderShowBuildings = BuildingsSwitch.IsOn;
        s.RenderShowCameraDirection = CameraSwitch.IsOn;
        s.RenderShowArmament = ArmamentSwitch.IsOn;
        s.RenderShowKillFeed = KillFeedSwitch.IsOn;
        s.RenderShowSpeedTrails = SpeedTrailsSwitch.IsOn;
        s.RenderShowShipConfig = ShipConfigSwitch.IsOn;
        s.Save();
        InvalidatePreview();
    }

    private async void DownloadTool_Click(object sender, RoutedEventArgs e)
    {
        busy = true;
        UpdateActionState();
        RenderStatusText.Text = "准备下载…";

        var log = new Progress<string>(text => RenderStatusText.Text = text);
        bool ok = await ReplayRenderer.InstallAsync(log);

        busy = false;
        RefreshToolStatus();
        RenderStatusText.Text = ok ? "渲染工具安装完成" : "渲染工具安装失败，请检查加速前缀或网络";
        UpdateActionState();

        if (ok && selectedReplayPath is not null)
        {
            _ = PreviewAsync();
        }
    }

    private async void Preview_Click(object sender, RoutedEventArgs e)
    {
        await PreviewAsync();
    }

    /// <summary>渲染单帧预览；成功后启用导出。</summary>
    private async Task PreviewAsync()
    {
        if (busy || !TryGetReplay(out string replay))
        {
            return;
        }

        busy = true;
        previewValid = false;
        UpdateActionState();
        RenderStatusText.Text = "渲染预览帧…";
        PreviewPlaceholder.Visibility = Visibility.Visible;
        PreviewPlaceholder.Text = "正在渲染预览…";
        PreviewImage.Visibility = Visibility.Collapsed;

        string dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SeaMonkeys", "render-preview");

        string lastLog = string.Empty;
        var log = new Progress<string>(text => lastLog = text);
        string? png = await ReplayRenderer.PreviewAsync(replay, dir, log);

        busy = false;

        if (png is not null)
        {
            PreviewImage.Source = new BitmapImage(new Uri(png));
            PreviewImage.Visibility = Visibility.Visible;
            PreviewPlaceholder.Visibility = Visibility.Collapsed;
            previewValid = true;
            RenderStatusText.Text = "预览完成，可导出视频";
        }
        else
        {
            PreviewPlaceholder.Text = "预览失败";
            PreviewPlaceholder.Visibility = Visibility.Visible;
            RenderStatusText.Text = string.IsNullOrEmpty(lastLog)
                ? "预览失败，请确认游戏路径与回放文件有效"
                : $"预览失败：{lastLog}";
        }

        UpdateActionState();
    }

    private async void Render_Click(object sender, RoutedEventArgs e)
    {
        if (!previewValid)
        {
            RenderStatusText.Text = "请先预览单帧，确认效果后再导出";
            return;
        }

        if (!TryGetReplay(out string replay))
        {
            return;
        }

        string suggested = Path.GetFileNameWithoutExtension(replay) + ".mp4";
        var save = new Windows.Storage.Pickers.FileSavePicker();
        save.FileTypeChoices.Add("视频", new List<string> { ".mp4" });
        save.SuggestedFileName = suggested;
        save.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.VideosLibrary;

        IntPtr hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.Current.MainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(save, hwnd);

        Windows.Storage.StorageFile? file = await save.PickSaveFileAsync();
        if (file is null)
        {
            return;
        }

        cts = new CancellationTokenSource();
        busy = true;
        UpdateActionState();
        ResultPanel.Visibility = Visibility.Collapsed;
        RenderProgressBar.Visibility = Visibility.Visible;
        RenderProgressBar.Value = 0;
        RenderStatusText.Text = "开始渲染…";

        var progress = new Progress<RenderProgress>(p =>
        {
            RenderProgressBar.Value = p.Fraction;
            RenderStatusText.Text = $"{StageLabel(p.Stage)} {p.Frame}/{p.Total}（{p.Fraction:P0}）";
        });

        string? mp4 = await ReplayRenderer.RenderAsync(replay, file.Path, progress, null, cts.Token);

        cts.Dispose();
        cts = null;
        busy = false;
        RenderProgressBar.Visibility = Visibility.Collapsed;

        if (mp4 is not null)
        {
            lastResultPath = mp4;
            long mb = new FileInfo(mp4).Length / (1024 * 1024);
            ResultText.Text = $"渲染完成：{Path.GetFileName(mp4)}（{mb} MB）";
            ResultPanel.Visibility = Visibility.Visible;
            RenderStatusText.Text = mp4;
        }
        else
        {
            RenderStatusText.Text = "渲染失败或被取消";
        }

        UpdateActionState();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        cts?.Cancel();
    }

    private bool TryGetReplay(out string replay)
    {
        replay = selectedReplayPath ?? string.Empty;
        if (string.IsNullOrWhiteSpace(replay) || !File.Exists(replay))
        {
            RenderStatusText.Text = "请先选择或导入一个有效的回放文件";
            return false;
        }

        return true;
    }

    private async void OpenResult_Click(object sender, RoutedEventArgs e)
    {
        if (lastResultPath is null || !File.Exists(lastResultPath))
        {
            return;
        }

        var file = await Windows.Storage.StorageFile.GetFileFromPathAsync(lastResultPath);
        await Windows.System.Launcher.LaunchFileAsync(file);
    }

    private void OpenResultFolder_Click(object sender, RoutedEventArgs e)
    {
        if (lastResultPath is null || !File.Exists(lastResultPath))
        {
            return;
        }

        System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{lastResultPath}\"");
    }

    /// <summary>渲染阶段名转为中文。</summary>
    private static string StageLabel(string stage) => stage.ToLowerInvariant() switch
    {
        "rendering" => "渲染",
        "encoding" => "编码",
        "muxing" => "封装",
        "dumping" => "导出帧",
        "processing" => "处理",
        "decoding" => "解码",
        "writing" => "写入",
        "video" => "视频",
        _ => stage,
    };

    private sealed record HistoryOption(string Display, string ReplayPath)
    {
        public override string ToString() => Display;
    }
}
