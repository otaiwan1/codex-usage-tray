namespace CodexUsageTray.Tests;

public sealed class MonitorSettingsTests
{
    [Fact]
    public void MissingSettingsDefaultToLocalAccount()
    {
        var settings = MonitorSettings.Load(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "missing.json"));

        Assert.Equal(MonitorAccountSource.Local, settings.AccountSource);
        Assert.True(settings.UseLocalSessionFallback);
        Assert.EndsWith(".codex", settings.CodexHome, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SeparateAccountNeverUsesLocalSessionFallback()
    {
        var path = Path.Combine(Path.GetTempPath(), $"monitor-settings-{Guid.NewGuid():N}.json");
        var home = Path.Combine(Path.GetTempPath(), $"codex-home-{Guid.NewGuid():N}");
        try
        {
            File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(new
            {
                accountSource = "Separate",
                codexHome = home
            }));
            var settings = MonitorSettings.Load(path);

            Assert.Equal(MonitorAccountSource.Separate, settings.AccountSource);
            Assert.Equal(home, settings.CodexHome);
            Assert.False(settings.UseLocalSessionFallback);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void InvalidSeparateHomeFailsClosed()
    {
        var path = Path.Combine(Path.GetTempPath(), $"monitor-settings-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, "{\"accountSource\":\"Separate\",\"codexHome\":\"relative\"}");
            var settings = MonitorSettings.Load(path);

            Assert.Equal(MonitorAccountSource.Separate, settings.AccountSource);
            Assert.False(settings.UseLocalSessionFallback);
            Assert.Equal(string.Empty, settings.CodexHome);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void CorruptSettingsFailClosed()
    {
        var path = Path.Combine(Path.GetTempPath(), $"monitor-settings-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, "{");
            var settings = MonitorSettings.Load(path);

            Assert.Equal(MonitorAccountSource.Separate, settings.AccountSource);
            Assert.False(settings.UseLocalSessionFallback);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
