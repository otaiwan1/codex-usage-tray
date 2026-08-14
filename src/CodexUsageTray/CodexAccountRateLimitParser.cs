using System.Text.Json;

namespace CodexUsageTray;

public static class CodexAccountRateLimitParser
{
    private const int SevenDaysInMinutes = 7 * 24 * 60;

    public static bool TryParseResponse(
        string response,
        DateTimeOffset reportedAt,
        out UsageSnapshot? snapshot)
    {
        snapshot = null;
        if (string.IsNullOrWhiteSpace(response))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(response);
            var root = document.RootElement;
            if (!root.TryGetProperty("result", out var result) ||
                result.ValueKind != JsonValueKind.Object ||
                !TrySelectCodexLimits(result, out var rateLimits))
            {
                return false;
            }

            var window = FindSevenDayWindow(rateLimits);
            if (window is null || !TryGetDouble(window.Value, "usedPercent", out var usedPercent))
            {
                return false;
            }

            DateTimeOffset? resetsAt = null;
            if (TryGetInt64(window.Value, "resetsAt", out var resetsAtUnix))
            {
                resetsAt = DateTimeOffset.FromUnixTimeSeconds(resetsAtUnix);
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

            string? planType = null;
            if (rateLimits.TryGetProperty("planType", out var planTypeElement) &&
                planTypeElement.ValueKind == JsonValueKind.String)
            {
                planType = planTypeElement.GetString();
            }

            long? availableResetCredits = null;
            if (result.TryGetProperty("rateLimitResetCredits", out var resetCredits) &&
                resetCredits.ValueKind == JsonValueKind.Object &&
                TryGetInt64(resetCredits, "availableCount", out var availableCount))
            {
                availableResetCredits = availableCount;
            }

            snapshot = new UsageSnapshot(
                usedPercent,
                resetsAt,
                reportedAt,
                balance,
                unlimited,
                planType,
                availableResetCredits);
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

    private static bool TrySelectCodexLimits(JsonElement result, out JsonElement rateLimits)
    {
        if (result.TryGetProperty("rateLimitsByLimitId", out var limitsById) &&
            limitsById.ValueKind == JsonValueKind.Object &&
            limitsById.TryGetProperty("codex", out var codexLimits) &&
            codexLimits.ValueKind == JsonValueKind.Object)
        {
            rateLimits = codexLimits;
            return true;
        }

        if (result.TryGetProperty("rateLimits", out var legacyLimits) &&
            legacyLimits.ValueKind == JsonValueKind.Object &&
            (!legacyLimits.TryGetProperty("limitId", out var limitId) ||
             limitId.ValueKind == JsonValueKind.Null ||
             (limitId.ValueKind == JsonValueKind.String && limitId.GetString() == "codex")))
        {
            rateLimits = legacyLimits;
            return true;
        }

        rateLimits = default;
        return false;
    }

    private static JsonElement? FindSevenDayWindow(JsonElement rateLimits)
    {
        foreach (var name in new[] { "primary", "secondary" })
        {
            if (rateLimits.TryGetProperty(name, out var candidate) &&
                candidate.ValueKind == JsonValueKind.Object &&
                TryGetInt64(candidate, "windowDurationMins", out var minutes) &&
                minutes == SevenDaysInMinutes)
            {
                return candidate;
            }
        }

        return null;
    }

    private static bool TryGetDouble(JsonElement element, string name, out double value)
    {
        value = 0;
        return element.TryGetProperty(name, out var property) &&
               property.ValueKind == JsonValueKind.Number &&
               property.TryGetDouble(out value);
    }

    private static bool TryGetInt64(JsonElement element, string name, out long value)
    {
        value = 0;
        return element.TryGetProperty(name, out var property) &&
               property.ValueKind == JsonValueKind.Number &&
               property.TryGetInt64(out value);
    }
}
