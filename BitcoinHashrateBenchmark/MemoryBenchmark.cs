using System.Diagnostics;

namespace BitcoinHashrateBenchmark;

internal enum MemoryBenchmarkKind
{
    Bandwidth,
    Latency
}

internal static class MemoryBenchmark
{
    private const int BandwidthElementCount = 8 * 1024 * 1024;
    private const int LatencyElementCount = 16 * 1024 * 1024;
    private const int ReportInterval = 65_536;
    private static long latencySink;

    public static Task<BenchmarkResult> RunAsync(
        MemoryBenchmarkKind kind,
        TimeSpan duration,
        IProgress<BenchmarkProgress> progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(progress);
        if (duration != Timeout.InfiniteTimeSpan && duration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(duration));
        }

        cancellationToken.ThrowIfCancellationRequested();
        return Task.Run(
            () => kind == MemoryBenchmarkKind.Bandwidth
                ? MeasureBandwidth(duration, progress, cancellationToken)
                : MeasureLatency(duration, progress, cancellationToken),
            CancellationToken.None);
    }

    private static BenchmarkResult MeasureBandwidth(
        TimeSpan duration,
        IProgress<BenchmarkProgress> progress,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var source = new long[BandwidthElementCount];
        var destination = new long[BandwidthElementCount];
        Array.Fill(source, 0x1234_5678);
        Array.Fill(destination, 0x8765_4321);
        cancellationToken.ThrowIfCancellationRequested();
        using var runCancellation = CreateRunCancellation(duration, cancellationToken);
        long totalBytes = 0;
        var stopwatch = Stopwatch.StartNew();
        var lastReportedElapsed = TimeSpan.Zero;

        while (!runCancellation.IsCancellationRequested)
        {
            source.AsSpan().CopyTo(destination);
            destination.AsSpan().CopyTo(source);
            totalBytes += (long)source.Length * sizeof(long) * 4;
            if (stopwatch.Elapsed - lastReportedElapsed >= TimeSpan.FromMilliseconds(250))
            {
                progress.Report(new BenchmarkProgress(totalBytes, stopwatch.Elapsed));
                lastReportedElapsed = stopwatch.Elapsed;
            }
        }

        stopwatch.Stop();
        var result = new BenchmarkResult(totalBytes, stopwatch.Elapsed);
        progress.Report(new BenchmarkProgress(result.Hashes, result.Elapsed));
        return result;
    }

    private static BenchmarkResult MeasureLatency(
        TimeSpan duration,
        IProgress<BenchmarkProgress> progress,
        CancellationToken cancellationToken)
    {
        var links = new int[LatencyElementCount];
        var order = new int[LatencyElementCount];
        for (var index = 0; index < order.Length; index++)
        {
            if ((index & 0xFFFFF) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            order[index] = index;
        }

        for (var index = order.Length - 1; index > 0; index--)
        {
            if ((index & 0xFFFFF) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            var other = Random.Shared.Next(index + 1);
            (order[index], order[other]) = (order[other], order[index]);
        }

        for (var index = 0; index < order.Length; index++)
        {
            if ((index & 0xFFFFF) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            links[order[index]] = order[(index + 1) % order.Length];
        }

        cancellationToken.ThrowIfCancellationRequested();
        using var runCancellation = CreateRunCancellation(duration, cancellationToken);
        var current = order[0];
        long accesses = 0;
        var stopwatch = Stopwatch.StartNew();
        var lastReportedElapsed = TimeSpan.Zero;

        while (!runCancellation.IsCancellationRequested)
        {
            var batchEnd = accesses + ReportInterval;
            while (accesses < batchEnd && !runCancellation.IsCancellationRequested)
            {
                current = links[current];
                accesses++;
            }

            if (stopwatch.Elapsed - lastReportedElapsed >= TimeSpan.FromMilliseconds(250))
            {
                progress.Report(new BenchmarkProgress(accesses, stopwatch.Elapsed));
                lastReportedElapsed = stopwatch.Elapsed;
            }
        }

        stopwatch.Stop();
        Volatile.Write(ref latencySink, current);
        var result = new BenchmarkResult(accesses, stopwatch.Elapsed);
        progress.Report(new BenchmarkProgress(result.Hashes, result.Elapsed));
        return result;
    }

    private static CancellationTokenSource CreateRunCancellation(
        TimeSpan duration,
        CancellationToken cancellationToken)
    {
        var source = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (duration != Timeout.InfiniteTimeSpan)
        {
            source.CancelAfter(duration);
        }

        return source;
    }
}
