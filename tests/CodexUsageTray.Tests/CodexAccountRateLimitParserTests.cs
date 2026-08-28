namespace CodexUsageTray.Tests;

public sealed class CodexAccountRateLimitParserTests
{
    [Fact]
    public void PrefersCodexBucketAndReadsBothWindowsAndAvailableResetCount()
    {
        const string response = """
            {"id":2,"result":{"rateLimits":{"limitId":"other","primary":{"usedPercent":80,"windowDurationMins":10080,"resetsAt":1787190000}},"rateLimitsByLimitId":{"other":{"primary":{"usedPercent":80,"windowDurationMins":10080,"resetsAt":1787190000}},"codex":{"limitId":"codex","primary":{"usedPercent":3,"windowDurationMins":300,"resetsAt":1787190000},"secondary":{"usedPercent":11,"windowDurationMins":10080,"resetsAt":1787197008},"credits":{"unlimited":false,"balance":"7"},"planType":"plus"}},"rateLimitResetCredits":{"availableCount":2}}}
            """;
        var reportedAt = new DateTimeOffset(2026, 8, 14, 1, 0, 0, TimeSpan.Zero);

        var parsed = CodexAccountRateLimitParser.TryParseResponse(response, reportedAt, out var snapshot);

        Assert.True(parsed);
        Assert.NotNull(snapshot);
        Assert.NotNull(snapshot.FiveHourLimit);
        Assert.Equal(97, snapshot.FiveHourLimit.RemainingPercent);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1787190000), snapshot.FiveHourLimit.ResetsAt);
        Assert.NotNull(snapshot.SevenDayLimit);
        Assert.Equal(89, snapshot.SevenDayLimit.RemainingPercent);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1787197008), snapshot.SevenDayLimit.ResetsAt);
        Assert.Equal(2, snapshot.AvailableResetCredits);
        Assert.Equal("7", snapshot.CreditBalance);
        Assert.Equal("plus", snapshot.PlanType);
        Assert.Equal(reportedAt, snapshot.ReportedAt);
    }

    [Fact]
    public void AcceptsLegacyCodexBucketAndNullableResetTime()
    {
        const string response = """
            {"id":2,"result":{"rateLimits":{"limitId":"codex","primary":null,"secondary":{"usedPercent":25,"windowDurationMins":10080,"resetsAt":null}},"rateLimitResetCredits":null}}
            """;

        Assert.True(CodexAccountRateLimitParser.TryParseResponse(
            response,
            DateTimeOffset.UtcNow,
            out var snapshot));
        Assert.NotNull(snapshot);
        Assert.Null(snapshot.FiveHourLimit);
        Assert.NotNull(snapshot.SevenDayLimit);
        Assert.Equal(75, snapshot.SevenDayLimit.RemainingPercent);
        Assert.Null(snapshot.SevenDayLimit.ResetsAt);
        Assert.Null(snapshot.AvailableResetCredits);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("{\"id\":2,\"error\":{\"message\":\"failed\"}}")]
    [InlineData("{\"id\":2,\"result\":{\"rateLimits\":{\"limitId\":\"other\",\"secondary\":{\"usedPercent\":1,\"windowDurationMins\":10080}}}}")]
    public void RejectsMalformedOrNonCodexResponses(string response)
    {
        Assert.False(CodexAccountRateLimitParser.TryParseResponse(
            response,
            DateTimeOffset.UtcNow,
            out var snapshot));
        Assert.Null(snapshot);
    }
}
