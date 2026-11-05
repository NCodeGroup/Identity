// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Security.Cryptography;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.Jose.Algorithms.KeyManagement;
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
            Domain = EccCurveDomain.FromCurve(ECCurve.NamedCurves.brainpoolP256r1),
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

    // An agreement over EC keys that names neither a curve nor a size, as a contributed agreement may declare.
    private sealed class SizelessEcdhKeyAgreement : IEcdhKeyAgreement
    {
        public AlgorithmKeyType AlgorithmKeyType { get; } = new(typeof(EccSecretKey), []);

        public int GetSharedSecretSizeBytes(SecretKey secretKey) =>
            throw new NotSupportedException();

        public void AgreeWithEphemeralKey(
            SecretKey recipientKey,
            IBufferWriter<byte> ephemeralPublicKeyWriter,
            Span<byte> sharedSecret
        ) => throw new NotSupportedException();

        public void Agree(
            SecretKey recipientKey,
            SecretKey ephemeralPublicKey,
            Span<byte> sharedSecret
        ) => throw new NotSupportedException();
    }

    // A collection whose only algorithm is ECDH-ES over the specified agreements.
    private IAlgorithmCollection CreateEcdhCollection(params IEcdhKeyAgreement[] keyAgreements) =>
        new DefaultAlgorithmCollection([
            new EcdhKeyManagementAlgorithm(
                ServiceProvider.GetRequiredService<ISecretKeyTypeCatalog>(),
                keyAgreements
            ),
        ]);

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

    // A symmetric key has no curve, which an algorithm whose key type names no curve does not ask for.
    [Fact]
    public void ValidateKeyAlgorithm_WhenSymmetricKeyMatchesAlgorithm_ReturnsNull()
    {
        var key = SecretKeyFactory.CreateSymmetric(
            new KeyMetadata { KeyId = "oct" },
            RandomNumberGenerator.GetBytes(32)
        );

        var error = Validate(key, SecretKeyUses.Signature, "HS256");

        Assert.Null(error);
    }

    // The key's type, size and curve fit, but two agreements accept it, so the algorithm itself refuses it.
    [Fact]
    public void ValidateKeyAlgorithm_WhenTheAlgorithmRefusesAKeyOfAnAcceptedTypeSizeAndCurve_ReturnsError()
    {
        var collection = CreateEcdhCollection(new EccEcdhKeyAgreement(), new EccEcdhKeyAgreement());

        var error = collection.ValidateKeyAlgorithm(
            new KeyAlgorithmDeclaration
            {
                Key = CreateEccKey(ECCurve.NamedCurves.nistP256),
                Use = SecretKeyUses.Encryption,
                Algorithm = "ECDH-ES",
            }
        );

        Assert.Equal(
            "The algorithm 'ECDH-ES' is not compatible with the key, whose parameters it does not accept.",
            error
        );
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
    [InlineData(256, "secp256k1", "'secp256k1'")]
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
    public void TryResolveKeyGeneration_WhenTypeHasNoCurvesAndCurveIsDeclared_ReturnsError()
    {
        Assert.False(
            TryResolve(typeof(RsaSecretKey), 2048, "P-256", "PS256", out _, out var error)
        );

        Assert.NotNull(error);
        Assert.Contains("'P-256'", error, StringComparison.Ordinal);
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

    [Theory]
    [InlineData(null)]
    [InlineData(384)]
    public void TryResolveKeyGeneration_WhenCurveIsNotOneTheAlgorithmAccepts_ListsItsCurves(
        int? keySizeBits
    )
    {
        Assert.False(
            TryResolve(
                typeof(EccSecretKey),
                keySizeBits,
                "secp256k1",
                "ECDH-ES",
                out _,
                out var error
            )
        );

        Assert.Equal(
            "The curve 'secp256k1' is not valid for algorithm 'ECDH-ES'; expected 'P-256' (256 bits), "
                + "'P-384' (384 bits), 'P-521' (521 bits).",
            error
        );
    }

    // See ADR-1398: a curve fixes the size of its keys, and the key type's descriptor refuses another size when it
    // validates the generation parameters, so the resolver passes the declared size on.
    [Fact]
    public void TryResolveKeyGeneration_WhenCurveIsDeclaredWithASize_PassesTheSizeToTheKeyType()
    {
        Assert.True(
            TryResolve(
                typeof(EccSecretKey),
                256,
                "P-384",
                "ECDH-ES",
                out var resolved,
                out var error
            )
        );

        Assert.Null(error);
        Assert.NotNull(resolved);
        Assert.Equal("P-384", resolved.Curve);
        Assert.Equal(256, resolved.KeySizeBits);
    }

    // An agreement that contributes a Brainpool curve beside the NIST curves, so two of the algorithm's curves share
    // the 256-bit size.
    private sealed class BrainpoolEcdhKeyAgreement : IEcdhKeyAgreement
    {
        public AlgorithmKeyType AlgorithmKeyType { get; } =
            AlgorithmKeyType.OnCurves(
                typeof(EccSecretKey),
                [new("P-256", 256), new("brainpoolP256r1", 256), new("P-384", 384)]
            );

        public int GetSharedSecretSizeBytes(SecretKey secretKey) =>
            throw new NotSupportedException();

        public void AgreeWithEphemeralKey(
            SecretKey recipientKey,
            IBufferWriter<byte> ephemeralPublicKeyWriter,
            Span<byte> sharedSecret
        ) => throw new NotSupportedException();

        public void Agree(
            SecretKey recipientKey,
            SecretKey ephemeralPublicKey,
            Span<byte> sharedSecret
        ) => throw new NotSupportedException();
    }

    [Fact]
    public void TryResolveKeyGeneration_WhenTwoCurvesHaveTheDeclaredSize_ReturnsError()
    {
        var collection = CreateEcdhCollection(new BrainpoolEcdhKeyAgreement());

        var result = collection.TryResolveKeyGeneration(
            new KeyGenerationDeclaration
            {
                KeyClrType = typeof(EccSecretKey),
                KeySizeBits = 256,
                Algorithm = "ECDH-ES",
            },
            out var resolved,
            out var error
        );

        Assert.False(result);
        Assert.Null(resolved);
        Assert.Equal(
            "The key size '256' bits does not select a curve for algorithm 'ECDH-ES', which accepts 'P-256', "
                + "'brainpoolP256r1' at that size; name the curve.",
            error
        );
    }

    [Theory]
    [InlineData(null, "brainpoolP256r1", "brainpoolP256r1", 256)]
    [InlineData(256, "brainpoolP256r1", "brainpoolP256r1", 256)]
    [InlineData(null, "P-256", "P-256", 256)]
    [InlineData(384, null, "P-384", 384)]
    [InlineData(null, null, "P-256", 256)]
    public void TryResolveKeyGeneration_WhenTwoCurvesShareASize_ResolvesWhatSelectsOne(
        int? keySizeBits,
        string? curve,
        string expectedCurve,
        int expectedKeySizeBits
    )
    {
        var collection = CreateEcdhCollection(new BrainpoolEcdhKeyAgreement());

        var result = collection.TryResolveKeyGeneration(
            new KeyGenerationDeclaration
            {
                KeyClrType = typeof(EccSecretKey),
                KeySizeBits = keySizeBits,
                Curve = curve,
                Algorithm = "ECDH-ES",
            },
            out var resolved,
            out var error
        );

        Assert.True(result);
        Assert.Null(error);
        Assert.NotNull(resolved);
        Assert.Equal(expectedCurve, resolved.Curve);
        Assert.Equal(expectedKeySizeBits, resolved.KeySizeBits);
    }

    [Fact]
    public void TryResolveKeyGeneration_WhenAlgorithmAcceptsNoKeySize_ReturnsError()
    {
        var collection = CreateEcdhCollection(new SizelessEcdhKeyAgreement());

        var result = collection.TryResolveKeyGeneration(
            new KeyGenerationDeclaration
            {
                KeyClrType = typeof(EccSecretKey),
                Algorithm = "ECDH-ES",
            },
            out var resolved,
            out var error
        );

        Assert.False(result);
        Assert.Null(resolved);
        Assert.Equal("The algorithm 'ECDH-ES' accepts no key size.", error);
    }

    #endregion

    #region ValidateKeySize Tests

    private string? ValidateSize(Type keyClrType, int keySizeBits, string? use, string algorithm) =>
        AlgorithmCollection.ValidateKeySize(
            new KeySizeDeclaration
            {
                KeyClrType = keyClrType,
                KeySizeBits = keySizeBits,
                Use = use,
                Algorithm = algorithm,
            }
        );

    [Theory]
    [InlineData(null)]
    [InlineData("sig")]
    public void ValidateKeySize_WhenAlgorithmAcceptsTheTypeAndSize_ReturnsNull(string? use)
    {
        Assert.Null(ValidateSize(typeof(SymmetricSecretKey), 256, use, "HS256"));
    }

    [Fact]
    public void ValidateKeySize_WhenSizeIsIllegal_ReturnsError()
    {
        var error = ValidateSize(typeof(SymmetricSecretKey), 128, null, "HS256");

        Assert.NotNull(error);
        Assert.Contains("'128'", error, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateKeySize_WhenUseContradictsAlgorithm_ReturnsError()
    {
        var error = ValidateSize(
            typeof(SymmetricSecretKey),
            256,
            SecretKeyUses.Encryption,
            "HS256"
        );

        Assert.NotNull(error);
        Assert.Contains("'sig'", error, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateKeySize_WhenKeyTypeIsIncompatible_ReturnsError()
    {
        var error = ValidateSize(typeof(SymmetricSecretKey), 256, null, "RS256");

        Assert.NotNull(error);
        Assert.Contains("type", error, StringComparison.Ordinal);
    }

    // See ADR-1398: an asymmetric key has parameters its type and size do not show, so it is checked by the key.
    [Theory]
    [InlineData("RS256", 2048)]
    [InlineData("RSA-OAEP-256", 3072)]
    public void ValidateKeySize_WhenKeyTypeIsAsymmetric_ReturnsError(
        string algorithm,
        int keySizeBits
    )
    {
        var error = ValidateSize(typeof(RsaSecretKey), keySizeBits, null, algorithm);

        Assert.Equal(
            $"The algorithm '{algorithm}' is checked against an asymmetric key by the key itself, whose parameters "
                + "its type and size do not show.",
            error
        );
    }

    // Type and size do not show a key's curve, so they never show that a curve-bound algorithm accepts the key.
    [Fact]
    public void ValidateKeySize_WhenAlgorithmBindsTheTypeToCurves_ReturnsError()
    {
        var error = ValidateSize(typeof(EccSecretKey), 256, null, "ES256");

        Assert.NotNull(error);
        Assert.Contains("curves", error, StringComparison.Ordinal);
    }

    #endregion
}
