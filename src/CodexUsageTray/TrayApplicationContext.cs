using Microsoft.Win32;

namespace CodexUsageTray;

public sealed class TrayApplicationContext : ApplicationContext
{
    private readonly SynchronizationContext uiContext;
    private readonly CodexUsageLogReader reader;
    private readonly CodexAccountRateLimitReader accountReader;
    private readonly UsageFileWatcher? watcher;
    private readonly MonitorSettings settings;
    private readonly System.Threading.Timer accountRefreshTimer;
    private readonly NotifyIcon notifyIcon;
    private readonly ToolStripMenuItem statusItem;
    private readonly ToolStripMenuItem refreshItem;
    private readonly CancellationTokenSource shutdown = new();
    private UsageSnapshot? latest;
    private Icon? currentIcon;
    private UsagePeriod selectedPeriod = UsagePeriod.SevenDays;
    private int refreshInProgress;
    private long lastAccountRefreshAttemptUtcTicks;

    private static readonly TimeSpan HoverRefreshAge = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan BackgroundRefreshInterval = TimeSpan.FromMinutes(15);

    public TrayApplicationContext()
    {
        uiContext = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
        settings = MonitorSettings.Load();
        var sessionsDirectory = Path.Combine(settings.CodexHome, "sessions");

        reader = new CodexUsageLogReader(sessionsDirectory);
        accountReader = new CodexAccountRateLimitReader(
            codexHome: settings.CodexHome,
            useIsolatedFileCredentials: settings.AccountSource == MonitorAccountSource.Separate);
        if (settings.UseLocalSessionFallback)
        {
            watcher = new UsageFileWatcher(sessionsDirectory);
            watcher.UsageFileChanged += OnUsageFileChanged;
        }

        statusItem = new ToolStripMenuItem("正在讀取 Codex 7d 額度…") { Enabled = false };
        refreshItem = new ToolStripMenuItem("立即重新讀取", null, async (_, _) => await RefreshAllAsync());
        var exitItem = new ToolStripMenuItem("結束", null, (_, _) => ExitThread());
        var menu = new ContextMenuStrip();
        menu.Items.Add(statusItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(refreshItem);
        menu.Items.Add(exitItem);

        currentIcon = RenderIcon(null, selectedPeriod, showPeriodRibbon: false);
        notifyIcon = new NotifyIcon
        {
            Icon = currentIcon,
            Text = "Codex 7d 額度：正在讀取",
            ContextMenuStrip = menu,
            Visible = true
        };
        notifyIcon.MouseMove += (_, _) =>
        {
            UpdateTooltip();
            var lastAttempt = new DateTimeOffset(
                Interlocked.Read(ref lastAccountRefreshAttemptUtcTicks),
                TimeSpan.Zero);
            if (DateTimeOffset.UtcNow - lastAttempt >= HoverRefreshAge)
            {
                _ = RefreshAllAsync();
            }
        };
        notifyIcon.MouseClick += (_, eventArgs) =>
        {
            if (eventArgs.Button == MouseButtons.Left)
            {
                if (latest?.HasBothWindows == true)
                {
                    selectedPeriod = selectedPeriod.Toggle();
                }
                UpdateDisplay();
                _ = RefreshAllAsync();
            }
        };

        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;

        accountRefreshTimer = new System.Threading.Timer(
            _ => _ = RefreshAllAsync(),
            null,
            BackgroundRefreshInterval,
            BackgroundRefreshInterval);

        _ = RefreshAllAsync();
    }

    private void OnUsageFileChanged(object? sender, string? path)
    {
        _ = path is null ? RefreshAllAsync() : UpdateFromChangedFileAsync(path);
    }

    private void OnUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs eventArgs) =>
        PostToUi(UpdateDisplay);

    private void OnDisplaySettingsChanged(object? sender, EventArgs eventArgs) =>
        PostToUi(UpdateDisplay);

    private async Task UpdateFromChangedFileAsync(string? path)
    {
        if (shutdown.IsCancellationRequested)
        {
            return;
        }

        try
        {
            var snapshot = await reader.ReadLatestFromFileAsync(path!, shutdown.Token, 64 * 1024);
            if (snapshot is null || (latest is not null && snapshot.ReportedAt < latest.ReportedAt))
            {
                return;
            }

            if (latest is not null)
            {
                snapshot = snapshot with
                {
                    FiveHourLimit = snapshot.FiveHourLimit ?? latest.FiveHourLimit,
                    SevenDayLimit = snapshot.SevenDayLimit ?? latest.SevenDayLimit,
                    AvailableResetCredits = latest.AvailableResetCredits ?? snapshot.AvailableResetCredits
                };
            }

            PostToUi(() => ApplySnapshot(snapshot));
        }
        catch (OperationCanceledException)
        {
            // Normal during application shutdown.
        }
    }

    private async Task RefreshAllAsync()
    {
        if (Interlocked.Exchange(ref refreshInProgress, 1) != 0)
        {
            return;
        }

        PostToUi(() => refreshItem.Enabled = false);
        try
        {
            Interlocked.Exchange(ref lastAccountRefreshAttemptUtcTicks, DateTimeOffset.UtcNow.UtcTicks);
            var snapshot = await accountReader.ReadAsync(shutdown.Token);
            if (snapshot is null && settings.UseLocalSessionFallback)
            {
                snapshot = await reader.FindLatestAsync(shutdown.Token);
            }
            PostToUi(() =>
            {
                if (snapshot is null && latest is null)
                {
                    UpdateDisplay();
                }
                else if (snapshot is not null)
                {
                    ApplySnapshot(snapshot);
                }
            });
        }
        catch (OperationCanceledException)
        {
            // Normal during application shutdown.
        }
        finally
        {
            Interlocked.Exchange(ref refreshInProgress, 0);
            PostToUi(() => refreshItem.Enabled = true);
        }
    }

    private void ApplySnapshot(UsageSnapshot snapshot)
    {
        latest = snapshot;
        selectedPeriod = snapshot.ResolvePeriod(selectedPeriod);
        UpdateDisplay();
    }

    private void UpdateDisplay()
    {
        var label = selectedPeriod.ToDisplayLabel();
        var limit = latest?.GetLimit(selectedPeriod);
        statusItem.Text = limit is null
            ? $"尚未找到 Codex {label} 額度資料"
            : $"Codex {label} 可用 {limit.RemainingPercent}%";

        if (latest is null)
        {
            notifyIcon.Text = $"Codex {label} 額度：尚無資料";
        }
        else
        {
            UpdateTooltip();
        }

        var nextIcon = RenderIcon(limit?.RemainingPercent, selectedPeriod, latest?.HasBothWindows == true);
        notifyIcon.Icon = nextIcon;
        var previous = currentIcon;
        currentIcon = nextIcon;
        previous?.Dispose();
    }

    private void UpdateTooltip()
    {
        if (latest is not null)
        {
            notifyIcon.Text = UsageTextFormatter.FormatTooltip(latest, DateTimeOffset.UtcNow);
        }
    }

    private static Icon RenderIcon(int? remainingPercent, UsagePeriod period, bool showPeriodRibbon) =>
        TrayIconRenderer.Render(
            remainingPercent,
            period,
            showPeriodRibbon,
            lightTheme: TrayDisplaySettings.IsLightTheme(),
            size: TrayDisplaySettings.IconSize);

    private void PostToUi(Action action)
    {
        if (!shutdown.IsCancellationRequested)
        {
            uiContext.Post(_ => action(), null);
        }
    }

    protected override void ExitThreadCore()
    {
        shutdown.Cancel();
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        if (watcher is not null)
        {
            watcher.UsageFileChanged -= OnUsageFileChanged;
            watcher.Dispose();
        }
        accountRefreshTimer.Dispose();
        notifyIcon.Visible = false;
        notifyIcon.Dispose();
        currentIcon?.Dispose();
        shutdown.Dispose();
        base.ExitThreadCore();
    }
}
