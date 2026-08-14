namespace CodexUsageTray;

public sealed record UsageSnapshot(
    double UsedPercent,
    DateTimeOffset? ResetsAt,
    DateTimeOffset ReportedAt,
    string? CreditBalance,
    bool UnlimitedCredits,
    string? PlanType,
    long? AvailableResetCredits = null)
{
    public int RemainingPercent => (int)Math.Round(
        Math.Clamp(100d - UsedPercent, 0d, 100d),
        MidpointRounding.AwayFromZero);
}
