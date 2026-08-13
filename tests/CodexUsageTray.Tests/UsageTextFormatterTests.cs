namespace CodexUsageTray.Tests;

public sealed class UsageTextFormatterTests
{
    [Fact]
    public void FormatsResetCountdownWithoutPolling()
    {
        var now = new DateTimeOffset(2026, 8, 13, 12, 0, 0, TimeSpan.Zero);

        Assert.Equal("2天3時", UsageTextFormatter.FormatRemaining(TimeSpan.FromHours(51)));
        Assert.Equal("2時15分", UsageTextFormatter.FormatRemaining(TimeSpan.FromMinutes(135)));
        Assert.Equal("待更新", UsageTextFormatter.FormatRemaining(TimeSpan.Zero));

        var snapshot = new UsageSnapshot(2, now.AddHours(51), now, "0", false, "plus");
        var tooltip = UsageTextFormatter.FormatTooltip(snapshot, now);
        Assert.Contains("7d 可用 98%", tooltip);
        Assert.Contains("重置 2天3時", tooltip);
        Assert.Contains("Credits 0", tooltip);
        Assert.True(tooltip.Length <= 63);
    }

    [Fact]
    public void TooltipAlwaysFitsNotifyIconLimit()
    {
        var now = DateTimeOffset.UtcNow;
        var snapshot = new UsageSnapshot(
            0,
            now.AddDays(7),
            now,
            new string('9', 200),
            false,
            "plus");

        Assert.True(UsageTextFormatter.FormatTooltip(snapshot, now).Length <= 63);
    }
}
