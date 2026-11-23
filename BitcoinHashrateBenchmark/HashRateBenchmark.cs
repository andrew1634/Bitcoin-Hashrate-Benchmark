using System.Buffers.Binary;
using System.Diagnostics;
using System.Security.Cryptography;

namespace BitcoinHashrateBenchmark;

internal readonly record struct BenchmarkProgress(long Hashes, TimeSpan Elapsed);

internal readonly record struct BenchmarkResult(long Hashes, TimeSpan Elapsed)
{
    public double HashesPerSecond => Elapsed.TotalSeconds == 0 ? 0 : Hashes / Elapsed.TotalSeconds;
}

internal static class HashRateBenchmark
{
    public static async Task<BenchmarkResult> RunAsync(
        TimeSpan duration,
        int threadCount,
        IProgress<BenchmarkProgress> progress,
        CancellationToken cancellationToken)
    {
        if (duration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(duration));
        }

        if (threadCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(threadCount));
        }

        using var runCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        runCancellation.CancelAfter(duration);

        long totalHashes = 0;
        var stopwatch = Stopwatch.StartNew();
        var workers = Enumerable.Range(0, threadCount)
            .Select(worker => Task.Run(() => HashUntilCancelled(worker, runCancellation.Token, count =>
            {
                Interlocked.Add(ref totalHashes, count);
            })))
            .ToArray();

        while (!runCancellation.IsCancellationRequested)
        {
            progress.Report(new BenchmarkProgress(Interlocked.Read(ref totalHashes), stopwatch.Elapsed));

            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(250), runCancellation.Token);
            }
            catch (OperationCanceledException) when (runCancellation.IsCancellationRequested)
            {
                break;
            }
        }

        await Task.WhenAll(workers);
        stopwatch.Stop();

        var result = new BenchmarkResult(Interlocked.Read(ref totalHashes), stopwatch.Elapsed);
        progress.Report(new BenchmarkProgress(result.Hashes, result.Elapsed));
        return result;
    }

    private static void HashUntilCancelled(int worker, CancellationToken cancellationToken, Action<long> addHashes)
    {
        Span<byte> header = stackalloc byte[80];
        Span<byte> firstHash = stackalloc byte[32];
        Span<byte> secondHash = stackalloc byte[32];
        uint nonce = (uint)worker;
        long localHashes = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(header[76..], nonce++);
            SHA256.HashData(header, firstHash);
            SHA256.HashData(firstHash, secondHash);
            localHashes++;

            if (localHashes == 4_096)
            {
                addHashes(localHashes);
                localHashes = 0;
            }
        }

        if (localHashes > 0)
        {
            addHashes(localHashes);
        }
    }
}
