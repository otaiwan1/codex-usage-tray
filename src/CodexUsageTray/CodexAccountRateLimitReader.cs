using System.Diagnostics;
using System.ComponentModel;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace CodexUsageTray;

public sealed class CodexAccountRateLimitReader
{
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(20);
    private readonly IReadOnlyList<string>? configuredExecutableCandidates;

    public CodexAccountRateLimitReader(IEnumerable<string>? executableCandidates = null)
    {
        configuredExecutableCandidates = executableCandidates?.ToArray();
    }

    public async Task<UsageSnapshot?> ReadAsync(CancellationToken cancellationToken = default)
    {
        var candidates = (configuredExecutableCandidates ?? FindExecutableCandidates().ToArray())
            .Where(File.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase);
        foreach (var executable in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var snapshot = await ReadFromProcessAsync(executable, cancellationToken);
                if (snapshot is not null)
                {
                    return snapshot;
                }
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // This candidate timed out. Try another installed Codex executable if present.
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or Win32Exception)
            {
                // An inaccessible or incompatible candidate is expected on some packaged installs.
            }
        }

        return null;
    }

    private static async Task<UsageSnapshot?> ReadFromProcessAsync(
        string executable,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(RequestTimeout);

        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardInputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            StandardOutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            StandardErrorEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)
        };
        startInfo.ArgumentList.Add("app-server");
        startInfo.ArgumentList.Add("--listen");
        startInfo.ArgumentList.Add("stdio://");

        using var process = new Process { StartInfo = startInfo };
        if (!process.Start())
        {
            return null;
        }

        var stderrDrain = process.StandardError.ReadToEndAsync();
        try
        {
            await WriteMessageAsync(process, new
            {
                method = "initialize",
                id = 1,
                @params = new
                {
                    clientInfo = new
                    {
                        name = "codex_usage_tray",
                        title = "Codex Usage Tray",
                        version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "unknown"
                    }
                }
            }, timeout.Token);

            if (await ReadResponseAsync(process, 1, timeout.Token) is null)
            {
                return null;
            }

            await WriteMessageAsync(process, new { method = "initialized" }, timeout.Token);
            await WriteMessageAsync(process, new { method = "account/rateLimits/read", id = 2 }, timeout.Token);
            var response = await ReadResponseAsync(process, 2, timeout.Token);
            if (response is null)
            {
                return null;
            }

            return CodexAccountRateLimitParser.TryParseResponse(
                response,
                DateTimeOffset.UtcNow,
                out var snapshot)
                ? snapshot
                : null;
        }
        finally
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch (InvalidOperationException)
            {
                // The short-lived process exited between the state check and the kill request.
            }

            try
            {
                await stderrDrain.WaitAsync(TimeSpan.FromSeconds(1));
            }
            catch (TimeoutException)
            {
                // Never delay shutdown for diagnostic output that the application does not retain.
            }
        }
    }

    private static async Task WriteMessageAsync(
        Process process,
        object message,
        CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(message);
        await process.StandardInput.WriteLineAsync(json.AsMemory(), cancellationToken);
        await process.StandardInput.FlushAsync(cancellationToken);
    }

    private static async Task<string?> ReadResponseAsync(
        Process process,
        int expectedId,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            var line = await process.StandardOutput.ReadLineAsync(cancellationToken);
            if (line is null)
            {
                return null;
            }

            try
            {
                using var document = JsonDocument.Parse(line);
                var root = document.RootElement;
                if (root.TryGetProperty("id", out var id) &&
                    id.ValueKind == JsonValueKind.Number &&
                    id.TryGetInt32(out var responseId) &&
                    responseId == expectedId)
                {
                    return root.TryGetProperty("error", out _) ? null : line;
                }
            }
            catch (JsonException)
            {
                // Ignore non-protocol stdout and keep waiting for the requested response.
            }
        }
    }

    private static IEnumerable<string> FindExecutableCandidates()
    {
        var configured = Environment.GetEnvironmentVariable("CODEX_USAGE_TRAY_CODEX_EXE");
        if (!string.IsNullOrWhiteSpace(configured))
        {
            yield return Environment.ExpandEnvironmentVariables(configured.Trim('"'));
        }

        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        yield return Path.Combine(profile, ".codex", ".sandbox-bin", "codex.exe");

        var path = Environment.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrWhiteSpace(path))
        {
            foreach (var directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                string? candidate = null;
                try
                {
                    candidate = Path.Combine(directory.Trim().Trim('"'), "codex.exe");
                }
                catch (ArgumentException)
                {
                    // Ignore malformed PATH entries.
                }

                if (candidate is not null)
                {
                    yield return candidate;
                }
            }
        }
    }
}
