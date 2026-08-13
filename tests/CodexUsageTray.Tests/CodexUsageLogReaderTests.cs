namespace CodexUsageTray.Tests;

public sealed class CodexUsageLogReaderTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), $"CodexUsageTrayTests-{Guid.NewGuid():N}");

    [Fact]
    public async Task FindsNewestReportAcrossSessionFiles()
    {
        Directory.CreateDirectory(directory);
        var older = Path.Combine(directory, "older.jsonl");
        var newerDirectory = Path.Combine(directory, "nested");
        Directory.CreateDirectory(newerDirectory);
        var newer = Path.Combine(newerDirectory, "newer.jsonl");

        await File.WriteAllTextAsync(older, Event("2026-08-12T12:00:00Z", 40));
        await File.WriteAllTextAsync(newer, $"{{\"type\":\"unrelated\"}}{Environment.NewLine}{Event("2026-08-13T12:00:00Z", 7)}");

        var reader = new CodexUsageLogReader(directory);
        var snapshot = await reader.FindLatestAsync();

        Assert.NotNull(snapshot);
        Assert.Equal(93, snapshot.RemainingPercent);
        Assert.Equal(new DateTimeOffset(2026, 8, 13, 12, 0, 0, TimeSpan.Zero), snapshot.ReportedAt);
    }

    [Fact]
    public async Task IgnoresPartialTrailingJsonLine()
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "session.jsonl");
        await File.WriteAllTextAsync(path, $"{Event("2026-08-13T12:00:00Z", 2)}{Environment.NewLine}{{\"unfinished\":");

        var reader = new CodexUsageLogReader(directory);
        var snapshot = await reader.ReadLatestFromFileAsync(path);

        Assert.NotNull(snapshot);
        Assert.Equal(98, snapshot.RemainingPercent);
    }

    private static string Event(string timestamp, double usedPercent) =>
        System.Text.Json.JsonSerializer.Serialize(new
        {
            timestamp,
            type = "event_msg",
            payload = new
            {
                type = "token_count",
                rate_limits = new
                {
                    primary = new
                    {
                        used_percent = usedPercent,
                        window_minutes = 10080,
                        resets_at = 1787197008
                    },
                    credits = new { unlimited = false, balance = "0" }
                }
            }
        });

    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
