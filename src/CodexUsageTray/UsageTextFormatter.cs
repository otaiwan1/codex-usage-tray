using System.Globalization;

namespace CodexUsageTray;

public static class UsageTextFormatter
{
    public static string FormatTooltip(UsageSnapshot snapshot, DateTimeOffset now)
    {
        var fiveHour = FormatLimit("5h", snapshot.FiveHourLimit, now);
        var sevenDay = FormatLimit("7d", snapshot.SevenDayLimit, now);
        var resets = FormatAvailableResets(snapshot.AvailableResetCredits);
        var updated = snapshot.ReportedAt.ToLocalTime().ToString("MM/dd HH:mm:ss", CultureInfo.InvariantCulture);
        return Shorten($"{fiveHour}\n{sevenDay}\n可用重置{resets} 更新{updated}", 63);
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

    private static string FormatLimit(string label, UsageLimit? limit, DateTimeOffset now)
    {
        if (limit is null)
        {
            return $"{label} 尚無資料";
        }

        var reset = limit.ResetsAt is null ? "未知" : FormatRemaining(limit.ResetsAt.Value - now);
        return $"{label} 可用 {limit.RemainingPercent}% 重置 {reset}";
    }

    private static string Shorten(string value, int maximumLength) =>
        value.Length <= maximumLength ? value : $"{value[..(maximumLength - 1)]}…";

    private static string FormatAvailableResets(long? availableResets) => availableResets switch
    {
        null => "—",
        > 999 => "999+",
        _ => availableResets.Value.ToString(CultureInfo.InvariantCulture)
    };
}
