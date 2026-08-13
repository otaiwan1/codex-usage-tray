using System.Text;

namespace CodexUsageTray;

public sealed class CodexUsageLogReader
{
    private const int MaxFilesToScan = 24;
    private const int MaxTailBytes = 512 * 1024;

    public CodexUsageLogReader(string sessionsDirectory)
    {
        SessionsDirectory = sessionsDirectory;
    }

    public string SessionsDirectory { get; }

    public async Task<UsageSnapshot?> FindLatestAsync(CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(SessionsDirectory))
        {
            return null;
        }

        var files = Directory.EnumerateFiles(SessionsDirectory, "*.jsonl", SearchOption.AllDirectories)
            .Select(path => new FileInfo(path))
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .Take(MaxFilesToScan)
            .ToArray();

        UsageSnapshot? latest = null;
        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var candidate = await ReadLatestFromFileAsync(file.FullName, cancellationToken);
            if (candidate is not null && (latest is null || candidate.ReportedAt > latest.ReportedAt))
            {
                latest = candidate;
            }
        }

        return latest;
    }

    public async Task<UsageSnapshot?> ReadLatestFromFileAsync(
        string path,
        CancellationToken cancellationToken = default,
        int maximumTailBytes = MaxTailBytes)
    {
        try
        {
            await using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete,
                bufferSize: 16 * 1024,
                options: FileOptions.Asynchronous | FileOptions.SequentialScan);

            var boundedTailBytes = Math.Clamp(maximumTailBytes, 4 * 1024, MaxTailBytes);
            var start = Math.Max(0, stream.Length - boundedTailBytes);
            stream.Seek(start, SeekOrigin.Begin);
            using var reader = new StreamReader(stream, Encoding.UTF8, true, 16 * 1024, leaveOpen: false);

            if (start > 0)
            {
                _ = await reader.ReadLineAsync(cancellationToken);
            }

            var tail = await reader.ReadToEndAsync(cancellationToken);
            foreach (var line in tail.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Reverse())
            {
                if (CodexUsageParser.TryParseLine(line, out var snapshot))
                {
                    return snapshot;
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Codex may rotate or still hold a session file. A later file event retries it.
        }

        return null;
    }
}
