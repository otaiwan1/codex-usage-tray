using System.Text.Json;

namespace CodexUsageTray;

public enum MonitorAccountSource
{
    Local,
    Separate
}

public sealed record MonitorSettings(MonitorAccountSource AccountSource, string CodexHome)
{
    public bool UseLocalSessionFallback => AccountSource == MonitorAccountSource.Local;

    public static string DefaultSettingsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CodexUsageTray", "monitor-settings.json");

    public static MonitorSettings Load(string? settingsPath = null)
    {
        var localHome = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex");
        settingsPath ??= DefaultSettingsPath;
        if (!File.Exists(settingsPath))
        {
            return new MonitorSettings(MonitorAccountSource.Local, localHome);
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(settingsPath));
            var root = document.RootElement;
            if (root.TryGetProperty("accountSource", out var source) &&
                source.ValueKind == JsonValueKind.String &&
                string.Equals(source.GetString(), "Local", StringComparison.OrdinalIgnoreCase))
            {
                return new MonitorSettings(MonitorAccountSource.Local, localHome);
            }

            if (root.TryGetProperty("accountSource", out source) &&
                source.ValueKind == JsonValueKind.String &&
                string.Equals(source.GetString(), "Separate", StringComparison.OrdinalIgnoreCase))
            {
                if (root.TryGetProperty("codexHome", out var home) &&
                    home.ValueKind == JsonValueKind.String &&
                    home.GetString() is { } homePath &&
                    Path.IsPathFullyQualified(homePath))
                {
                    return new MonitorSettings(MonitorAccountSource.Separate, Path.GetFullPath(homePath));
                }

                // A malformed separate-account setting must never silently show the local account.
                return new MonitorSettings(MonitorAccountSource.Separate, string.Empty);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            // A broken explicit configuration must not silently switch the monitored account.
        }

        return new MonitorSettings(MonitorAccountSource.Separate, string.Empty);
    }
}
