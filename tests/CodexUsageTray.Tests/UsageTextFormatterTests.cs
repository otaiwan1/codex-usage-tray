namespace CodexUsageTray.Tests;

public sealed class UsageTextFormatterTests
{
    [Fact]
    public void FormatsBothWindowsWithMultilineDetails()
    {
        var now = new DateTimeOffset(2026, 8, 13, 12, 0, 0, TimeSpan.Zero);

        Assert.Equal("2天3時", UsageTextFormatter.FormatRemaining(TimeSpan.FromHours(51)));
        Assert.Equal("2時15分", UsageTextFormatter.FormatRemaining(TimeSpan.FromMinutes(135)));
        Assert.Equal("待更新", UsageTextFormatter.FormatRemaining(TimeSpan.Zero));

        var snapshot = new UsageSnapshot(
            new UsageLimit(34, now.AddHours(2)),
            new UsageLimit(2, now.AddHours(51)),
            now,
            "0",
            false,
            "plus",
            2);
        var tooltip = UsageTextFormatter.FormatTooltip(snapshot, now);

        Assert.Contains("5h 可用 66% 重置 2時0分", tooltip);
        Assert.Contains("7d 可用 98% 重置 2天3時", tooltip);
        Assert.Contains("可用重置2", tooltip);
        Assert.Contains($"更新{now.ToLocalTime():MM/dd HH:mm:ss}", tooltip);
        Assert.Equal(3, tooltip.Split('\n').Length);
        Assert.DoesNotContain(" | ", tooltip);
        Assert.True(tooltip.Length <= 63);
    }

    [Fact]
    public void TooltipAlwaysListsBothWindowsWhenOneIsUnavailable()
    {
        var now = DateTimeOffset.UtcNow;
        var snapshot = new UsageSnapshot(
            null,
            new UsageLimit(0, now.AddDays(7)),
            now,
            new string('9', 200),
            false,
            "plus");

        var tooltip = UsageTextFormatter.FormatTooltip(snapshot, now);

        Assert.Contains("5h 尚無資料", tooltip);
        Assert.Contains("7d 可用 100%", tooltip);
        Assert.True(tooltip.Length <= 63);
    }

    [Fact]
    public void TooltipKeepsUpdateSecondsWhenResetCountIsUnexpectedlyLarge()
    {
        var now = new DateTimeOffset(2026, 8, 14, 2, 3, 4, TimeSpan.Zero);
        var snapshot = new UsageSnapshot(
            new UsageLimit(11, now.AddHours(4)),
            new UsageLimit(11, now.AddDays(6)),
            now,
            null,
            false,
            "plus",
            long.MaxValue);

        var tooltip = UsageTextFormatter.FormatTooltip(snapshot, now);

        Assert.Contains("可用重置999+", tooltip);
        Assert.Contains($"更新{now.ToLocalTime():MM/dd HH:mm:ss}", tooltip);
        Assert.True(tooltip.Length <= 63);
    }

    [Fact]
    public void UsagePeriodToggleAlternatesBetweenBothWindows()
    {
        Assert.Equal(UsagePeriod.FiveHours, UsagePeriod.SevenDays.Toggle());
        Assert.Equal(UsagePeriod.SevenDays, UsagePeriod.FiveHours.Toggle());
    }
}
