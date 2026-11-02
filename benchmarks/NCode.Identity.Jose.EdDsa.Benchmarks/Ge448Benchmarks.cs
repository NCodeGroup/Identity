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

    /// <summary>
    /// Derives two points from random scalars and encodes the first.
    /// </summary>
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

    /// <summary>
    /// Adds two points.
    /// </summary>
    [Benchmark]
    public void Add() => Result = Ge448.Add(_p, _q);

    /// <summary>
    /// Doubles a point.
    /// </summary>
    [Benchmark]
    public void Dbl() => Result = Ge448.Dbl(_p);

    /// <summary>
    /// Multiplies the base point by a scalar.
    /// </summary>
    [Benchmark]
    public void ScalarMultBase() => Result = Ge448.ScalarMultBase(Scalar);

    /// <summary>
    /// Multiplies an arbitrary point by a scalar.
    /// </summary>
    [Benchmark]
    public void ScalarMult() => Result = Ge448.ScalarMult(Scalar, _q);

    /// <summary>
    /// Encodes a point.
    /// </summary>
    [Benchmark]
    public void Encode() => _p.ToBytes(Output);

    /// <summary>
    /// Decodes an encoded point.
    /// </summary>
    /// <returns><c>true</c> when the encoding is a valid point; otherwise, <c>false</c>.</returns>
    [Benchmark]
    public bool Decode() => Ge448.TryFromBytes(Encoded, out _decoded);
}
