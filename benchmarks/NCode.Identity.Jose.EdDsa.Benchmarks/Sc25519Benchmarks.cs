// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using BenchmarkDotNet.Attributes;
using NCode.Identity.Jose.EdDsa.Math;

namespace NCode.Identity.Jose.EdDsa.Benchmarks;

/// <summary>
/// Measures the arithmetic modulo the group order L of Ed25519.
/// </summary>
[MemoryDiagnoser]
public class Sc25519Benchmarks
{
    private byte[] Wide { get; set; } = [];

    private byte[] A { get; set; } = [];

    private byte[] B { get; set; } = [];

    private byte[] C { get; set; } = [];

    private byte[] Result { get; set; } = [];

    /// <summary>
    /// Generates a random wide value to reduce and three random scalars below the group order.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        var random = new Random(25519);
        Wide = new byte[64];
        A = new byte[32];
        B = new byte[32];
        C = new byte[32];
        Result = new byte[32];
        random.NextBytes(Wide);
        random.NextBytes(A);
        random.NextBytes(B);
        random.NextBytes(C);
        A[^1] &= 0x0F;
        B[^1] &= 0x0F;
        C[^1] &= 0x0F;
    }

    /// <summary>
    /// Reduces a 64-byte value modulo the group order.
    /// </summary>
    [Benchmark]
    public void Reduce() => Sc25519.Reduce(Wide, Result);

    /// <summary>
    /// Computes a × b + c modulo the group order.
    /// </summary>
    [Benchmark]
    public void MulAdd() => Sc25519.MulAdd(A, B, C, Result);
}
