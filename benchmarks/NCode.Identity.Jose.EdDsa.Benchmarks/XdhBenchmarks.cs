// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using BenchmarkDotNet.Attributes;
using NCode.Identity.Jose.EdDsa.Math;

namespace NCode.Identity.Jose.EdDsa.Benchmarks;

/// <summary>
/// Measures the X25519 and X448 Diffie-Hellman functions of <c>ECDH-ES</c> over <c>OKP</c> keys.
/// </summary>
[MemoryDiagnoser]
public class XdhBenchmarks
{
    private byte[] X25519PrivateKey { get; set; } = [];

    private byte[] X25519PeerPublicKey { get; set; } = [];

    private byte[] X25519SharedSecret { get; set; } = [];

    private byte[] X448PrivateKey { get; set; } = [];

    private byte[] X448PeerPublicKey { get; set; } = [];

    private byte[] X448SharedSecret { get; set; } = [];

    /// <summary>
    /// Generates a private key and a peer's public key on each curve.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        var random = new Random(7748);
        X25519PrivateKey = new byte[X25519.PrivateKeySize];
        X25519PeerPublicKey = new byte[X25519.PublicKeySize];
        X25519SharedSecret = new byte[X25519.SharedSecretSize];
        X448PrivateKey = new byte[X448.PrivateKeySize];
        X448PeerPublicKey = new byte[X448.PublicKeySize];
        X448SharedSecret = new byte[X448.SharedSecretSize];
        random.NextBytes(X25519PrivateKey);
        random.NextBytes(X448PrivateKey);

        var peerPrivateKey = new byte[X448.PrivateKeySize];
        random.NextBytes(peerPrivateKey);
        X25519.GeneratePublicKey(
            peerPrivateKey.AsSpan(0, X25519.PrivateKeySize),
            X25519PeerPublicKey
        );
        X448.GeneratePublicKey(peerPrivateKey, X448PeerPublicKey);
    }

    /// <summary>
    /// Derives an X25519 public key from its private key.
    /// </summary>
    [Benchmark]
    public void GeneratePublicKeyX25519() =>
        X25519.GeneratePublicKey(X25519PrivateKey, X25519SharedSecret);

    /// <summary>
    /// Agrees an X25519 shared secret with the peer's public key.
    /// </summary>
    /// <returns><c>true</c> when the shared secret is not all zeros; otherwise, <c>false</c>.</returns>
    [Benchmark]
    public bool AgreeX25519() =>
        X25519.TryAgree(X25519PrivateKey, X25519PeerPublicKey, X25519SharedSecret);

    /// <summary>
    /// Derives an X448 public key from its private key.
    /// </summary>
    [Benchmark]
    public void GeneratePublicKeyX448() => X448.GeneratePublicKey(X448PrivateKey, X448SharedSecret);

    /// <summary>
    /// Agrees an X448 shared secret with the peer's public key.
    /// </summary>
    /// <returns><c>true</c> when the shared secret is not all zeros; otherwise, <c>false</c>.</returns>
    [Benchmark]
    public bool AgreeX448() => X448.TryAgree(X448PrivateKey, X448PeerPublicKey, X448SharedSecret);
}
