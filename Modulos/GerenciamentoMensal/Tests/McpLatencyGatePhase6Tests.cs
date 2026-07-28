using Xunit;

namespace Tests;

public sealed class McpLatencyGatePhase6Tests
{
    [Fact]
    public void Runner_reports_repeatable_nearest_rank_percentiles()
    {
        var summary = McpLatencyGateRunner.Summarize(
            "fixture",
            Enumerable.Range(1, 100).Select(value => (double)value).ToArray());

        Assert.Equal(50, summary.P50Milliseconds);
        Assert.Equal(95, summary.P95Milliseconds);
        Assert.Equal(99, summary.P99Milliseconds);
    }
}
