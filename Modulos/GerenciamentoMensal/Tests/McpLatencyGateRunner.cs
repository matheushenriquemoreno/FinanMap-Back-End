using System.Diagnostics;

namespace Tests;

public sealed record McpLatencySummary(
    string Operation,
    int Samples,
    double P50Milliseconds,
    double P95Milliseconds,
    double P99Milliseconds);

public static class McpLatencyGateRunner
{
    public static async Task<McpLatencySummary> MeasureAsync(
        string operation,
        int warmupCount,
        int sampleCount,
        Func<CancellationToken, Task> execute,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);
        ArgumentNullException.ThrowIfNull(execute);
        if (warmupCount < 0)
            throw new ArgumentOutOfRangeException(nameof(warmupCount));
        if (sampleCount < 20)
            throw new ArgumentOutOfRangeException(
                nameof(sampleCount),
                "Use pelo menos 20 amostras para um P95 estável.");

        for (var index = 0; index < warmupCount; index++)
            await execute(cancellationToken);

        var durations = new double[sampleCount];
        for (var index = 0; index < sampleCount; index++)
        {
            var startedAt = Stopwatch.GetTimestamp();
            await execute(cancellationToken);
            durations[index] = Stopwatch
                .GetElapsedTime(startedAt)
                .TotalMilliseconds;
        }

        return Summarize(operation, durations);
    }

    public static McpLatencySummary Summarize(
        string operation,
        IReadOnlyCollection<double> durationsMilliseconds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);
        if (durationsMilliseconds.Count == 0)
            throw new ArgumentException(
                "Ao menos uma duração é obrigatória.",
                nameof(durationsMilliseconds));
        var ordered = durationsMilliseconds.Order().ToArray();
        return new McpLatencySummary(
            operation,
            ordered.Length,
            Percentile(ordered, 0.50),
            Percentile(ordered, 0.95),
            Percentile(ordered, 0.99));
    }

    private static double Percentile(double[] ordered, double percentile)
    {
        var rank = (int)Math.Ceiling(percentile * ordered.Length);
        return ordered[Math.Clamp(rank - 1, 0, ordered.Length - 1)];
    }
}
