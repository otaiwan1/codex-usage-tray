namespace CodexUsageTray;

public enum UsagePeriod
{
    FiveHours,
    SevenDays
}

public static class UsagePeriodExtensions
{
    public static string ToDisplayLabel(this UsagePeriod period) => period switch
    {
        UsagePeriod.FiveHours => "5h",
        UsagePeriod.SevenDays => "7d",
        _ => throw new ArgumentOutOfRangeException(nameof(period))
    };

    public static UsagePeriod Toggle(this UsagePeriod period) => period switch
    {
        UsagePeriod.FiveHours => UsagePeriod.SevenDays,
        UsagePeriod.SevenDays => UsagePeriod.FiveHours,
        _ => throw new ArgumentOutOfRangeException(nameof(period))
    };
}

public sealed record UsageLimit(
    double UsedPercent,
    DateTimeOffset? ResetsAt)
{
    public int RemainingPercent => (int)Math.Round(
        Math.Clamp(100d - UsedPercent, 0d, 100d),
        MidpointRounding.AwayFromZero);
}

public sealed record UsageSnapshot(
    UsageLimit? FiveHourLimit,
    UsageLimit? SevenDayLimit,
    DateTimeOffset ReportedAt,
    string? CreditBalance,
    bool UnlimitedCredits,
    string? PlanType,
    long? AvailableResetCredits = null)
{
    public bool HasBothWindows => FiveHourLimit is not null && SevenDayLimit is not null;

    public UsagePeriod ResolvePeriod(UsagePeriod preferred) => GetLimit(preferred) is not null
        ? preferred
        : GetLimit(preferred.Toggle()) is not null ? preferred.Toggle() : preferred;

    public UsageLimit? GetLimit(UsagePeriod period) => period switch
    {
        UsagePeriod.FiveHours => FiveHourLimit,
        UsagePeriod.SevenDays => SevenDayLimit,
        _ => throw new ArgumentOutOfRangeException(nameof(period))
    };
}
