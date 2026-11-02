// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using BenchmarkDotNet.Attributes;
using NCode.Identity.Jose.EdDsa.Math;

namespace NCode.Identity.Jose.EdDsa.Benchmarks;

/// <summary>
/// Measures the GF(2^448 − 2^224 − 1) field arithmetic of Ed448.
/// </summary>
[MemoryDiagnoser]
public class Fe448Benchmarks
{
    private Fe448 _a;
    private Fe448 _b;

    private Fe448 Result { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var random = new Random(448);
        Span<byte> a = stackalloc byte[57];
        Span<byte> b = stackalloc byte[57];
        random.NextBytes(a);
        random.NextBytes(b);
        _a = Fe448.FromBytes(a);
        _b = Fe448.FromBytes(b);
    }

    [Benchmark]
    public void Add() => Result = Fe448.Add(_a, _b);

    [Benchmark]
    public void Mul() => Result = Fe448.Mul(_a, _b);

    [Benchmark]
    public void Sqr() => Result = Fe448.Sqr(_a);

    [Benchmark]
    public void Invert() => Result = Fe448.Invert(_a);
}
