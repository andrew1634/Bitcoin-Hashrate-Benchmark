using System.Diagnostics;

namespace BitcoinHashrateBenchmark;

internal enum MemoryBenchmarkKind
{
    Bandwidth,
    Latency
}

internal static class MemoryBenchmark
{
    private const int ReportInterval = 65_536;
    private static long latencySink;

    public static Task<BenchmarkResult> RunAsync(
        MemoryBenchmarkKind kind,
        TimeSpan duration,
        int workingSetMiB,
        IProgress<BenchmarkProgress> progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(progress);
        if (workingSetMiB is < 8 or > 256 || workingSetMiB % 8 != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(workingSetMiB));
        }
        if (duration != Timeout.InfiniteTimeSpan && duration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(duration));
        }

        cancellationToken.ThrowIfCancellationRequested();
        return Task.Run(
            () => kind == MemoryBenchmarkKind.Bandwidth
                ? MeasureBandwidth(duration, workingSetMiB, progress, cancellationToken)
                : MeasureLatency(duration, workingSetMiB, progress, cancellationToken),
            CancellationToken.None);
    }

    private static BenchmarkResult MeasureBandwidth(
        TimeSpan duration,
        int workingSetMiB,
        IProgress<BenchmarkProgress> progress,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var elementCount = checked(workingSetMiB * 1024 * 1024 / sizeof(long));
        var source = new long[elementCount];
        var destination = new long[elementCount];
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
        int workingSetMiB,
        IProgress<BenchmarkProgress> progress,
        CancellationToken cancellationToken)
    {
        var elementCount = checked(workingSetMiB * 1024 * 1024 / sizeof(int));
        var links = new int[elementCount];
        var order = new int[elementCount];
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
