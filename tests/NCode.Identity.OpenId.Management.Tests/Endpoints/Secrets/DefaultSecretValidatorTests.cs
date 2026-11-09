// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Buffers.Text;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.Jose;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.OpenId.Management.Contracts.Secrets;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.KeyMaterial;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.KeyTypes;
using NCode.Identity.Secrets.Persistence;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Secrets.RemoteKeys;

namespace NCode.Identity.OpenId.Management.Endpoints.Secrets;

public sealed class DefaultSecretValidatorTests : IAsyncLifetime
{
    private const string BrainpoolAlgorithm = "X-ES256-BP";

    private static EccCurveSpecification Brainpool256 { get; } =
        new()
        {
            CurveName = "brainpoolP256r1",
            CurveSizeBits = 256,
            Curve = ECCurve.NamedCurves.brainpoolP256r1,
            Domain = EccCurveDomain.FromCurve(ECCurve.NamedCurves.brainpoolP256r1),
        };

    private ServiceProvider ServiceProvider { get; }
    private DefaultSecretValidator Validator { get; }
    private MockRepository Mocks { get; } = new(MockBehavior.Strict);
    private Mock<IRemoteKeyDescriptorCatalog> MockCatalog { get; }
    private DefaultSecretValidator RemoteValidator { get; }

    public DefaultSecretValidatorTests()
    {
        // Resolve the real algorithm collection and key-type catalog so the test asserts against the exact set
        // of algorithms and key types the server supports — the same source of truth the validator reads.
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddLogging();
        var secrets = serviceCollection.AddSecretsLibrary();
        secrets.AddPersistenceLibrary();
        secrets.AddJoseLibrary();
        // A second 256-bit curve and an ECDSA algorithm bound to it, as the opt-in secp256k1 package contributes ES256K.
        serviceCollection.AddSingleton(Brainpool256);
        serviceCollection.AddSingleton(
            new EccSignatureAlgorithmSpecification
            {
                Code = BrainpoolAlgorithm,
                Curve = Brainpool256,
                HashAlgorithmName = HashAlgorithmName.SHA256,
            }
        );
        ServiceProvider = serviceCollection.BuildServiceProvider();

        Validator = new DefaultSecretValidator(
            ServiceProvider.GetRequiredService<IAlgorithmCollectionProvider>(),
            ServiceProvider.GetRequiredService<ISecretKeyTypeCatalog>(),
            ServiceProvider.GetRequiredService<ISecretSerializer>(),
            ServiceProvider.GetRequiredService<IRemoteKeyDescriptorCatalog>()
        );

        MockCatalog = Mocks.Create<IRemoteKeyDescriptorCatalog>();
        RemoteValidator = new DefaultSecretValidator(
            ServiceProvider.GetRequiredService<IAlgorithmCollectionProvider>(),
            ServiceProvider.GetRequiredService<ISecretKeyTypeCatalog>(),
            ServiceProvider.GetRequiredService<ISecretSerializer>(),
            MockCatalog.Object
        );
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        Mocks.Verify();
        await ServiceProvider.DisposeAsync();
    }

    private static DateTimeOffset CreatedWhen { get; } = DateTimeOffset.UnixEpoch;

    private static CreateSecretRequest CreateRequest(
        string secretType,
        int keySizeBits,
        string? use = null,
        string? algorithm = null,
        string? curve = null
    ) =>
        new()
        {
            SecretType = secretType,
            KeySizeBits = keySizeBits,
            Curve = curve,
            Use = use,
            Algorithm = algorithm,
            ExpiresWhen = DateTimeOffset.UnixEpoch.AddYears(1),
        };

    private ManagementError? CreateError(CreateSecretRequest request) =>
        Validator.ValidateCreate(request, CreatedWhen).Error;

    #region Lifecycle Tests

    [Fact]
    public void ValidateCreate_WhenTheLifecycleIsInOrder_ReturnsNull()
    {
        var request = new CreateSecretRequest
        {
            SecretType = "rsa",
            KeySizeBits = 2048,
            ExpiresWhen = CreatedWhen.AddDays(99),
            ActivatesWhen = CreatedWhen.AddDays(2),
            RetiresWhen = CreatedWhen.AddDays(92),
        };

        Assert.Null(CreateError(request));
    }

    [Fact]
    public void ValidateCreate_WhenTheActivationPrecedesTheCreation_Returns400()
    {
        var request = new CreateSecretRequest
        {
            SecretType = "rsa",
            KeySizeBits = 2048,
            ExpiresWhen = CreatedWhen.AddDays(99),
            ActivatesWhen = CreatedWhen.AddSeconds(-1),
        };

        var error = CreateError(request);

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status400BadRequest, error.StatusCode);
        Assert.Contains("cannot activate", error.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateCreate_WhenTheRetirementFollowsTheExpiry_Returns400()
    {
        var request = new CreateSecretRequest
        {
            SecretType = "rsa",
            KeySizeBits = 2048,
            ExpiresWhen = CreatedWhen.AddDays(99),
            RetiresWhen = CreatedWhen.AddDays(100),
        };

        var error = CreateError(request);

        Assert.NotNull(error);
        Assert.Contains("cannot expire", error.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateImport_WhenTheLifecycleIsOutOfOrder_Returns400()
    {
        var request = new ImportSecretRequest
        {
            SecretType = SecretTypes.Rsa,
            Format = SecretKeyImportFormats.Jwk,
            KeyMaterial = "{}",
            ExpiresWhen = CreatedWhen.AddDays(99),
            ActivatesWhen = CreatedWhen.AddDays(3),
            RetiresWhen = CreatedWhen.AddDays(2),
        };

        var error = Validator.ValidateImport(request, CreatedWhen);

        Assert.NotNull(error);
        Assert.Contains("cannot retire", error.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateUpdate_WhenTheActivationPrecedesTheKeysCreation_Returns400()
    {
        var key = CreateRsaKey();
        var request = new UpdateSecretRequest
        {
            Use = null,
            Algorithm = null,
            ExpiresWhen = key.ExpiresWhen,
            ActivatesWhen = key.CreatedWhen.AddSeconds(-1),
        };

        var error = Validator.ValidateUpdate(key, request);

        Assert.NotNull(error);
        Assert.Contains("cannot activate", error.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateUpdate_WhenTheInstantsAreOmitted_JudgesTheStoredOnesAgainstTheNewExpiry()
    {
        // The stored activation lies past the new expiry, which the update keeps, so the update is refused as the
        // handler would write it.
        var key = CreateRsaKey(
            activatesWhen: CreatedWhen.AddDays(20),
            retiresWhen: CreatedWhen.AddDays(92)
        );
        var request = new UpdateSecretRequest
        {
            Use = null,
            Algorithm = null,
            ExpiresWhen = CreatedWhen.AddDays(10),
        };

        var error = Validator.ValidateUpdate(key, request);

        Assert.NotNull(error);
        Assert.Contains("cannot retire", error.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateUpdate_WhenTheInstantsAreOmittedAndTheStoredOnesFitTheNewExpiry_ReturnsNull()
    {
        var key = CreateRsaKey(
            activatesWhen: CreatedWhen.AddDays(2),
            retiresWhen: CreatedWhen.AddDays(92)
        );
        var request = new UpdateSecretRequest
        {
            Use = null,
            Algorithm = null,
            ExpiresWhen = CreatedWhen.AddDays(10),
        };

        Assert.Null(Validator.ValidateUpdate(key, request));
    }

    [Fact]
    public void ValidateUpdate_WhenTheLifecycleIsInOrder_ReturnsNull()
    {
        var key = CreateRsaKey();
        var request = new UpdateSecretRequest
        {
            Use = null,
            Algorithm = null,
            ExpiresWhen = key.ExpiresWhen,
            ActivatesWhen = key.CreatedWhen.AddDays(1),
            RetiresWhen = key.ExpiresWhen.AddDays(-1),
        };

        Assert.Null(Validator.ValidateUpdate(key, request));
    }

    #endregion

    #region Curve Tests

    [Fact]
    public void ValidateCreate_WhenEccWithKnownCurve_ReturnsNull()
    {
        Assert.Null(CreateError(CreateRequest("ecc", 384, curve: "P-384")));
    }

    [Fact]
    public void ValidateCreate_WhenEccWithUnknownCurve_Returns400()
    {
        var error = CreateError(CreateRequest("ecc", 256, curve: "not-a-curve"));
        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status400BadRequest, error.StatusCode);
    }

    [Theory]
    [InlineData("symmetric", 256)]
    [InlineData("rsa", 2048)]
    public void ValidateCreate_WhenCurveOnNonEcc_Returns400(string secretType, int keySizeBits)
    {
        var error = CreateError(CreateRequest(secretType, keySizeBits, curve: "P-256"));
        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status400BadRequest, error.StatusCode);
    }

    [Theory]
    [InlineData("ES256", "brainpoolP256r1")]
    [InlineData(BrainpoolAlgorithm, "P-256")]
    [InlineData("ECDH-ES", "brainpoolP256r1")]
    public void ValidateCreate_WhenCurveIsNotOneTheAlgorithmAccepts_Returns400(
        string algorithm,
        string curve
    )
    {
        var error = CreateError(CreateRequest("ecc", 256, algorithm: algorithm, curve: curve));

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status400BadRequest, error.StatusCode);
        Assert.Contains($"'{curve}'", error.Detail, StringComparison.Ordinal);
    }

    // The key type selects P-256 for a 256-bit key; the algorithm's own curve is generated instead.
    [Fact]
    public void ValidateCreate_WhenAlgorithmIsBoundToACurveAndNoneIsNamed_ReturnsNull()
    {
        Assert.Null(CreateError(CreateRequest("ecc", 256, algorithm: BrainpoolAlgorithm)));
    }

    // See ADR-1398: a curve fixes the size of its keys, so a request that names a curve and another size is refused,
    // with or without an algorithm.
    [Theory]
    [InlineData(0, null, "P-384", 384)]
    [InlineData(256, null, "P-384", 384)]
    [InlineData(384, "ECDH-ES", "P-256", 256)]
    [InlineData(384, BrainpoolAlgorithm, "brainpoolP256r1", 256)]
    public void ValidateCreate_WhenTheCurvesSizeIsNotTheRequestedSize_Returns400(
        int keySizeBits,
        string? algorithm,
        string curve,
        int curveSizeBits
    )
    {
        var error = CreateError(
            CreateRequest("ecc", keySizeBits, algorithm: algorithm, curve: curve)
        );

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status400BadRequest, error.StatusCode);
        Assert.Equal(
            $"The ECC key size '{keySizeBits}' bits is invalid for the curve '{curve}', whose keys are "
                + $"{curveSizeBits} bits.",
            error.Detail
        );
    }

    // Gives the ECDH-ES algorithms a Brainpool curve beside the core's NIST curves, so two of their curves share the
    // 256-bit size; validation never performs an agreement.
    private sealed class BrainpoolAgreementStub : IEcdhKeyAgreement
    {
        public AlgorithmKeyType AlgorithmKeyType { get; } =
            AlgorithmKeyType.OnCurves(
                typeof(EccSecretKey),
                [new AlgorithmCurve(Brainpool256.CurveName, Brainpool256.CurveSizeBits)]
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

    private static ServiceProvider BuildBrainpoolEcdhServiceProvider()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddLogging();
        var secrets = serviceCollection.AddSecretsLibrary();
        secrets.AddPersistenceLibrary();
        secrets.AddJoseLibrary();
        serviceCollection.AddSingleton(Brainpool256);
        serviceCollection.AddSingleton<IEcdhKeyAgreement, BrainpoolAgreementStub>();
        return serviceCollection.BuildServiceProvider();
    }

    private static DefaultSecretValidator CreateValidator(IServiceProvider serviceProvider) =>
        new(
            serviceProvider.GetRequiredService<IAlgorithmCollectionProvider>(),
            serviceProvider.GetRequiredService<ISecretKeyTypeCatalog>(),
            serviceProvider.GetRequiredService<ISecretSerializer>(),
            serviceProvider.GetRequiredService<IRemoteKeyDescriptorCatalog>()
        );

    // See ADR-1398: P-256 and brainpoolP256r1 are both 256 bits, so the size selects neither.
    [Fact]
    public async Task ValidateCreate_WhenTwoOfTheAlgorithmsCurvesHaveTheRequestedSize_Returns400()
    {
        await using var serviceProvider = BuildBrainpoolEcdhServiceProvider();

        var error = CreateValidator(serviceProvider)
            .ValidateCreate(CreateRequest("ecc", 256, algorithm: "ECDH-ES"), CreatedWhen)
            .Error;

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status400BadRequest, error.StatusCode);
        Assert.Equal(
            "The key size '256' bits does not select a curve for algorithm 'ECDH-ES', which accepts 'P-256', "
                + "'brainpoolP256r1' at that size; name the curve.",
            error.Detail
        );
    }

    [Theory]
    [InlineData("P-256")]
    [InlineData("brainpoolP256r1")]
    public async Task ValidateCreate_WhenTheCurveSelectsAmongCurvesOfOneSize_GeneratesOnIt(
        string curve
    )
    {
        await using var serviceProvider = BuildBrainpoolEcdhServiceProvider();

        var validation = CreateValidator(serviceProvider)
            .ValidateCreate(
                CreateRequest("ecc", 256, algorithm: "ECDH-ES", curve: curve),
                CreatedWhen
            );

        Assert.True(validation.IsValid);
        Assert.Equal(curve, validation.GenerationParameters.Curve);
        Assert.Equal(256, validation.GenerationParameters.KeySizeBits);
    }

    #endregion

    #region GenerationParameters Tests

    [Theory]
    [InlineData("ES256", 256, "P-256")]
    [InlineData(BrainpoolAlgorithm, 256, "brainpoolP256r1")]
    [InlineData("ECDH-ES", 384, "P-384")]
    [InlineData("ECDH-ES", 521, "P-521")]
    public void ValidateCreate_WhenRequestNamesNoCurve_GeneratesOnTheAlgorithmsCurveOfTheSize(
        string algorithm,
        int keySizeBits,
        string expected
    )
    {
        var validation = Validator.ValidateCreate(
            CreateRequest("ecc", keySizeBits, algorithm: algorithm),
            CreatedWhen
        );

        Assert.True(validation.IsValid);
        Assert.Equal(expected, validation.GenerationParameters.Curve);
        Assert.Equal(keySizeBits, validation.GenerationParameters.KeySizeBits);
    }

    [Fact]
    public void ValidateCreate_WhenRequestNamesACurveTheAlgorithmAccepts_GeneratesOnIt()
    {
        var validation = Validator.ValidateCreate(
            CreateRequest("ecc", 384, algorithm: "ECDH-ES", curve: "P-384"),
            CreatedWhen
        );

        Assert.True(validation.IsValid);
        Assert.Equal("P-384", validation.GenerationParameters.Curve);
        Assert.Equal(384, validation.GenerationParameters.KeySizeBits);
    }

    [Theory]
    [InlineData("ecc", 384, null, null)]
    [InlineData("ecc", 384, null, "P-384")]
    [InlineData("rsa", 2048, "RS256", null)]
    public void ValidateCreate_WhenNoAlgorithmCurveApplies_GeneratesWithTheRequestsParameters(
        string secretType,
        int keySizeBits,
        string? algorithm,
        string? curve
    )
    {
        var validation = Validator.ValidateCreate(
            CreateRequest(secretType, keySizeBits, algorithm: algorithm, curve: curve),
            CreatedWhen
        );

        Assert.True(validation.IsValid);
        Assert.Equal(curve, validation.GenerationParameters.Curve);
        Assert.Equal(keySizeBits, validation.GenerationParameters.KeySizeBits);
    }

    [Fact]
    public void ValidateCreate_WhenRequestIsNotCoherent_HasNoGenerationParameters()
    {
        var validation = Validator.ValidateCreate(CreateRequest("unknown", 256), CreatedWhen);

        Assert.False(validation.IsValid);
        Assert.Null(validation.GenerationParameters);
        Assert.Equal(StatusCodes.Status400BadRequest, validation.Error.StatusCode);
    }

    #endregion

    #region SecretType Tests

    [Theory]
    [InlineData("symmetric", 256)]
    [InlineData("rsa", 2048)]
    [InlineData("ecc", 384)]
    public void ValidateCreate_WhenKnownSecretTypeAndNoUseOrAlgorithm_ReturnsNull(
        string secretType,
        int keySizeBits
    )
    {
        var error = CreateError(CreateRequest(secretType, keySizeBits));

        Assert.Null(error);
    }

    [Theory]
    [InlineData("")]
    [InlineData("oct")]
    [InlineData("RSA")]
    [InlineData("unknown")]
    public void ValidateCreate_WhenUnknownSecretType_Returns400(string secretType)
    {
        var error = CreateError(CreateRequest(secretType, 256));

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status400BadRequest, error.StatusCode);
    }

    #endregion

    #region KeySize Tests

    [Theory]
    [InlineData(0)]
    [InlineData(64)]
    [InlineData(100)]
    [InlineData(-128)]
    public void ValidateCreate_WhenSymmetricKeySizeInvalid_Returns400(int keySizeBits)
    {
        var error = CreateError(CreateRequest("symmetric", keySizeBits));

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status400BadRequest, error.StatusCode);
    }

    [Theory]
    [InlineData(1024)]
    [InlineData(2047)]
    [InlineData(32768)]
    public void ValidateCreate_WhenRsaKeySizeInvalid_Returns400(int keySizeBits)
    {
        var error = CreateError(CreateRequest("rsa", keySizeBits));

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status400BadRequest, error.StatusCode);
    }

    [Theory]
    [InlineData(2048)]
    [InlineData(3072)]
    [InlineData(4096)]
    public void ValidateCreate_WhenRsaKeySizeValid_ReturnsNull(int keySizeBits)
    {
        var error = CreateError(CreateRequest("rsa", keySizeBits));

        Assert.Null(error);
    }

    [Theory]
    [InlineData(255)]
    [InlineData(512)]
    [InlineData(520)]
    public void ValidateCreate_WhenEccKeySizeInvalid_Returns400(int keySizeBits)
    {
        var error = CreateError(CreateRequest("ecc", keySizeBits));

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status400BadRequest, error.StatusCode);
    }

    [Theory]
    [InlineData(384)]
    [InlineData(521)]
    public void ValidateCreate_WhenEccKeySizeValid_ReturnsNull(int keySizeBits)
    {
        var error = CreateError(CreateRequest("ecc", keySizeBits));

        Assert.Null(error);
    }

    // See ADR-1398: a request without an algorithm reaches the key type alone, whose catalog holds P-256 and
    // brainpoolP256r1 at 256 bits, so the size selects neither.
    [Fact]
    public void ValidateCreate_WhenTwoCatalogCurvesHaveTheRequestedSizeAndNoAlgorithm_Returns400()
    {
        var error = CreateError(CreateRequest("ecc", 256));

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status400BadRequest, error.StatusCode);
        Assert.Equal(
            "The ECC key size '256' bits does not select a curve; 'P-256', 'brainpoolP256r1' have that size, so name "
                + "the curve.",
            error.Detail
        );
    }

    [Theory]
    [InlineData("P-256")]
    [InlineData("brainpoolP256r1")]
    public void ValidateCreate_WhenTheCurveSelectsAmongCatalogCurvesOfOneSizeAndNoAlgorithm_GeneratesOnIt(
        string curve
    )
    {
        var validation = Validator.ValidateCreate(
            CreateRequest("ecc", 256, curve: curve),
            CreatedWhen
        );

        Assert.True(validation.IsValid);
        Assert.Equal(curve, validation.GenerationParameters.Curve);
        Assert.Equal(256, validation.GenerationParameters.KeySizeBits);
    }

    #endregion

    #region Use Tests

    [Theory]
    [InlineData("sig")]
    [InlineData("enc")]
    public void ValidateCreate_WhenKnownUse_ReturnsNull(string use)
    {
        var error = CreateError(CreateRequest("symmetric", 256, use));

        Assert.Null(error);
    }

    [Theory]
    [InlineData("")]
    [InlineData("signature")]
    [InlineData("SIG")]
    public void ValidateCreate_WhenUnknownUse_Returns400(string use)
    {
        var error = CreateError(CreateRequest("symmetric", 256, use));

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status400BadRequest, error.StatusCode);
    }

    #endregion

    #region Algorithm Compatibility Tests

    [Theory]
    [InlineData("rsa", 2048, "sig", "RS256")]
    [InlineData("rsa", 2048, "sig", "PS512")]
    [InlineData("ecc", 256, "sig", "ES256")]
    [InlineData("symmetric", 256, "sig", "HS256")]
    [InlineData("rsa", 2048, "enc", "RSA-OAEP-256")]
    [InlineData("ecc", 256, "enc", "ECDH-ES")]
    [InlineData("symmetric", 256, "enc", "A256KW")]
    public void ValidateCreate_WhenAlgorithmMatchesUseAndType_ReturnsNull(
        string secretType,
        int keySizeBits,
        string use,
        string algorithm
    )
    {
        var error = CreateError(CreateRequest(secretType, keySizeBits, use, algorithm));

        Assert.Null(error);
    }

    [Fact]
    public void ValidateCreate_WhenSignatureAlgorithmPairedWithEncryptionUse_Returns400()
    {
        var error = CreateError(CreateRequest("rsa", 2048, "enc", "RS256"));

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status400BadRequest, error.StatusCode);
    }

    [Fact]
    public void ValidateCreate_WhenEncryptionAlgorithmPairedWithSignatureUse_Returns400()
    {
        var error = CreateError(CreateRequest("rsa", 2048, "sig", "RSA-OAEP"));

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status400BadRequest, error.StatusCode);
    }

    [Fact]
    public void ValidateCreate_WhenAlgorithmRequiresDifferentSecretType_Returns400()
    {
        var error = CreateError(CreateRequest("ecc", 256, "sig", "RS256"));

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status400BadRequest, error.StatusCode);
    }

    [Fact]
    public void ValidateCreate_WhenAlgorithmIncompatibleWithTypeAndNoUse_Returns400()
    {
        var error = CreateError(CreateRequest("symmetric", 256, use: null, algorithm: "RS256"));

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status400BadRequest, error.StatusCode);
    }

    [Fact]
    public void ValidateCreate_WhenKeySizeIllegalForAlgorithm_Returns400()
    {
        // RS256 requires a key of at least 2048 bits (RFC 7518); 1024 is a multiple of 8 but too small.
        var error = CreateError(CreateRequest("rsa", 1024, "sig", "RS256"));

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status400BadRequest, error.StatusCode);
    }

    [Fact]
    public void ValidateCreate_WhenAlgorithmNotRegistered_Returns400()
    {
        // An algorithm the server does not support can never be used, so creation is rejected.
        var error = CreateError(CreateRequest("rsa", 2048, "sig", algorithm: "CUSTOM-ALG"));

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status400BadRequest, error.StatusCode);
    }

    #endregion

    #region ValidateUpdate Tests

    private static PersistedSecret CreateRsaKey(
        DateTimeOffset? activatesWhen = null,
        DateTimeOffset? retiresWhen = null
    )
    {
        using var rsa = RSA.Create(2048);
        return CreatePublicKey(
            SecretTypes.Rsa,
            2048,
            rsa.ExportSubjectPublicKeyInfo(),
            "PS256",
            activatesWhen,
            retiresWhen
        );
    }

    private static PersistedSecret CreateEccKey(ECCurve curve)
    {
        using var ecdsa = ECDsa.Create(curve);
        return CreatePublicKey(SecretTypes.Ecc, 256, ecdsa.ExportSubjectPublicKeyInfo(), null);
    }

    private static PersistedSecret CreatePublicKey(
        string secretType,
        int keySizeBits,
        byte[] subjectPublicKeyInfo,
        string? algorithm,
        DateTimeOffset? activatesWhen = null,
        DateTimeOffset? retiresWhen = null
    ) =>
        new PersistedSecretBuilder
        {
            SecretId = "key-1",
            Use = "sig",
            Algorithm = algorithm,
            ActivatesWhen = activatesWhen ?? DateTimeOffset.UnixEpoch,
            RetiresWhen = retiresWhen ?? DateTimeOffset.UnixEpoch.AddYears(1),
            SecretType = secretType,
            SecretEncoding = SecretEncodings.Spki,
            KeySizeBits = keySizeBits,
            EncodedValue = Base64Url.EncodeToString(subjectPublicKeyInfo),
        }.Build();

    // A brainpoolP256r1 key has P-256's type and size, so only its curve shows that ES256 does not accept it.
    [Fact]
    public void ValidateUpdate_WhenAlgorithmRefusesTheKeysCurve_ReturnsBadRequest()
    {
        var error = Validator.ValidateUpdate(
            CreateEccKey(ECCurve.NamedCurves.brainpoolP256r1),
            CreateUpdateRequest("sig", "ES256")
        );

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status400BadRequest, error.StatusCode);
        Assert.Contains("'brainpoolP256r1'", error.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateUpdate_WhenAlgorithmAcceptsTheKeysCurve_ReturnsNull()
    {
        var error = Validator.ValidateUpdate(
            CreateEccKey(ECCurve.NamedCurves.nistP256),
            CreateUpdateRequest("sig", "ES256")
        );

        Assert.Null(error);
    }

    private static UpdateSecretRequest CreateUpdateRequest(string? use, string? algorithm) =>
        new()
        {
            Use = use,
            Algorithm = algorithm,
            ExpiresWhen = DateTimeOffset.UnixEpoch.AddYears(2),
        };

    [Theory]
    [InlineData(null, null)]
    [InlineData("sig", null)]
    [InlineData("sig", "PS256")]
    [InlineData("enc", "RSA-OAEP-256")]
    [InlineData(null, "RS256")]
    public void ValidateUpdate_WhenMetadataFitsTheKey_ReturnsNull(string? use, string? algorithm)
    {
        var error = Validator.ValidateUpdate(CreateRsaKey(), CreateUpdateRequest(use, algorithm));

        Assert.Null(error);
    }

    [Theory]
    [InlineData("both", null)]
    [InlineData("sig", "ES256")]
    [InlineData("sig", "RSA-OAEP-256")]
    [InlineData("enc", "PS256")]
    [InlineData("sig", "HS256")]
    [InlineData(null, "not-an-algorithm")]
    public void ValidateUpdate_WhenMetadataDoesNotFitTheKey_ReturnsBadRequest(
        string? use,
        string? algorithm
    )
    {
        var error = Validator.ValidateUpdate(CreateRsaKey(), CreateUpdateRequest(use, algorithm));

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status400BadRequest, error.StatusCode);
    }

    // A symmetric key has no stored public half, and no curve, so its type and size are what the algorithm checks.
    [Theory]
    [InlineData(256, "HS256", true)]
    [InlineData(256, "RS256", false)]
    [InlineData(256, "not-an-algorithm", false)]
    public void ValidateUpdate_WhenKeyIsSymmetric_ChecksItsTypeAndSize(
        int keySizeBits,
        string algorithm,
        bool expected
    )
    {
        var secret = new PersistedSecretBuilder
        {
            SecretId = "key-1",
            Use = "sig",
            KeySizeBits = keySizeBits,
            EncodedValue = "protected",
        }.Build();

        var error = Validator.ValidateUpdate(secret, CreateUpdateRequest("sig", algorithm));

        Assert.Equal(expected, error is null);
    }

    [Fact]
    public void ValidateUpdate_WhenSecretTypeIsUnknown_ReturnsBadRequest()
    {
        var secret = CreateRsaKey();
        secret = new PersistedSecretBuilder
        {
            SecretId = secret.SecretId,
            ConcurrencyToken = secret.ConcurrencyToken,
            Use = secret.Use,
            Algorithm = secret.Algorithm,
            CreatedWhen = secret.CreatedWhen,
            ExpiresWhen = secret.ExpiresWhen,
            ActivatesWhen = secret.CreatedWhen,
            RetiresWhen = secret.ExpiresWhen,
            SecretType = "unknown",
            SecretEncoding = secret.SecretEncoding,
            Origin = secret.Origin,
            KeySizeBits = secret.KeySizeBits,
            EncodedValue = secret.EncodedValue,
        }.Build();

        var error = Validator.ValidateUpdate(secret, CreateUpdateRequest("sig", "PS256"));

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status400BadRequest, error.StatusCode);
    }

    private static PersistedSecret CreateRemoteEccKey()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        return new PersistedSecretBuilder
        {
            SecretId = "key-1",
            Use = "sig",
            Algorithm = "ES256",
            ActivatesWhen = DateTimeOffset.UnixEpoch,
            RetiresWhen = DateTimeOffset.UnixEpoch.AddYears(1),
            SecretType = SecretTypes.Ecc,
            SecretEncoding = SecretEncodings.Remote,
            KeySizeBits = 256,
            EncodedValue = "aws-kms:arn:aws:kms:us-west-2:111122223333:key/k",
            PublicKey = Base64Url.EncodeToString(ecdsa.ExportSubjectPublicKeyInfo()),
        }.Build();
    }

    [Fact]
    public void ValidateUpdate_WhenARemoteKeysAlgorithmChanges_ReturnsBadRequest()
    {
        var error = Validator.ValidateUpdate(
            CreateRemoteEccKey(),
            CreateUpdateRequest("sig", "ES384")
        );

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status400BadRequest, error.StatusCode);
        Assert.Contains("'ES256'", error.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateUpdate_WhenARemoteKeysAlgorithmIsRemoved_ReturnsBadRequest()
    {
        var error = Validator.ValidateUpdate(
            CreateRemoteEccKey(),
            CreateUpdateRequest("sig", null)
        );

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status400BadRequest, error.StatusCode);
    }

    [Fact]
    public void ValidateUpdate_WhenARemoteKeyKeepsItsAlgorithm_ReturnsNull()
    {
        var error = Validator.ValidateUpdate(
            CreateRemoteEccKey(),
            CreateUpdateRequest("sig", "ES256")
        );

        Assert.Null(error);
    }

    #endregion

    #region ValidateImport Tests

    private static ImportSecretRequest CreateImportRequest(string secretType, string format) =>
        new()
        {
            SecretType = secretType,
            Format = format,
            KeyMaterial = "material",
            ExpiresWhen = DateTimeOffset.UnixEpoch.AddYears(1),
        };

    [Theory]
    [InlineData(SecretTypes.Rsa, SecretKeyImportFormats.Jwk)]
    [InlineData(SecretTypes.Rsa, SecretKeyImportFormats.Pem)]
    [InlineData(SecretTypes.Ecc, SecretKeyImportFormats.Certificate)]
    public void ValidateImport_WhenTheSecretTypeImportsTheFormat_ReturnsNull(
        string secretType,
        string format
    )
    {
        var error = Validator.ValidateImport(CreateImportRequest(secretType, format), CreatedWhen);

        Assert.Null(error);
    }

    [Theory]
    [InlineData("JWK")]
    [InlineData("openssh")]
    [InlineData("")]
    public void ValidateImport_WhenTheSecretTypeDoesNotImportTheFormat_ReturnsBadRequestListingItsFormats(
        string format
    )
    {
        var error = Validator.ValidateImport(
            CreateImportRequest(SecretTypes.Rsa, format),
            CreatedWhen
        );

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status400BadRequest, error.StatusCode);
        Assert.Contains($"'{format}'", error.Detail, StringComparison.Ordinal);
        Assert.Contains("'jwk', 'pem', 'certificate'", error.Detail, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(SecretTypes.Symmetric)]
    [InlineData("unknown")]
    public void ValidateImport_WhenTheSecretTypeImportsNoPublicKey_ReturnsBadRequest(
        string secretType
    )
    {
        var error = Validator.ValidateImport(
            CreateImportRequest(secretType, SecretKeyImportFormats.Jwk),
            CreatedWhen
        );

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status400BadRequest, error.StatusCode);
        Assert.Contains($"'{secretType}'", error.Detail, StringComparison.Ordinal);
    }

    #endregion

    #region ValidateCreateRemote Tests

    private const string ProviderName = RemoteKeyProviderNames.AwsKms;

    private void SetupDescribedProviders(params string[] names) =>
        MockCatalog
            .Setup(x => x.Providers)
            .Returns(
                names.Select(name => new RemoteKeyProviderDescriptor { Name = name }).ToArray()
            )
            .Verifiable();

    private void SetupDescribedProvidersThatCreateNoKeys(params string[] names) =>
        MockCatalog
            .Setup(x => x.Providers)
            .Returns(
                names
                    .Select(name => new RemoteKeyProviderDescriptor
                    {
                        Name = name,
                        CreatesKeys = false,
                    })
                    .ToArray()
            )
            .Verifiable();

    private void SetupRow(RemoteKeyAlgorithmSelector selector, RemoteKeyAlgorithm? row)
    {
        MockCatalog
            .Setup(x => x.TryGetAlgorithm(selector, out row))
            .Returns(row is not null)
            .Verifiable();
    }

    private static RemoteKeyAlgorithmSelector Es256Selector { get; } =
        new()
        {
            Provider = ProviderName,
            Algorithm = "ES256",
            SecretType = SecretTypes.Ecc,
            KeySizeBits = 256,
            Curve = "P-256",
        };

    private static RemoteSigningAlgorithm Es256Row { get; } =
        new()
        {
            Provider = ProviderName,
            Algorithm = "ES256",
            SecretType = SecretTypes.Ecc,
            KeySizeBits = 256,
            Curve = "P-256",
            KeySpec = "ECC_NIST_P256",
            SigningAlgorithm = "ECDSA_SHA_256",
        };

    private static RemoteKeyAlgorithmSelector Rsa3072OaepSelector { get; } =
        new()
        {
            Provider = ProviderName,
            Algorithm = "RSA-OAEP-256",
            SecretType = SecretTypes.Rsa,
            KeySizeBits = 3072,
        };

    private static RemoteDecryptionAlgorithm Rsa3072OaepRow { get; } =
        new()
        {
            Provider = ProviderName,
            Algorithm = "RSA-OAEP-256",
            SecretType = SecretTypes.Rsa,
            KeySizeBits = 3072,
            KeySpec = "RSA_3072",
            EncryptionAlgorithm = "RSAES_OAEP_SHA_256",
        };

    private static CreateRemoteSecretRequest CreateRemoteRequest(
        string secretType = SecretTypes.Ecc,
        int keySizeBits = 256,
        string algorithm = "ES256",
        string provider = ProviderName
    ) =>
        new()
        {
            RemoteKeyProvider = provider,
            SecretType = secretType,
            KeySizeBits = keySizeBits,
            Algorithm = algorithm,
            ExpiresWhen = CreatedWhen.AddYears(1),
        };

    [Fact]
    public void ValidateCreateRemote_WhenTheProviderHoldsTheKey_ResolvesTheCurveAndLooksTheRowUp()
    {
        SetupDescribedProviders(ProviderName);
        SetupRow(Es256Selector, Es256Row);

        var validation = RemoteValidator.ValidateCreateRemote(CreateRemoteRequest(), CreatedWhen);

        Assert.True(validation.IsValid);
        Assert.Equal("P-256", validation.GenerationParameters.Curve);
        Assert.Equal(256, validation.GenerationParameters.KeySizeBits);
    }

    [Fact]
    public void ValidateCreateRemote_WhenTheProviderCreatesNoKeys_ReturnsBadRequest()
    {
        SetupDescribedProvidersThatCreateNoKeys(ProviderName);
        SetupRow(Es256Selector, Es256Row);

        var error = RemoteValidator.ValidateCreateRemote(CreateRemoteRequest(), CreatedWhen).Error;

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status400BadRequest, error.StatusCode);
        Assert.Equal(RemoteKeyRefusals.CreatesNoKeys(ProviderName), error.Detail);
    }

    [Fact]
    public void ValidateCreateRemote_WhenTheProviderIsNotRegistered_ReturnsBadRequest()
    {
        SetupDescribedProviders(ProviderName);

        var error = RemoteValidator
            .ValidateCreateRemote(CreateRemoteRequest(provider: "unknown"), CreatedWhen)
            .Error;

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status400BadRequest, error.StatusCode);
        Assert.Equal(
            "The remote key provider 'unknown' is not registered; expected 'aws-kms'.",
            error.Detail
        );
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void ValidateCreateRemote_WhenTheProviderIsNotNamed_ReturnsBadRequest(string? provider)
    {
        SetupDescribedProviders(ProviderName);

        var error = RemoteValidator
            .ValidateCreateRemote(CreateRemoteRequest(provider: provider!), CreatedWhen)
            .Error;

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status400BadRequest, error.StatusCode);
        Assert.Equal("The remote key provider is not named; expected 'aws-kms'.", error.Detail);
    }

    [Fact]
    public void ValidateCreateRemote_WhenNoProviderIsRegistered_SaysSo()
    {
        SetupDescribedProviders();

        var error = RemoteValidator
            .ValidateCreateRemote(CreateRemoteRequest(provider: "unknown"), CreatedWhen)
            .Error;

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status400BadRequest, error.StatusCode);
        Assert.Equal(
            "The remote key provider 'unknown' is not registered; the deployment registers no remote key provider.",
            error.Detail
        );
    }

    [Fact]
    public void ValidateCreateRemote_WhenTheSecretTypeCannotBeHeldInAKeyManagementService_ReturnsBadRequest()
    {
        var error = RemoteValidator
            .ValidateCreateRemote(
                CreateRemoteRequest(secretType: SecretTypes.Symmetric, keySizeBits: 256),
                CreatedWhen
            )
            .Error;

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status400BadRequest, error.StatusCode);
        Assert.Equal(
            "The secret type 'symmetric' cannot be held in a key management service.",
            error.Detail
        );
    }

    [Fact]
    public void ValidateCreateRemote_WhenTheProviderHasNoRowForTheKey_ReturnsBadRequest()
    {
        SetupDescribedProviders(ProviderName);
        SetupRow(Es256Selector, null);

        var error = RemoteValidator.ValidateCreateRemote(CreateRemoteRequest(), CreatedWhen).Error;

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status400BadRequest, error.StatusCode);
        Assert.Equal(
            "The remote key provider 'aws-kms' holds no 'ES256' key of the 'ecc' type with 256 bits on the curve 'P-256'.",
            error.Detail
        );
    }

    [Fact]
    public void ValidateCreateRemote_WhenTheLifecycleIsOutOfOrder_ReturnsBadRequest()
    {
        var request = new CreateRemoteSecretRequest
        {
            RemoteKeyProvider = ProviderName,
            SecretType = SecretTypes.Ecc,
            KeySizeBits = 256,
            Algorithm = "ES256",
            ExpiresWhen = CreatedWhen.AddDays(99),
            ActivatesWhen = CreatedWhen.AddDays(3),
            RetiresWhen = CreatedWhen.AddDays(2),
        };

        var error = RemoteValidator.ValidateCreateRemote(request, CreatedWhen).Error;

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status400BadRequest, error.StatusCode);
        Assert.Contains("cannot retire", error.Detail, StringComparison.Ordinal);
    }

    #endregion

    #region ValidateImportRemote Tests

    private static ImportRemoteSecretRequest CreateImportRemoteRequest(
        string provider = ProviderName,
        string remoteKeyId = "key-1",
        string secretType = SecretTypes.Ecc,
        string algorithm = "ES256",
        int keySizeBits = 256,
        string? curve = null
    ) =>
        new()
        {
            RemoteKeyProvider = provider,
            RemoteKeyId = remoteKeyId,
            SecretType = secretType,
            KeySizeBits = keySizeBits,
            Curve = curve,
            Algorithm = algorithm,
            ExpiresWhen = CreatedWhen.AddYears(1),
        };

    [Fact]
    public void ValidateImportRemote_WhenTheProviderHoldsTheKey_ResolvesTheCurveAndLooksTheRowUp()
    {
        SetupDescribedProviders(ProviderName);
        SetupRow(Es256Selector, Es256Row);

        var validation = RemoteValidator.ValidateImportRemote(
            CreateImportRemoteRequest(),
            CreatedWhen
        );

        Assert.True(validation.IsValid);
        Assert.Equal("P-256", validation.GenerationParameters.Curve);
        Assert.Equal(256, validation.GenerationParameters.KeySizeBits);
    }

    [Fact]
    public void ValidateImportRemote_WhenTheProviderHasNoRowForTheKey_ReturnsBadRequest()
    {
        SetupDescribedProviders(ProviderName);
        SetupRow(Es256Selector, null);

        var validation = RemoteValidator.ValidateImportRemote(
            CreateImportRemoteRequest(),
            CreatedWhen
        );

        Assert.False(validation.IsValid);
        Assert.NotNull(validation.Error);
        Assert.Equal(StatusCodes.Status400BadRequest, validation.Error.StatusCode);
        Assert.Equal(
            "The remote key provider 'aws-kms' holds no 'ES256' key of the 'ecc' type with 256 bits on the curve 'P-256'.",
            validation.Error.Detail
        );
    }

    [Fact]
    public void ValidateImportRemote_WhenTheKeyIdentifierIsEmpty_ReturnsBadRequest()
    {
        var validation = RemoteValidator.ValidateImportRemote(
            CreateImportRemoteRequest(remoteKeyId: " "),
            CreatedWhen
        );

        Assert.False(validation.IsValid);
        Assert.NotNull(validation.Error);
        Assert.Equal(StatusCodes.Status400BadRequest, validation.Error.StatusCode);
        Assert.Equal(
            "The key management service's key identifier must not be empty.",
            validation.Error.Detail
        );
    }

    [Fact]
    public void ValidateImportRemote_WhenTheKeyIdIsTooLong_ReturnsTheLengthError()
    {
        var maxKeyIdLength = SecretMaxLengths.EncodedValue - ProviderName.Length - 1;

        var validation = RemoteValidator.ValidateImportRemote(
            CreateImportRemoteRequest(remoteKeyId: new string('k', SecretMaxLengths.EncodedValue)),
            CreatedWhen
        );

        Assert.False(validation.IsValid);
        Assert.NotNull(validation.Error);
        Assert.Equal(StatusCodes.Status400BadRequest, validation.Error.StatusCode);
        Assert.Equal(
            $"The key management service's key identifier must be at most {maxKeyIdLength} characters.",
            validation.Error.Detail
        );
    }

    [Fact]
    public void ValidateImportRemote_WhenTheKeyIdFitsExactly_IsValid()
    {
        SetupDescribedProviders(ProviderName);
        SetupRow(Es256Selector, Es256Row);
        var maxKeyIdLength = SecretMaxLengths.EncodedValue - ProviderName.Length - 1;

        var validation = RemoteValidator.ValidateImportRemote(
            CreateImportRemoteRequest(remoteKeyId: new string('k', maxKeyIdLength)),
            CreatedWhen
        );

        Assert.True(validation.IsValid);
    }

    [Fact]
    public void ValidateImportRemote_WhenTheKeyIdHoldsAControlCharacter_ReturnsTheCharacterError()
    {
        var validation = RemoteValidator.ValidateImportRemote(
            CreateImportRemoteRequest(remoteKeyId: "key\n1"),
            CreatedWhen
        );

        Assert.False(validation.IsValid);
        Assert.NotNull(validation.Error);
        Assert.Equal(StatusCodes.Status400BadRequest, validation.Error.StatusCode);
        Assert.Equal(
            "The key management service's key identifier must not hold control characters or surrounding white space.",
            validation.Error.Detail
        );
    }

    [Fact]
    public void ValidateImportRemote_WhenTheProviderIsNotRegistered_ReturnsBadRequest()
    {
        SetupDescribedProviders(ProviderName);

        var validation = RemoteValidator.ValidateImportRemote(
            CreateImportRemoteRequest(provider: "unknown"),
            CreatedWhen
        );

        Assert.False(validation.IsValid);
        Assert.NotNull(validation.Error);
        Assert.Equal(StatusCodes.Status400BadRequest, validation.Error.StatusCode);
        Assert.Equal(
            "The remote key provider 'unknown' is not registered; expected 'aws-kms'.",
            validation.Error.Detail
        );
    }

    [Fact]
    public void ValidateImportRemote_WhenAKeyManagementAlgorithmHasAnotherKeyType_ReturnsBadRequest()
    {
        SetupDescribedProviders(ProviderName);

        var validation = RemoteValidator.ValidateImportRemote(
            CreateImportRemoteRequest(algorithm: "RSA-OAEP"),
            CreatedWhen
        );

        Assert.False(validation.IsValid);
        Assert.NotNull(validation.Error);
        Assert.Equal(StatusCodes.Status400BadRequest, validation.Error.StatusCode);
        Assert.Equal(
            "The algorithm 'RSA-OAEP' is not compatible with the key's type.",
            validation.Error.Detail
        );
    }

    [Fact]
    public void ValidateImportRemote_KeyManagementAlgorithm_IsValid()
    {
        SetupDescribedProviders(ProviderName);
        SetupRow(Rsa3072OaepSelector, Rsa3072OaepRow);

        var validation = RemoteValidator.ValidateImportRemote(
            CreateImportRemoteRequest(
                secretType: SecretTypes.Rsa,
                algorithm: "RSA-OAEP-256",
                keySizeBits: 3072
            ),
            CreatedWhen
        );

        Assert.True(validation.IsValid);
        Assert.Equal(3072, validation.GenerationParameters.KeySizeBits);
    }

    [Fact]
    public void ValidateImportRemote_WhenTheAlgorithmSignsWithAnotherKeyType_ReturnsBadRequest()
    {
        SetupDescribedProviders(ProviderName);

        var validation = RemoteValidator.ValidateImportRemote(
            CreateImportRemoteRequest(algorithm: "RS256"),
            CreatedWhen
        );

        Assert.False(validation.IsValid);
        Assert.NotNull(validation.Error);
        Assert.Equal(StatusCodes.Status400BadRequest, validation.Error.StatusCode);
        Assert.Equal(
            "The algorithm 'RS256' is not compatible with the key's type.",
            validation.Error.Detail
        );
    }

    #endregion

    #region ValidateRemoteKey Tests

    [Fact]
    public void ValidateRemoteKey_WhenTheProviderHoldsTheRow_ResolvesTheKey()
    {
        SetupDescribedProviders(ProviderName);
        SetupRow(Es256Selector, Es256Row);

        var validation = RemoteValidator.ValidateRemoteKey(Es256Selector);

        Assert.True(validation.IsValid);
        Assert.Equal("P-256", validation.GenerationParameters.Curve);
        Assert.Equal(256, validation.GenerationParameters.KeySizeBits);
    }

    [Fact]
    public void ValidateRemoteKey_WhenTheProviderCreatesNoKeys_IsValid()
    {
        SetupDescribedProvidersThatCreateNoKeys(ProviderName);
        SetupRow(Es256Selector, Es256Row);

        var validation = RemoteValidator.ValidateRemoteKey(Es256Selector);

        Assert.True(validation.IsValid);
        Assert.Equal("P-256", validation.GenerationParameters.Curve);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void ValidateRemoteKey_WhenTheProviderIsNotNamed_ReturnsBadRequest(string? provider)
    {
        SetupDescribedProviders(ProviderName);

        var validation = RemoteValidator.ValidateRemoteKey(
            Es256Selector with
            {
                Provider = provider!,
            }
        );

        Assert.False(validation.IsValid);
        Assert.NotNull(validation.Error);
        Assert.Equal(StatusCodes.Status400BadRequest, validation.Error.StatusCode);
        Assert.Equal(
            "The remote key provider is not named; expected 'aws-kms'.",
            validation.Error.Detail
        );
    }

    [Fact]
    public void ValidateRemoteKey_WhenTheCurveIsNotNamed_LooksTheRowUpOnTheResolvedCurve()
    {
        SetupDescribedProviders(ProviderName);
        SetupRow(Es256Selector, Es256Row);

        var validation = RemoteValidator.ValidateRemoteKey(Es256Selector with { Curve = null });

        Assert.True(validation.IsValid);
        Assert.Equal("P-256", validation.GenerationParameters.Curve);
    }

    [Fact]
    public void ValidateRemoteKey_WhenTheAlgorithmIsNotRegistered_ReturnsBadRequest()
    {
        SetupDescribedProviders(ProviderName);

        var validation = RemoteValidator.ValidateRemoteKey(
            Es256Selector with
            {
                Algorithm = "XS999",
            }
        );

        Assert.False(validation.IsValid);
        Assert.NotNull(validation.Error);
        Assert.Equal(StatusCodes.Status400BadRequest, validation.Error.StatusCode);
        Assert.Equal(
            "The algorithm 'XS999' is not a supported signature or key-management algorithm.",
            validation.Error.Detail
        );
    }

    [Fact]
    public void ValidateRemoteKey_WhenTheSizeIsNotTheAlgorithms_ReturnsBadRequest()
    {
        SetupDescribedProviders(ProviderName);
        var selector = new RemoteKeyAlgorithmSelector
        {
            Provider = ProviderName,
            Algorithm = "RS256",
            SecretType = SecretTypes.Rsa,
            KeySizeBits = 1024,
        };

        var validation = RemoteValidator.ValidateRemoteKey(selector);

        Assert.False(validation.IsValid);
        Assert.NotNull(validation.Error);
        Assert.Equal(StatusCodes.Status400BadRequest, validation.Error.StatusCode);
    }

    [Fact]
    public void ValidateRemoteKey_KeyManagementAlgorithmWithARow_IsValid()
    {
        SetupDescribedProviders(ProviderName);
        SetupRow(Rsa3072OaepSelector, Rsa3072OaepRow);

        var validation = RemoteValidator.ValidateRemoteKey(Rsa3072OaepSelector);

        Assert.True(validation.IsValid);
        Assert.Equal(3072, validation.GenerationParameters.KeySizeBits);
    }

    [Fact]
    public void ValidateRemoteKey_RowHoldsAnotherUse_IsInvalid()
    {
        SetupDescribedProviders(ProviderName);
        var signingRow = new RemoteSigningAlgorithm
        {
            Provider = ProviderName,
            Algorithm = "RSA-OAEP-256",
            SecretType = SecretTypes.Rsa,
            KeySizeBits = 3072,
            KeySpec = "RSA_3072",
            SigningAlgorithm = "RSASSA_PKCS1_V1_5_SHA_256",
        };
        SetupRow(Rsa3072OaepSelector, signingRow);

        var validation = RemoteValidator.ValidateRemoteKey(Rsa3072OaepSelector);

        Assert.False(validation.IsValid);
        Assert.NotNull(validation.Error);
        Assert.Equal(StatusCodes.Status400BadRequest, validation.Error.StatusCode);
        Assert.Contains("'sig'", validation.Error.Detail, StringComparison.Ordinal);
        Assert.Contains("'enc'", validation.Error.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateRemoteKey_UnregisteredAlgorithm_IsInvalid()
    {
        SetupDescribedProviders(ProviderName);

        var validation = RemoteValidator.ValidateRemoteKey(
            Rsa3072OaepSelector with
            {
                Algorithm = "XYZ",
            }
        );

        Assert.False(validation.IsValid);
        Assert.NotNull(validation.Error);
        Assert.Equal(StatusCodes.Status400BadRequest, validation.Error.StatusCode);
        Assert.Equal(
            "The algorithm 'XYZ' is not a supported signature or key-management algorithm.",
            validation.Error.Detail
        );
    }

    [Fact]
    public void ValidateRemoteKey_WhenTheProviderHasNoRow_ReturnsBadRequest()
    {
        SetupDescribedProviders(ProviderName);
        SetupRow(Es256Selector, null);

        var validation = RemoteValidator.ValidateRemoteKey(Es256Selector);

        Assert.False(validation.IsValid);
        Assert.NotNull(validation.Error);
        Assert.Equal(StatusCodes.Status400BadRequest, validation.Error.StatusCode);
        Assert.Equal(
            "The remote key provider 'aws-kms' holds no 'ES256' key of the 'ecc' type with 256 bits on the curve 'P-256'.",
            validation.Error.Detail
        );
    }

    #endregion
}
