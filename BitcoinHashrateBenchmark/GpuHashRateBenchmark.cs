using System.Diagnostics;
using ComputeSharp;

namespace BitcoinHashrateBenchmark;

internal static partial class GpuHashRateBenchmark
{
    public const string Backend = "direct3d12";

    private const int BatchSize = 32_768;

    private static readonly uint[] RoundConstants =
    [
        0x428A2F98, 0x71374491, 0xB5C0FBCF, 0xE9B5DBA5, 0x3956C25B, 0x59F111F1, 0x923F82A4, 0xAB1C5ED5,
        0xD807AA98, 0x12835B01, 0x243185BE, 0x550C7DC3, 0x72BE5D74, 0x80DEB1FE, 0x9BDC06A7, 0xC19BF174,
        0xE49B69C1, 0xEFBE4786, 0x0FC19DC6, 0x240CA1CC, 0x2DE92C6F, 0x4A7484AA, 0x5CB0A9DC, 0x76F988DA,
        0x983E5152, 0xA831C66D, 0xB00327C8, 0xBF597FC7, 0xC6E00BF3, 0xD5A79147, 0x06CA6351, 0x14292967,
        0x27B70A85, 0x2E1B2138, 0x4D2C6DFC, 0x53380D13, 0x650A7354, 0x766A0ABB, 0x81C2C92E, 0x92722C85,
        0xA2BFE8A1, 0xA81A664B, 0xC24B8B70, 0xC76C51A3, 0xD192E819, 0xD6990624, 0xF40E3585, 0x106AA070,
        0x19A4C116, 0x1E376C08, 0x2748774C, 0x34B0BCB5, 0x391C0CB3, 0x4ED8AA4A, 0x5B9CCA4F, 0x682E6FF3,
        0x748F82EE, 0x78A5636F, 0x84C87814, 0x8CC70208, 0x90BEFFFA, 0xA4506CEB, 0xBEF9A3F7, 0xC67178F2
    ];

    private static readonly uint[] ShaderConstants = CreateShaderConstants();

    public static bool IsAvailable => TryGetDevice(out _, out _);

    private static bool TryGetDevice(out GraphicsDevice? device, out string errorMessage)
    {
        try
        {
            device = GraphicsDevice.GetDefault();
            if (device.IsHardwareAccelerated)
            {
                errorMessage = string.Empty;
                return true;
            }

            errorMessage = "No hardware-accelerated Direct3D 12 GPU was found. Install a supported GPU and current graphics driver; the software WARP fallback is not used.";
            return false;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            device = null;
            errorMessage = $"A Direct3D 12 GPU is unavailable: {exception.Message} Install or update a Direct3D 12-capable graphics driver.";
            return false;
        }
    }

    public static async Task<BenchmarkResult> RunAsync(
        TimeSpan duration,
        IProgress<BenchmarkProgress> progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(progress);
        if (duration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(duration));
        }

        if (!TryGetDevice(out GraphicsDevice? device, out string errorMessage))
        {
            throw new NotSupportedException(errorMessage);
        }

        using var runCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        runCancellation.CancelAfter(duration);

        return await Task.Run(
            () => RunBatches(device!, runCancellation.Token, progress),
            CancellationToken.None).ConfigureAwait(false);
    }

    private static BenchmarkResult RunBatches(
        GraphicsDevice device,
        CancellationToken cancellationToken,
        IProgress<BenchmarkProgress> progress)
    {
        using ReadWriteBuffer<uint> output = device.AllocateReadWriteBuffer<uint>(1);
        using ReadOnlyBuffer<uint> constants = device.AllocateReadOnlyBuffer(ShaderConstants);
        var completionMarker = new uint[1];

        long totalHashes = 0;
        uint nonceBase = 0;
        var stopwatch = Stopwatch.StartNew();
        var lastReportedElapsed = TimeSpan.Zero;

        while (!cancellationToken.IsCancellationRequested)
        {
            device.For(BatchSize, new DoubleSha256Shader(output, constants, nonceBase));
            output.CopyTo(completionMarker);
            totalHashes += BatchSize;
            nonceBase += BatchSize;
            var elapsed = stopwatch.Elapsed;
            if (elapsed - lastReportedElapsed >= TimeSpan.FromMilliseconds(250))
            {
                progress.Report(new BenchmarkProgress(totalHashes, elapsed));
                lastReportedElapsed = elapsed;
            }
        }

        stopwatch.Stop();
        var result = new BenchmarkResult(totalHashes, stopwatch.Elapsed);
        progress.Report(new BenchmarkProgress(result.Hashes, result.Elapsed));
        return result;
    }

    [ThreadGroupSize(DefaultThreadGroupSizes.X)]
    [GeneratedComputeShaderDescriptor]
    public readonly partial struct DoubleSha256Shader(
        ReadWriteBuffer<uint> output,
        ReadOnlyBuffer<uint> roundConstants,
        uint nonceBase) : IComputeShader
    {
        public void Execute()
        {
            uint nonce = nonceBase + (uint)ThreadIds.X;
            uint w0 = 0;
            uint w1 = 0;
            uint w2 = 0;
            uint w3 = 0;
            uint w4 = 0;
            uint w5 = 0;
            uint w6 = 0;
            uint w7 = 0;
            uint w8 = 0;
            uint w9 = 0;
            uint w10 = 0;
            uint w11 = 0;
            uint w12 = 0;
            uint w13 = 0;
            uint w14 = 0;
            uint w15 = 0;
            uint h0 = 0x6A09E667;
            uint h1 = 0xBB67AE85;
            uint h2 = 0x3C6EF372;
            uint h3 = 0xA54FF53A;
            uint h4 = 0x510E527F;
            uint h5 = 0x9B05688C;
            uint h6 = 0x1F83D9AB;
            uint h7 = 0x5BE0CD19;

            Compress(ref w0, ref w1, ref w2, ref w3, ref w4, ref w5, ref w6, ref w7,
                ref w8, ref w9, ref w10, ref w11, ref w12, ref w13, ref w14, ref w15,
                ref h0, ref h1, ref h2, ref h3, ref h4, ref h5, ref h6, ref h7);

            uint firstBlockWord3 =
                roundConstants[64 + (int)(nonce & 0xFF)] |
                (roundConstants[64 + (int)((nonce >> 8) & 0xFF)] >> 8) |
                (roundConstants[64 + (int)((nonce >> 16) & 0xFF)] >> 16) |
                (roundConstants[64 + (int)(nonce >> 24)] >> 24);
            w0 = 0;
            w1 = 0;
            w2 = 0;
            w3 = firstBlockWord3;
            w4 = 0x80000000;
            w5 = 0;
            w6 = 0;
            w7 = 0;
            w8 = 0;
            w9 = 0;
            w10 = 0;
            w11 = 0;
            w12 = 0;
            w13 = 0;
            w14 = 0;
            w15 = 640;
            Compress(ref w0, ref w1, ref w2, ref w3, ref w4, ref w5, ref w6, ref w7,
                ref w8, ref w9, ref w10, ref w11, ref w12, ref w13, ref w14, ref w15,
                ref h0, ref h1, ref h2, ref h3, ref h4, ref h5, ref h6, ref h7);

            uint firstHash0 = h0;
            uint firstHash1 = h1;
            uint firstHash2 = h2;
            uint firstHash3 = h3;
            uint firstHash4 = h4;
            uint firstHash5 = h5;
            uint firstHash6 = h6;
            uint firstHash7 = h7;

            h0 = 0x6A09E667;
            h1 = 0xBB67AE85;
            h2 = 0x3C6EF372;
            h3 = 0xA54FF53A;
            h4 = 0x510E527F;
            h5 = 0x9B05688C;
            h6 = 0x1F83D9AB;
            h7 = 0x5BE0CD19;
            w0 = firstHash0;
            w1 = firstHash1;
            w2 = firstHash2;
            w3 = firstHash3;
            w4 = firstHash4;
            w5 = firstHash5;
            w6 = firstHash6;
            w7 = firstHash7;
            w8 = 0x80000000;
            w9 = 0;
            w10 = 0;
            w11 = 0;
            w12 = 0;
            w13 = 0;
            w14 = 0;
            w15 = 256;
            Compress(ref w0, ref w1, ref w2, ref w3, ref w4, ref w5, ref w6, ref w7,
                ref w8, ref w9, ref w10, ref w11, ref w12, ref w13, ref w14, ref w15,
                ref h0, ref h1, ref h2, ref h3, ref h4, ref h5, ref h6, ref h7);

            if (ThreadIds.X == 0)
            {
                output[0] = h0;
            }
        }

        private void Compress(
            ref uint w0,
            ref uint w1,
            ref uint w2,
            ref uint w3,
            ref uint w4,
            ref uint w5,
            ref uint w6,
            ref uint w7,
            ref uint w8,
            ref uint w9,
            ref uint w10,
            ref uint w11,
            ref uint w12,
            ref uint w13,
            ref uint w14,
            ref uint w15,
            ref uint h0,
            ref uint h1,
            ref uint h2,
            ref uint h3,
            ref uint h4,
            ref uint h5,
            ref uint h6,
            ref uint h7)
        {
            uint a = h0;
            uint b = h1;
            uint c = h2;
            uint d = h3;
            uint e = h4;
            uint f = h5;
            uint g = h6;
            uint h = h7;

            for (int i = 0; i < 64; i++)
            {
                uint word = 0;
                switch (i & 15)
                {
                    case 0:
                        if (i >= 16) w0 += (RotateRight(w1, 7) ^ RotateRight(w1, 18) ^ (w1 >> 3)) + w9 + (RotateRight(w14, 17) ^ RotateRight(w14, 19) ^ (w14 >> 10));
                        word = w0;
                        break;
                    case 1:
                        if (i >= 16) w1 += (RotateRight(w2, 7) ^ RotateRight(w2, 18) ^ (w2 >> 3)) + w10 + (RotateRight(w15, 17) ^ RotateRight(w15, 19) ^ (w15 >> 10));
                        word = w1;
                        break;
                    case 2:
                        if (i >= 16) w2 += (RotateRight(w3, 7) ^ RotateRight(w3, 18) ^ (w3 >> 3)) + w11 + (RotateRight(w0, 17) ^ RotateRight(w0, 19) ^ (w0 >> 10));
                        word = w2;
                        break;
                    case 3:
                        if (i >= 16) w3 += (RotateRight(w4, 7) ^ RotateRight(w4, 18) ^ (w4 >> 3)) + w12 + (RotateRight(w1, 17) ^ RotateRight(w1, 19) ^ (w1 >> 10));
                        word = w3;
                        break;
                    case 4:
                        if (i >= 16) w4 += (RotateRight(w5, 7) ^ RotateRight(w5, 18) ^ (w5 >> 3)) + w13 + (RotateRight(w2, 17) ^ RotateRight(w2, 19) ^ (w2 >> 10));
                        word = w4;
                        break;
                    case 5:
                        if (i >= 16) w5 += (RotateRight(w6, 7) ^ RotateRight(w6, 18) ^ (w6 >> 3)) + w14 + (RotateRight(w3, 17) ^ RotateRight(w3, 19) ^ (w3 >> 10));
                        word = w5;
                        break;
                    case 6:
                        if (i >= 16) w6 += (RotateRight(w7, 7) ^ RotateRight(w7, 18) ^ (w7 >> 3)) + w15 + (RotateRight(w4, 17) ^ RotateRight(w4, 19) ^ (w4 >> 10));
                        word = w6;
                        break;
                    case 7:
                        if (i >= 16) w7 += (RotateRight(w8, 7) ^ RotateRight(w8, 18) ^ (w8 >> 3)) + w0 + (RotateRight(w5, 17) ^ RotateRight(w5, 19) ^ (w5 >> 10));
                        word = w7;
                        break;
                    case 8:
                        if (i >= 16) w8 += (RotateRight(w9, 7) ^ RotateRight(w9, 18) ^ (w9 >> 3)) + w1 + (RotateRight(w6, 17) ^ RotateRight(w6, 19) ^ (w6 >> 10));
                        word = w8;
                        break;
                    case 9:
                        if (i >= 16) w9 += (RotateRight(w10, 7) ^ RotateRight(w10, 18) ^ (w10 >> 3)) + w2 + (RotateRight(w7, 17) ^ RotateRight(w7, 19) ^ (w7 >> 10));
                        word = w9;
                        break;
                    case 10:
                        if (i >= 16) w10 += (RotateRight(w11, 7) ^ RotateRight(w11, 18) ^ (w11 >> 3)) + w3 + (RotateRight(w8, 17) ^ RotateRight(w8, 19) ^ (w8 >> 10));
                        word = w10;
                        break;
                    case 11:
                        if (i >= 16) w11 += (RotateRight(w12, 7) ^ RotateRight(w12, 18) ^ (w12 >> 3)) + w4 + (RotateRight(w9, 17) ^ RotateRight(w9, 19) ^ (w9 >> 10));
                        word = w11;
                        break;
                    case 12:
                        if (i >= 16) w12 += (RotateRight(w13, 7) ^ RotateRight(w13, 18) ^ (w13 >> 3)) + w5 + (RotateRight(w10, 17) ^ RotateRight(w10, 19) ^ (w10 >> 10));
                        word = w12;
                        break;
                    case 13:
                        if (i >= 16) w13 += (RotateRight(w14, 7) ^ RotateRight(w14, 18) ^ (w14 >> 3)) + w6 + (RotateRight(w11, 17) ^ RotateRight(w11, 19) ^ (w11 >> 10));
                        word = w13;
                        break;
                    case 14:
                        if (i >= 16) w14 += (RotateRight(w15, 7) ^ RotateRight(w15, 18) ^ (w15 >> 3)) + w7 + (RotateRight(w12, 17) ^ RotateRight(w12, 19) ^ (w12 >> 10));
                        word = w14;
                        break;
                    default:
                        if (i >= 16) w15 += (RotateRight(w0, 7) ^ RotateRight(w0, 18) ^ (w0 >> 3)) + w8 + (RotateRight(w13, 17) ^ RotateRight(w13, 19) ^ (w13 >> 10));
                        word = w15;
                        break;
                }

                uint sum1 = RotateRight(e, 6) ^ RotateRight(e, 11) ^ RotateRight(e, 25);
                uint choice = (e & f) ^ (~e & g);
                uint temporary1 = h + sum1 + choice + roundConstants[i] + word;
                uint sum0 = RotateRight(a, 2) ^ RotateRight(a, 13) ^ RotateRight(a, 22);
                uint majority = (a & b) ^ (a & c) ^ (b & c);
                uint temporary2 = sum0 + majority;

                h = g;
                g = f;
                f = e;
                e = d + temporary1;
                d = c;
                c = b;
                b = a;
                a = temporary1 + temporary2;
            }

            h0 += a;
            h1 += b;
            h2 += c;
            h3 += d;
            h4 += e;
            h5 += f;
            h6 += g;
            h7 += h;
        }

        private static uint RotateRight(uint value, int amount)
        {
            return (value >> amount) | (value << (32 - amount));
        }
    }

    private static uint[] CreateShaderConstants()
    {
        var constants = new uint[RoundConstants.Length + 256];
        Array.Copy(RoundConstants, constants, RoundConstants.Length);
        for (var value = 0; value < 256; value++)
        {
            constants[RoundConstants.Length + value] = (uint)value << 24;
        }

        return constants;
    }
}
