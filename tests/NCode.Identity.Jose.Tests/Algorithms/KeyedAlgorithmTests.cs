// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.Jose.Tests.Algorithms;

public class KeyedAlgorithmTests : BaseTests
{
    private static AlgorithmKeyType EccKeyType { get; } =
        new(typeof(EccSecretKey), [new KeySizes(256, 256, 0)]);

    private static AlgorithmKeyType RsaKeyType { get; } =
        new(typeof(RsaSecretKey), [new KeySizes(2048, 4096, 1024)]);

    // An algorithm that serves the key types it is given, as ECDH-ES serves EC and OKP keys.
    private sealed class MultiKeyTypeAlgorithm(params AlgorithmKeyType[] keyTypes) : KeyedAlgorithm
    {
        public override string Code => "X-MULTI";

        public override AlgorithmType Type => AlgorithmType.KeyManagement;

        public override IReadOnlyList<AlgorithmKeyType> KeyTypes { get; } = keyTypes;
    }

    #region TryGetKeyType Tests

    [Fact]
    public void TryGetKeyType_WhenSeveralKeyTypesAccept_ReturnsTheFirst()
    {
        var first = new AlgorithmKeyType(typeof(RsaSecretKey), []);
        var algorithm = new MultiKeyTypeAlgorithm(
            first,
            new AlgorithmKeyType(typeof(AsymmetricSecretKey), [])
        );

        Assert.True(algorithm.TryGetKeyType(typeof(RsaSecretKey), out var keyType));
        Assert.Same(first, keyType);
    }

    [Fact]
    public void TryGetKeyType_WhenNoKeyTypeAccepts_ReturnsFalse()
    {
        var algorithm = new MultiKeyTypeAlgorithm(EccKeyType);

        Assert.False(algorithm.TryGetKeyType(typeof(RsaSecretKey), out var keyType));
        Assert.Null(keyType);
    }

    #endregion

    #region IsKeyCompatible Tests

    [Fact]
    public void IsKeyCompatible_ChecksTheSizesOfTheMatchingKeyType()
    {
        var algorithm = new MultiKeyTypeAlgorithm(EccKeyType, RsaKeyType);
        using var rsa2048 = RSA.Create(2048);
        using var rsa1024 = RSA.Create(1024);
        using var ecc = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var metadata = new KeyMetadata { KeyId = "key" };

        Assert.True(algorithm.IsKeyCompatible(SecretKeyFactory.CreateRsa(metadata, rsa2048)));
        Assert.True(algorithm.IsKeyCompatible(SecretKeyFactory.CreateEcc(metadata, ecc)));
        Assert.False(algorithm.IsKeyCompatible(SecretKeyFactory.CreateRsa(metadata, rsa1024)));
    }

    #endregion

    #region AcceptsOnly Tests

    [Fact]
    public void AcceptsOnly_WhenEveryKeyTypeDerivesFromTheFamily_ReturnsTrue()
    {
        var algorithm = new MultiKeyTypeAlgorithm(EccKeyType, RsaKeyType);

        Assert.True(algorithm.AcceptsOnly<AsymmetricSecretKey>());
        Assert.False(algorithm.AcceptsOnly<RsaSecretKey>());
        Assert.False(algorithm.AcceptsOnly<SymmetricSecretKey>());
    }

    [Fact]
    public void AcceptsOnly_WhenNoKeyType_ReturnsFalse()
    {
        Assert.False(new MultiKeyTypeAlgorithm().AcceptsOnly<SecretKey>());
    }

    #endregion
}
