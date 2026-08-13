using System.Globalization;

namespace CodexUsageTray;

public static class UsageTextFormatter
{
    public static string FormatTooltip(UsageSnapshot snapshot, DateTimeOffset now)
    {
        var reset = FormatRemaining(snapshot.ResetsAt - now);
        var credits = snapshot.UnlimitedCredits ? "∞" : Shorten(snapshot.CreditBalance ?? "—", 10);
        var updated = snapshot.ReportedAt.ToLocalTime().ToString("MM/dd HH:mm", CultureInfo.InvariantCulture);
        return Shorten($"7d 可用 {snapshot.RemainingPercent}% | 重置 {reset} | Credits {credits} | {updated}", 63);
    }

    public static string FormatRemaining(TimeSpan remaining)
    {
        if (remaining <= TimeSpan.Zero)
        {
            return "待更新";
        }

        if (remaining.TotalDays >= 1)
        {
            return $"{(int)remaining.TotalDays}天{remaining.Hours}時";
        }

        if (remaining.TotalHours >= 1)
        {
            return $"{(int)remaining.TotalHours}時{remaining.Minutes}分";
        }

        return $"{Math.Max(1, remaining.Minutes)}分";
    }

    private static string Shorten(string value, int maximumLength) =>
        value.Length <= maximumLength ? value : $"{value[..(maximumLength - 1)]}…";
}
