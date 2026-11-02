// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using BenchmarkDotNet.Attributes;
using NCode.Identity.Jose.EdDsa.Math;

namespace NCode.Identity.Jose.EdDsa.Benchmarks;

/// <summary>
/// Measures the arithmetic modulo the group order L of Ed448.
/// </summary>
[MemoryDiagnoser]
public class Sc448Benchmarks
{
    private byte[] Wide { get; set; } = [];

    private byte[] A { get; set; } = [];

    private byte[] B { get; set; } = [];

    private byte[] C { get; set; } = [];

    private byte[] Result { get; set; } = [];

    [GlobalSetup]
    public void Setup()
    {
        var random = new Random(448);
        Wide = new byte[114];
        A = new byte[57];
        B = new byte[57];
        C = new byte[57];
        Result = new byte[57];
        random.NextBytes(Wide);
        random.NextBytes(A);
        random.NextBytes(B);
        random.NextBytes(C);
        A[^1] = 0;
        B[^1] = 0;
        C[^1] = 0;
    }

    [Benchmark]
    public void Reduce() => Sc448.Reduce(Wide, Result);

    [Benchmark]
    public void MulAdd() => Sc448.MulAdd(A, B, C, Result);
}
