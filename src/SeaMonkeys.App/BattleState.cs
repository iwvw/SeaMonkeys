using System.Collections.ObjectModel;
using System.Globalization;
using Microsoft.UI.Dispatching;
using SeaMonkeys.Core.Intake;
using SeaMonkeys.Core.Models;
using SeaMonkeys.Core.Observation;
using SeaMonkeys.Core.Parsing;
using SeaMonkeys.Core.Servers;
using SeaMonkeys.Core.Settings;
using SeaMonkeys.Core.Vortex;

namespace SeaMonkeys.App;

public sealed class ParticipantRow
{
    public required string Name { get; init; }
    public required string Clan { get; init; }
    public required string Ship { get; init; }
    public required string ShipTypeKey { get; init; }
    public required string Tier { get; init; }
    public required string Account { get; init; }
    public required string AccountExp { get; init; }
    public required string AccountRate { get; init; }
    public required string Weighted { get; init; }
    public required string ShipRate { get; init; }
    public required string ShipDamage { get; init; }
    public required string ShipWinrate { get; init; }
    public required string Component { get; init; }
    public required string Grade { get; init; }
    public required string AvgDamage { get; init; }
    public required string State { get; init; }
    public required double WeightedValue { get; init; }
    public required int TypeOrder { get; init; }
    public required int TierValue { get; init; }
    public required double BattlesValue { get; init; }
    public required double WinrateValue { get; init; }
    public required bool IsAvailable { get; init; }

    public required string AccountId { get; init; }
    public required string ServerCode { get; init; }
    public required WatchStatus Watch { get; init; }
    public required string WatchLabel { get; init; }

    // 详细统计（tooltip）：账号 单野/双人/三人 的场次与胜率，单船 单野/双人/三人 的场次、胜率、场均伤害。
    public required string AccountSolo { get; init; }
    public required string AccountDiv2 { get; init; }
    public required string AccountDiv3 { get; init; }
    public required string ShipSolo { get; init; }
    public required string ShipDiv2 { get; init; }
    public required string ShipDiv3 { get; init; }
    public required string AccountSoloWr { get; init; }
    public required string AccountDiv2Wr { get; init; }
    public required string AccountDiv3Wr { get; init; }
    public required string ShipSoloWr { get; init; }
    public required string ShipDiv2Wr { get; init; }
    public required string ShipDiv3Wr { get; init; }
    public required string ShipSoloDmg { get; init; }
    public required string ShipDiv2Dmg { get; init; }
    public required string ShipDiv3Dmg { get; init; }

    public static ParticipantRow From(Participant p)
    {
        PlayerStatistics s = p.Statistics;
        bool ok = s.IsAvailable;
        double wr = s.WeightedWinrate;
        double totalWr = s.Winrate;
        WatchStatus watch = WatchListRepository.Current.Get(p.Server, p.AccountId);

        return new ParticipantRow
        {
            Name = p.Name,
            Clan = p.ClanTag ?? string.Empty,
            Ship = ShipCatalog.Current.GetName(p.ShipId),
            ShipTypeKey = ShipCatalog.Current.GetTypeKey(p.ShipId),
            Tier = ShipCatalog.Roman(ShipCatalog.Current.GetTier(p.ShipId)),
            Account = ok ? $"{s.Battles:0}场 · {s.Experience / Math.Max(1, s.Battles):0}" : "-",
            AccountExp = string.Empty,
            AccountRate = ok ? $"胜率 {s.Winrate:P1}" : "-",
            Weighted = ok ? s.WeightedWinrate.ToString("P1", CultureInfo.InvariantCulture) : "-",
            ShipRate = ok && s.ShipBattles > 0 ? $"{s.ShipBattles:0}场 · {s.ShipDamageDealt / s.ShipBattles:0}" : "-",
            ShipDamage = string.Empty,
            ShipWinrate = ok && s.ShipBattles > 0 ? $"胜率 {s.ShipWinrate:P1}" : "-",
            Component = ok ? GradeText(GradeOf(totalWr, s.Battles)) : "-",
            Grade = ok ? GradeOf(totalWr, s.Battles) : "-",
            AvgDamage = ok ? s.AvgDamage.ToString("0", CultureInfo.InvariantCulture) : "-",
            State = ok ? string.Empty : s.IsHidden ? "隐藏档案" : "无数据",
            WeightedValue = ok ? wr : -1,
            TypeOrder = ShipCatalog.Current.GetTypeOrder(p.ShipId),
            TierValue = ShipCatalog.Current.GetTier(p.ShipId),
            BattlesValue = ok ? s.Battles : 0,
            WinrateValue = ok ? s.Winrate : 0,
            IsAvailable = ok,
            AccountId = p.AccountId ?? string.Empty,
            ServerCode = p.Server == Server.Auto ? string.Empty : p.Server.ToCode(),
            Watch = watch,
            WatchLabel = WatchGlyph(watch),
            AccountSolo = ok ? $"{s.BattlesSolo:0}" : "-",
            AccountDiv2 = ok ? $"{s.BattlesDiv2:0}" : "-",
            AccountDiv3 = ok ? $"{s.BattlesDiv3:0}" : "-",
            ShipSolo = ok ? $"{s.ShipBattlesSolo:0}" : "-",
            ShipDiv2 = ok ? $"{s.ShipBattlesDiv2:0}" : "-",
            ShipDiv3 = ok ? $"{s.ShipBattlesDiv3:0}" : "-",
            AccountSoloWr = ok ? Pct(s.WinsSolo, s.BattlesSolo) : "-",
            AccountDiv2Wr = ok ? Pct(s.WinsDiv2, s.BattlesDiv2) : "-",
            AccountDiv3Wr = ok ? Pct(s.WinsDiv3, s.BattlesDiv3) : "-",
            ShipSoloWr = ok ? Pct(s.ShipWinsSolo, s.ShipBattlesSolo) : "-",
            ShipDiv2Wr = ok ? Pct(s.ShipWinsDiv2, s.ShipBattlesDiv2) : "-",
            ShipDiv3Wr = ok ? Pct(s.ShipWinsDiv3, s.ShipBattlesDiv3) : "-",
            ShipSoloDmg = ok ? Avg(s.ShipDamageDealtSolo, s.ShipBattlesSolo) : "-",
            ShipDiv2Dmg = ok ? Avg(s.ShipDamageDealtDiv2, s.ShipBattlesDiv2) : "-",
            ShipDiv3Dmg = ok ? Avg(s.ShipDamageDealtDiv3, s.ShipBattlesDiv3) : "-",
        };
    }

    private static string Pct(double wins, double battles)
        => battles <= 0 ? "-" : $"{wins / battles:P1}";

    private static string Avg(double total, double battles)
        => battles <= 0 ? "-" : $"{total / battles:0}";

    private static string WatchGlyph(WatchStatus status) => status switch
    {
        WatchStatus.Positive => "★",
        WatchStatus.Negtive => "✖",
        WatchStatus.Cheater => "⚑",
        _ => string.Empty,
    };

    // 分级阈值取自 ApeRadar 的 8 色胜率色带边界（0.47/0.49/0.52/0.54/0.56/0.60/0.65），
    // 合并橙+黄、绿+深绿后压缩为 6 档：区 / 路边一条 / 正常 / 糕手 / 大佬 / 神佬。
    // 糕手 [56%,60%)，大佬 [60%,65%]，神佬 >65% 且场次 ≥500（沿用 ApeRadar 成分算法的高段门槛）。
    private static string GradeOf(double wr, double battles) => wr switch
    {
        <= 0.47 => "区",
        <= 0.52 => "路边一条",
        <= 0.56 => "正常",
        < 0.60 => "糕手",
        <= 0.65 => "大佬",
        _ => battles >= 500 ? "神佬" : "大佬",
    };

    /// <summary>成分显示文案：与印章文字可不同（区在界面上显示为"海猴"）。</summary>
    private static string GradeText(string grade) => grade switch
    {
        "区" => "海猴",
        _ => grade,
    };
}

public sealed class BattleState : System.ComponentModel.INotifyPropertyChanged
{
    public static BattleState Current { get; } = new();

    private readonly ArenaInfoParser parser = new();
    private SeaMonkeys.Core.Models.Battle? lastBattle;
    private SeaMonkeysSettings settings = new();
    private ArenaInfoLocator? arenaLocator;
    private DispatcherQueueTimer? observeTimer;
    private DispatcherQueue? dispatcher;

    public ObservableCollection<ParticipantRow> Allies { get; } = new();
    public ObservableCollection<ParticipantRow> Enemies { get; } = new();

    public string Summary { get; private set; } = "等待对局";
    public string BattleTime { get; private set; } = string.Empty;
    public string Status { get; private set; } = "空闲";
    public string BattleType { get; private set; } = string.Empty;
    public bool IsBusy { get; private set; }
    public double Progress { get; private set; }

    /// <summary>当前正在进行的步骤描述，显示在加载环旁。</summary>
    public string ActivityText { get; private set; } = string.Empty;

    private int loadGeneration;

    /// <summary>每次对局数据（名单）就绪时自增，供视图判断是否需要重建卡片。</summary>
    public int DataVersion { get; private set; }

    public string AllyAvgBattles { get; private set; } = "-";
    public string AllyAvgWinrate { get; private set; } = "-";
    public string AllyAvgWeighted { get; private set; } = "-";
    public string EnemyAvgBattles { get; private set; } = "-";
    public string EnemyAvgWinrate { get; private set; } = "-";
    public string EnemyAvgWeighted { get; private set; } = "-";

    public event EventHandler? Changed;

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

    /// <summary>由窗口在启动时注入 UI 调度器，用于把观察回调切回 UI 线程。</summary>
    public void InitializeDispatcher(DispatcherQueue dispatcherQueue) => dispatcher = dispatcherQueue;

    public async Task LoadLatestAsync()
    {
        ApplySettings();
#if DEBUG
        if (File.Exists(DebugReplayPath))
        {
            await LoadFileAsync(DebugReplayPath);
            StartObservation();
            return;
        }
#endif
        string? gamePath = ResolveGamePath();
        if (gamePath is null)
        {
            SetState("未找到对局文件，请检查游戏路径", "未找到对局", busy: false, progress: 0);
            StartObservation();
            return;
        }

        // 优先当前对局元数据（开局即存在）；无则退回最新回放文件。
        arenaLocator ??= new ArenaInfoLocator();
        string arenaInfo = arenaLocator.FindLatest(gamePath, requireNewer: false);
        string? replayPath = !string.IsNullOrEmpty(arenaInfo)
            ? arenaInfo
            : GameLocator.FindLatestReplay(gamePath);

        if (replayPath is null)
        {
            SetState("未找到对局文件，请检查游戏路径", "未找到对局", busy: false, progress: 0);
            StartObservation();
            return;
        }

        await LoadFileAsync(replayPath);
        StartObservation();
    }

    /// <summary>解析游戏路径：设置优先，其次自动探测（含 Steam 库）。</summary>
    private static string? ResolveGamePath()
    {
        string configured = AppSettings.Current.GamePath;
        if (!string.IsNullOrWhiteSpace(configured) && Directory.Exists(configured))
        {
            return configured;
        }

        string? detected = GameLocator.FindGamePath();
        if (detected is not null)
        {
            AppSettings.Current.GamePath = detected;
            AppSettings.Current.Save();
        }

        return detected;
    }

    /// <summary>
    /// 启动对局观察：每秒轮询 replay 目录下的 tempArenaInfo.json，
    /// 文件写入时间变新即视为新对局并自动加载。对局开局即可显示，无需等打完。
    /// </summary>
    private void StartObservation()
    {
        if (observeTimer is not null || !AppSettings.Current.ObserveReplays)
        {
            return;
        }

        if (dispatcher is null || ResolveGamePath() is null)
        {
            return;
        }

        arenaLocator ??= new ArenaInfoLocator();

        observeTimer = dispatcher.CreateTimer();
        observeTimer.Interval = TimeSpan.FromSeconds(1);
        observeTimer.IsRepeating = true;
        observeTimer.Tick += (_, _) => PollArenaInfo();
        observeTimer.Start();
    }

    private void PollArenaInfo()
    {
        if (IsBusy)
        {
            return;
        }

        string? gamePath = ResolveGamePath();
        if (gamePath is null)
        {
            return;
        }

        string latest = arenaLocator!.FindLatest(gamePath, requireNewer: true);
        if (!string.IsNullOrEmpty(latest))
        {
            _ = LoadFileAsync(latest);
        }
    }

    /// <summary>观察状态描述，供界面显示。</summary>
    public bool IsObserving => observeTimer?.IsRunning == true;

    public string ObservedDirectory
    {
        get
        {
            string? gamePath = ResolveGamePath();
            return gamePath is null ? string.Empty : Path.Combine(gamePath, "replays");
        }
    }

    /// <summary>按当前设置重建对局观察（设置变更后调用）。</summary>
    public void RestartObservation()
    {
        if (observeTimer is not null)
        {
            observeTimer.Stop();
            observeTimer = null;
        }

        arenaLocator?.Reset();
        StartObservation();
    }

#if DEBUG
    private const string DebugReplayPath =
        @"D:\Game\Steam\steamapps\common\World of Warships\replays\15.8.0.0\20261004_021238_PASS710-Archerfish_41_Conquest.wowsreplay";
#endif

    public async Task LoadFileAsync(string replayPath)
    {
        ApplySettings();
        int generation = ++loadGeneration;
        IsBusy = true;
        SetState($"解析 {Path.GetFileName(replayPath)} ...", Summary, busy: true, progress: 0);

        try
        {
            Battle battle = await Task.Run(() => parser.Parse(replayPath));
            lastBattle = battle;
            BattleType = string.IsNullOrEmpty(battle.GameType) ? battle.MatchGroup : battle.GameType;
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(BattleType)));
            string modeLabel = ModeLabel(battle.GameType, battle.MatchGroup);
            string mapLabel = MapCatalog.GetName(battle.MapId, battle.MapDisplayName);
            BattleTime = battle.StartTime.ToLocalTime().ToString("MM-dd HH:mm");
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(BattleTime)));
            using var transport = new HttpVortexTransport(settings);
            var source = new VortexStatsSource(transport);
            var intake = new BattleIntake(source, settings);

            Server server;
            if (settings.Server != Server.Auto)
            {
                // 服务器已在设置中确定，直接使用，不再识别。
                server = settings.Server;
                SetState($"服务器 {server.ToDisplayName()} · 获取战绩...", mapLabel, busy: true, progress: 0);
            }
            else
            {
                SetState("识别服务器...", mapLabel, busy: true, progress: 0);
                server = await intake.ResolveServerAsync(battle, ResolveGamePath() ?? string.Empty);

                // 首次自动识别出服务器后固化到设置：下次直接使用，不再识别；
                // 仅当用户在设置里手动切回「自动」或改选其它服务器时才重新判定。
                if (server != Server.Auto)
                {
                    AppSettings.Current.ServerIndex = server.ToSettingsIndex();
                    AppSettings.Current.Save();
                }
            }

            // 先占位上屏：显示昵称/舰船，战绩栏留空，随后逐个填充。
            FillRows(battle);
            DataVersion++;

            int total = battle.Participants.Count;
            var progress = new Progress<int>(done =>
            {
                if (generation == loadGeneration)
                {
                    SetState($"获取战绩 {done}/{total}", mapLabel, busy: true, progress: total == 0 ? 0 : (double)done / total);
                }
            });

            var onReady = new Progress<Participant>(_ =>
            {
                if (generation == loadGeneration)
                {
                    FillRows(battle);
                    DataVersion++;
                    Changed?.Invoke(this, EventArgs.Empty);
                }
            });

            await intake.EnrichAsync(battle, server, progress, onReady);
            // 作废该代号：排在完成之后的进度回调不再置回忙碌。
            loadGeneration++;

            FillRows(battle);
            DataVersion++;

            (AllyAvgBattles, AllyAvgWinrate, AllyAvgWeighted) = Averages(Allies);
            (EnemyAvgBattles, EnemyAvgWinrate, EnemyAvgWeighted) = Averages(Enemies);
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(AllyAvgBattles)));
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(AllyAvgWinrate)));
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(AllyAvgWeighted)));
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(EnemyAvgBattles)));
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(EnemyAvgWinrate)));
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(EnemyAvgWeighted)));

            SetState($"服务器 {server.ToDisplayName()} · 完成", Summary, busy: false, progress: 1);
            SaveHistory(battle, server, mapLabel, modeLabel, replayPath);
            NotificationService.Success($"已加载 {mapLabel} · {battle.Participants.Count} 人");
            TrimWorkingSet();
        }
        catch (Exception ex)
        {
            SetState($"失败：{ex.Message}", Summary, busy: false, progress: 0);
            NotificationService.Error($"加载失败：{ex.Message}");
            try
            {
                File.WriteAllText(
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SeaMonkeys", "last-error.log"),
                    ex.ToString());
            }
            catch { }
        }
        finally
        {
            IsBusy = false;
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(IsBusy)));
        }
    }

    /// <summary>观察名单变化后刷新高亮。</summary>
    public void RefreshWatchHighlight()
    {
        if (lastBattle is not null)
        {
            FillRows(lastBattle);
            DataVersion++;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>按当前参与者状态重建两栏行（用于实时上屏）。</summary>
    private void FillRows(Battle battle)
    {
        Allies.Clear();
        Enemies.Clear();
        foreach (Participant p in Sort(battle.Allies))
        {
            Allies.Add(ParticipantRow.From(p));
        }
        foreach (Participant p in Sort(battle.Enemies))
        {
            Enemies.Add(ParticipantRow.From(p));
        }
    }

    private void SaveHistory(Battle battle, Server server, string mapLabel, string modeLabel, string replayPath)
    {
        var entry = new BattleHistoryEntry
        {
            StartTime = battle.StartTime,
            MapName = mapLabel,
            Mode = modeLabel,
            Server = server.ToDisplayName(),
            PlayerCount = battle.Participants.Count,
            AllyAvgWeighted = ParsePct(AllyAvgWeighted),
            EnemyAvgWeighted = ParsePct(EnemyAvgWeighted),
            ReplayPath = replayPath,
        };

        foreach (Participant p in battle.Participants)
        {
            entry.Players.Add(new BattleHistoryPlayer
            {
                Name = p.Name,
                Clan = p.ClanTag ?? string.Empty,
                Ship = ShipCatalog.Current.GetName(p.ShipId),
                Relation = p.Relation == Relation.Enemy ? "敌军" : "友军",
                Component = "-",
                Weighted = p.Statistics.WeightedWinrate,
                Winrate = p.Statistics.Winrate,
            });
        }

        BattleHistoryRepository.Current.Add(entry);
    }

    private static double ParsePct(string value)
        => double.TryParse(value.TrimEnd('%'), out double v) ? v / 100 : 0;

    [System.Runtime.InteropServices.DllImport("psapi.dll", SetLastError = true)]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool EmptyWorkingSet(IntPtr hProcess);

    /// <summary>把工作集交还系统，降低常驻内存观感。</summary>
    private static void TrimWorkingSet()
    {
        try
        {
            using var process = System.Diagnostics.Process.GetCurrentProcess();
            _ = EmptyWorkingSet(process.Handle);
        }
        catch
        {
        }
    }

    /// <summary>供外部（如窗口最小化时）触发工作集压缩。</summary>
    public static void TrimWorkingSetPublic() => TrimWorkingSet();

    private void ApplySettings()
    {
        var s = AppSettings.Current;
        settings.ProxyBaseUrl = string.IsNullOrWhiteSpace(s.ProxyBaseUrl) ? null : s.ProxyBaseUrl;
        settings.RequestDelayMs = Math.Max(0, s.RequestDelayMs);
        settings.MaximumParallelRequests = Math.Max(1, s.ParallelRequests);
settings.Server = ServerExtensions.FromSettingsIndex(s.ServerIndex);
    }

    private static IEnumerable<Participant> Sort(IEnumerable<Participant> participants)
    {
        int mode = AppSettings.Current.SortMode;
        return participants
            .OrderBy(p => ShipCatalog.Current.GetTypeOrder(p.ShipId))
            .ThenByDescending(p => mode switch
            {
                1 => p.Statistics.Winrate,
                2 => p.Statistics.Battles,
                _ => p.Statistics.WeightedWinrate,
            });
    }

    private static (string Battles, string Winrate, string Weighted) Averages(IEnumerable<ParticipantRow> rows)
    {
        var list = rows.Where(r => r.WeightedValue >= 0).ToList();
        if (list.Count == 0)
        {
            return ("-", "-", "-");
        }

        double avgBattles = list.Average(r => r.BattlesValue);
        double avgWinrate = list.Average(r => r.WinrateValue);
        double avgWeighted = list.Average(r => r.WeightedValue);
        return (
            $"{avgBattles:0}",
            $"{avgWinrate:P1}",
            $"{avgWeighted:P1}");
    }

    private static string ModeLabel(string gameType, string matchGroup)
    {
        if (!string.IsNullOrEmpty(gameType))
        {
            string label = gameType switch
            {
                "RandomBattle" => "标准战",
                "RankedBattle" => "排位战",
                "ClanBattle" => "军团战",
                "CooperativeBattle" => "联合作战",
                "CoopBattle" => "联合作战",
                "ScenarioBattle" => "剧情",
                "OperationBattle" => "剧情",
                "TrainingBattle" => "训练",
                _ => gameType,
            };
            if (!string.Equals(label, "pvp", StringComparison.OrdinalIgnoreCase))
            {
                return label;
            }
        }

        return matchGroup.ToLowerInvariant() switch
        {
            "pvp" => "标准战",
            "ranked" => "排位战",
            "clan" => "军团战",
            "coop" => "联合作战",
            "scenario" => "剧情",
            _ => "对局",
        };
    }

    private void SetState(string status, string summary, bool busy, double progress)
    {
        Status = status;
        Summary = summary;
        IsBusy = busy;
        Progress = progress;
        ActivityText = busy ? status : string.Empty;
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Status)));
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Summary)));
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(IsBusy)));
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Progress)));
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(ActivityText)));
        Changed?.Invoke(this, EventArgs.Empty);
    }
}

