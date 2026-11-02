// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using BenchmarkDotNet.Attributes;
using NCode.Identity.Jose.EdDsa.Math;

namespace NCode.Identity.Jose.EdDsa.Benchmarks;

/// <summary>
/// Measures signing and verifying with Ed25519 and Ed448.
/// </summary>
[MemoryDiagnoser]
public class EdDsaSignatureBenchmarks
{
    private byte[] Message { get; set; } = [];

    private byte[] Ed25519PrivateKey { get; set; } = [];

    private byte[] Ed25519PublicKey { get; set; } = [];

    private byte[] Ed25519Signature { get; set; } = [];

    private byte[] Ed448PrivateKey { get; set; } = [];

    private byte[] Ed448PublicKey { get; set; } = [];

    private byte[] Ed448Signature { get; set; } = [];

    [GlobalSetup]
    public void Setup()
    {
        var random = new Random(8032);
        Message = new byte[64];
        Ed25519PrivateKey = new byte[Ed25519.SeedSize];
        Ed25519PublicKey = new byte[Ed25519.PublicKeySize];
        Ed25519Signature = new byte[Ed25519.SignatureSize];
        Ed448PrivateKey = new byte[Ed448.SeedSize];
        Ed448PublicKey = new byte[Ed448.PublicKeySize];
        Ed448Signature = new byte[Ed448.SignatureSize];
        random.NextBytes(Message);
        random.NextBytes(Ed25519PrivateKey);
        random.NextBytes(Ed448PrivateKey);

        Ed25519.GeneratePublicKey(Ed25519PrivateKey, Ed25519PublicKey);
        Ed448.GeneratePublicKey(Ed448PrivateKey, Ed448PublicKey);
        Ed25519.Sign(Ed25519PrivateKey, Message, Ed25519Signature);
        Ed448.Sign(Ed448PrivateKey, Message, Ed448Signature);
    }

    [Benchmark]
    public void SignEd25519() => Ed25519.Sign(Ed25519PrivateKey, Message, Ed25519Signature);

    [Benchmark]
    public bool VerifyEd25519() => Ed25519.Verify(Ed25519PublicKey, Message, Ed25519Signature);

    [Benchmark]
    public void SignEd448() => Ed448.Sign(Ed448PrivateKey, Message, Ed448Signature);

    [Benchmark]
    public bool VerifyEd448() => Ed448.Verify(Ed448PublicKey, Message, Ed448Signature);
}
