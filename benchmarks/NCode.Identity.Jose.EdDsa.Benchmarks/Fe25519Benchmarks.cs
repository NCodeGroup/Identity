// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using BenchmarkDotNet.Attributes;
using NCode.Identity.Jose.EdDsa.Math;

namespace NCode.Identity.Jose.EdDsa.Benchmarks;

/// <summary>
/// Measures the GF(2^255 − 19) field arithmetic of Ed25519.
/// </summary>
[MemoryDiagnoser]
public class Fe25519Benchmarks
{
    private Fe25519 _a;
    private Fe25519 _b;

    private Fe25519 Result { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var random = new Random(25519);
        Span<byte> a = stackalloc byte[32];
        Span<byte> b = stackalloc byte[32];
        random.NextBytes(a);
        random.NextBytes(b);
        _a = Fe25519.FromBytes(a);
        _b = Fe25519.FromBytes(b);
    }

    [Benchmark]
    public void Add() => Result = Fe25519.Add(_a, _b);

    [Benchmark]
    public void Mul() => Result = Fe25519.Mul(_a, _b);

    [Benchmark]
    public void Sqr() => Result = Fe25519.Sqr(_a);

    [Benchmark]
    public void Invert() => Result = Fe25519.Invert(_a);
}
