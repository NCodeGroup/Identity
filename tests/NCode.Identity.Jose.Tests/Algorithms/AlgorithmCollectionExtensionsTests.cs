// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.Jose.Tests.Algorithms;

public class AlgorithmCollectionExtensionsTests : BaseTests
{
    private ServiceProvider ServiceProvider { get; }
    private IAlgorithmCollection AlgorithmCollection { get; }

    public AlgorithmCollectionExtensionsTests()
    {
        // The real registered algorithms are the source of truth the rule reads.
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddLogging();
        serviceCollection.AddSecretsLibrary().AddJoseLibrary();
        ServiceProvider = serviceCollection.BuildServiceProvider();

        AlgorithmCollection = ServiceProvider
            .GetRequiredService<IAlgorithmCollectionProvider>()
            .Collection;
    }

    protected override async ValueTask DisposeAsyncCore()
    {
        await ServiceProvider.DisposeAsync();
        await base.DisposeAsyncCore();
    }

    private string? Validate(Type keyType, int keySizeBits, string? use, string algorithm) =>
        AlgorithmCollection.ValidateKeyAlgorithm(
            new KeyAlgorithmDeclaration
            {
                KeyType = keyType,
                KeySizeBits = keySizeBits,
                Use = use,
                Algorithm = algorithm,
            }
        );

    #region ValidateKeyAlgorithm Tests

    [Theory]
    [InlineData(null)]
    [InlineData("sig")]
    public void ValidateKeyAlgorithm_WhenSignatureAlgorithmMatchesKey_ReturnsNull(string? use)
    {
        var error = Validate(typeof(RsaSecretKey), 2048, use, "RS256");

        Assert.Null(error);
    }

    [Fact]
    public void ValidateKeyAlgorithm_WhenKeyManagementAlgorithmMatchesKey_ReturnsNull()
    {
        var error = Validate(typeof(RsaSecretKey), 2048, "enc", "RSA-OAEP-256");

        Assert.Null(error);
    }

    [Fact]
    public void ValidateKeyAlgorithm_WhenAlgorithmIsUnknown_ReturnsError()
    {
        var error = Validate(typeof(RsaSecretKey), 2048, null, "XX999");

        Assert.NotNull(error);
        Assert.Contains("'XX999'", error, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateKeyAlgorithm_WhenUseContradictsAlgorithm_ReturnsError()
    {
        var error = Validate(typeof(RsaSecretKey), 2048, SecretKeyUses.Encryption, "RS256");

        Assert.NotNull(error);
        Assert.Contains("'sig'", error, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateKeyAlgorithm_WhenKeyTypeIsIncompatible_ReturnsError()
    {
        var error = Validate(typeof(EccSecretKey), 256, null, "RS256");

        Assert.NotNull(error);
        Assert.Contains("type", error, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateKeyAlgorithm_WhenKeySizeIsIllegal_ReturnsError()
    {
        var error = Validate(typeof(EccSecretKey), 384, null, "ES256");

        Assert.NotNull(error);
        Assert.Contains("'384'", error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("P-256", "ES256")]
    [InlineData("P-384", "ECDH-ES")]
    public void ValidateKeyAlgorithm_WhenCurveIsOneTheAlgorithmAccepts_ReturnsNull(
        string curve,
        string algorithm
    )
    {
        var keySizeBits = curve == "P-256" ? 256 : 384;

        var error = AlgorithmCollection.ValidateKeyAlgorithm(
            new KeyAlgorithmDeclaration
            {
                KeyType = typeof(EccSecretKey),
                KeySizeBits = keySizeBits,
                Use = null,
                Algorithm = algorithm,
                Curve = curve,
            }
        );

        Assert.Null(error);
    }

    // secp256k1 has P-256's size, so only the curve tells the key apart.
    [Theory]
    [InlineData("ES256")]
    [InlineData("ECDH-ES")]
    public void ValidateKeyAlgorithm_WhenCurveIsNotOneTheAlgorithmAccepts_ReturnsError(
        string algorithm
    )
    {
        var error = AlgorithmCollection.ValidateKeyAlgorithm(
            new KeyAlgorithmDeclaration
            {
                KeyType = typeof(EccSecretKey),
                KeySizeBits = 256,
                Use = null,
                Algorithm = algorithm,
                Curve = "secp256k1",
            }
        );

        Assert.NotNull(error);
        Assert.Contains("'secp256k1'", error, StringComparison.Ordinal);
    }

    // P-384 is a curve ECDH-ES accepts, and 256 bits a size it accepts, but not together.
    [Fact]
    public void ValidateKeyAlgorithm_WhenSizeIsNotTheCurves_ReturnsError()
    {
        var error = AlgorithmCollection.ValidateKeyAlgorithm(
            new KeyAlgorithmDeclaration
            {
                KeyType = typeof(EccSecretKey),
                KeySizeBits = 256,
                Use = null,
                Algorithm = "ECDH-ES",
                Curve = "P-384",
            }
        );

        Assert.NotNull(error);
        Assert.Contains("'P-384'", error, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateKeyAlgorithm_WhenKeyIsOnACurveTheAlgorithmRefuses_ReturnsError()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.brainpoolP256r1);
        var key = SecretKeyFactory.CreateEcc(new KeyMetadata { KeyId = "brainpool" }, ecdsa);

        var error = AlgorithmCollection.ValidateKeyAlgorithm(
            new KeyAlgorithmDeclaration
            {
                KeyType = typeof(EccSecretKey),
                KeySizeBits = key.KeySizeBits,
                Use = null,
                Algorithm = "ES256",
                Key = key,
            }
        );

        Assert.NotNull(error);
        Assert.Contains("not compatible with the key", error, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateKeyAlgorithm_WhenKeyIsOnTheAlgorithmsCurve_ReturnsNull()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var key = SecretKeyFactory.CreateEcc(new KeyMetadata { KeyId = "p256" }, ecdsa);

        var error = AlgorithmCollection.ValidateKeyAlgorithm(
            new KeyAlgorithmDeclaration
            {
                KeyType = typeof(EccSecretKey),
                KeySizeBits = key.KeySizeBits,
                Use = null,
                Algorithm = "ES256",
                Key = key,
            }
        );

        Assert.Null(error);
    }

    #endregion
}
