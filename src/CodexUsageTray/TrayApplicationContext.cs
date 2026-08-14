namespace CodexUsageTray;

public sealed class TrayApplicationContext : ApplicationContext
{
    private readonly SynchronizationContext uiContext;
    private readonly CodexUsageLogReader reader;
    private readonly CodexAccountRateLimitReader accountReader;
    private readonly UsageFileWatcher watcher;
    private readonly System.Threading.Timer accountRefreshTimer;
    private readonly NotifyIcon notifyIcon;
    private readonly ToolStripMenuItem statusItem;
    private readonly ToolStripMenuItem refreshItem;
    private readonly CancellationTokenSource shutdown = new();
    private UsageSnapshot? latest;
    private Icon? currentIcon;
    private int refreshInProgress;
    private long lastAccountRefreshAttemptUtcTicks;

    private static readonly TimeSpan HoverRefreshAge = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan BackgroundRefreshInterval = TimeSpan.FromMinutes(15);

    public TrayApplicationContext()
    {
        uiContext = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
        var sessionsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".codex",
            "sessions");

        reader = new CodexUsageLogReader(sessionsDirectory);
        accountReader = new CodexAccountRateLimitReader();
        watcher = new UsageFileWatcher(sessionsDirectory);
        watcher.UsageFileChanged += OnUsageFileChanged;

        statusItem = new ToolStripMenuItem("正在讀取 Codex 7d 額度…") { Enabled = false };
        refreshItem = new ToolStripMenuItem("立即重新讀取", null, async (_, _) => await RefreshAllAsync());
        var exitItem = new ToolStripMenuItem("結束", null, (_, _) => ExitThread());
        var menu = new ContextMenuStrip();
        menu.Items.Add(statusItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(refreshItem);
        menu.Items.Add(exitItem);

        currentIcon = TrayIconRenderer.Render(null);
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
                _ = RefreshAllAsync();
            }
        };

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

            if (latest?.AvailableResetCredits is not null)
            {
                snapshot = snapshot with { AvailableResetCredits = latest.AvailableResetCredits };
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
            var snapshot = await accountReader.ReadAsync(shutdown.Token) ??
                           await reader.FindLatestAsync(shutdown.Token);
            PostToUi(() =>
            {
                if (snapshot is null && latest is null)
                {
                    statusItem.Text = "尚未找到 Codex 7d 額度資料";
                    notifyIcon.Text = "Codex 7d 額度：尚無資料";
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
        statusItem.Text = $"Codex 7d 可用 {snapshot.RemainingPercent}%";
        UpdateTooltip();

        var nextIcon = TrayIconRenderer.Render(snapshot.RemainingPercent);
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
        watcher.UsageFileChanged -= OnUsageFileChanged;
        watcher.Dispose();
        accountRefreshTimer.Dispose();
        notifyIcon.Visible = false;
        notifyIcon.Dispose();
        currentIcon?.Dispose();
        shutdown.Dispose();
        base.ExitThreadCore();
    }
}
