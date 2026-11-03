// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.KeyMaterial;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.KeyTypes;

namespace NCode.Identity.Jose.Tests.Algorithms;

public class AlgorithmCollectionExtensionsTests : BaseTests
{
    private static EccCurveSpecification BrainpoolCurve { get; } =
        new()
        {
            CurveName = "brainpoolP256r1",
            CurveSizeBits = 256,
            Curve = ECCurve.NamedCurves.brainpoolP256r1,
        };

    private ServiceProvider ServiceProvider { get; }
    private IAlgorithmCollection AlgorithmCollection { get; }

    public AlgorithmCollectionExtensionsTests()
    {
        // The real registered algorithms are the source of truth the rule reads.
        var serviceCollection = new ServiceCollection();
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

    private static RsaSecretKey CreateRsaKey()
    {
        using var rsa = RSA.Create(2048);
        return SecretKeyFactory.CreateRsa(new KeyMetadata { KeyId = "rsa" }, rsa);
    }

    private static EccSecretKey CreateEccKey(ECCurve curve)
    {
        using var ecdsa = ECDsa.Create(curve);
        return SecretKeyFactory.CreateEcc(new KeyMetadata { KeyId = "ecc" }, ecdsa);
    }

    // A key on a curve the application registers in the elliptic-curve catalog, which names it.
    private static EccSecretKey CreateBrainpoolKey()
    {
        var factory = new DefaultSecretKeyFactory(
            new DefaultSecretKeyDataProtectorFactory(NullLoggerFactory.Instance),
            new DefaultEccCurveCatalog([BrainpoolCurve])
        );
        using var ecdsa = ECDsa.Create(BrainpoolCurve.Curve);
        return factory.CreateEcc(new KeyMetadata { KeyId = "brainpool" }, ecdsa);
    }

    private string? Validate(SecretKey key, string? use, string algorithm) =>
        AlgorithmCollection.ValidateKeyAlgorithm(
            new KeyAlgorithmDeclaration
            {
                Key = key,
                Use = use,
                Algorithm = algorithm,
            }
        );

    private bool TryResolve(
        Type keyClrType,
        int? keySizeBits,
        string? curve,
        string algorithm,
        out ResolvedKeyGeneration? resolved,
        out string? error,
        string? use = null
    ) =>
        AlgorithmCollection.TryResolveKeyGeneration(
            new KeyGenerationDeclaration
            {
                KeyClrType = keyClrType,
                KeySizeBits = keySizeBits,
                Curve = curve,
                Use = use,
                Algorithm = algorithm,
            },
            out resolved,
            out error
        );

    #region ValidateKeyAlgorithm Tests

    [Theory]
    [InlineData(null)]
    [InlineData("sig")]
    public void ValidateKeyAlgorithm_WhenSignatureAlgorithmMatchesKey_ReturnsNull(string? use)
    {
        var error = Validate(CreateRsaKey(), use, "RS256");

        Assert.Null(error);
    }

    [Fact]
    public void ValidateKeyAlgorithm_WhenKeyManagementAlgorithmMatchesKey_ReturnsNull()
    {
        var error = Validate(CreateRsaKey(), "enc", "RSA-OAEP-256");

        Assert.Null(error);
    }

    [Fact]
    public void ValidateKeyAlgorithm_WhenAlgorithmIsUnknown_ReturnsError()
    {
        var error = Validate(CreateRsaKey(), null, "XX999");

        Assert.NotNull(error);
        Assert.Contains("'XX999'", error, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateKeyAlgorithm_WhenUseContradictsAlgorithm_ReturnsError()
    {
        var error = Validate(CreateRsaKey(), SecretKeyUses.Encryption, "RS256");

        Assert.NotNull(error);
        Assert.Contains("'sig'", error, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateKeyAlgorithm_WhenKeyTypeIsIncompatible_ReturnsError()
    {
        var error = Validate(CreateEccKey(ECCurve.NamedCurves.nistP256), null, "RS256");

        Assert.NotNull(error);
        Assert.Contains("type", error, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateKeyAlgorithm_WhenKeySizeIsIllegal_ReturnsError()
    {
        var error = Validate(CreateEccKey(ECCurve.NamedCurves.nistP384), null, "ES256");

        Assert.NotNull(error);
        Assert.Contains("'384'", error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("ES256")]
    [InlineData("ECDH-ES")]
    public void ValidateKeyAlgorithm_WhenKeyIsOnACurveTheAlgorithmAccepts_ReturnsNull(
        string algorithm
    )
    {
        var error = Validate(CreateEccKey(ECCurve.NamedCurves.nistP256), null, algorithm);

        Assert.Null(error);
    }

    // brainpoolP256r1 has P-256's size, so only the curve tells the key apart.
    [Theory]
    [InlineData("ES256")]
    [InlineData("ECDH-ES")]
    public void ValidateKeyAlgorithm_WhenKeyIsOnANamedCurveTheAlgorithmRefuses_ReturnsError(
        string algorithm
    )
    {
        var error = Validate(CreateBrainpoolKey(), null, algorithm);

        Assert.NotNull(error);
        Assert.Contains("'brainpoolP256r1'", error, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateKeyAlgorithm_WhenKeysCurveHasNoName_ReturnsError()
    {
        var error = Validate(CreateEccKey(ECCurve.NamedCurves.brainpoolP256r1), null, "ES256");

        Assert.NotNull(error);
        Assert.Contains("without a named curve", error, StringComparison.Ordinal);
    }

    #endregion

    #region TryResolveKeyGeneration Tests

    [Theory]
    [InlineData(null, null, "P-256", 256)]
    [InlineData(384, null, "P-384", 384)]
    [InlineData(null, "P-521", "P-521", 521)]
    [InlineData(521, "P-521", "P-521", 521)]
    public void TryResolveKeyGeneration_WhenTypeHasCurves_ResolvesTheCurveAndItsSize(
        int? keySizeBits,
        string? curve,
        string expectedCurve,
        int expectedKeySizeBits
    )
    {
        Assert.True(
            TryResolve(
                typeof(EccSecretKey),
                keySizeBits,
                curve,
                "ECDH-ES",
                out var resolved,
                out var error
            )
        );

        Assert.Null(error);
        Assert.NotNull(resolved);
        Assert.Equal(expectedCurve, resolved.Curve);
        Assert.Equal(expectedKeySizeBits, resolved.KeySizeBits);
        Assert.Equal(SecretKeyUses.Encryption, resolved.Use);
        Assert.Equal("ECDH-ES", resolved.Algorithm.Code);
        Assert.Equal(typeof(EccSecretKey), resolved.AlgorithmKeyType.KeyClrType);
    }

    [Theory]
    [InlineData(null, "secp256k1", "'secp256k1'")]
    [InlineData(256, "P-384", "'P-384'")]
    [InlineData(320, null, "'320'")]
    public void TryResolveKeyGeneration_WhenCurveOrSizeIsNotOneTheAlgorithmAccepts_ReturnsError(
        int? keySizeBits,
        string? curve,
        string expected
    )
    {
        Assert.False(
            TryResolve(
                typeof(EccSecretKey),
                keySizeBits,
                curve,
                "ECDH-ES",
                out var resolved,
                out var error
            )
        );

        Assert.Null(resolved);
        Assert.NotNull(error);
        Assert.Contains(expected, error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null, 2048)]
    [InlineData(3072, 3072)]
    public void TryResolveKeyGeneration_WhenTypeHasNoCurves_ResolvesTheSize(
        int? keySizeBits,
        int expected
    )
    {
        Assert.True(
            TryResolve(typeof(RsaSecretKey), keySizeBits, null, "PS256", out var resolved, out _)
        );

        Assert.NotNull(resolved);
        Assert.Equal(expected, resolved.KeySizeBits);
        Assert.Null(resolved.Curve);
        Assert.Equal(SecretKeyUses.Signature, resolved.Use);
    }

    [Fact]
    public void TryResolveKeyGeneration_WhenTypeHasNoCurvesAndSizeIsIllegal_ReturnsError()
    {
        Assert.False(TryResolve(typeof(RsaSecretKey), 1024, null, "PS256", out _, out var error));

        Assert.NotNull(error);
        Assert.Contains("'1024'", error, StringComparison.Ordinal);
    }

    [Fact]
    public void TryResolveKeyGeneration_WhenKeyTypeIsIncompatible_ReturnsError()
    {
        Assert.False(TryResolve(typeof(RsaSecretKey), 2048, null, "ES256", out _, out var error));

        Assert.NotNull(error);
        Assert.Contains("type", error, StringComparison.Ordinal);
    }

    [Fact]
    public void TryResolveKeyGeneration_WhenUseContradictsAlgorithm_ReturnsError()
    {
        Assert.False(
            TryResolve(
                typeof(EccSecretKey),
                256,
                null,
                "ES256",
                out _,
                out var error,
                use: SecretKeyUses.Encryption
            )
        );

        Assert.NotNull(error);
        Assert.Contains("'sig'", error, StringComparison.Ordinal);
    }

    [Fact]
    public void TryResolveKeyGeneration_WhenAlgorithmIsUnknown_ReturnsError()
    {
        Assert.False(TryResolve(typeof(EccSecretKey), 256, null, "XX999", out _, out var error));

        Assert.NotNull(error);
        Assert.Contains("'XX999'", error, StringComparison.Ordinal);
    }

    #endregion
}
