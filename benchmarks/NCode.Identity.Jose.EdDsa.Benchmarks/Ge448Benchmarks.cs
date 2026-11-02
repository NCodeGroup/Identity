// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using BenchmarkDotNet.Attributes;
using NCode.Identity.Jose.EdDsa.Math;

namespace NCode.Identity.Jose.EdDsa.Benchmarks;

/// <summary>
/// Measures the Edwards group arithmetic of Ed448.
/// </summary>
[MemoryDiagnoser]
public class Ge448Benchmarks
{
    private Ge448 _p;
    private Ge448 _q;
    private Ge448 _decoded;

    private byte[] Scalar { get; set; } = [];

    private byte[] Encoded { get; set; } = [];

    private byte[] Output { get; set; } = [];

    private Ge448 Result { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var random = new Random(448);
        Scalar = new byte[57];
        Encoded = new byte[57];
        Output = new byte[57];
        var other = new byte[57];
        random.NextBytes(Scalar);
        random.NextBytes(other);
        Scalar[^1] = 0;
        other[^1] = 0;

        _p = Ge448.ScalarMultBase(Scalar);
        _q = Ge448.ScalarMultBase(other);
        _p.ToBytes(Encoded);
    }

    [Benchmark]
    public void Add() => Result = Ge448.Add(_p, _q);

    [Benchmark]
    public void Dbl() => Result = Ge448.Dbl(_p);

    [Benchmark]
    public void ScalarMultBase() => Result = Ge448.ScalarMultBase(Scalar);

    [Benchmark]
    public void ScalarMult() => Result = Ge448.ScalarMult(Scalar, _q);

    [Benchmark]
    public void Encode() => _p.ToBytes(Output);

    [Benchmark]
    public bool Decode() => Ge448.TryFromBytes(Encoded, out _decoded);
}
