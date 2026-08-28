using System.Globalization;
using System.Text.Json;

namespace CodexUsageTray;

public static class CodexUsageParser
{
    private const int FiveHoursInMinutes = 5 * 60;
    private const int SevenDaysInMinutes = 7 * 24 * 60;

    public static bool TryParseLine(string line, out UsageSnapshot? snapshot)
    {
        snapshot = null;

        if (string.IsNullOrWhiteSpace(line) ||
            !line.Contains("\"rate_limits\"", StringComparison.Ordinal))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;

            if (!TryGetString(root, "type", out var envelopeType) || envelopeType != "event_msg" ||
                !root.TryGetProperty("payload", out var payload) ||
                !TryGetString(payload, "type", out var payloadType) || payloadType != "token_count" ||
                !payload.TryGetProperty("rate_limits", out var rateLimits))
            {
                return false;
            }

            var fiveHourLimit = FindWindow(rateLimits, FiveHoursInMinutes);
            var sevenDayLimit = FindWindow(rateLimits, SevenDaysInMinutes);
            if (fiveHourLimit is null && sevenDayLimit is null)
            {
                return false;
            }

            var reportedAt = DateTimeOffset.UtcNow;
            if (TryGetString(root, "timestamp", out var timestamp) &&
                DateTimeOffset.TryParse(timestamp, CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal, out var parsedTimestamp))
            {
                reportedAt = parsedTimestamp;
            }

            string? balance = null;
            var unlimited = false;
            if (rateLimits.TryGetProperty("credits", out var credits) &&
                credits.ValueKind == JsonValueKind.Object)
            {
                if (credits.TryGetProperty("balance", out var balanceElement))
                {
                    balance = balanceElement.ValueKind switch
                    {
                        JsonValueKind.String => balanceElement.GetString(),
                        JsonValueKind.Number => balanceElement.GetRawText(),
                        _ => null
                    };
                }

                if (credits.TryGetProperty("unlimited", out var unlimitedElement) &&
                    unlimitedElement.ValueKind is JsonValueKind.True or JsonValueKind.False)
                {
                    unlimited = unlimitedElement.GetBoolean();
                }
            }

            TryGetString(rateLimits, "plan_type", out var planType);
            snapshot = new UsageSnapshot(
                fiveHourLimit,
                sevenDayLimit,
                reportedAt,
                balance,
                unlimited,
                planType);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    private static UsageLimit? FindWindow(JsonElement rateLimits, int windowDurationMinutes)
    {
        foreach (var name in new[] { "primary", "secondary" })
        {
            if (!rateLimits.TryGetProperty(name, out var candidate) ||
                candidate.ValueKind != JsonValueKind.Object ||
                !TryGetInt64(candidate, "window_minutes", out var minutes) ||
                minutes != windowDurationMinutes ||
                !TryGetDouble(candidate, "used_percent", out var usedPercent))
            {
                continue;
            }

            DateTimeOffset? resetsAt = null;
            if (TryGetInt64(candidate, "resets_at", out var resetsAtUnix))
            {
                resetsAt = DateTimeOffset.FromUnixTimeSeconds(resetsAtUnix);
            }

            return new UsageLimit(usedPercent, resetsAt);
        }

        return null;
    }

    private static bool TryGetString(JsonElement element, string name, out string? value)
    {
        value = null;
        if (!element.TryGetProperty(name, out var property) || property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = property.GetString();
        return value is not null;
    }

    private static bool TryGetDouble(JsonElement element, string name, out double value)
    {
        value = 0;
        return element.TryGetProperty(name, out var property) && property.TryGetDouble(out value);
    }

    private static bool TryGetInt64(JsonElement element, string name, out long value)
    {
        value = 0;
        return element.TryGetProperty(name, out var property) && property.TryGetInt64(out value);
    }
}
