// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using BenchmarkDotNet.Attributes;
using NCode.Identity.Jose.EdDsa.Math;

namespace NCode.Identity.Jose.EdDsa.Benchmarks;

/// <summary>
/// Measures the twisted Edwards group arithmetic of Ed25519.
/// </summary>
[MemoryDiagnoser]
public class Ge25519Benchmarks
{
    private Ge25519 _p;
    private Ge25519 _q;
    private Ge25519 _decoded;

    private byte[] Scalar { get; set; } = [];

    private byte[] Encoded { get; set; } = [];

    private byte[] Output { get; set; } = [];

    private Ge25519 Result { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var random = new Random(25519);
        Scalar = new byte[32];
        Encoded = new byte[32];
        Output = new byte[32];
        var other = new byte[32];
        random.NextBytes(Scalar);
        random.NextBytes(other);
        Scalar[^1] &= 0x0F;
        other[^1] &= 0x0F;

        _p = Ge25519.ScalarMultBase(Scalar);
        _q = Ge25519.ScalarMultBase(other);
        _p.ToBytes(Encoded);
    }

    [Benchmark]
    public void Add() => Result = Ge25519.Add(_p, _q);

    [Benchmark]
    public void Dbl() => Result = Ge25519.Dbl(_p);

    [Benchmark]
    public void ScalarMultBase() => Result = Ge25519.ScalarMultBase(Scalar);

    [Benchmark]
    public void ScalarMult() => Result = Ge25519.ScalarMult(Scalar, _q);

    [Benchmark]
    public void Encode() => _p.ToBytes(Output);

    [Benchmark]
    public bool Decode() => Ge25519.TryFromBytes(Encoded, out _decoded);
}
