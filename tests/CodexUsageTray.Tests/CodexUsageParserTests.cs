namespace CodexUsageTray.Tests;

public sealed class CodexUsageParserTests
{
    [Fact]
    public void ParsesSevenDayPrimaryWindowAndCredits()
    {
        const string json = """
            {"timestamp":"2026-08-13T12:54:37.952Z","type":"event_msg","payload":{"type":"token_count","rate_limits":{"primary":{"used_percent":12.4,"window_minutes":10080,"resets_at":1787197008},"secondary":null,"credits":{"unlimited":false,"balance":"3"},"plan_type":"plus"}}}
            """;

        var parsed = CodexUsageParser.TryParseLine(json, out var snapshot);

        Assert.True(parsed);
        Assert.NotNull(snapshot);
        Assert.Equal(88, snapshot.RemainingPercent);
        Assert.Equal("3", snapshot.CreditBalance);
        Assert.Equal("plus", snapshot.PlanType);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1787197008), snapshot.ResetsAt);
    }

    [Fact]
    public void SelectsSevenDaySecondaryWindow()
    {
        const string json = """
            {"timestamp":"2026-08-13T12:54:37Z","type":"event_msg","payload":{"type":"token_count","rate_limits":{"primary":{"used_percent":50,"window_minutes":300,"resets_at":1787190000},"secondary":{"used_percent":7,"window_minutes":10080,"resets_at":1787197008},"credits":{"unlimited":true,"balance":null}}}}
            """;

        Assert.True(CodexUsageParser.TryParseLine(json, out var snapshot));
        Assert.NotNull(snapshot);
        Assert.Equal(93, snapshot.RemainingPercent);
        Assert.True(snapshot.UnlimitedCredits);
    }

    [Theory]
    [InlineData(-5, 100)]
    [InlineData(101, 0)]
    [InlineData(33.5, 67)]
    public void RemainingPercentageIsRoundedAndClamped(double used, int expected)
    {
        var snapshot = new UsageSnapshot(used, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null, false, null);

        Assert.Equal(expected, snapshot.RemainingPercent);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("{\"type\":\"event_msg\",\"payload\":{\"type\":\"token_count\"}}")]
    [InlineData("{\"type\":\"event_msg\",\"payload\":{\"type\":\"token_count\",\"rate_limits\":{\"primary\":{\"used_percent\":2,\"window_minutes\":300,\"resets_at\":1}}}}")]
    public void RejectsMissingOrMalformedSevenDayUsage(string input)
    {
        Assert.False(CodexUsageParser.TryParseLine(input, out var snapshot));
        Assert.Null(snapshot);
    }
}
